using SwConst;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using CAD;
using Mathematics;

namespace SolidworksLibrary
{
    public class PartBuilder
    {
        // ================================================================
        // Instance state for database operations
        // ================================================================
        private readonly string _databasePath;
        private string ConnectionString => $"Data Source={_databasePath};Version=3;Foreign Keys=True;";

        /// <summary>
        /// Creates a PartBuilder instance configured for database operations.
        /// </summary>
        /// <param name="databasePath">Full path to the SQLite database file.</param>
        public PartBuilder(string databasePath)
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
        /// Persists one or more <see cref="CAD_Part"/> objects (and their full dependent
        /// entity graph) to the database.
        /// Uses INSERT OR REPLACE for idempotent upsert semantics.
        /// </summary>
        public void SaveParts(IEnumerable<CAD_Part> parts)
        {
            if (parts == null) throw new ArgumentNullException(nameof(parts));

            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                using (var txn = conn.BeginTransaction())
                {
                    foreach (var part in parts)
                    {
                        SavePartCore(conn, part);
                    }
                    txn.Commit();
                }
            }
        }

        /// <summary>Convenience overload — persists a single part.</summary>
        public void SavePart(CAD_Part part)
        {
            if (part == null) throw new ArgumentNullException(nameof(part));
            SaveParts(new[] { part });
        }

