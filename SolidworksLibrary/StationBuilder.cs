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
    public class StationBuilder
    {
        // ================================================================
        // Enums (unchanged)
        // ================================================================
        public enum CoordinateSystemType
        {
            Cartesian,
            Cylindrical,
            Spherical
        }

        public enum CartesianAxis { X, Y, Z }
        public enum CylindricalAxis { R, Theta, Z }
        public enum SphericalAxis { R, Theta, Phi }

        // ================================================================
        // Instance state for database operations
        // ================================================================
        private readonly string _databasePath;
        private string ConnectionString => $"Data Source={_databasePath};Version=3;Foreign Keys=True;";

        /// <summary>
        /// Creates a StationBuilder instance configured for database operations.
        /// </summary>
        /// <param name="databasePath">Full path to the SQLite database file.</param>
        public StationBuilder(string databasePath)
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
        /// The CAD_Station_Schema.sql file includes its own dependency stubs (Point, Vector,
        /// CoordinateSystem, CAD_Model, CAD_SketchPlane) so it is safe to run standalone.
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
        /// Persists one or more <see cref="CAD_Station"/> objects (and their dependent
        /// sketch planes, coordinate systems, vectors, and points) to the database.
        /// Uses INSERT OR REPLACE for idempotent upsert semantics.
        /// </summary>
        public void SaveStations(IEnumerable<CAD_Station> stations)
        {
            if (stations == null) throw new ArgumentNullException(nameof(stations));

            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                using (var txn = conn.BeginTransaction())
                {
                    foreach (var station in stations)
                    {
                        SaveStationCore(conn, station);
                    }
                    txn.Commit();
                }
            }
        }

        /// <summary>Convenience overload — persists a single station.</summary>
        public void SaveStation(CAD_Station station)
        {
            if (station == null) throw new ArgumentNullException(nameof(station));
            SaveStations(new[] { station });
        }

        private void SaveStationCore(SQLiteConnection conn, CAD_Station station)
        {
            var stationId = station.ID ?? GenerateId();

            // Ensure owning model row exists (stub)
            string modelId = null;
            if (station.MyModel != null)
            {
                modelId = station.MyModel.Name ?? GenerateId();
                EnsureModel(conn, station.MyModel, modelId);
            }

            // Persist each sketch plane and build junction data
            string currentSketchPlaneId = null;
            var sketchPlaneIds = new List<string>();

            for (int i = 0; i < station.MySketchPlanes.Count; i++)
            {
                var plane = station.MySketchPlanes[i];
                var planeId = plane.Name ?? GenerateId();

                EnsureSketchPlane(conn, plane, planeId, modelId);
                sketchPlaneIds.Add(planeId);

                if (plane == station.CurrentSketchPlane)
                    currentSketchPlaneId = planeId;
            }

            // If there's a current but it wasn't in the list, persist it too
            if (currentSketchPlaneId == null && station.CurrentSketchPlane != null)
            {
                currentSketchPlaneId = station.CurrentSketchPlane.Name ?? GenerateId();
                EnsureSketchPlane(conn, station.CurrentSketchPlane, currentSketchPlaneId, modelId);
            }

            // INSERT OR REPLACE the station row
            const string stationSql =
                @"INSERT OR REPLACE INTO CAD_Station
                  (StationID, Name, Version, MyType,
                   AxialLocation, RadialLocation, AngularLocation, WingLocation, FloorLocation,
                   MyModelID, CurrentSketchPlaneID)
                  VALUES
                  (@id, @name, @ver, @type,
                   @axial, @radial, @angular, @wing, @floor,
                   @modelId, @curPlaneId);";

            using (var cmd = new SQLiteCommand(stationSql, conn))
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
                cmd.Parameters.AddWithValue("@modelId", (object)modelId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@curPlaneId", (object)currentSketchPlaneId ?? DBNull.Value);
                cmd.ExecuteNonQuery();
            }

            // Delete old junction rows and re-insert
            using (var cmd = new SQLiteCommand(
                "DELETE FROM CAD_Station_SketchPlane WHERE StationID = @id;", conn))
            {
                cmd.Parameters.AddWithValue("@id", stationId);
                cmd.ExecuteNonQuery();
            }

            for (int i = 0; i < sketchPlaneIds.Count; i++)
            {
                using (var cmd = new SQLiteCommand(
                    @"INSERT INTO CAD_Station_SketchPlane (StationID, SketchPlaneID, SortOrder)
                      VALUES (@sid, @pid, @ord);", conn))
                {
                    cmd.Parameters.AddWithValue("@sid", stationId);
                    cmd.Parameters.AddWithValue("@pid", sketchPlaneIds[i]);
                    cmd.Parameters.AddWithValue("@ord", i);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // ================================================================
        // Database: Read operations
        // ================================================================

        /// <summary>
        /// Loads all stations from the database, optionally filtered by model ID.
        /// Reconstructs the full object graph including sketch planes, coordinate
        /// systems, normal vectors, and origin points.
        /// </summary>
        /// <param name="modelId">If non-null, only stations belonging to this model are returned.</param>
        public List<CAD_Station> LoadStations(string modelId = null)
        {
            var stations = new List<CAD_Station>();

            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();

                var sql = modelId != null
                    ? "SELECT * FROM CAD_Station WHERE MyModelID = @mid ORDER BY StationID;"
                    : "SELECT * FROM CAD_Station ORDER BY StationID;";

                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    if (modelId != null)
                        cmd.Parameters.AddWithValue("@mid", modelId);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var station = ReadStationFromRow(reader);
                            stations.Add(station);
                        }
                    }
                }

                // Load sketch planes for each station
                foreach (var station in stations)
                {
                    LoadSketchPlanesForStation(conn, station);
                }

                // Load model stubs and wire them up
                LoadModelsForStations(conn, stations);
            }

            return stations;
        }

        /// <summary>Loads a single station by its ID.</summary>
        public CAD_Station LoadStation(string stationId)
        {
            if (string.IsNullOrWhiteSpace(stationId))
                throw new ArgumentException("Station ID must not be null or empty.", nameof(stationId));

            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();

                CAD_Station station = null;
                using (var cmd = new SQLiteCommand(
                    "SELECT * FROM CAD_Station WHERE StationID = @id;", conn))
                {
                    cmd.Parameters.AddWithValue("@id", stationId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                            station = ReadStationFromRow(reader);
                    }
                }

                if (station == null) return null;

                LoadSketchPlanesForStation(conn, station);
                LoadModelsForStations(conn, new List<CAD_Station> { station });
                return station;
            }
        }

        // ================================================================
        // Database: Private helpers — Read
        // ================================================================

        private static CAD_Station ReadStationFromRow(SQLiteDataReader reader)
        {
            var id = reader["StationID"] as string;
            var type = (CAD_Station.StationTypeEnum)Convert.ToInt32(reader["MyType"]);

            // Determine the primary location value based on station type
            double primaryValue;
            switch (type)
            {
                case CAD_Station.StationTypeEnum.Axial:
                    primaryValue = Convert.ToDouble(reader["AxialLocation"]);
                    break;
                case CAD_Station.StationTypeEnum.Radial:
                    primaryValue = Convert.ToDouble(reader["RadialLocation"]);
                    break;
                case CAD_Station.StationTypeEnum.Angular:
                    primaryValue = Convert.ToDouble(reader["AngularLocation"]);
                    break;
                case CAD_Station.StationTypeEnum.Wing:
                    primaryValue = Convert.ToDouble(reader["WingLocation"]);
                    break;
                default:
                    primaryValue = 0;
                    break;
            }

            var station = new CAD_Station(id, type, primaryValue)
            {
                Name = reader["Name"] as string,
                Version = reader["Version"] as string
            };

            // Set any additional location values that may have been stored
            // (SetLocation in the constructor already set the primary one)
            SetAllLocations(station, reader);

            return station;
        }

        private static void SetAllLocations(CAD_Station station, SQLiteDataReader reader)
        {
            // The constructor's SetLocation already set the primary location.
            // We need to set the others that may have non-zero values stored in DB.
            // Since the location setters are private, we use SetLocation for each non-zero value
            // but only for types different from the primary type.
            // However, SetLocation changes MyType — so we save/restore it.
            var originalType = station.MyType;

            double axial = Convert.ToDouble(reader["AxialLocation"]);
            double radial = Convert.ToDouble(reader["RadialLocation"]);
            double angular = Convert.ToDouble(reader["AngularLocation"]);
            double wing = Convert.ToDouble(reader["WingLocation"]);

            if (axial != 0 && originalType != CAD_Station.StationTypeEnum.Axial)
                station.SetLocation(CAD_Station.StationTypeEnum.Axial, axial);
            if (radial != 0 && originalType != CAD_Station.StationTypeEnum.Radial)
                station.SetLocation(CAD_Station.StationTypeEnum.Radial, radial);
            if (angular != 0 && originalType != CAD_Station.StationTypeEnum.Angular)
                station.SetLocation(CAD_Station.StationTypeEnum.Angular, angular);
            if (wing != 0 && originalType != CAD_Station.StationTypeEnum.Wing)
                station.SetLocation(CAD_Station.StationTypeEnum.Wing, wing);

            // Restore original type since SetLocation overrides it
            station.SetLocation(originalType,
                originalType == CAD_Station.StationTypeEnum.Axial ? axial :
                originalType == CAD_Station.StationTypeEnum.Radial ? radial :
                originalType == CAD_Station.StationTypeEnum.Angular ? angular :
                originalType == CAD_Station.StationTypeEnum.Wing ? wing : 0);
        }

        private void LoadSketchPlanesForStation(SQLiteConnection conn, CAD_Station station)
        {
            var stationId = station.ID;
            string currentPlaneId = null;

            // Get the current sketch plane ID from the station row
            using (var cmd = new SQLiteCommand(
                "SELECT CurrentSketchPlaneID FROM CAD_Station WHERE StationID = @id;", conn))
            {
                cmd.Parameters.AddWithValue("@id", stationId);
                var result = cmd.ExecuteScalar();
                currentPlaneId = result as string;
            }

            // Load sketch planes via junction table
            const string sql =
                @"SELECT sp.*, ssp.SortOrder
                  FROM CAD_Station_SketchPlane ssp
                  JOIN CAD_SketchPlane sp ON ssp.SketchPlaneID = sp.SketchPlaneID
                  WHERE ssp.StationID = @id
                  ORDER BY ssp.SortOrder;";

            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", stationId);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var plane = ReadSketchPlaneFromRow(conn, reader);
                        bool isCurrent = (reader["SketchPlaneID"] as string) == currentPlaneId;
                        station.AddSketchPlane(plane, isCurrent);
                    }
                }
            }
        }

        private CAD_SketchPlane ReadSketchPlaneFromRow(SQLiteConnection conn, SQLiteDataReader reader)
        {
            var name = reader["Name"] as string;
            var funcType = (CAD_SketchPlane.FunctionalTypeEnum)Convert.ToInt32(reader["FunctionalType"]);
            var geomType = (CAD_SketchPlane.GeometryTypeEnum)Convert.ToInt32(reader["GeometryType"]);

            var plane = new CAD_SketchPlane(name, funcType, geomType)
            {
                Version = reader["Version"] as string,
                Path = reader["Path"] as string,
                IsWorkplane = Convert.ToInt32(reader["IsWorkplane"]) != 0
            };

            // Load coordinate system if present
            var csId = reader["MyCoordinateSystemID"] as string;
            if (csId != null)
            {
                plane.MyCoordinateSystem = LoadCoordinateSystem(conn, csId);
            }

            // Load normal vector if present
            var normalId = reader["NormalVectorID"] as string;
            if (normalId != null)
            {
                var normalVec = LoadVector(conn, normalId);
                if (normalVec != null)
                {
                    plane.SetNormal(normalVec.X_Value, normalVec.Y_Value, normalVec.Z_Value, false);
                }
            }

            return plane;
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

                    var cs = new CoordinateSystem
                    {
                        CoordinateSystemID = reader["CoordinateSystemID"] as string,
                        Name = reader["Name"] as string,
                        MyType = (CoordinateSystem.CoordinateSystemTypeEnum)Convert.ToInt32(reader["MyType"]),
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
            }
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

        private void LoadModelsForStations(SQLiteConnection conn, List<CAD_Station> stations)
        {
            // Build a set of unique model IDs referenced by the stations
            var modelCache = new Dictionary<string, CAD_Model>();

            foreach (var station in stations)
            {
                // Re-query to get the MyModelID for this station
                string modelId = null;
                using (var cmd = new SQLiteCommand(
                    "SELECT MyModelID FROM CAD_Station WHERE StationID = @id;", conn))
                {
                    cmd.Parameters.AddWithValue("@id", station.ID);
                    var result = cmd.ExecuteScalar();
                    modelId = result as string;
                }

                if (modelId == null) continue;

                if (!modelCache.TryGetValue(modelId, out var model))
                {
                    model = LoadModel(conn, modelId);
                    if (model != null)
                        modelCache[modelId] = model;
                }

                station.MyModel = model;
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

        // ================================================================
        // Database: Private helpers — Write
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

        private void EnsureSketchPlane(SQLiteConnection conn, CAD_SketchPlane plane,
            string planeId, string modelId)
        {
            // Persist coordinate system and normal vector first
            string csId = null;
            if (plane.MyCoordinateSystem != null)
            {
                csId = plane.MyCoordinateSystem.CoordinateSystemID
                       ?? plane.MyCoordinateSystem.Name
                       ?? GenerateId();
                EnsureCoordinateSystem(conn, plane.MyCoordinateSystem, csId);
            }

            string normalVecId = null;
            if (plane.NormalVector != null)
            {
                normalVecId = plane.NormalVector.VectorID ?? GenerateId();
                EnsureVector(conn, plane.NormalVector, normalVecId);
            }

            const string sql =
                @"INSERT OR REPLACE INTO CAD_SketchPlane
                  (SketchPlaneID, Name, Version, Path, IsWorkplane,
                   GeometryType, FunctionalType, MyModelID,
                   MyCoordinateSystemID, NormalVectorID)
                  VALUES
                  (@id, @name, @ver, @path, @wkp,
                   @geom, @func, @modelId,
                   @csId, @normalId);";

            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", planeId);
                cmd.Parameters.AddWithValue("@name", (object)plane.Name ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ver", (object)plane.Version ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@path", (object)plane.Path ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@wkp", plane.IsWorkplane ? 1 : 0);
                cmd.Parameters.AddWithValue("@geom", (int)plane.GeometryType);
                cmd.Parameters.AddWithValue("@func", (int)plane.FunctionalType);
                cmd.Parameters.AddWithValue("@modelId", (object)modelId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@csId", (object)csId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@normalId", (object)normalVecId ?? DBNull.Value);
                cmd.ExecuteNonQuery();
            }
        }

        private void EnsureCoordinateSystem(SQLiteConnection conn, CoordinateSystem cs, string csId)
        {
            // Persist origin point and base vector first
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
            // Persist start/end points first
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

        private static string GenerateId() => Guid.NewGuid().ToString("N");

        // ================================================================
        // Original SolidWorks-based static methods (unchanged)
        // ================================================================

        public static List<CAD_Station> Build(SldWorks.SldWorks swApp, SolidworksModel model,
            int quantity, double spacing, CoordinateSystemType coordSystem, int axis)
        {
            ModelDoc2 swModel = (ModelDoc2)model.SwModelObject;
            FeatureManager featMgr = swModel.FeatureManager;
            var stations = new List<CAD_Station>();

            for (int i = 0; i < quantity; i++)
            {
                double offset = i * spacing;
                double[] position = CalculatePosition(coordSystem, axis, spacing, offset);

                CAD_SketchPlane sketchPlane = CreateOffsetPlane(swModel, featMgr, model, position, i, coordSystem, axis);
                if (sketchPlane != null)
                {
                    CreateCoordinateSystem(swModel, featMgr, position, i, sketchPlane);
                    CreateSketchOnPlane(swModel, sketchPlane, i, coordSystem, axis);

                    CAD_Station station = new CAD_Station(sketchPlane,
                        "Station_" + (i + 1), GetStationType(coordSystem, axis));
                    station.Name = "Station_" + (i + 1);
                    station.SetLocation(station.MyType, offset);
                    station.MyModel = model.MyCADModel;

                    stations.Add(station);
                }
            }

            return stations;
        }

        public static CAD_Station.StationTypeEnum GetStationType(CoordinateSystemType coordSystem, int axis)
        {
            switch (coordSystem)
            {
                case CoordinateSystemType.Cartesian:
                    return CAD_Station.StationTypeEnum.Axial;
                case CoordinateSystemType.Cylindrical:
                    CylindricalAxis cyAxis = (CylindricalAxis)axis;
                    if (cyAxis == CylindricalAxis.Theta)
                        return CAD_Station.StationTypeEnum.Angular;
                    if (cyAxis == CylindricalAxis.R)
                        return CAD_Station.StationTypeEnum.Radial;
                    return CAD_Station.StationTypeEnum.Axial;
                case CoordinateSystemType.Spherical:
                    SphericalAxis spAxis = (SphericalAxis)axis;
                    if (spAxis == SphericalAxis.R)
                        return CAD_Station.StationTypeEnum.Radial;
                    return CAD_Station.StationTypeEnum.Angular;
                default:
                    return CAD_Station.StationTypeEnum.Other;
            }
        }

        public static CAD_SketchPlane.GeometryTypeEnum GetGeometryType(CoordinateSystemType coordSystem)
        {
            switch (coordSystem)
            {
                case CoordinateSystemType.Cartesian:
                    return CAD_SketchPlane.GeometryTypeEnum.Cartesian;
                case CoordinateSystemType.Cylindrical:
                    return CAD_SketchPlane.GeometryTypeEnum.Cylindrical;
                case CoordinateSystemType.Spherical:
                    return CAD_SketchPlane.GeometryTypeEnum.Spherical;
                default:
                    return CAD_SketchPlane.GeometryTypeEnum.Cartesian;
            }
        }

        public static double[] CalculatePosition(CoordinateSystemType coordSystem, int axis,
            double spacing, double offset)
        {
            double[] position = new double[3];

            switch (coordSystem)
            {
                case CoordinateSystemType.Cartesian:
                    position = CalculateCartesian(axis, offset);
                    break;
                case CoordinateSystemType.Cylindrical:
                    position = CalculateCylindrical(axis, spacing, offset);
                    break;
                case CoordinateSystemType.Spherical:
                    position = CalculateSpherical(axis, spacing, offset);
                    break;
            }

            return position;
        }

        private static double[] CalculateCartesian(int axis, double offset)
        {
            double[] position = new double[3];
            CartesianAxis cAxis = (CartesianAxis)axis;

            switch (cAxis)
            {
                case CartesianAxis.X:
                    position[0] = offset;
                    break;
                case CartesianAxis.Y:
                    position[1] = offset;
                    break;
                case CartesianAxis.Z:
                    position[2] = offset;
                    break;
            }

            return position;
        }

        private static double[] CalculateCylindrical(int axis, double spacing, double offset)
        {
            double[] position = new double[3];
            CylindricalAxis cyAxis = (CylindricalAxis)axis;

            switch (cyAxis)
            {
                case CylindricalAxis.R:
                    position[0] = offset;
                    break;
                case CylindricalAxis.Theta:
                    double theta = offset;
                    position[0] = spacing * Math.Cos(theta);
                    position[1] = spacing * Math.Sin(theta);
                    break;
                case CylindricalAxis.Z:
                    position[2] = offset;
                    break;
            }

            return position;
        }

        private static double[] CalculateSpherical(int axis, double spacing, double offset)
        {
            double[] position = new double[3];
            SphericalAxis spAxis = (SphericalAxis)axis;

            switch (spAxis)
            {
                case SphericalAxis.R:
                    position[0] = offset;
                    break;
                case SphericalAxis.Theta:
                    double theta = offset;
                    position[0] = spacing * Math.Sin(theta);
                    position[2] = spacing * Math.Cos(theta);
                    break;
                case SphericalAxis.Phi:
                    double phi = offset;
                    position[0] = spacing * Math.Cos(phi);
                    position[1] = spacing * Math.Sin(phi);
                    break;
            }

            return position;
        }

        private static CAD_SketchPlane CreateOffsetPlane(ModelDoc2 swModel, FeatureManager featMgr,
            SolidworksModel model, double[] position, int index,
            CoordinateSystemType coordSystem, int axis)
        {
            double distance = Math.Sqrt(position[0] * position[0] +
                                        position[1] * position[1] +
                                        position[2] * position[2]);

            string planeName = "Station_Plane_" + (index + 1);
            double[] normal = GetPlaneNormal(coordSystem, axis);

            if (distance == 0 && index == 0)
            {
                Feature baseFeat = GetBasePlane(swModel, coordSystem, axis);
                if (baseFeat == null) return null;

                CAD_SketchPlane sketchPlane = new CAD_SketchPlane(planeName,
                    CAD_SketchPlane.FunctionalTypeEnum.Section, GetGeometryType(coordSystem));
                sketchPlane.SetNormal(normal[0], normal[1], normal[2]);
                sketchPlane.MyModel = model.MyCADModel;
                sketchPlane.Path = baseFeat.Name;
                return sketchPlane;
            }

            Feature baseFeature = GetBasePlane(swModel, coordSystem, axis);
            if (baseFeature == null) return null;

            baseFeature.Select2(false, 0);
            RefPlane refPlane = (RefPlane)featMgr.InsertRefPlane(
                (int)swRefPlaneReferenceConstraints_e.swRefPlaneReferenceConstraint_Distance,
                distance, 0, 0, 0, 0);

            if (refPlane != null)
            {
                Feature feat = (Feature)refPlane;
                feat.Name = planeName;
            }

            CAD_SketchPlane result = new CAD_SketchPlane(planeName,
                CAD_SketchPlane.FunctionalTypeEnum.Section, GetGeometryType(coordSystem));
            result.SetNormal(normal[0], normal[1], normal[2]);
            result.MyModel = model.MyCADModel;
            result.Path = planeName;
            return result;
        }

        private static double[] GetPlaneNormal(CoordinateSystemType coordSystem, int axis)
        {
            double[] normal = new double[3];

            switch (coordSystem)
            {
                case CoordinateSystemType.Cartesian:
                    CartesianAxis cAxis = (CartesianAxis)axis;
                    if (cAxis == CartesianAxis.X) normal[0] = 1;
                    else if (cAxis == CartesianAxis.Y) normal[1] = 1;
                    else normal[2] = 1;
                    break;
                case CoordinateSystemType.Cylindrical:
                    CylindricalAxis cyAxis = (CylindricalAxis)axis;
                    if (cyAxis == CylindricalAxis.Z) normal[2] = 1;
                    else normal[1] = 1;
                    break;
                default:
                    normal[2] = 1;
                    break;
            }

            return normal;
        }

        private static Feature GetBasePlane(ModelDoc2 swModel,
            CoordinateSystemType coordSystem, int axis)
        {
            string planeName;

            switch (coordSystem)
            {
                case CoordinateSystemType.Cartesian:
                    CartesianAxis cAxis = (CartesianAxis)axis;
                    planeName = cAxis == CartesianAxis.X ? "Right Plane" :
                                cAxis == CartesianAxis.Y ? "Top Plane" : "Front Plane";
                    break;
                case CoordinateSystemType.Cylindrical:
                    CylindricalAxis cyAxis = (CylindricalAxis)axis;
                    planeName = cyAxis == CylindricalAxis.Z ? "Front Plane" : "Top Plane";
                    break;
                default:
                    planeName = "Front Plane";
                    break;
            }

            return FindFeatureByName(swModel, planeName);
        }

        private static Feature FindFeatureByName(ModelDoc2 swModel, string name)
        {
            Feature feat = (Feature)swModel.FirstFeature();
            while (feat != null)
            {
                if (feat.Name == name)
                    return feat;
                feat = (Feature)feat.GetNextFeature();
            }
            return null;
        }

        private static void CreateCoordinateSystem(ModelDoc2 swModel, FeatureManager featMgr,
            double[] position, int index, CAD_SketchPlane sketchPlane)
        {
            swModel.ClearSelection2(true);

            string planeName = sketchPlane.Path + sketchPlane.Name;
            ModelDocExtension swExt = swModel.Extension;

            swExt.SelectByID2(planeName, "PLANE", position[0], position[1], position[2],
                false, 1, null, 0);

            Feature csysFeat = (Feature)featMgr.InsertCoordinateSystem(false, false, false);

            string csysName = "Station_CSYS_" + (index + 1);
            if (csysFeat != null)
            {
                csysFeat.Name = csysName;
            }

            CoordinateSystem csys = new CoordinateSystem();
            csys.OriginLocation = new Mathematics.Point
            {
                X_Value = position[0],
                Y_Value = position[1],
                Z_Value_Cartesian = position[2]
            };
            sketchPlane.MyCoordinateSystem = csys;
        }

        private static void CreateSketchOnPlane(ModelDoc2 swModel, CAD_SketchPlane sketchPlane,
            int index, CoordinateSystemType coordSystem, int axis)
        {
            Feature planeFeat = FindFeatureByName(swModel, sketchPlane.Path + sketchPlane.Name);
            if (planeFeat != null)
            {
                planeFeat.Select2(false, 0);
            }

            swModel.InsertSketch2(true);
            swModel.InsertSketch2(false);

            string sketchName = "Station_Sketch_" + (index + 1);
            Feature sketchFeat = FindFeatureByName(swModel, "Sketch" + (index + 1));
            if (sketchFeat != null)
            {
                sketchFeat.Name = sketchName;
            }

            CAD_Sketch sketch = new CAD_Sketch(sketchName);
            sketchPlane.AddSketch(sketch);
        }
    }
}
