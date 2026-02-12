using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using CAD;
using Mathematics;
using SldWorks;
using SwConst;

namespace SolidworksLibrary
{
    public class FeatureBuilder
    {
        // ================================================================
        // Instance state for database operations
        // ================================================================
        private readonly string _databasePath;
        private string ConnectionString => $"Data Source={_databasePath};Version=3;Foreign Keys=True;";

        /// <summary>
        /// Creates a FeatureBuilder instance configured for database operations.
        /// </summary>
        /// <param name="databasePath">Full path to the SQLite database file.</param>
        public FeatureBuilder(string databasePath)
        {
            if (string.IsNullOrWhiteSpace(databasePath))
                throw new ArgumentException("Database path must not be null or empty.", nameof(databasePath));
            _databasePath = databasePath;
        }

        // ================================================================
        // Database: Schema initialization
        // ================================================================

        /// <summary>
        /// Reads all *_Schema.sql files from <paramref name="sqlFolderPath"/> and executes
        /// them against the database to create or ensure tables, indexes, and views exist.
        /// </summary>
        public void InitializeSchema(string sqlFolderPath)
        {
            if (string.IsNullOrWhiteSpace(sqlFolderPath))
                throw new ArgumentException("SQL folder path must not be null or empty.", nameof(sqlFolderPath));
            if (!Directory.Exists(sqlFolderPath))
                throw new DirectoryNotFoundException($"SQL folder not found: {sqlFolderPath}");

            var sqlFiles = Directory.GetFiles(sqlFolderPath, "*_Schema.sql")
                                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                                    .ToArray();

            if (sqlFiles.Length == 0)
                throw new FileNotFoundException($"No *_Schema.sql files found in: {sqlFolderPath}");

            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                foreach (var sqlFile in sqlFiles)
                {
                    var sql = File.ReadAllText(sqlFile);
                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        // ================================================================
        // Database: Write operations
        // ================================================================

        /// <summary>
        /// Persists one or more <see cref="CAD_Feature"/> objects (and their dependent
        /// dimensions, sketches, stations, sub-features, libraries, coordinate systems,
        /// vectors, and points) to the database.
        /// Uses INSERT OR REPLACE for idempotent upsert semantics.
        /// </summary>
        public void SaveFeatures(IEnumerable<CAD_Feature> features)
        {
            if (features == null) throw new ArgumentNullException(nameof(features));

            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                using (var txn = conn.BeginTransaction())
                {
                    foreach (var feature in features)
                    {
                        SaveFeatureCore(conn, feature);
                    }
                    txn.Commit();
                }
            }
        }

        /// <summary>Convenience overload — persists a single feature.</summary>
        public void SaveFeature(CAD_Feature feature)
        {
            if (feature == null) throw new ArgumentNullException(nameof(feature));
            SaveFeatures(new[] { feature });
        }

        private void SaveFeatureCore(SQLiteConnection conn, CAD_Feature feature)
        {
            var featureId = feature.Name ?? GenerateId();

            // Ensure owning model
            string modelId = null;
            if (feature.MyModel != null)
            {
                modelId = feature.MyModel.Name ?? GenerateId();
                EnsureModel(conn, feature.MyModel, modelId);
            }

            // Ensure origin coordinate system
            string originCsId = null;
            if (feature.Origin != null)
            {
                originCsId = feature.Origin.CoordinateSystemID
                             ?? feature.Origin.Name
                             ?? GenerateId();
                EnsureCoordinateSystem(conn, feature.Origin, originCsId);
            }

            // Ensure cursor references
            string currentDimId = null;
            if (feature.CurrentDimension != null)
            {
                currentDimId = feature.CurrentDimension.DimensionID ?? GenerateId();
                EnsureDimension(conn, currentDimId, feature.CurrentDimension);
            }

            string currentFeatureId = null;
            if (feature.CurrentFeature != null)
            {
                currentFeatureId = feature.CurrentFeature.Name ?? GenerateId();
                SaveFeatureCore(conn, feature.CurrentFeature);
            }

            string currentSketchId = null;
            if (feature.CurrentCAD_Sketch != null)
            {
                currentSketchId = feature.CurrentCAD_Sketch.SketchID ?? GenerateId();
                EnsureSketch(conn, feature.CurrentCAD_Sketch, currentSketchId);
            }

            string currentStationId = null;
            if (feature.CurrentCAD_Station != null)
            {
                currentStationId = feature.CurrentCAD_Station.ID ?? GenerateId();
                EnsureStation(conn, feature.CurrentCAD_Station, currentStationId);
            }

            string currentLibraryId = null;
            if (feature.CurrentLibrary != null)
            {
                currentLibraryId = feature.CurrentLibrary.Name ?? GenerateId();
                EnsureLibrary(conn, feature.CurrentLibrary, currentLibraryId);
            }

            // INSERT OR REPLACE the feature row
            const string featureSql =
                @"INSERT OR REPLACE INTO CAD_Feature
                  (FeatureID, Name, Version, GeometricFeatureType,
                   MyModelID, OriginCSysID,
                   CurrentDimensionID, CurrentFeatureID,
                   CurrentCAD_SketchID, CurrentCAD_StationID, CurrentLibraryID)
                  VALUES
                  (@id, @name, @ver, @gft,
                   @modelId, @csId,
                   @curDimId, @curFeatId,
                   @curSketchId, @curStationId, @curLibId);";

            using (var cmd = new SQLiteCommand(featureSql, conn))
            {
                cmd.Parameters.AddWithValue("@id", featureId);
                cmd.Parameters.AddWithValue("@name", (object)feature.Name ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ver", (object)feature.Version ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@gft", (int)feature.GeometricFeatureType);
                cmd.Parameters.AddWithValue("@modelId", (object)modelId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@csId", (object)originCsId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@curDimId", (object)currentDimId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@curFeatId", (object)currentFeatureId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@curSketchId", (object)currentSketchId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@curStationId", (object)currentStationId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@curLibId", (object)currentLibraryId ?? DBNull.Value);
                cmd.ExecuteNonQuery();
            }

            // Junction tables — clear and re-insert each collection
            SaveFeature3DOperations(conn, featureId, feature.ThreeDimOperations);
            SaveFeatureDimensions(conn, featureId, feature.MyDimensions);
            SaveFeatureSketches(conn, featureId, feature.Sketches);
            SaveFeatureStations(conn, featureId, feature.Stations);
            SaveFeatureSubFeatures(conn, featureId, feature.MyFeatures);
            SaveFeatureLibraries(conn, featureId, feature.MyLibraries);
        }

        // ================================================================
        // Database: Junction table save helpers
        // ================================================================

        private static void SaveFeature3DOperations(SQLiteConnection conn, string featureId,
            List<CAD_Feature.Feature3DOperationEnum> operations)
        {
            using (var cmd = new SQLiteCommand(
                "DELETE FROM CAD_Feature_3DOperation WHERE FeatureID = @id;", conn))
            {
                cmd.Parameters.AddWithValue("@id", featureId);
                cmd.ExecuteNonQuery();
            }

            for (int i = 0; i < operations.Count; i++)
            {
                using (var cmd = new SQLiteCommand(
                    @"INSERT INTO CAD_Feature_3DOperation (FeatureID, Operation, SortOrder)
                      VALUES (@fid, @op, @ord);", conn))
                {
                    cmd.Parameters.AddWithValue("@fid", featureId);
                    cmd.Parameters.AddWithValue("@op", (int)operations[i]);
                    cmd.Parameters.AddWithValue("@ord", i);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void SaveFeatureDimensions(SQLiteConnection conn, string featureId,
            List<CAD_Dimension> dimensions)
        {
            using (var cmd = new SQLiteCommand(
                "DELETE FROM CAD_Feature_Dimension WHERE FeatureID = @id;", conn))
            {
                cmd.Parameters.AddWithValue("@id", featureId);
                cmd.ExecuteNonQuery();
            }

            for (int i = 0; i < dimensions.Count; i++)
            {
                var dim = dimensions[i];
                var dimId = dim.DimensionID ?? GenerateId();
                EnsureDimension(conn, dimId, dim);

                using (var cmd = new SQLiteCommand(
                    @"INSERT INTO CAD_Feature_Dimension (FeatureID, DimensionID, SortOrder)
                      VALUES (@fid, @did, @ord);", conn))
                {
                    cmd.Parameters.AddWithValue("@fid", featureId);
                    cmd.Parameters.AddWithValue("@did", dimId);
                    cmd.Parameters.AddWithValue("@ord", i);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void SaveFeatureSketches(SQLiteConnection conn, string featureId,
            List<CAD_Sketch> sketches)
        {
            using (var cmd = new SQLiteCommand(
                "DELETE FROM CAD_Feature_Sketch WHERE FeatureID = @id;", conn))
            {
                cmd.Parameters.AddWithValue("@id", featureId);
                cmd.ExecuteNonQuery();
            }

            for (int i = 0; i < sketches.Count; i++)
            {
                var sketch = sketches[i];
                var sketchId = sketch.SketchID ?? GenerateId();
                EnsureSketch(conn, sketch, sketchId);

                using (var cmd = new SQLiteCommand(
                    @"INSERT INTO CAD_Feature_Sketch (FeatureID, SketchID, SortOrder)
                      VALUES (@fid, @sid, @ord);", conn))
                {
                    cmd.Parameters.AddWithValue("@fid", featureId);
                    cmd.Parameters.AddWithValue("@sid", sketchId);
                    cmd.Parameters.AddWithValue("@ord", i);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void SaveFeatureStations(SQLiteConnection conn, string featureId,
            List<CAD_Station> stations)
        {
            using (var cmd = new SQLiteCommand(
                "DELETE FROM CAD_Feature_Station WHERE FeatureID = @id;", conn))
            {
                cmd.Parameters.AddWithValue("@id", featureId);
                cmd.ExecuteNonQuery();
            }

            for (int i = 0; i < stations.Count; i++)
            {
                var station = stations[i];
                var stationId = station.ID ?? GenerateId();
                EnsureStation(conn, station, stationId);

                using (var cmd = new SQLiteCommand(
                    @"INSERT INTO CAD_Feature_Station (FeatureID, StationID, SortOrder)
                      VALUES (@fid, @sid, @ord);", conn))
                {
                    cmd.Parameters.AddWithValue("@fid", featureId);
                    cmd.Parameters.AddWithValue("@sid", stationId);
                    cmd.Parameters.AddWithValue("@ord", i);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void SaveFeatureSubFeatures(SQLiteConnection conn, string parentFeatureId,
            List<CAD_Feature> subFeatures)
        {
            using (var cmd = new SQLiteCommand(
                "DELETE FROM CAD_Feature_SubFeature WHERE ParentFeatureID = @id;", conn))
            {
                cmd.Parameters.AddWithValue("@id", parentFeatureId);
                cmd.ExecuteNonQuery();
            }

            for (int i = 0; i < subFeatures.Count; i++)
            {
                var child = subFeatures[i];
                // Save the child feature itself first (recursive)
                SaveFeatureCore(conn, child);
                var childId = child.Name ?? GenerateId();

                using (var cmd = new SQLiteCommand(
                    @"INSERT INTO CAD_Feature_SubFeature (ParentFeatureID, ChildFeatureID, SortOrder)
                      VALUES (@pid, @cid, @ord);", conn))
                {
                    cmd.Parameters.AddWithValue("@pid", parentFeatureId);
                    cmd.Parameters.AddWithValue("@cid", childId);
                    cmd.Parameters.AddWithValue("@ord", i);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void SaveFeatureLibraries(SQLiteConnection conn, string featureId,
            List<CAD_Library> libraries)
        {
            using (var cmd = new SQLiteCommand(
                "DELETE FROM CAD_Feature_Library WHERE FeatureID = @id;", conn))
            {
                cmd.Parameters.AddWithValue("@id", featureId);
                cmd.ExecuteNonQuery();
            }

            for (int i = 0; i < libraries.Count; i++)
            {
                var lib = libraries[i];
                var libId = lib.Name ?? GenerateId();
                EnsureLibrary(conn, lib, libId);

                using (var cmd = new SQLiteCommand(
                    @"INSERT INTO CAD_Feature_Library (FeatureID, LibraryID, SortOrder)
                      VALUES (@fid, @lid, @ord);", conn))
                {
                    cmd.Parameters.AddWithValue("@fid", featureId);
                    cmd.Parameters.AddWithValue("@lid", libId);
                    cmd.Parameters.AddWithValue("@ord", i);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // ================================================================
        // Database: Read operations
        // ================================================================

        /// <summary>
        /// Loads all features from the database, optionally filtered by model ID.
        /// Reconstructs the object graph including dimensions, sketches, stations,
        /// sub-features, libraries, coordinate systems, vectors, and points.
        /// </summary>
        public List<CAD_Feature> LoadFeatures(string modelId = null)
        {
            var features = new List<CAD_Feature>();

            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();

                var sql = modelId != null
                    ? "SELECT * FROM CAD_Feature WHERE MyModelID = @mid ORDER BY FeatureID;"
                    : "SELECT * FROM CAD_Feature ORDER BY FeatureID;";

                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    if (modelId != null)
                        cmd.Parameters.AddWithValue("@mid", modelId);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var feature = ReadFeatureFromRow(reader);
                            features.Add(feature);
                        }
                    }
                }

                // Load child collections and references for each feature
                foreach (var feature in features)
                {
                    var featureId = feature.Name;
                    if (featureId == null) continue;
                    LoadFeatureChildren(conn, feature, featureId);
                }
            }

            return features;
        }

        /// <summary>Loads a single feature by its ID.</summary>
        public CAD_Feature LoadFeature(string featureId)
        {
            if (string.IsNullOrWhiteSpace(featureId))
                throw new ArgumentException("Feature ID must not be null or empty.", nameof(featureId));

            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();

                CAD_Feature feature = null;
                using (var cmd = new SQLiteCommand(
                    "SELECT * FROM CAD_Feature WHERE FeatureID = @id;", conn))
                {
                    cmd.Parameters.AddWithValue("@id", featureId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                            feature = ReadFeatureFromRow(reader);
                    }
                }

                if (feature == null) return null;

                LoadFeatureChildren(conn, feature, featureId);
                return feature;
            }
        }

        // ================================================================
        // Database: Private helpers — Read
        // ================================================================

        private static CAD_Feature ReadFeatureFromRow(SQLiteDataReader reader)
        {
            var feature = new CAD_Feature
            {
                Name = reader["Name"] as string,
                Version = reader["Version"] as string,
                GeometricFeatureType = (CAD_Feature.GeometricFeatureTypeEnum)
                    Convert.ToInt32(reader["GeometricFeatureType"])
            };

            return feature;
        }

        private void LoadFeatureChildren(SQLiteConnection conn, CAD_Feature feature, string featureId)
        {
            // Load model
            var modelId = GetScalar(conn,
                "SELECT MyModelID FROM CAD_Feature WHERE FeatureID = @id;", featureId);
            if (modelId != null)
                feature.MyModel = LoadModel(conn, modelId);

            // Load origin coordinate system
            var originCsId = GetScalar(conn,
                "SELECT OriginCSysID FROM CAD_Feature WHERE FeatureID = @id;", featureId);
            if (originCsId != null)
                feature.Origin = LoadCoordinateSystem(conn, originCsId);

            // Load 3D operations
            LoadFeature3DOperations(conn, feature, featureId);

            // Load dimensions
            LoadFeatureDimensions(conn, feature, featureId);

            // Load sketches
            LoadFeatureSketches(conn, feature, featureId);

            // Load stations
            LoadFeatureStations(conn, feature, featureId);

            // Load sub-features (one level to avoid infinite recursion)
            LoadFeatureSubFeatures(conn, feature, featureId);

            // Load libraries
            LoadFeatureLibraries(conn, feature, featureId);

            // Load cursor references
            var curDimId = GetScalar(conn,
                "SELECT CurrentDimensionID FROM CAD_Feature WHERE FeatureID = @id;", featureId);
            if (curDimId != null)
                feature.CurrentDimension = LoadDimension(conn, curDimId);

            var curSketchId = GetScalar(conn,
                "SELECT CurrentCAD_SketchID FROM CAD_Feature WHERE FeatureID = @id;", featureId);
            if (curSketchId != null)
                feature.CurrentCAD_Sketch = LoadSketchStub(conn, curSketchId);

            var curStationId = GetScalar(conn,
                "SELECT CurrentCAD_StationID FROM CAD_Feature WHERE FeatureID = @id;", featureId);
            if (curStationId != null)
                feature.CurrentCAD_Station = LoadStationStub(conn, curStationId);

            var curLibId = GetScalar(conn,
                "SELECT CurrentLibraryID FROM CAD_Feature WHERE FeatureID = @id;", featureId);
            if (curLibId != null)
                feature.CurrentLibrary = LoadLibrary(conn, curLibId);
        }

        private static string GetScalar(SQLiteConnection conn, string sql, string id)
        {
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                return cmd.ExecuteScalar() as string;
            }
        }

        private static void LoadFeature3DOperations(SQLiteConnection conn, CAD_Feature feature,
            string featureId)
        {
            const string sql =
                @"SELECT Operation FROM CAD_Feature_3DOperation
                  WHERE FeatureID = @id ORDER BY SortOrder;";

            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", featureId);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var op = (CAD_Feature.Feature3DOperationEnum)
                            Convert.ToInt32(reader["Operation"]);
                        feature.ThreeDimOperations.Add(op);
                    }
                }
            }
        }

        private void LoadFeatureDimensions(SQLiteConnection conn, CAD_Feature feature,
            string featureId)
        {
            const string sql =
                @"SELECT d.*
                  FROM CAD_Feature_Dimension fd
                  JOIN CAD_Dimension d ON fd.DimensionID = d.DimensionID
                  WHERE fd.FeatureID = @id
                  ORDER BY fd.SortOrder;";

            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", featureId);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var dim = ReadDimensionFromRow(conn, reader);
                        feature.AddDimension(dim);
                    }
                }
            }
        }

        private void LoadFeatureSketches(SQLiteConnection conn, CAD_Feature feature,
            string featureId)
        {
            const string sql =
                @"SELECT sk.*
                  FROM CAD_Feature_Sketch fs
                  JOIN CAD_Sketch sk ON fs.SketchID = sk.SketchID
                  WHERE fs.FeatureID = @id
                  ORDER BY fs.SortOrder;";

            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", featureId);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var sketch = ReadSketchStubFromRow(reader);
                        feature.AddSketch(sketch);
                    }
                }
            }
        }

        private void LoadFeatureStations(SQLiteConnection conn, CAD_Feature feature,
            string featureId)
        {
            const string sql =
                @"SELECT st.*
                  FROM CAD_Feature_Station fst
                  JOIN CAD_Station st ON fst.StationID = st.StationID
                  WHERE fst.FeatureID = @id
                  ORDER BY fst.SortOrder;";

            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", featureId);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var station = ReadStationStubFromRow(reader);
                        feature.AddStation(station);
                    }
                }
            }
        }

        private void LoadFeatureSubFeatures(SQLiteConnection conn, CAD_Feature feature,
            string featureId)
        {
            const string sql =
                @"SELECT cf.*
                  FROM CAD_Feature_SubFeature fsf
                  JOIN CAD_Feature cf ON fsf.ChildFeatureID = cf.FeatureID
                  WHERE fsf.ParentFeatureID = @id
                  ORDER BY fsf.SortOrder;";

            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", featureId);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var child = ReadFeatureFromRow(reader);
                        feature.AddFeature(child);
                    }
                }
            }
        }

        private void LoadFeatureLibraries(SQLiteConnection conn, CAD_Feature feature,
            string featureId)
        {
            const string sql =
                @"SELECT lib.*
                  FROM CAD_Feature_Library fl
                  JOIN CAD_Library lib ON fl.LibraryID = lib.LibraryID
                  WHERE fl.FeatureID = @id
                  ORDER BY fl.SortOrder;";

            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", featureId);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var lib = ReadLibraryFromRow(reader);
                        feature.AddLibrary(lib);
                    }
                }
            }
        }

        // ================================================================
        // Database: Row-to-object readers
        // ================================================================

        private CAD_Dimension ReadDimensionFromRow(SQLiteConnection conn, SQLiteDataReader reader)
        {
            var dim = new CAD_Dimension
            {
                DimensionID = reader["DimensionID"] as string,
                Name = reader["Name"] as string,
                Description = reader["Description"] as string,
                IsOrdinate = Convert.ToInt32(reader["IsOrdinate"]) != 0,
                DimensionNominalValue = Convert.ToDouble(reader["DimensionNominalValue"]),
                DimensionUpperLimitValue = Convert.ToDouble(reader["DimensionUpperLimitValue"]),
                DimensionLowerLimitValue = Convert.ToDouble(reader["DimensionLowerLimitValue"]),
                MyDimensionType = (CAD_Dimension.DimensionType)
                    Convert.ToInt32(reader["MyDimensionType"])
            };

            var centerPtId = reader["CenterPointID"] as string;
            if (centerPtId != null)
                dim.CenterPoint = LoadPoint(conn, centerPtId);

            var leaderEndId = reader["LeaderLineEndPointID"] as string;
            if (leaderEndId != null)
                dim.LeaderLineEndPoint = LoadPoint(conn, leaderEndId);

            var leaderBendId = reader["LeaderLineBendPointID"] as string;
            if (leaderBendId != null)
                dim.LeaderLineBendPoint = LoadPoint(conn, leaderBendId);

            var dimPtId = reader["DimensionPointID"] as string;
            if (dimPtId != null)
                dim.DimensionPoint = LoadPoint(conn, dimPtId);

            var refPtId = reader["ReferencePointID"] as string;
            if (refPtId != null)
                dim.ReferencePoint = LoadPoint(conn, refPtId);

            var dimModelId = reader["MyModelID"] as string;
            if (dimModelId != null)
                dim.MyModel = LoadModel(conn, dimModelId);

            return dim;
        }

        private static CAD_Sketch ReadSketchStubFromRow(SQLiteDataReader reader)
        {
            return new CAD_Sketch(reader["SketchID"] as string)
            {
                Version = reader["Version"] as string,
                IsTwoD = Convert.ToInt32(reader["IsTwoD"]) != 0
            };
        }

        private static CAD_Station ReadStationStubFromRow(SQLiteDataReader reader)
        {
            var stationType = (CAD_Station.StationTypeEnum)
                Convert.ToInt32(reader["MyType"]);

            double locationValue = 0;
            switch (stationType)
            {
                case CAD_Station.StationTypeEnum.Axial:
                    locationValue = Convert.ToDouble(reader["AxialLocation"]);
                    break;
                case CAD_Station.StationTypeEnum.Radial:
                    locationValue = Convert.ToDouble(reader["RadialLocation"]);
                    break;
                case CAD_Station.StationTypeEnum.Angular:
                    locationValue = Convert.ToDouble(reader["AngularLocation"]);
                    break;
                case CAD_Station.StationTypeEnum.Wing:
                    locationValue = Convert.ToDouble(reader["WingLocation"]);
                    break;
            }

            var station = new CAD_Station(null, reader["StationID"] as string, stationType)
            {
                Name = reader["Name"] as string,
                Version = reader["Version"] as string
            };

            station.SetLocation(stationType, locationValue);
            return station;
        }

        private static CAD_Library ReadLibraryFromRow(SQLiteDataReader reader)
        {
            var lib = new CAD_Library
            {
                Name = reader["Name"] as string,
                Description = reader["Description"] as string,
                LocalPath = reader["LocalPath"] as string
            };

            var urlStr = reader["Url"] as string;
            if (urlStr != null)
                lib.TrySetUrl(urlStr);

            return lib;
        }

        // ================================================================
        // Database: Shared entity loaders
        // ================================================================

        private CAD_Dimension LoadDimension(SQLiteConnection conn, string dimId)
        {
            const string sql = "SELECT * FROM CAD_Dimension WHERE DimensionID = @id;";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", dimId);
                using (var reader = cmd.ExecuteReader())
                {
                    if (!reader.Read()) return null;
                    return ReadDimensionFromRow(conn, reader);
                }
            }
        }

        private static CAD_Sketch LoadSketchStub(SQLiteConnection conn, string sketchId)
        {
            const string sql = "SELECT * FROM CAD_Sketch WHERE SketchID = @id;";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", sketchId);
                using (var reader = cmd.ExecuteReader())
                {
                    if (!reader.Read()) return null;
                    return ReadSketchStubFromRow(reader);
                }
            }
        }

        private static CAD_Station LoadStationStub(SQLiteConnection conn, string stationId)
        {
            const string sql = "SELECT * FROM CAD_Station WHERE StationID = @id;";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", stationId);
                using (var reader = cmd.ExecuteReader())
                {
                    if (!reader.Read()) return null;
                    return ReadStationStubFromRow(reader);
                }
            }
        }

        private static CAD_Library LoadLibrary(SQLiteConnection conn, string libraryId)
        {
            const string sql = "SELECT * FROM CAD_Library WHERE LibraryID = @id;";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", libraryId);
                using (var reader = cmd.ExecuteReader())
                {
                    if (!reader.Read()) return null;
                    return ReadLibraryFromRow(reader);
                }
            }
        }

        private CoordinateSystem LoadCoordinateSystem(SQLiteConnection conn, string csId)
        {
            const string sql = "SELECT * FROM CoordinateSystem WHERE CoordinateSystemID = @id;";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", csId);
                using (var reader = cmd.ExecuteReader())
                {
                    if (!reader.Read()) return null;
                    return ReadCoordinateSystemFromRow(conn, reader);
                }
            }
        }

        private CoordinateSystem ReadCoordinateSystemFromRow(SQLiteConnection conn,
            SQLiteDataReader reader)
        {
            var cs = new CoordinateSystem
            {
                CoordinateSystemID = reader["CoordinateSystemID"] as string,
                Name = reader["Name"] as string,
                MyType = (CoordinateSystem.CoordinateSystemTypeEnum)
                    Convert.ToInt32(reader["MyType"]),
                IsWCS = Convert.ToInt32(reader["IsWCS"]) != 0,
                Is2D = Convert.ToInt32(reader["Is2D"]) != 0
            };

            var originId = reader["OriginLocationPointID"] as string;
            if (originId != null)
                cs.OriginLocation = LoadPoint(conn, originId);

            var baseVecId = reader["BaseVectorID"] as string;
            if (baseVecId != null)
                cs.BaseVector = LoadVector(conn, baseVecId);

            return cs;
        }

        private Point LoadPoint(SQLiteConnection conn, string pointId)
        {
            const string sql = "SELECT * FROM Point WHERE PointID = @id;";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", pointId);
                using (var reader = cmd.ExecuteReader())
                {
                    if (!reader.Read()) return null;
                    return ReadPointFromRow(reader);
                }
            }
        }

        private Vector LoadVector(SQLiteConnection conn, string vectorId)
        {
            const string sql = "SELECT * FROM Vector WHERE VectorID = @id;";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", vectorId);
                using (var reader = cmd.ExecuteReader())
                {
                    if (!reader.Read()) return null;

                    var v = new Vector
                    {
                        VectorID = reader["VectorID"] as string,
                        Name = reader["Name"] as string,
                        IsKnotVector = Convert.ToInt32(reader["IsKnotVector"]) != 0,
                        VectorType = (Vector.VectorTypeEnum)Convert.ToInt32(reader["VectorType"]),
                        X_Value = Convert.ToDouble(reader["X_Value"]),
                        Y_Value = Convert.ToDouble(reader["Y_Value"]),
                        Z_Value = Convert.ToDouble(reader["Z_Value"]),
                        Cyl_R = Convert.ToDouble(reader["Cyl_R"]),
                        Cyl_Theta = Convert.ToDouble(reader["Cyl_Theta"]),
                        L = Convert.ToDouble(reader["L"]),
                        Sph_R = Convert.ToDouble(reader["Sph_R"]),
                        Sph_Theta = Convert.ToDouble(reader["Sph_Theta"]),
                        Phi = Convert.ToDouble(reader["Phi"])
                    };

                    var startPtId = reader["StartPointID"] as string;
                    if (startPtId != null)
                        v.StartPoint = LoadPoint(conn, startPtId);

                    var endPtId = reader["EndPointID"] as string;
                    if (endPtId != null)
                        v.EndPoint = LoadPoint(conn, endPtId);

                    return v;
                }
            }
        }