        private void SavePartCore(SQLiteConnection conn, CAD_Part part)
        {
            var partId = part.Name ?? part.PartNumber ?? GenerateId();

            // Ensure cursor / FK references

            string currentMassPropsId = null;
            if (part.CurrentMassProperties != null)
            {
                currentMassPropsId = GenerateId();
                EnsureMassProperties(conn, part.CurrentMassProperties, currentMassPropsId);
            }

            string myMassPropsId = null;
            if (part.MyMassProperties != null)
            {
                myMassPropsId = GenerateId();
                EnsureMassProperties(conn, part.MyMassProperties, myMassPropsId);
            }

            string centerOfMassId = null;
            if (part.CenterOfMass != null)
            {
                centerOfMassId = part.CenterOfMass.PointID ?? GenerateId();
                EnsurePoint(conn, part.CenterOfMass, centerOfMassId);
            }

            string currentModelId = null;
            if (part.CurrentModel != null)
            {
                currentModelId = part.CurrentModel.Name ?? GenerateId();
                EnsureModel(conn, part.CurrentModel, currentModelId);
            }

            string originCsId = null;
            if (part.MyOrigin != null)
            {
                originCsId = part.MyOrigin.CoordinateSystemID
                             ?? part.MyOrigin.Name
                             ?? GenerateId();
                EnsureCoordinateSystem(conn, part.MyOrigin, originCsId);
            }

            string currentSketchId = null;
            if (part.CurrentSketch != null)
            {
                currentSketchId = part.CurrentSketch.SketchID ?? GenerateId();
                EnsureSketch(conn, part.CurrentSketch, currentSketchId);
            }

            string currentFeatureId = null;
            if (part.CurrentFeature != null)
            {
                currentFeatureId = part.CurrentFeature.Name ?? GenerateId();
                EnsureFeature(conn, part.CurrentFeature, currentFeatureId);
            }

            string currentBodyId = null;
            if (part.CurrentBody != null)
            {
                currentBodyId = part.CurrentBody.Name ?? GenerateId();
                EnsureBody(conn, part.CurrentBody, currentBodyId);
            }

            string currentDrawingId = null;
            if (part.CurrentDrawing != null)
            {
                currentDrawingId = part.CurrentDrawing.DrawingNumber
                                   ?? part.CurrentDrawing.Title
                                   ?? GenerateId();
                EnsureDrawing(conn, part.CurrentDrawing, currentDrawingId);
            }

            string currentDimId = null;
            if (part.CurrentDimension != null)
            {
                currentDimId = part.CurrentDimension.DimensionID ?? GenerateId();
                EnsureDimension(conn, currentDimId, part.CurrentDimension);
            }

            string currentParamId = null;
            if (part.CurrentParameter != null)
            {
                currentParamId = part.CurrentParameter.Id
                                 ?? part.CurrentParameter.Name
                                 ?? GenerateId();
                EnsureParameter(conn, part.CurrentParameter, currentParamId);
            }

            string assemblyId = null;
            if (part.MyAssembly != null)
            {
                assemblyId = part.MyAssembly.Name ?? GenerateId();
                EnsureAssembly(conn, part.MyAssembly, assemblyId);
            }

            string currentLibraryId = null;
            if (part.CurrentLibrary != null)
            {
                currentLibraryId = part.CurrentLibrary.Name ?? GenerateId();
                EnsureLibrary(conn, part.CurrentLibrary, currentLibraryId);
            }

            string currentInterfaceId = null;
            if (part.CurrentInterface != null)
            {
                currentInterfaceId = part.CurrentInterface.ID
                                     ?? part.CurrentInterface.Name
                                     ?? GenerateId();
                EnsureInterface(conn, part.CurrentInterface, currentInterfaceId);
            }

            // INSERT OR REPLACE the part row
            const string partSql =
                @"INSERT OR REPLACE INTO CAD_Part
                  (PartID, Name, Version, PartNumber, Description,
                   CurrentMassPropertiesID, MyMassPropertiesID, CenterOfMassPointID,
                   CurrentModelID, CurrentCoordinateSystemID,
                   CurrentSketchID, CurrentFeatureID, CurrentBodyID,
                   CurrentDrawingID, CurrentDimensionID, CurrentParameterID,
                   MyAssemblyID, CurrentLibraryID, CurrentInterfaceID)
                  VALUES
                  (@id, @name, @ver, @pn, @desc,
                   @curMpId, @myMpId, @comId,
                   @curModelId, @originCsId,
                   @curSketchId, @curFeatId, @curBodyId,
                   @curDrawId, @curDimId, @curParamId,
                   @asmId, @curLibId, @curIfaceId);";

            using (var cmd = new SQLiteCommand(partSql, conn))
            {
                cmd.Parameters.AddWithValue("@id", partId);
                cmd.Parameters.AddWithValue("@name", (object)part.Name ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ver", (object)part.Version ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@pn", (object)part.PartNumber ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@desc", (object)part.Description ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@curMpId", (object)currentMassPropsId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@myMpId", (object)myMassPropsId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@comId", (object)centerOfMassId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@curModelId", (object)currentModelId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@originCsId", (object)originCsId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@curSketchId", (object)currentSketchId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@curFeatId", (object)currentFeatureId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@curBodyId", (object)currentBodyId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@curDrawId", (object)currentDrawingId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@curDimId", (object)currentDimId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@curParamId", (object)currentParamId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@asmId", (object)assemblyId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@curLibId", (object)currentLibraryId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@curIfaceId", (object)currentInterfaceId ?? DBNull.Value);
                cmd.ExecuteNonQuery();
            }

            // Junction tables — clear and re-insert each collection
            SavePartSketches(conn, partId, part.MySketches);
            SavePartFeatures(conn, partId, part.MyFeatures);
            SavePartBodies(conn, partId, part.MyBodies);
            SavePartDrawings(conn, partId, part.MyDrawings);
            SavePartDimensions(conn, partId, part.MyDimensions);
            SavePartParameters(conn, partId, part.MyParameters);
            SavePartModels(conn, partId, part.MyModels);
            SavePartCoordinateSystems(conn, partId, part.MyCoordinateSystems);
            SavePartInterfaces(conn, partId, part.MyInterfaces);
            if (part.MyLibraries != null)
                SavePartLibraries(conn, partId, part.MyLibraries);
            SavePartMassProperties(conn, partId, part.MyMassPropertiesList);
            SavePartStations(conn, partId, "Axial", part.AxialStations);
            SavePartStations(conn, partId, "Radial", part.RadialStations);
            SavePartStations(conn, partId, "Angular", part.AngularStations);
            SavePartStations(conn, partId, "Wing", part.WingStations);
        }

        // ================================================================
        // Database: Junction table save helpers
        // ================================================================

        private void SavePartSketches(SQLiteConnection conn, string partId, List<CAD_Sketch> sketches)
        {
            DeleteJunction(conn, "CAD_Part_Sketch", "PartID", partId);
            for (int i = 0; i < sketches.Count; i++)
            {
                var sk = sketches[i];
                var skId = sk.SketchID ?? GenerateId();
                EnsureSketch(conn, sk, skId);
                InsertJunction(conn, "CAD_Part_Sketch", "PartID", partId, "SketchID", skId, i);
            }
        }

        private void SavePartFeatures(SQLiteConnection conn, string partId, List<CAD_Feature> features)
        {
            DeleteJunction(conn, "CAD_Part_Feature", "PartID", partId);
            for (int i = 0; i < features.Count; i++)
            {
                var f = features[i];
                var fId = f.Name ?? GenerateId();
                EnsureFeature(conn, f, fId);
                InsertJunction(conn, "CAD_Part_Feature", "PartID", partId, "FeatureID", fId, i);
            }
        }

        private void SavePartBodies(SQLiteConnection conn, string partId, List<CAD_Body> bodies)
        {
            DeleteJunction(conn, "CAD_Part_Body", "PartID", partId);
            for (int i = 0; i < bodies.Count; i++)
            {
                var b = bodies[i];
                var bId = b.Name ?? GenerateId();
                EnsureBody(conn, b, bId);
                InsertJunction(conn, "CAD_Part_Body", "PartID", partId, "BodyID", bId, i);
            }
        }

        private void SavePartDrawings(SQLiteConnection conn, string partId, List<CAD_Drawing> drawings)
        {
            DeleteJunction(conn, "CAD_Part_Drawing", "PartID", partId);
            for (int i = 0; i < drawings.Count; i++)
            {
                var d = drawings[i];
                var dId = d.DrawingNumber ?? d.Title ?? GenerateId();
                EnsureDrawing(conn, d, dId);
                InsertJunction(conn, "CAD_Part_Drawing", "PartID", partId, "DrawingID", dId, i);
            }
        }

        private void SavePartDimensions(SQLiteConnection conn, string partId, List<CAD_Dimension> dimensions)
        {
            DeleteJunction(conn, "CAD_Part_Dimension", "PartID", partId);
            for (int i = 0; i < dimensions.Count; i++)
            {
                var dim = dimensions[i];
                var dimId = dim.DimensionID ?? GenerateId();
                EnsureDimension(conn, dimId, dim);
                InsertJunction(conn, "CAD_Part_Dimension", "PartID", partId, "DimensionID", dimId, i);
            }
        }

        private void SavePartParameters(SQLiteConnection conn, string partId, List<CAD_Parameter> parameters)
        {
            DeleteJunction(conn, "CAD_Part_Parameter", "PartID", partId);
            for (int i = 0; i < parameters.Count; i++)
            {
                var p = parameters[i];
                var pId = p.Id ?? p.Name ?? GenerateId();
                EnsureParameter(conn, p, pId);
                InsertJunction(conn, "CAD_Part_Parameter", "PartID", partId, "MathParameterID", pId, i);
            }
        }

        private void SavePartModels(SQLiteConnection conn, string partId, List<CAD_Model> models)
        {
            DeleteJunction(conn, "CAD_Part_Model", "PartID", partId);
            for (int i = 0; i < models.Count; i++)
            {
                var m = models[i];
                var mId = m.Name ?? GenerateId();
                EnsureModel(conn, m, mId);
                InsertJunction(conn, "CAD_Part_Model", "PartID", partId, "ModelID", mId, i);
            }
        }

        private void SavePartCoordinateSystems(SQLiteConnection conn, string partId,
            List<CoordinateSystem> coordinateSystems)
        {
            DeleteJunction(conn, "CAD_Part_CoordinateSystem", "PartID", partId);
            for (int i = 0; i < coordinateSystems.Count; i++)
            {
                var cs = coordinateSystems[i];
                var csId = cs.CoordinateSystemID ?? cs.Name ?? GenerateId();
                EnsureCoordinateSystem(conn, cs, csId);
                InsertJunction(conn, "CAD_Part_CoordinateSystem", "PartID", partId,
                    "CoordinateSystemID", csId, i);
            }
        }

        private void SavePartInterfaces(SQLiteConnection conn, string partId,
            List<CAD_Interface> interfaces)
        {
            DeleteJunction(conn, "CAD_Part_Interface", "PartID", partId);
            for (int i = 0; i < interfaces.Count; i++)
            {
                var iface = interfaces[i];
                var ifId = iface.ID ?? iface.Name ?? GenerateId();
                EnsureInterface(conn, iface, ifId);
                InsertJunction(conn, "CAD_Part_Interface", "PartID", partId, "InterfaceID", ifId, i);
            }
        }

        private void SavePartLibraries(SQLiteConnection conn, string partId,
            List<CAD_Library> libraries)
        {
            DeleteJunction(conn, "CAD_Part_Library", "PartID", partId);
            for (int i = 0; i < libraries.Count; i++)
            {
                var lib = libraries[i];
                var libId = lib.Name ?? GenerateId();
                EnsureLibrary(conn, lib, libId);
                InsertJunction(conn, "CAD_Part_Library", "PartID", partId, "LibraryID", libId, i);
            }
        }

        private void SavePartMassProperties(SQLiteConnection conn, string partId,
            List<MassProperties> massPropsList)
        {
            DeleteJunction(conn, "CAD_Part_MassProperties", "PartID", partId);
            for (int i = 0; i < massPropsList.Count; i++)
            {
                var mp = massPropsList[i];
                var mpId = GenerateId();
                EnsureMassProperties(conn, mp, mpId);
                InsertJunction(conn, "CAD_Part_MassProperties", "PartID", partId,
                    "MassPropertiesID", mpId, i);
            }
        }

        private void SavePartStations(SQLiteConnection conn, string partId,
            string category, List<CAD_Station> stations)
        {
            // Delete only for the given category
            using (var cmd = new SQLiteCommand(
                "DELETE FROM CAD_Part_Station WHERE PartID = @pid AND StationCategory = @cat;", conn))
            {
                cmd.Parameters.AddWithValue("@pid", partId);
                cmd.Parameters.AddWithValue("@cat", category);
                cmd.ExecuteNonQuery();
            }

            for (int i = 0; i < stations.Count; i++)
            {
                var st = stations[i];
                var stId = st.ID ?? GenerateId();
                EnsureStation(conn, st, stId);

                using (var cmd = new SQLiteCommand(
                    @"INSERT INTO CAD_Part_Station (PartID, StationID, StationCategory, SortOrder)
                      VALUES (@pid, @sid, @cat, @ord);", conn))
                {
                    cmd.Parameters.AddWithValue("@pid", partId);
                    cmd.Parameters.AddWithValue("@sid", stId);
                    cmd.Parameters.AddWithValue("@cat", category);
                    cmd.Parameters.AddWithValue("@ord", i);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // Generic junction table helpers
        private static void DeleteJunction(SQLiteConnection conn, string table,
            string keyCol, string keyVal)
        {
            using (var cmd = new SQLiteCommand($"DELETE FROM {table} WHERE {keyCol} = @id;", conn))
            {
                cmd.Parameters.AddWithValue("@id", keyVal);
                cmd.ExecuteNonQuery();
            }
        }

        private static void InsertJunction(SQLiteConnection conn, string table,
            string parentCol, string parentVal,
            string childCol, string childVal, int sortOrder)
        {
            using (var cmd = new SQLiteCommand(
                $@"INSERT INTO {table} ({parentCol}, {childCol}, SortOrder)
                   VALUES (@pid, @cid, @ord);", conn))
            {
                cmd.Parameters.AddWithValue("@pid", parentVal);
                cmd.Parameters.AddWithValue("@cid", childVal);
                cmd.Parameters.AddWithValue("@ord", sortOrder);
                cmd.ExecuteNonQuery();
            }
        }

        // ================================================================
        // Database: Read operations
        // ================================================================

        /// <summary>
        /// Loads all parts from the database, optionally filtered by assembly ID.
        /// Reconstructs the full object graph.
        /// </summary>
        public List<CAD_Part> LoadParts(string assemblyId = null)
        {
            var parts = new List<CAD_Part>();

            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();

                var sql = assemblyId != null
                    ? "SELECT * FROM CAD_Part WHERE MyAssemblyID = @aid ORDER BY PartID;"
                    : "SELECT * FROM CAD_Part ORDER BY PartID;";

                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    if (assemblyId != null)
                        cmd.Parameters.AddWithValue("@aid", assemblyId);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var part = ReadPartFromRow(reader);
                            parts.Add(part);
                        }
                    }
                }

                foreach (var part in parts)
                {
                    var partId = part.Name ?? part.PartNumber;
                    if (partId == null) continue;
                    LoadPartChildren(conn, part, partId);
                }
            }

            return parts;
        }

        /// <summary>Loads a single part by its ID.</summary>
        public CAD_Part LoadPart(string partId)
        {
            if (string.IsNullOrWhiteSpace(partId))
                throw new ArgumentException("Part ID must not be null or empty.", nameof(partId));

            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();

                CAD_Part part = null;
                using (var cmd = new SQLiteCommand(
                    "SELECT * FROM CAD_Part WHERE PartID = @id;", conn))
                {
                    cmd.Parameters.AddWithValue("@id", partId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                            part = ReadPartFromRow(reader);
                    }
                }

                if (part == null) return null;

                LoadPartChildren(conn, part, partId);
                return part;
            }
        }

        // ================================================================
        // Database: Private helpers — Read
        // ================================================================

        private static CAD_Part ReadPartFromRow(SQLiteDataReader reader)
        {
            return new CAD_Part
            {
                Name = reader["Name"] as string,
                Version = reader["Version"] as string,
                PartNumber = reader["PartNumber"] as string,
                Description = reader["Description"] as string
            };
        }

        private void LoadPartChildren(SQLiteConnection conn, CAD_Part part, string partId)
        {
            // Load MyOrigin coordinate system
            var originCsId = GetScalar(conn,
                "SELECT CurrentCoordinateSystemID FROM CAD_Part WHERE PartID = @id;", partId);
            if (originCsId != null)
                part.MyOrigin = LoadCoordinateSystem(conn, originCsId);

            // Load CenterOfMass point
            var comId = GetScalar(conn,
                "SELECT CenterOfMassPointID FROM CAD_Part WHERE PartID = @id;", partId);
            if (comId != null)
                part.CenterOfMass = LoadPoint(conn, comId);

            // Load MyMassProperties
            var mpId = GetScalar(conn,
                "SELECT MyMassPropertiesID FROM CAD_Part WHERE PartID = @id;", partId);
            if (mpId != null)
                part.MyMassProperties = LoadMassProperties(conn, mpId);

            // Load CurrentMassProperties
            var cmpId = GetScalar(conn,
                "SELECT CurrentMassPropertiesID FROM CAD_Part WHERE PartID = @id;", partId);
            if (cmpId != null)
                part.CurrentMassProperties = LoadMassProperties(conn, cmpId);

            // Load CurrentModel
            var curModelId = GetScalar(conn,
                "SELECT CurrentModelID FROM CAD_Part WHERE PartID = @id;", partId);
            if (curModelId != null)
                part.CurrentModel = LoadModel(conn, curModelId);

            // Load Assembly
            var asmId = GetScalar(conn,
                "SELECT MyAssemblyID FROM CAD_Part WHERE PartID = @id;", partId);
            if (asmId != null)
                part.MyAssembly = LoadAssemblyStub(conn, asmId);

            // Load cursor references
            var curSkId = GetScalar(conn,
                "SELECT CurrentSketchID FROM CAD_Part WHERE PartID = @id;", partId);
            if (curSkId != null)
                part.CurrentSketch = LoadSketchStub(conn, curSkId);

            var curFeatId = GetScalar(conn,
                "SELECT CurrentFeatureID FROM CAD_Part WHERE PartID = @id;", partId);
            if (curFeatId != null)
                part.CurrentFeature = LoadFeatureStub(conn, curFeatId);

            var curBodyId = GetScalar(conn,
                "SELECT CurrentBodyID FROM CAD_Part WHERE PartID = @id;", partId);
            if (curBodyId != null)
                part.CurrentBody = LoadBodyStub(conn, curBodyId);

            var curDrawId = GetScalar(conn,
                "SELECT CurrentDrawingID FROM CAD_Part WHERE PartID = @id;", partId);
            if (curDrawId != null)
                part.CurrentDrawing = LoadDrawingStub(conn, curDrawId);

            var curDimId = GetScalar(conn,
                "SELECT CurrentDimensionID FROM CAD_Part WHERE PartID = @id;", partId);
            if (curDimId != null)
                part.CurrentDimension = LoadDimension(conn, curDimId);

            var curParamId = GetScalar(conn,
                "SELECT CurrentParameterID FROM CAD_Part WHERE PartID = @id;", partId);
            if (curParamId != null)
                part.CurrentParameter = LoadParameterStub(conn, curParamId);

            var curLibId = GetScalar(conn,
                "SELECT CurrentLibraryID FROM CAD_Part WHERE PartID = @id;", partId);
            if (curLibId != null)
                part.CurrentLibrary = LoadLibrary(conn, curLibId);

            var curIfaceId = GetScalar(conn,
                "SELECT CurrentInterfaceID FROM CAD_Part WHERE PartID = @id;", partId);
            if (curIfaceId != null)
                part.CurrentInterface = LoadInterfaceStub(conn, curIfaceId);

            // Load collections
            LoadPartSketchCollection(conn, part, partId);
            LoadPartFeatureCollection(conn, part, partId);
            LoadPartBodyCollection(conn, part, partId);
            LoadPartDrawingCollection(conn, part, partId);
            LoadPartDimensionCollection(conn, part, partId);
            LoadPartParameterCollection(conn, part, partId);
            LoadPartModelCollection(conn, part, partId);
            LoadPartCoordinateSystemCollection(conn, part, partId);
            LoadPartInterfaceCollection(conn, part, partId);
            LoadPartLibraryCollection(conn, part, partId);
            LoadPartMassPropertiesCollection(conn, part, partId);
            LoadPartStationCollection(conn, part, partId, "Axial", part.AxialStations);
            LoadPartStationCollection(conn, part, partId, "Radial", part.RadialStations);
            LoadPartStationCollection(conn, part, partId, "Angular", part.AngularStations);
            LoadPartStationCollection(conn, part, partId, "Wing", part.WingStations);
        }

        private static string GetScalar(SQLiteConnection conn, string sql, string id)
        {
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                return cmd.ExecuteScalar() as string;
            }
        }

        // ================================================================
        // Database: Collection loaders
        // ================================================================

        private void LoadPartSketchCollection(SQLiteConnection conn, CAD_Part part, string partId)
        {
            const string sql =
                @"SELECT sk.* FROM CAD_Part_Sketch ps
                  JOIN CAD_Sketch sk ON ps.SketchID = sk.SketchID
                  WHERE ps.PartID = @id ORDER BY ps.SortOrder;";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", partId);
                using (var reader = cmd.ExecuteReader())
                    while (reader.Read())
                        part.AddSketch(ReadSketchStubFromRow(reader));
            }
        }

        private void LoadPartFeatureCollection(SQLiteConnection conn, CAD_Part part, string partId)
        {
            const string sql =
                @"SELECT f.* FROM CAD_Part_Feature pf
                  JOIN CAD_Feature f ON pf.FeatureID = f.FeatureID
                  WHERE pf.PartID = @id ORDER BY pf.SortOrder;";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", partId);
                using (var reader = cmd.ExecuteReader())
                    while (reader.Read())
                        part.AddFeature(ReadFeatureStubFromRow(reader));
            }
        }

        private void LoadPartBodyCollection(SQLiteConnection conn, CAD_Part part, string partId)
        {
            const string sql =
                @"SELECT b.* FROM CAD_Part_Body pb
                  JOIN CAD_Body b ON pb.BodyID = b.BodyID
                  WHERE pb.PartID = @id ORDER BY pb.SortOrder;";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", partId);
                using (var reader = cmd.ExecuteReader())
                    while (reader.Read())
                        part.AddBody(ReadBodyStubFromRow(reader));
            }
        }

        private void LoadPartDrawingCollection(SQLiteConnection conn, CAD_Part part, string partId)
        {
            const string sql =
                @"SELECT d.* FROM CAD_Part_Drawing pd
                  JOIN CAD_Drawing d ON pd.DrawingID = d.DrawingID
                  WHERE pd.PartID = @id ORDER BY pd.SortOrder;";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", partId);
                using (var reader = cmd.ExecuteReader())
                    while (reader.Read())
                        part.MyDrawings.Add(ReadDrawingStubFromRow(reader));
            }
        }

        private void LoadPartDimensionCollection(SQLiteConnection conn, CAD_Part part, string partId)
        {
            const string sql =
                @"SELECT d.* FROM CAD_Part_Dimension pd
                  JOIN CAD_Dimension d ON pd.DimensionID = d.DimensionID
                  WHERE pd.PartID = @id ORDER BY pd.SortOrder;";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", partId);
                using (var reader = cmd.ExecuteReader())
                    while (reader.Read())
                        part.AddDimension(ReadDimensionFromRow(conn, reader));
            }
        }

        private void LoadPartParameterCollection(SQLiteConnection conn, CAD_Part part, string partId)
        {
            const string sql =
                @"SELECT p.* FROM CAD_Part_Parameter pp
                  JOIN MathParameter p ON pp.MathParameterID = p.MathParameterID
                  WHERE pp.PartID = @id ORDER BY pp.SortOrder;";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", partId);
                using (var reader = cmd.ExecuteReader())
                    while (reader.Read())
                        part.AddParameter(ReadParameterStubFromRow(reader));
            }
        }

        private void LoadPartModelCollection(SQLiteConnection conn, CAD_Part part, string partId)
        {
            const string sql =
                @"SELECT m.* FROM CAD_Part_Model pm
                  JOIN CAD_Model m ON pm.ModelID = m.ModelID
                  WHERE pm.PartID = @id ORDER BY pm.SortOrder;";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", partId);
                using (var reader = cmd.ExecuteReader())
                    while (reader.Read())
                        part.MyModels.Add(ReadModelFromRow(reader));
            }
        }

        private void LoadPartCoordinateSystemCollection(SQLiteConnection conn, CAD_Part part,
            string partId)
        {
            const string sql =
                @"SELECT cs.* FROM CAD_Part_CoordinateSystem pcs
                  JOIN CoordinateSystem cs ON pcs.CoordinateSystemID = cs.CoordinateSystemID
                  WHERE pcs.PartID = @id ORDER BY pcs.SortOrder;";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", partId);
                using (var reader = cmd.ExecuteReader())
                    while (reader.Read())
                        part.MyCoordinateSystems.Add(ReadCoordinateSystemFromRow(conn, reader));
            }
        }

        private void LoadPartInterfaceCollection(SQLiteConnection conn, CAD_Part part, string partId)
        {
            const string sql =
                @"SELECT i.* FROM CAD_Part_Interface pi
                  JOIN CAD_Interface i ON pi.InterfaceID = i.InterfaceID
                  WHERE pi.PartID = @id ORDER BY pi.SortOrder;";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", partId);
                using (var reader = cmd.ExecuteReader())
                    while (reader.Read())
                        part.MyInterfaces.Add(ReadInterfaceStubFromRow(reader));
            }
        }

        private void LoadPartLibraryCollection(SQLiteConnection conn, CAD_Part part, string partId)
        {
            const string sql =
                @"SELECT lib.* FROM CAD_Part_Library pl
                  JOIN CAD_Library lib ON pl.LibraryID = lib.LibraryID
                  WHERE pl.PartID = @id ORDER BY pl.SortOrder;";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", partId);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if (part.MyLibraries == null)
                            part.MyLibraries = new List<CAD_Library>();
                        part.MyLibraries.Add(ReadLibraryFromRow(reader));
                    }
                }
            }
        }

        private void LoadPartMassPropertiesCollection(SQLiteConnection conn, CAD_Part part,
            string partId)
        {
            const string sql =
                @"SELECT mp.* FROM CAD_Part_MassProperties pmp
                  JOIN MassProperties mp ON pmp.MassPropertiesID = mp.MassPropertiesID
                  WHERE pmp.PartID = @id ORDER BY pmp.SortOrder;";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", partId);
                using (var reader = cmd.ExecuteReader())
                    while (reader.Read())
                        part.MyMassPropertiesList.Add(ReadMassPropertiesFromRow(conn, reader));
            }
        }

        private void LoadPartStationCollection(SQLiteConnection conn, CAD_Part part,
            string partId, string category, List<CAD_Station> targetList)
        {
            const string sql =
                @"SELECT st.* FROM CAD_Part_Station ps
                  JOIN CAD_Station st ON ps.StationID = st.StationID
                  WHERE ps.PartID = @id AND ps.StationCategory = @cat
                  ORDER BY ps.SortOrder;";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", partId);
                cmd.Parameters.AddWithValue("@cat", category);
                using (var reader = cmd.ExecuteReader())
                    while (reader.Read())
                        targetList.Add(ReadStationStubFromRow(reader));
            }
        }

        // ================================================================
        // Database: Row-to-object readers
        // ================================================================

        private static CAD_Sketch ReadSketchStubFromRow(SQLiteDataReader reader)
        {
            return new CAD_Sketch(reader["SketchID"] as string)
            {
                Version = reader["Version"] as string,
                IsTwoD = Convert.ToInt32(reader["IsTwoD"]) != 0
            };
        }

        private static CAD_Feature ReadFeatureStubFromRow(SQLiteDataReader reader)
        {
            return new CAD_Feature
            {
                Name = reader["Name"] as string,
                Version = reader["Version"] as string,
                GeometricFeatureType = (CAD_Feature.GeometricFeatureTypeEnum)
                    Convert.ToInt32(reader["GeometricFeatureType"])
            };
        }

        private static CAD_Body ReadBodyStubFromRow(SQLiteDataReader reader)
        {
            return new CAD_Body
            {
                Name = reader["Name"] as string,
                Version = reader["Version"] as string,
                PartNumber = reader["PartNumber"] as string,
                GeometricFeatureType = (CAD_Feature.GeometricFeatureTypeEnum)
                    Convert.ToInt32(reader["GeometricFeatureType"])
            };
        }

        private static CAD_Drawing ReadDrawingStubFromRow(SQLiteDataReader reader)
        {
            return new CAD_Drawing
            {
                Title = reader["Title"] as string,
                DrawingNumber = reader["DrawingNumber"] as string,
                Revision = reader["Revision"] as string,
                DrawingStandard = (CAD_Drawing.DrawingStandardEnum)
                    Convert.ToInt32(reader["DrawingStandard"]),
                MyFormat = (CAD_Drawing.DocFormatEnum)Convert.ToInt32(reader["MyFormat"]),
                MyDrawingSize = (CAD_Drawing.DrawingSize)Convert.ToInt32(reader["MyDrawingSize"])
            };
        }

        private static CAD_Station ReadStationStubFromRow(SQLiteDataReader reader)
        {
            var stationType = (CAD_Station.StationTypeEnum)Convert.ToInt32(reader["MyType"]);
            double locationValue = 0;
            switch (stationType)
            {
                case CAD_Station.StationTypeEnum.Axial:
                    locationValue = Convert.ToDouble(reader["AxialLocation"]); break;
                case CAD_Station.StationTypeEnum.Radial:
                    locationValue = Convert.ToDouble(reader["RadialLocation"]); break;
                case CAD_Station.StationTypeEnum.Angular:
                    locationValue = Convert.ToDouble(reader["AngularLocation"]); break;
                case CAD_Station.StationTypeEnum.Wing:
                    locationValue = Convert.ToDouble(reader["WingLocation"]); break;
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

        private static CAD_Interface ReadInterfaceStubFromRow(SQLiteDataReader reader)
        {
            return new CAD_Interface
            {
                Name = reader["Name"] as string,
                ID = reader["ID"] as string,
                Version = reader["Version"] as string,
                InterfaceKind = (CAD_Interface.InterfaceType)
                    Convert.ToInt32(reader["InterfaceKind"] ?? 0)
            };
        }

        private static CAD_Parameter ReadParameterStubFromRow(SQLiteDataReader reader)
        {
            return new CAD_Parameter
            {
                Name = reader["Name"] as string,
                Id = reader["MathParameterID"] as string,
                Description = reader["Description"] as string,
                Comments = reader["Comments"] as string,
                MyParameterType = (CAD_Parameter.ParameterType)
                    Convert.ToInt32(reader["MyParameterType"]),
                SolidWorksParameterName = reader["SolidWorksParameterName"] as string,
                Fusion360ParameterName = reader["Fusion360ParameterName"] as string
            };
        }

        private static CAD_Model ReadModelFromRow(SQLiteDataReader reader)
        {
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
            var cpId = reader["CenterPointID"] as string;
            if (cpId != null) dim.CenterPoint = LoadPoint(conn, cpId);
            var leId = reader["LeaderLineEndPointID"] as string;
            if (leId != null) dim.LeaderLineEndPoint = LoadPoint(conn, leId);
            var lbId = reader["LeaderLineBendPointID"] as string;
            if (lbId != null) dim.LeaderLineBendPoint = LoadPoint(conn, lbId);
            var dpId = reader["DimensionPointID"] as string;
            if (dpId != null) dim.DimensionPoint = LoadPoint(conn, dpId);
            var rpId = reader["ReferencePointID"] as string;
            if (rpId != null) dim.ReferencePoint = LoadPoint(conn, rpId);
            var dmId = reader["MyModelID"] as string;
            if (dmId != null) dim.MyModel = LoadModel(conn, dmId);
            return dim;
        }

        private MassProperties ReadMassPropertiesFromRow(SQLiteConnection conn,
            SQLiteDataReader reader)
        {
            var mp = new MassProperties
            {
                Mass = Convert.ToDouble(reader["Mass"])
            };
            var cogId = reader["CenterOfGravityPointID"] as string;
            if (cogId != null)
                mp.CenterOfGravity = LoadPoint(conn, cogId);
            var csId = reader["CurrentCoordinateSystemID"] as string;
            if (csId != null)
                mp.CurrentCoordinateSystem = LoadCoordinateSystem(conn, csId);
            return mp;
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
            if (originId != null) cs.OriginLocation = LoadPoint(conn, originId);
            var baseVecId = reader["BaseVectorID"] as string;
            if (baseVecId != null) cs.BaseVector = LoadVector(conn, baseVecId);
            return cs;
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
        // Database: Shared entity loaders
        // ================================================================

        private CAD_Sketch LoadSketchStub(SQLiteConnection conn, string sketchId)
        {
            const string sql = "SELECT * FROM CAD_Sketch WHERE SketchID = @id;";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", sketchId);
                using (var reader = cmd.ExecuteReader())
                    return reader.Read() ? ReadSketchStubFromRow(reader) : null;
            }
        }

        private CAD_Feature LoadFeatureStub(SQLiteConnection conn, string featureId)
        {
            const string sql = "SELECT * FROM CAD_Feature WHERE FeatureID = @id;";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", featureId);
                using (var reader = cmd.ExecuteReader())
                    return reader.Read() ? ReadFeatureStubFromRow(reader) : null;
            }
        }

        private CAD_Body LoadBodyStub(SQLiteConnection conn, string bodyId)
        {
            const string sql = "SELECT * FROM CAD_Body WHERE BodyID = @id;";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", bodyId);
                using (var reader = cmd.ExecuteReader())
                    return reader.Read() ? ReadBodyStubFromRow(reader) : null;
            }
        }

        private CAD_Drawing LoadDrawingStub(SQLiteConnection conn, string drawingId)
        {
            const string sql = "SELECT * FROM CAD_Drawing WHERE DrawingID = @id;";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", drawingId);
                using (var reader = cmd.ExecuteReader())
                    return reader.Read() ? ReadDrawingStubFromRow(reader) : null;
            }
        }

        private CAD_Dimension LoadDimension(SQLiteConnection conn, string dimId)
        {
            const string sql = "SELECT * FROM CAD_Dimension WHERE DimensionID = @id;";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", dimId);
                using (var reader = cmd.ExecuteReader())
                    return reader.Read() ? ReadDimensionFromRow(conn, reader) : null;
            }
        }

        private CAD_Parameter LoadParameterStub(SQLiteConnection conn, string paramId)
        {
            const string sql = "SELECT * FROM MathParameter WHERE MathParameterID = @id;";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", paramId);
                using (var reader = cmd.ExecuteReader())
                    return reader.Read() ? ReadParameterStubFromRow(reader) : null;
            }
        }

        private CAD_Interface LoadInterfaceStub(SQLiteConnection conn, string ifaceId)
        {
            const string sql = "SELECT * FROM CAD_Interface WHERE InterfaceID = @id;";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", ifaceId);
                using (var reader = cmd.ExecuteReader())
                    return reader.Read() ? ReadInterfaceStubFromRow(reader) : null;
            }
        }

        private static CAD_Library LoadLibrary(SQLiteConnection conn, string libraryId)
        {
            const string sql = "SELECT * FROM CAD_Library WHERE LibraryID = @id;";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", libraryId);
                using (var reader = cmd.ExecuteReader())
                    return reader.Read() ? ReadLibraryFromRow(reader) : null;
            }
        }

        private CAD_Assembly LoadAssemblyStub(SQLiteConnection conn, string asmId)
        {
            const string sql = "SELECT * FROM CAD_Assembly WHERE AssemblyID = @id;";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", asmId);
                using (var reader = cmd.ExecuteReader())
                {
                    if (!reader.Read()) return null;
                    return new CAD_Assembly
                    {
                        Name = reader["Name"] as string,
                        Version = reader["Version"] as string,
                        Description = reader["Description"] as string,
                        IsSubAssembly = Convert.ToInt32(reader["IsSubAssembly"]) != 0,
                        IsConfigurationItem = Convert.ToInt32(reader["IsConfigurationItem"]) != 0
                    };
                }
            }
        }

        private MassProperties LoadMassProperties(SQLiteConnection conn, string mpId)
        {
            const string sql = "SELECT * FROM MassProperties WHERE MassPropertiesID = @id;";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", mpId);
                using (var reader = cmd.ExecuteReader())
                    return reader.Read() ? ReadMassPropertiesFromRow(conn, reader) : null;
            }
        }

        private CoordinateSystem LoadCoordinateSystem(SQLiteConnection conn, string csId)
        {
            const string sql = "SELECT * FROM CoordinateSystem WHERE CoordinateSystemID = @id;";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", csId);
                using (var reader = cmd.ExecuteReader())
                    return reader.Read() ? ReadCoordinateSystemFromRow(conn, reader) : null;
            }
        }

        private Point LoadPoint(SQLiteConnection conn, string pointId)
        {
            const string sql = "SELECT * FROM Point WHERE PointID = @id;";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", pointId);
                using (var reader = cmd.ExecuteReader())
                    return reader.Read() ? ReadPointFromRow(reader) : null;
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
                    if (startPtId != null) v.StartPoint = LoadPoint(conn, startPtId);
                    var endPtId = reader["EndPointID"] as string;
                    if (endPtId != null) v.EndPoint = LoadPoint(conn, endPtId);
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
                    return reader.Read() ? ReadModelFromRow(reader) : null;
            }
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
                   X_Value, Y_Value, Z_Value, Cyl_R, Cyl_Theta, L,
                   Sph_R, Sph_Theta, Phi, StartPointID, EndPointID)
                  VALUES
                  (@id, @name, @knot, @type,
                   @x, @y, @z, @cr, @ct, @l,
                   @sr, @st, @phi, @startId, @endId);";
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
                   Longitude, Latitude, Altitude, Real_Value, Complex_Value)
                  VALUES
                  (@id, @wpt, @type, @is2d,
                   @x, @y, @zc, @rc, @tc, @zcyl,
                   @rs, @ts, @phi, @lon, @lat, @alt, @real, @cplx);";
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
            string cpId = null;
            if (dim.CenterPoint != null)
            { cpId = dim.CenterPoint.PointID ?? GenerateId(); EnsurePoint(conn, dim.CenterPoint, cpId); }
            string leId = null;
            if (dim.LeaderLineEndPoint != null)
            { leId = dim.LeaderLineEndPoint.PointID ?? GenerateId(); EnsurePoint(conn, dim.LeaderLineEndPoint, leId); }
            string lbId = null;
            if (dim.LeaderLineBendPoint != null)
            { lbId = dim.LeaderLineBendPoint.PointID ?? GenerateId(); EnsurePoint(conn, dim.LeaderLineBendPoint, lbId); }
            string dpId = null;
            if (dim.DimensionPoint != null)
            { dpId = dim.DimensionPoint.PointID ?? GenerateId(); EnsurePoint(conn, dim.DimensionPoint, dpId); }
            string rpId = null;
            if (dim.ReferencePoint != null)
            { rpId = dim.ReferencePoint.PointID ?? GenerateId(); EnsurePoint(conn, dim.ReferencePoint, rpId); }
            string dmId = null;
            if (dim.MyModel != null)
            { dmId = dim.MyModel.Name ?? GenerateId(); EnsureModel(conn, dim.MyModel, dmId); }

            const string sql =
                @"INSERT OR IGNORE INTO CAD_Dimension
                  (DimensionID, Name, Description, IsOrdinate,
                   CenterPointID, LeaderLineEndPointID, LeaderLineBendPointID,
                   DimensionPointID, ReferencePointID, MyModelID,
                   DimensionNominalValue, DimensionUpperLimitValue, DimensionLowerLimitValue,
                   MyDimensionType)
                  VALUES
                  (@id, @name, @desc, @ord,
                   @cpId, @leId, @lbId, @dpId, @rpId, @modelId,
                   @nom, @upper, @lower, @dimType);";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", dimId);
                cmd.Parameters.AddWithValue("@name", (object)dim.Name ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@desc", (object)dim.Description ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ord", dim.IsOrdinate ? 1 : 0);
                cmd.Parameters.AddWithValue("@cpId", (object)cpId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@leId", (object)leId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@lbId", (object)lbId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@dpId", (object)dpId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@rpId", (object)rpId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@modelId", (object)dmId ?? DBNull.Value);
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
                @"INSERT OR IGNORE INTO CAD_Sketch (SketchID, Version, IsTwoD)
                  VALUES (@id, @ver, @is2d);";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", sketchId);
                cmd.Parameters.AddWithValue("@ver", (object)sketch.Version ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@is2d", sketch.IsTwoD ? 1 : 0);
                cmd.ExecuteNonQuery();
            }
        }

        private static void EnsureFeature(SQLiteConnection conn, CAD_Feature feature, string featureId)
        {
            const string sql =
                @"INSERT OR IGNORE INTO CAD_Feature (FeatureID, Name, Version, GeometricFeatureType)
                  VALUES (@id, @name, @ver, @gft);";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", featureId);
                cmd.Parameters.AddWithValue("@name", (object)feature.Name ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ver", (object)feature.Version ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@gft", (int)feature.GeometricFeatureType);
                cmd.ExecuteNonQuery();
            }
        }

        private static void EnsureBody(SQLiteConnection conn, CAD_Body body, string bodyId)
        {
            const string sql =
                @"INSERT OR IGNORE INTO CAD_Body
                  (BodyID, Name, Version, PartNumber, GeometricFeatureType)
                  VALUES (@id, @name, @ver, @pn, @gft);";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", bodyId);
                cmd.Parameters.AddWithValue("@name", (object)body.Name ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ver", (object)body.Version ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@pn", (object)body.PartNumber ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@gft", (int)body.GeometricFeatureType);
                cmd.ExecuteNonQuery();
            }
        }

        private static void EnsureDrawing(SQLiteConnection conn, CAD_Drawing drawing, string drawingId)
        {
            const string sql =
                @"INSERT OR IGNORE INTO CAD_Drawing
                  (DrawingID, Title, DrawingNumber, Revision,
                   DrawingStandard, MyFormat, MyDrawingSize)
                  VALUES (@id, @title, @num, @rev, @std, @fmt, @sz);";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", drawingId);
                cmd.Parameters.AddWithValue("@title", (object)drawing.Title ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@num", (object)drawing.DrawingNumber ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@rev", (object)drawing.Revision ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@std", (int)drawing.DrawingStandard);
                cmd.Parameters.AddWithValue("@fmt", (int)drawing.MyFormat);
                cmd.Parameters.AddWithValue("@sz", (int)drawing.MyDrawingSize);
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
                  VALUES (@id, @name, @ver, @type,
                          @axial, @radial, @angular, @wing, @floor);";
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
                @"INSERT OR IGNORE INTO CAD_Library (LibraryID, Name, Description, LocalPath, Url)
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

        private static void EnsureInterface(SQLiteConnection conn, CAD_Interface iface, string ifaceId)
        {
            const string sql =
                @"INSERT OR IGNORE INTO CAD_Interface
                  (InterfaceID, Name, ID, Version, InterfaceKind)
                  VALUES (@id, @name, @uid, @ver, @kind);";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", ifaceId);
                cmd.Parameters.AddWithValue("@name", (object)iface.Name ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@uid", (object)iface.ID ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ver", (object)iface.Version ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@kind", (int)iface.InterfaceKind);
                cmd.ExecuteNonQuery();
            }
        }

        private void EnsureMassProperties(SQLiteConnection conn, MassProperties mp, string mpId)
        {
            string cogId = null;
            if (mp.CenterOfGravity != null)
            {
                cogId = mp.CenterOfGravity.PointID ?? GenerateId();
                EnsurePoint(conn, mp.CenterOfGravity, cogId);
            }
            string csId = null;
            if (mp.CurrentCoordinateSystem != null)
            {
                csId = mp.CurrentCoordinateSystem.CoordinateSystemID
                       ?? mp.CurrentCoordinateSystem.Name
                       ?? GenerateId();
                EnsureCoordinateSystem(conn, mp.CurrentCoordinateSystem, csId);
            }

            const string sql =
                @"INSERT OR IGNORE INTO MassProperties
                  (MassPropertiesID, Mass, CenterOfGravityPointID, CurrentCoordinateSystemID)
                  VALUES (@id, @mass, @cogId, @csId);";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", mpId);
                cmd.Parameters.AddWithValue("@mass", mp.Mass);
                cmd.Parameters.AddWithValue("@cogId", (object)cogId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@csId", (object)csId ?? DBNull.Value);
                cmd.ExecuteNonQuery();
            }
        }

        private static void EnsureParameter(SQLiteConnection conn, CAD_Parameter param, string paramId)
        {
            const string sql =
                @"INSERT OR IGNORE INTO MathParameter
                  (MathParameterID, Name, Description, Comments,
                   MyParameterType, SolidWorksParameterName, Fusion360ParameterName)
                  VALUES (@id, @name, @desc, @comments, @type, @swName, @f360Name);";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", paramId);
                cmd.Parameters.AddWithValue("@name", (object)param.Name ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@desc", (object)param.Description ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@comments", (object)param.Comments ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@type", (int)param.MyParameterType);
                cmd.Parameters.AddWithValue("@swName", (object)param.SolidWorksParameterName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@f360Name", (object)param.Fusion360ParameterName ?? DBNull.Value);
                cmd.ExecuteNonQuery();
            }
        }

        private static void EnsureAssembly(SQLiteConnection conn, CAD_Assembly asm, string asmId)
        {
            const string sql =
                @"INSERT OR IGNORE INTO CAD_Assembly
                  (AssemblyID, Name, Version, Description, IsSubAssembly, IsConfigurationItem)
                  VALUES (@id, @name, @ver, @desc, @sub, @cfg);";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", asmId);
                cmd.Parameters.AddWithValue("@name", (object)asm.Name ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ver", (object)asm.Version ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@desc", (object)asm.Description ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@sub", asm.IsSubAssembly ? 1 : 0);
                cmd.Parameters.AddWithValue("@cfg", asm.IsConfigurationItem ? 1 : 0);
                cmd.ExecuteNonQuery();
            }
        }

        private static string GenerateId() => Guid.NewGuid().ToString("N");

        // ================================================================
        // Original SolidWorks-based static methods (unchanged)
        // ================================================================

        public static SldWorks.PartDoc CreateNewPart(SldWorks.SldWorks swApp)
        {
            string partTemplate = swApp.GetUserPreferenceStringValue(
                (int)swUserPreferenceStringValue_e.swDefaultTemplatePart);

            if (!string.IsNullOrEmpty(partTemplate))
            {
                object model = swApp.NewDocument(partTemplate, 0, 0, 0);
                if (model == null)
                {
                    Console.WriteLine("Failed to create document. Template not found at: " + partTemplate);
                }
                else
                {
                    return (SldWorks.PartDoc)model;
                }
            }

            return null;
        }

        public static string GetPartTemplate(SldWorks.SldWorks swApp)
        {
            return swApp.GetUserPreferenceStringValue(
                (int)swUserPreferenceStringValue_e.swDefaultTemplatePart);
        }

        public static SolidworksModel CreateModel(SldWorks.SldWorks swApp)
        {
            var model = new SolidworksModel();
            model.SwModelObject = CreateNewPart(swApp);
            return model;
        }

        public static List<CAD_Station> BuildStations(SldWorks.SldWorks swApp,
            SolidworksModel model, int qty, double spacing,
            StationBuilder.CoordinateSystemType coordSystem, int axis)
        {
            return StationBuilder.Build(swApp, model, qty, spacing, coordSystem, axis);
        }

        public static List<string> GetStationPlaneNames(List<CAD_Station> stations)
        {
            var planeNames = new List<string>(stations.Count);
            foreach (var station in stations)
            {
                string planeName = station.CurrentSketchPlane?.Path ?? station.CurrentSketchPlane?.Name;
                if (!string.IsNullOrEmpty(planeName))
                {
                    planeNames.Add(planeName);
                }
            }
            return planeNames;
        }
    }
}