        private CAD_Model LoadModel(SQLiteConnection conn, string modelId)
        {
            const string sql = "SELECT * FROM CAD_Model WHERE ModelID = @id;";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", modelId);
                using (var reader = cmd.ExecuteReader())
                {
                    if (!reader.Read()) return null;

                    return new CAD_Model
                    {
                        Name = reader["Name"] as string,
                        Version = reader["Version"] as string,
                        Description = reader["Description"] as string,
                        FilePath = reader["FilePath"] as string,
                        CAD_AppName = (CAD_Model.CAD_AppEnum)Convert.ToInt32(reader["CAD_AppName"]),
                        ModelType = (CAD_Model.CAD_ModelTypeEnum)Convert.ToInt32(reader["ModelType"]),
                        FileType = (CAD_Model.CAD_FileTypeEnum)Convert.ToInt32(reader["FileType"])
                    };
                }
            }
        }

        private static Point ReadPointFromRow(SQLiteDataReader reader)
        {
            return new Point
            {
                PointID = reader["PointID"] as string,
                IsWeightPoint = Convert.ToInt32(reader["IsWeightPoint"]) != 0,
                MyType = (Point.PointTypeEnum)Convert.ToInt32(reader["MyType"]),
                Is2D = Convert.ToInt32(reader["Is2D"]) != 0,
                X_Value = Convert.ToDouble(reader["X_Value"]),
                Y_Value = Convert.ToDouble(reader["Y_Value"]),
                Z_Value_Cartesian = Convert.ToDouble(reader["Z_Value_Cartesian"]),
                R_Value_Cylindrical = Convert.ToDouble(reader["R_Value_Cylindrical"]),
                Theta_Value_Cylindrical = Convert.ToDouble(reader["Theta_Value_Cylindrical"]),
                Z_Value_Cylindrical = Convert.ToDouble(reader["Z_Value_Cylindrical"]),
                R_Value_Spherical = Convert.ToDouble(reader["R_Value_Spherical"]),
                Theta_Value_Spherical = Convert.ToDouble(reader["Theta_Value_Spherical"]),
                Phi_Value = Convert.ToDouble(reader["Phi_Value"]),
                Longitude = Convert.ToDouble(reader["Longitude"]),
                Latitude = Convert.ToDouble(reader["Latitude"]),
                Altitude = Convert.ToDouble(reader["Altitude"]),
                Real_Value = Convert.ToDouble(reader["Real_Value"]),
                Complex_Value = Convert.ToDouble(reader["Complex_Value"])
            };
        }

        // ================================================================
        // Database: Private helpers — Write (Ensure* methods)
        // ================================================================

        private void EnsureModel(SQLiteConnection conn, CAD_Model model, string modelId)
        {
            const string sql =
                @"INSERT OR IGNORE INTO CAD_Model
                  (ModelID, Name, Version, Description, FilePath, CAD_AppName, ModelType, FileType)
                  VALUES (@id, @name, @ver, @desc, @fp, @app, @mt, @ft);";

            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", modelId);
                cmd.Parameters.AddWithValue("@name", (object)model.Name ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ver", (object)model.Version ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@desc", (object)model.Description ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@fp", (object)model.FilePath ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@app", (int)model.CAD_AppName);
                cmd.Parameters.AddWithValue("@mt", (int)model.ModelType);
                cmd.Parameters.AddWithValue("@ft", (int)model.FileType);
                cmd.ExecuteNonQuery();
            }
        }

        private void EnsureCoordinateSystem(SQLiteConnection conn, CoordinateSystem cs, string csId)
        {
            string originPtId = null;
            if (cs.OriginLocation != null)
            {
                originPtId = cs.OriginLocation.PointID ?? GenerateId();
                EnsurePoint(conn, cs.OriginLocation, originPtId);
            }

            string baseVecId = null;
            if (cs.BaseVector != null)
            {
                baseVecId = cs.BaseVector.VectorID ?? GenerateId();
                EnsureVector(conn, cs.BaseVector, baseVecId);
            }

            const string sql =
                @"INSERT OR IGNORE INTO CoordinateSystem
                  (CoordinateSystemID, Name, MyType, IsWCS, Is2D,
                   OriginLocationPointID, BaseVectorID)
                  VALUES (@id, @name, @type, @wcs, @is2d, @originId, @baseVecId);";

            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", csId);
                cmd.Parameters.AddWithValue("@name", (object)cs.Name ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@type", (int)cs.MyType);
                cmd.Parameters.AddWithValue("@wcs", cs.IsWCS ? 1 : 0);
                cmd.Parameters.AddWithValue("@is2d", cs.Is2D ? 1 : 0);
                cmd.Parameters.AddWithValue("@originId", (object)originPtId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@baseVecId", (object)baseVecId ?? DBNull.Value);
                cmd.ExecuteNonQuery();
            }
        }

        private void EnsureVector(SQLiteConnection conn, Vector vector, string vectorId)
        {
            string startPtId = null;
            if (vector.StartPoint != null)
            {
                startPtId = vector.StartPoint.PointID ?? GenerateId();
                EnsurePoint(conn, vector.StartPoint, startPtId);
            }

            string endPtId = null;
            if (vector.EndPoint != null)
            {
                endPtId = vector.EndPoint.PointID ?? GenerateId();
                EnsurePoint(conn, vector.EndPoint, endPtId);
            }

            const string sql =
                @"INSERT OR IGNORE INTO Vector
                  (VectorID, Name, IsKnotVector, VectorType,
                   X_Value, Y_Value, Z_Value,
                   Cyl_R, Cyl_Theta, L,
                   Sph_R, Sph_Theta, Phi,
                   StartPointID, EndPointID)
                  VALUES
                  (@id, @name, @knot, @type,
                   @x, @y, @z,
                   @cr, @ct, @l,
                   @sr, @st, @phi,
                   @startId, @endId);";

            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", vectorId);
                cmd.Parameters.AddWithValue("@name", (object)vector.Name ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@knot", vector.IsKnotVector ? 1 : 0);
                cmd.Parameters.AddWithValue("@type", (int)vector.VectorType);
                cmd.Parameters.AddWithValue("@x", vector.X_Value);
                cmd.Parameters.AddWithValue("@y", vector.Y_Value);
                cmd.Parameters.AddWithValue("@z", vector.Z_Value);
                cmd.Parameters.AddWithValue("@cr", vector.Cyl_R);
                cmd.Parameters.AddWithValue("@ct", vector.Cyl_Theta);
                cmd.Parameters.AddWithValue("@l", vector.L);
                cmd.Parameters.AddWithValue("@sr", vector.Sph_R);
                cmd.Parameters.AddWithValue("@st", vector.Sph_Theta);
                cmd.Parameters.AddWithValue("@phi", vector.Phi);
                cmd.Parameters.AddWithValue("@startId", (object)startPtId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@endId", (object)endPtId ?? DBNull.Value);
                cmd.ExecuteNonQuery();
            }
        }

        private static void EnsurePoint(SQLiteConnection conn, Point point, string pointId)
        {
            const string sql =
                @"INSERT OR IGNORE INTO Point
                  (PointID, IsWeightPoint, MyType, Is2D,
                   X_Value, Y_Value, Z_Value_Cartesian,
                   R_Value_Cylindrical, Theta_Value_Cylindrical, Z_Value_Cylindrical,
                   R_Value_Spherical, Theta_Value_Spherical, Phi_Value,
                   Longitude, Latitude, Altitude,
                   Real_Value, Complex_Value)
                  VALUES
                  (@id, @wpt, @type, @is2d,
                   @x, @y, @zc,
                   @rc, @tc, @zcyl,
                   @rs, @ts, @phi,
                   @lon, @lat, @alt,
                   @real, @cplx);";

            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", pointId);
                cmd.Parameters.AddWithValue("@wpt", point.IsWeightPoint ? 1 : 0);
                cmd.Parameters.AddWithValue("@type", (int)point.MyType);
                cmd.Parameters.AddWithValue("@is2d", point.Is2D ? 1 : 0);
                cmd.Parameters.AddWithValue("@x", point.X_Value);
                cmd.Parameters.AddWithValue("@y", point.Y_Value);
                cmd.Parameters.AddWithValue("@zc", point.Z_Value_Cartesian);
                cmd.Parameters.AddWithValue("@rc", point.R_Value_Cylindrical);
                cmd.Parameters.AddWithValue("@tc", point.Theta_Value_Cylindrical);
                cmd.Parameters.AddWithValue("@zcyl", point.Z_Value_Cylindrical);
                cmd.Parameters.AddWithValue("@rs", point.R_Value_Spherical);
                cmd.Parameters.AddWithValue("@ts", point.Theta_Value_Spherical);
                cmd.Parameters.AddWithValue("@phi", point.Phi_Value);
                cmd.Parameters.AddWithValue("@lon", point.Longitude);
                cmd.Parameters.AddWithValue("@lat", point.Latitude);
                cmd.Parameters.AddWithValue("@alt", point.Altitude);
                cmd.Parameters.AddWithValue("@real", point.Real_Value);
                cmd.Parameters.AddWithValue("@cplx", point.Complex_Value);
                cmd.ExecuteNonQuery();
            }
        }

        private void EnsureDimension(SQLiteConnection conn, string dimId, CAD_Dimension dim)
        {
            // Ensure points referenced by the dimension
            string centerPtId = null;
            if (dim.CenterPoint != null)
            {
                centerPtId = dim.CenterPoint.PointID ?? GenerateId();
                EnsurePoint(conn, dim.CenterPoint, centerPtId);
            }

            string leaderEndPtId = null;
            if (dim.LeaderLineEndPoint != null)
            {
                leaderEndPtId = dim.LeaderLineEndPoint.PointID ?? GenerateId();
                EnsurePoint(conn, dim.LeaderLineEndPoint, leaderEndPtId);
            }

            string leaderBendPtId = null;
            if (dim.LeaderLineBendPoint != null)
            {
                leaderBendPtId = dim.LeaderLineBendPoint.PointID ?? GenerateId();
                EnsurePoint(conn, dim.LeaderLineBendPoint, leaderBendPtId);
            }

            string dimPtId = null;
            if (dim.DimensionPoint != null)
            {
                dimPtId = dim.DimensionPoint.PointID ?? GenerateId();
                EnsurePoint(conn, dim.DimensionPoint, dimPtId);
            }

            string refPtId = null;
            if (dim.ReferencePoint != null)
            {
                refPtId = dim.ReferencePoint.PointID ?? GenerateId();
                EnsurePoint(conn, dim.ReferencePoint, refPtId);
            }

            // Ensure model stub
            string dimModelId = null;
            if (dim.MyModel != null)
            {
                dimModelId = dim.MyModel.Name ?? GenerateId();
                EnsureModel(conn, dim.MyModel, dimModelId);
            }

            const string sql =
                @"INSERT OR IGNORE INTO CAD_Dimension
                  (DimensionID, Name, Description, IsOrdinate,
                   CenterPointID, LeaderLineEndPointID, LeaderLineBendPointID,
                   DimensionPointID, ReferencePointID,
                   MyModelID,
                   DimensionNominalValue, DimensionUpperLimitValue, DimensionLowerLimitValue,
                   MyDimensionType)
                  VALUES
                  (@id, @name, @desc, @ord,
                   @cpId, @leId, @lbId,
                   @dpId, @rpId,
                   @modelId,
                   @nom, @upper, @lower,
                   @dimType);";

            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", dimId);
                cmd.Parameters.AddWithValue("@name", (object)dim.Name ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@desc", (object)dim.Description ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ord", dim.IsOrdinate ? 1 : 0);
                cmd.Parameters.AddWithValue("@cpId", (object)centerPtId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@leId", (object)leaderEndPtId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@lbId", (object)leaderBendPtId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@dpId", (object)dimPtId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@rpId", (object)refPtId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@modelId", (object)dimModelId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@nom", dim.DimensionNominalValue);
                cmd.Parameters.AddWithValue("@upper", dim.DimensionUpperLimitValue);
                cmd.Parameters.AddWithValue("@lower", dim.DimensionLowerLimitValue);
                cmd.Parameters.AddWithValue("@dimType", (int)dim.MyDimensionType);
                cmd.ExecuteNonQuery();
            }
        }

        private static void EnsureSketch(SQLiteConnection conn, CAD_Sketch sketch, string sketchId)
        {
            const string sql =
                @"INSERT OR IGNORE INTO CAD_Sketch
                  (SketchID, Version, IsTwoD)
                  VALUES (@id, @ver, @is2d);";

            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", sketchId);
                cmd.Parameters.AddWithValue("@ver", (object)sketch.Version ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@is2d", sketch.IsTwoD ? 1 : 0);
                cmd.ExecuteNonQuery();
            }
        }

        private static void EnsureStation(SQLiteConnection conn, CAD_Station station, string stationId)
        {
            const string sql =
                @"INSERT OR IGNORE INTO CAD_Station
                  (StationID, Name, Version, MyType,
                   AxialLocation, RadialLocation, AngularLocation,
                   WingLocation, FloorLocation)
                  VALUES
                  (@id, @name, @ver, @type,
                   @axial, @radial, @angular,
                   @wing, @floor);";

            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", stationId);
                cmd.Parameters.AddWithValue("@name", (object)station.Name ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ver", (object)station.Version ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@type", (int)station.MyType);
                cmd.Parameters.AddWithValue("@axial", station.AxialLocation);
                cmd.Parameters.AddWithValue("@radial", station.RadialLocation);
                cmd.Parameters.AddWithValue("@angular", station.AngularLocation);
                cmd.Parameters.AddWithValue("@wing", station.WingLocation);
                cmd.Parameters.AddWithValue("@floor", station.FloorLocation);
                cmd.ExecuteNonQuery();
            }
        }

        private static void EnsureLibrary(SQLiteConnection conn, CAD_Library lib, string libId)
        {
            const string sql =
                @"INSERT OR IGNORE INTO CAD_Library
                  (LibraryID, Name, Description, LocalPath, Url)
                  VALUES (@id, @name, @desc, @path, @url);";

            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", libId);
                cmd.Parameters.AddWithValue("@name", (object)lib.Name ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@desc", (object)lib.Description ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@path", (object)lib.LocalPath ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@url", lib.Url != null ? (object)lib.Url.ToString() : DBNull.Value);
                cmd.ExecuteNonQuery();
            }
        }

        private static string GenerateId() => Guid.NewGuid().ToString("N");

        // ================================================================
        // Original SolidWorks-based static methods (unchanged)
        // ================================================================

        // -------------------------------------------
        // Extrusion (Boss)
        // -------------------------------------------

        public static object CreateExtrusion(FeatureManager featMgr,
            bool singleDirection, bool flipDirection,
            int endCondition1, double depth1,
            int endCondition2, double depth2,
            bool draftWhileExtruding1, double draftAngle1,
            bool draftWhileExtruding2, double draftAngle2,
            bool merge, bool useFeatScope, bool useAutoSelect)
        {
            return featMgr.FeatureExtrusion3(
                singleDirection, flipDirection, false,
                endCondition1, endCondition2,
                depth1, depth2,
                draftWhileExtruding1, draftWhileExtruding2,
                draftWhileExtruding1, draftWhileExtruding2,
                draftAngle1, draftAngle2,
                false, false, false, false,
                merge, useFeatScope, useAutoSelect,
                0, 0, false);
        }

        // -------------------------------------------
        // Cut Extrusion
        // -------------------------------------------

        public static object CreateCutExtrusion(FeatureManager featMgr,
            bool singleDirection, bool flipDirection,
            int endCondition1, double depth1,
            int endCondition2, double depth2,
            bool draftWhileExtruding1, double draftAngle1,
            bool draftWhileExtruding2, double draftAngle2,
            bool normalCut, bool useFeatScope, bool useAutoSelect)
        {
            return featMgr.FeatureCut4(
                singleDirection, flipDirection, false,
                endCondition1, endCondition2,
                depth1, depth2,
                draftWhileExtruding1, draftWhileExtruding2,
                draftWhileExtruding1, draftWhileExtruding2,
                draftAngle1, draftAngle2,
                false, false, false, false,
                normalCut, useFeatScope, useAutoSelect,
                false, false, false,
                0, 0.0, false, false);
        }

        // -------------------------------------------
        // Revolve
        // -------------------------------------------

        public static object CreateRevolve(FeatureManager featMgr,
            bool singleDirection, bool isSolid,
            bool isCut, bool reverseDirection,
            int endCondition1, double angle1,
            int endCondition2, double angle2,
            bool merge, bool useFeatScope, bool useAutoSelect)
        {
            return featMgr.FeatureRevolve2(
                singleDirection, isSolid, false, isCut,
                reverseDirection, false,
                endCondition1, endCondition2,
                angle1, angle2,
                false, false,
                0.0, 0.0,
                0, 0.0, 0.0,
                merge, useFeatScope, useAutoSelect);
        }

        // -------------------------------------------
        // Hole Wizard (Threaded Holes)
        // -------------------------------------------

        public static object CreateHoleWizard(FeatureManager featMgr,
            int holeType, int standard,
            int fastenerType, string size, short endCondition,
            double diameter, double depth,
            double headClearance, double headDiameter,
            double headDepth, double threadDepth,
            double threadDiameter)
        {
            return featMgr.HoleWizard5(
                holeType, standard, fastenerType,
                size, endCondition,
                diameter, depth,
                headClearance, headDiameter, headDepth,
                threadDiameter, threadDepth,
                0, 0, 0, 0, 0, 0, 0, 0,
                "", false, false, false, false, false, false);
        }

        public static object CreateThreadedHole(FeatureManager featMgr,
            string size, double depth,
            double threadDepth, int standard, int fastenerType)
        {
            return featMgr.HoleWizard5(
                (int)swWzdGeneralHoleTypes_e.swWzdTap,
                standard, fastenerType,
                size,
                (short)swEndConditions_e.swEndCondBlind,
                0, depth,
                0, 0, 0,
                0, threadDepth,
                0, 0, 0, 0, 0, 0, 0, 0,
                "", false, false, false, false, false, false);
        }

        public static object CreateCounterboreHole(FeatureManager featMgr,
            string size, double depth,
            double cboreDiameter, double cboreDepth,
            int standard, int fastenerType)
        {
            return featMgr.HoleWizard5(
                (int)swWzdGeneralHoleTypes_e.swWzdCounterBore,
                standard, fastenerType,
                size,
                (short)swEndConditions_e.swEndCondBlind,
                0, depth,
                0, cboreDiameter, cboreDepth,
                0, 0,
                0, 0, 0, 0, 0, 0, 0, 0,
                "", false, false, false, false, false, false);
        }

        public static object CreateCountersinkHole(FeatureManager featMgr,
            string size, double depth,
            double csinkDiameter, double csinkAngle,
            int standard, int fastenerType)
        {
            return featMgr.HoleWizard5(
                (int)swWzdGeneralHoleTypes_e.swWzdCounterSink,
                standard, fastenerType,
                size,
                (short)swEndConditions_e.swEndCondBlind,
                0, depth,
                0, csinkDiameter, csinkAngle,
                0, 0,
                0, 0, 0, 0, 0, 0, 0, 0,
                "", false, false, false, false, false, false);
        }

        // -------------------------------------------
        // Chamfer
        // -------------------------------------------

        public static void CreateChamfer(ModelDoc2 swModelDoc,
            double width, double angle, bool flipDirection)
        {
            swModelDoc.FeatureChamfer(width, angle, flipDirection);
        }

        // -------------------------------------------
        // Fillet
        // -------------------------------------------

        public static bool CreateFillet(ModelDoc2 swModelDoc, double radius,
            int filletType, int overflowType, int radiusType,
            bool propagateToTangentFaces)
        {
            int result = swModelDoc.FeatureFillet2(radius, true,
                false, false, 0, 0, 0);
            return result == 0;
        }

        public static bool CreateConstantRadiusFillet(ModelDoc2 swModelDoc,
            double radius, bool propagateToTangentFaces)
        {
            int result = swModelDoc.FeatureFillet2(radius, true,
                false, false, 0, 0, 0);
            return result == 0;
        }

        // -------------------------------------------
        // Shell
        // -------------------------------------------

        public static void CreateShell(ModelDoc2 swModelDoc,
            double thickness, bool shellOutward)
        {
            swModelDoc.InsertFeatureShell(thickness, shellOutward);
        }

        // -------------------------------------------
        // Draft
        // -------------------------------------------

        public static object CreateDraft(FeatureManager featMgr,
            double angle, bool reverseDirection, int draftType)
        {
            DraftFeatureData draftData = (DraftFeatureData)featMgr.CreateDefinition(
                (int)swFeatureNameID_e.swFmDraft);
            draftData.Angle = angle;
            draftData.Type = draftType;
            draftData.ReverseDirection = reverseDirection;
            return featMgr.CreateFeature(draftData);
        }

        // -------------------------------------------
        // Linear Pattern
        // -------------------------------------------

        public static object CreateLinearPattern(FeatureManager featMgr,
            int numDir1, double spacingDir1,
            int numDir2, double spacingDir2,
            bool reverseDir1, bool reverseDir2,
            bool geometryPattern, bool varySketch,
            string skipInstances1, string skipInstances2)
        {
            return featMgr.FeatureLinearPattern4(
                numDir1, spacingDir1,
                numDir2, spacingDir2,
                reverseDir1, reverseDir2,
                skipInstances1, skipInstances2,
                geometryPattern, varySketch,
                true, true,
                false, false,
                false, false, false, false,
                0, 0);
        }

        // -------------------------------------------
        // Circular Pattern
        // -------------------------------------------

        public static object CreateCircularPattern(FeatureManager featMgr,
            int totalInstances, double angularSpacing,
            bool reverseDirection, bool geometryPattern,
            bool equalSpacing, bool varySketch,
            string skipInstances)
        {
            return featMgr.FeatureCircularPattern4(
                totalInstances, angularSpacing,
                reverseDirection, skipInstances,
                geometryPattern, equalSpacing, varySketch);
        }

        // -------------------------------------------
        // Mirror
        // -------------------------------------------

        public static object CreateMirrorFeature(FeatureManager featMgr,
            bool geometryPattern, bool propagateVisualProps)
        {
            return featMgr.InsertMirrorFeature2(
                geometryPattern, false, propagateVisualProps, false, 0);
        }

        // -------------------------------------------
        // Rib
        // -------------------------------------------

        public static void CreateRib(FeatureManager featMgr,
            double thickness, int ribType, bool flipMaterial,
            bool reverseThickness, bool naturalDraft, double draftAngle)
        {
            featMgr.InsertRib(
                reverseThickness, flipMaterial, thickness, ribType,
                false, naturalDraft, false, draftAngle, false, false);
        }

        // -------------------------------------------
        // Slot (Cut)
        // -------------------------------------------

        public static object CreateSlotCut(FeatureManager featMgr,
            double depth, bool singleDirection, bool flipDirection)
        {
            return featMgr.FeatureCut4(
                singleDirection, flipDirection, false,
                (int)swEndConditions_e.swEndCondBlind, 0,
                depth, 0,
                false, false, false, false,
                0, 0,
                false, false, false, false,
                false, true, true,
                false, false, false,
                0, 0.0, false, false);
        }

        // -------------------------------------------
        // Joint
        // -------------------------------------------

        public static object CreateJoint(FeatureManager featMgr,
            int jointType, double clearance, bool flipDirection)
        {
            return featMgr.FeatureExtrusion3(
                true, flipDirection, false,
                (int)swEndConditions_e.swEndCondBlind, 0,
                clearance, 0,
                false, false, false, false,
                0, 0,
                false, false, false, false,
                true, true, true,
                0, 0, false);
        }

        // -------------------------------------------
        // Bead (Weldment)
        // -------------------------------------------

        public static object CreateBead(FeatureManager featMgr,
            double beadWidth, double beadHeight,
            int beadType, bool flipDirection)
        {
            return featMgr.FeatureExtrusion3(
                true, flipDirection, false,
                (int)swEndConditions_e.swEndCondBlind, 0,
                beadHeight, 0,
                false, false, false, false,
                0, 0,
                false, false, false, false,
                true, true, true,
                0, 0, false);
        }

        // -------------------------------------------
        // Keyway
        // -------------------------------------------

        public static object CreateKeyway(FeatureManager featMgr,
            double width, double depth,
            double length, int keywayType, bool flipDirection)
        {
            return featMgr.FeatureCut4(
                true, flipDirection, false,
                (int)swEndConditions_e.swEndCondBlind, 0,
                depth, 0,
                false, false, false, false,
                0, 0,
                false, false, false, false,
                false, true, true,
                false, false, false,
                0, 0.0, false, false);
        }

        // -------------------------------------------
        // Leg
        // -------------------------------------------

        public static object CreateLeg(FeatureManager featMgr,
            double height, double width,
            double thickness, int legType)
        {
            return featMgr.FeatureExtrusion3(
                true, false, false,
                (int)swEndConditions_e.swEndCondBlind, 0,
                height, 0,
                false, false, false, false,
                0, 0,
                false, false, false, false,
                true, true, true,
                0, 0, false);
        }

        // -------------------------------------------
        // Arm
        // -------------------------------------------

        public static object CreateArm(FeatureManager featMgr,
            double length, double width,
            double thickness, int armType)
        {
            return featMgr.FeatureExtrusion3(
                true, false, false,
                (int)swEndConditions_e.swEndCondBlind, 0,
                length, 0,
                false, false, false, false,
                0, 0,
                false, false, false, false,
                true, true, true,
                0, 0, false);
        }

        // -------------------------------------------
        // Embossment
        // -------------------------------------------

        public static object CreateEmbossment(FeatureManager featMgr,
            double depth, double taperAngle,
            bool flipDirection, int embossType)
        {
            return featMgr.FeatureExtrusion3(
                true, flipDirection, false,
                (int)swEndConditions_e.swEndCondBlind, 0,
                depth, 0,
                taperAngle > 0, false, taperAngle > 0, false,
                taperAngle, 0,
                false, false, false, false,
                true, true, true,
                0, 0, false);
        }

        // -------------------------------------------
        // Gusset
        // -------------------------------------------

        public static object CreateGusset(FeatureManager featMgr,
            double thickness, double height,
            double width, int gussetType, bool flipDirection)
        {
            return featMgr.FeatureExtrusion3(
                true, flipDirection, false,
                (int)swEndConditions_e.swEndCondBlind, 0,
                thickness, 0,
                false, false, false, false,
                0, 0,
                false, false, false, false,
                true, true, true,
                0, 0, false);
        }

        // -------------------------------------------
        // Web
        // -------------------------------------------

        public static object CreateWeb(FeatureManager featMgr, ModelDoc2 swModelDoc,
            double thickness, double height,
            int webType, bool flipDirection)
        {
            featMgr.InsertRib(
                false, flipDirection, thickness, webType,
                false, false, false, 0, false, false);
            return swModelDoc.Extension.GetLastFeatureAdded();
        }

        // -------------------------------------------
        // Tab (Sheet Metal)
        // -------------------------------------------

        public static object CreateTab(FeatureManager featMgr,
            double length, double width,
            double thickness, int tabType, bool flipDirection)
        {
            return featMgr.FeatureExtrusion3(
                true, flipDirection, false,
                (int)swEndConditions_e.swEndCondBlind, 0,
                thickness, 0,
                false, false, false, false,
                0, 0,
                false, false, false, false,
                true, true, true,
                0, 0, false);
        }

        // -------------------------------------------
        // Coil / Spring
        // -------------------------------------------

        public static object CreateCoil(FeatureManager featMgr,
            double pitch, double diameter,
            double height, int numCoils, bool clockwise,
            int coilType, double wireDiameter)
        {
            return featMgr.FeatureRevolve2(
                false, true, false, false,
                false, false,
                (int)swEndConditions_e.swEndCondBlind, 0,
                height * numCoils, 0,
                false, false,
                0.0, 0.0,
                0, 0.0, 0.0,
                true, true, true);
        }

        // -------------------------------------------
        // Helicoil / Thread Insert
        // -------------------------------------------

        public static object CreateHelicoil(FeatureManager featMgr,
            double pitch, double diameter,
            double depth, int numTurns, bool clockwise,
            int threadType)
        {
            return featMgr.FeatureRevolve2(
                false, true, false, true,
                false, false,
                (int)swEndConditions_e.swEndCondBlind, 0,
                depth, 0,
                false, false,
                0.0, 0.0,
                0, 0.0, 0.0,
                true, true, true);
        }

        // -------------------------------------------
        // Sweep
        // -------------------------------------------

        public static object CreateSweep(FeatureManager featMgr,
            bool isSolid, bool isCut,
            bool isThinFeature, double thinWallThickness,
            bool merge, bool useFeatScope, bool useAutoSelect,
            int startTangentType, int endTangentType,
            bool alignWithEndFaces, bool maintainTangency)
        {
            SweepFeatureData sweepData = (SweepFeatureData)featMgr.CreateDefinition(
                isCut ? (int)swFeatureNameID_e.swFmSweepCut : (int)swFeatureNameID_e.swFmSweep);
            sweepData.MaintainTangency = maintainTangency;
            sweepData.ThinFeature = isThinFeature;
            if (isThinFeature)
            {
                sweepData.ThinWallType = 0;
                sweepData.SetWallThickness(true, thinWallThickness);
            }
            return featMgr.CreateFeature(sweepData);
        }

        // -------------------------------------------
        // Loft
        // -------------------------------------------

        public static object CreateLoft(FeatureManager featMgr,
            bool isSolid, bool isCut,
            bool isThinFeature, double thinWallThickness,
            bool merge, bool useFeatScope, bool useAutoSelect,
            int startTangentType, int endTangentType,
            bool closeProfile, bool maintainTangency)
        {
            if (isCut)
            {
                return featMgr.FeatureCut4(
                    true, false, false,
                    (int)swEndConditions_e.swEndCondBlind, 0,
                    thinWallThickness, 0,
                    false, false, false, false,
                    0, 0,
                    false, false, false, false,
                    false, useFeatScope, useAutoSelect,
                    false, false, false,
                    0, 0.0, false, false);
            }
            else
            {
                return featMgr.InsertProtrusionBlend2(
                    closeProfile, maintainTangency, false,
                    1.0,
                    (short)startTangentType, (short)endTangentType,
                    0.0, 0.0,
                    false, isThinFeature, false,
                    thinWallThickness, 0.0,
                    (short)0,
                    merge, useFeatScope, useAutoSelect, 0);
            }
        }

        // -------------------------------------------
        // Other Pattern (Table-Driven / Sketch-Driven)
        // -------------------------------------------

        public static object CreateOtherPattern(FeatureManager featMgr,
            int patternType, object patternParameters, bool geometryPattern)
        {
            SketchPatternFeatureData patternData = (SketchPatternFeatureData)featMgr.CreateDefinition(
                (int)swFeatureNameID_e.swFmSketchPattern);
            patternData.GeometryPattern = geometryPattern;
            return featMgr.CreateFeature(patternData);
        }

        // -------------------------------------------
        // Rounded Slot
        // -------------------------------------------

        public static object CreateRoundedSlot(FeatureManager featMgr,
            double length, double width,
            double depth, bool singleDirection, bool flipDirection)
        {
            return featMgr.FeatureCut4(
                singleDirection, flipDirection, false,
                (int)swEndConditions_e.swEndCondBlind, 0,
                depth, 0,
                false, false, false, false,
                0, 0,
                false, false, false, false,
                false, true, true,
                false, false, false,
                0, 0.0, false, false);
        }

        // -------------------------------------------
        // Square Slot
        // -------------------------------------------

        public static object CreateSquareSlot(FeatureManager featMgr,
            double length, double width,
            double depth, bool singleDirection, bool flipDirection)
        {
            return featMgr.FeatureCut4(
                singleDirection, flipDirection, false,
                (int)swEndConditions_e.swEndCondBlind, 0,
                depth, 0,
                false, false, false, false,
                0, 0,
                false, false, false, false,
                false, true, true,
                false, false, false,
                0, 0.0, false, false);
        }
    }
}
