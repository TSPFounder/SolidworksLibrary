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
    public class SketchBuilder
    {
        // ================================================================
        // Instance state for database operations
        // ================================================================
        private readonly string _databasePath;
        private string ConnectionString => $"Data Source={_databasePath};Version=3;Foreign Keys=True;";

        /// <summary>
        /// Creates a SketchBuilder instance configured for database operations.
        /// </summary>
        /// <param name="databasePath">Full path to the SQLite database file.</param>
        public SketchBuilder(string databasePath)
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
        /// Persists one or more <see cref="CAD_Sketch"/> objects (and their dependent
        /// points, segments, coordinate systems, parameters, dimensions, and constraints)
        /// to the database. Uses INSERT OR REPLACE for idempotent upsert semantics.
        /// </summary>
        public void SaveSketches(IEnumerable<CAD_Sketch> sketches)
        {
            if (sketches == null) throw new ArgumentNullException(nameof(sketches));

            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                using (var txn = conn.BeginTransaction())
                {
                    foreach (var sketch in sketches)
                    {
                        SaveSketchCore(conn, sketch);
                    }
                    txn.Commit();
                }
            }
        }

        /// <summary>Convenience overload — persists a single sketch.</summary>
        public void SaveSketch(CAD_Sketch sketch)
        {
            if (sketch == null) throw new ArgumentNullException(nameof(sketch));
            SaveSketches(new[] { sketch });
        }

        private void SaveSketchCore(SQLiteConnection conn, CAD_Sketch sketch)
        {
            var sketchId = sketch.SketchID ?? GenerateId();

            // Ensure owning model
            string modelId = null;
            if (sketch.MyModel != null)
            {
                modelId = sketch.MyModel.Name ?? GenerateId();
                EnsureModel(conn, sketch.MyModel, modelId);
            }

            // Ensure sketch plane
            string sketchPlaneId = null;
            if (sketch.MySketchPlane != null)
            {
                sketchPlaneId = sketch.MySketchPlane.Name ?? GenerateId();
                EnsureSketchPlane(conn, sketch.MySketchPlane, sketchPlaneId, modelId);
            }

            // Ensure coordinate systems
            string currentCsId = null;
            if (sketch.CurrentCoordinateSystem != null)
            {
                currentCsId = sketch.CurrentCoordinateSystem.CoordinateSystemID
                              ?? sketch.CurrentCoordinateSystem.Name
                              ?? GenerateId();
                EnsureCoordinateSystem(conn, sketch.CurrentCoordinateSystem, currentCsId);
            }

            string baseCsId = null;
            if (sketch.BaseCoordinateSystem != null)
            {
                baseCsId = sketch.BaseCoordinateSystem.CoordinateSystemID
                           ?? sketch.BaseCoordinateSystem.Name
                           ?? GenerateId();
                EnsureCoordinateSystem(conn, sketch.BaseCoordinateSystem, baseCsId);
            }

            // Ensure cursor references
            string currentPointId = null;
            if (sketch.CurrentPoint != null)
            {
                currentPointId = sketch.CurrentPoint.PointID ?? GenerateId();
                EnsurePoint(conn, sketch.CurrentPoint, currentPointId);
            }

            string currentSegId = null;
            if (sketch.CurrentSegment != null)
            {
                currentSegId = sketch.CurrentSegment.SegmentID ?? GenerateId();
                EnsureSegment(conn, sketch.CurrentSegment, currentSegId);
            }

            string prevSegId = null;
            if (sketch.PreviousSegment != null)
            {
                prevSegId = sketch.PreviousSegment.SegmentID ?? GenerateId();
                EnsureSegment(conn, sketch.PreviousSegment, prevSegId);
            }

            // INSERT OR REPLACE the sketch row
            const string sketchSql =
                @"INSERT OR REPLACE INTO CAD_Sketch
                  (SketchID, Version, IsTwoD,
                   MyModelID, MySketchPlaneID,
                   CurrentPointID, CurrentSegmentID, PreviousSegmentID,
                   CurrentCoordinateSystemID, BaseCoordinateSystemID)
                  VALUES
                  (@id, @ver, @is2d,
                   @modelId, @planeId,
                   @curPtId, @curSegId, @prevSegId,
                   @curCsId, @baseCsId);";

            using (var cmd = new SQLiteCommand(sketchSql, conn))
            {
                cmd.Parameters.AddWithValue("@id", sketchId);
                cmd.Parameters.AddWithValue("@ver", (object)sketch.Version ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@is2d", sketch.IsTwoD ? 1 : 0);
                cmd.Parameters.AddWithValue("@modelId", (object)modelId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@planeId", (object)sketchPlaneId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@curPtId", (object)currentPointId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@curSegId", (object)currentSegId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@prevSegId", (object)prevSegId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@curCsId", (object)currentCsId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@baseCsId", (object)baseCsId ?? DBNull.Value);
                cmd.ExecuteNonQuery();
            }

            // Junction tables — clear and re-insert each collection
            SaveSketchPoints(conn, sketchId, sketch.MyPoints);
            SaveSketchSegments(conn, sketchId, sketch.MySegments, "CAD_Sketch_Segment");
            SaveSketchSegments(conn, sketchId, sketch.MyProfile, "CAD_Sketch_ProfileSegment");
            SaveSketchCoordinateSystems(conn, sketchId, sketch.MyCoordinateSystems);
        }

        private void SaveSketchPoints(SQLiteConnection conn, string sketchId, IReadOnlyList<Point> points)
        {
            using (var cmd = new SQLiteCommand(
                "DELETE FROM CAD_Sketch_Point WHERE SketchID = @id;", conn))
            {
                cmd.Parameters.AddWithValue("@id", sketchId);
                cmd.ExecuteNonQuery();
            }

            for (int i = 0; i < points.Count; i++)
            {
                var pt = points[i];
                var ptId = pt.PointID ?? GenerateId();
                EnsurePoint(conn, pt, ptId);

                using (var cmd = new SQLiteCommand(
                    @"INSERT INTO CAD_Sketch_Point (SketchID, PointID, SortOrder)
                      VALUES (@sid, @pid, @ord);", conn))
                {
                    cmd.Parameters.AddWithValue("@sid", sketchId);
                    cmd.Parameters.AddWithValue("@pid", ptId);
                    cmd.Parameters.AddWithValue("@ord", i);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void SaveSketchSegments(SQLiteConnection conn, string sketchId,
            IReadOnlyList<Segment> segments, string junctionTable)
        {
            using (var cmd = new SQLiteCommand(
                $"DELETE FROM {junctionTable} WHERE SketchID = @id;", conn))
            {
                cmd.Parameters.AddWithValue("@id", sketchId);
                cmd.ExecuteNonQuery();
            }

            for (int i = 0; i < segments.Count; i++)
            {
                var seg = segments[i];
                var segId = seg.SegmentID ?? GenerateId();
                EnsureSegment(conn, seg, segId);

                using (var cmd = new SQLiteCommand(
                    $@"INSERT INTO {junctionTable} (SketchID, SegmentID, SortOrder)
                       VALUES (@sid, @segId, @ord);", conn))
                {
                    cmd.Parameters.AddWithValue("@sid", sketchId);
                    cmd.Parameters.AddWithValue("@segId", segId);
                    cmd.Parameters.AddWithValue("@ord", i);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void SaveSketchCoordinateSystems(SQLiteConnection conn, string sketchId,
            IReadOnlyList<CoordinateSystem> coordinateSystems)
        {
            using (var cmd = new SQLiteCommand(
                "DELETE FROM CAD_Sketch_CoordinateSystem WHERE SketchID = @id;", conn))
            {
                cmd.Parameters.AddWithValue("@id", sketchId);
                cmd.ExecuteNonQuery();
            }

            for (int i = 0; i < coordinateSystems.Count; i++)
            {
                var cs = coordinateSystems[i];
                var csId = cs.CoordinateSystemID ?? cs.Name ?? GenerateId();
                EnsureCoordinateSystem(conn, cs, csId);

                using (var cmd = new SQLiteCommand(
                    @"INSERT INTO CAD_Sketch_CoordinateSystem (SketchID, CoordinateSystemID, SortOrder)
                      VALUES (@sid, @csId, @ord);", conn))
                {
                    cmd.Parameters.AddWithValue("@sid", sketchId);
                    cmd.Parameters.AddWithValue("@csId", csId);
                    cmd.Parameters.AddWithValue("@ord", i);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // ================================================================
        // Database: Read operations
        // ================================================================

        /// <summary>
        /// Loads all sketches from the database, optionally filtered by model ID.
        /// Reconstructs the object graph including points, segments, coordinate systems,
        /// sketch plane, and model references.
        /// </summary>
        public List<CAD_Sketch> LoadSketches(string modelId = null)
        {
            var sketches = new List<CAD_Sketch>();

            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();

                var sql = modelId != null
                    ? "SELECT * FROM CAD_Sketch WHERE MyModelID = @mid ORDER BY SketchID;"
                    : "SELECT * FROM CAD_Sketch ORDER BY SketchID;";

                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    if (modelId != null)
                        cmd.Parameters.AddWithValue("@mid", modelId);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var sketch = ReadSketchFromRow(reader);
                            sketches.Add(sketch);
                        }
                    }
                }

                // Load child collections and references for each sketch
                foreach (var sketch in sketches)
                {
                    LoadSketchChildren(conn, sketch);
                }
            }

            return sketches;
        }

        /// <summary>Loads a single sketch by its ID.</summary>
        public CAD_Sketch LoadSketch(string sketchId)
        {
            if (string.IsNullOrWhiteSpace(sketchId))
                throw new ArgumentException("Sketch ID must not be null or empty.", nameof(sketchId));

            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();

                CAD_Sketch sketch = null;
                using (var cmd = new SQLiteCommand(
                    "SELECT * FROM CAD_Sketch WHERE SketchID = @id;", conn))
                {
                    cmd.Parameters.AddWithValue("@id", sketchId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                            sketch = ReadSketchFromRow(reader);
                    }
                }

                if (sketch == null) return null;

                LoadSketchChildren(conn, sketch);
                return sketch;
            }
        }

        // ================================================================
        // Database: Private helpers — Read
        // ================================================================

        private static CAD_Sketch ReadSketchFromRow(SQLiteDataReader reader)
        {
            var sketchId = reader["SketchID"] as string;

            var sketch = new CAD_Sketch(sketchId)
            {
                Version = reader["Version"] as string,
                IsTwoD = Convert.ToInt32(reader["IsTwoD"]) != 0
            };

            return sketch;
        }

        private void LoadSketchChildren(SQLiteConnection conn, CAD_Sketch sketch)
        {
            var sketchId = sketch.SketchID;

            // Load points
            LoadSketchPoints(conn, sketch);

            // Load segments
            LoadSketchSegmentCollection(conn, sketch, "CAD_Sketch_Segment", isProfile: false);

            // Load profile segments
            LoadSketchSegmentCollection(conn, sketch, "CAD_Sketch_ProfileSegment", isProfile: true);

            // Load coordinate systems
            LoadSketchCoordinateSystems(conn, sketch);

            // Load sketch plane
            string planeId = null;
            using (var cmd = new SQLiteCommand(
                "SELECT MySketchPlaneID FROM CAD_Sketch WHERE SketchID = @id;", conn))
            {
                cmd.Parameters.AddWithValue("@id", sketchId);
                planeId = cmd.ExecuteScalar() as string;
            }
            if (planeId != null)
            {
                sketch.MySketchPlane = LoadSketchPlane(conn, planeId);
            }

            // Load base and current coordinate systems by ID
            string baseCsId = null;
            string curCsId = null;
            using (var cmd = new SQLiteCommand(
                "SELECT BaseCoordinateSystemID, CurrentCoordinateSystemID FROM CAD_Sketch WHERE SketchID = @id;", conn))
            {
                cmd.Parameters.AddWithValue("@id", sketchId);
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        baseCsId = reader["BaseCoordinateSystemID"] as string;
                        curCsId = reader["CurrentCoordinateSystemID"] as string;
                    }
                }
            }
            if (baseCsId != null)
                sketch.BaseCoordinateSystem = LoadCoordinateSystem(conn, baseCsId);
            if (curCsId != null)
                sketch.CurrentCoordinateSystem = LoadCoordinateSystem(conn, curCsId);

            // Load owning model
            string modelId = null;
            using (var cmd = new SQLiteCommand(
                "SELECT MyModelID FROM CAD_Sketch WHERE SketchID = @id;", conn))
            {
                cmd.Parameters.AddWithValue("@id", sketchId);
                modelId = cmd.ExecuteScalar() as string;
            }
            if (modelId != null)
                sketch.MyModel = LoadModel(conn, modelId);
        }

        private void LoadSketchPoints(SQLiteConnection conn, CAD_Sketch sketch)
        {
            const string sql =
                @"SELECT p.*
                  FROM CAD_Sketch_Point sp
                  JOIN Point p ON sp.PointID = p.PointID
                  WHERE sp.SketchID = @id
                  ORDER BY sp.SortOrder;";

            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", sketch.SketchID);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        sketch.AddPoint(ReadPointFromRow(reader));
                    }
                }
            }
        }

        private void LoadSketchSegmentCollection(SQLiteConnection conn, CAD_Sketch sketch,
            string junctionTable, bool isProfile)
        {
            var sql = $@"SELECT seg.*
                         FROM {junctionTable} jt
                         JOIN Segment seg ON jt.SegmentID = seg.SegmentID
                         WHERE jt.SketchID = @id
                         ORDER BY jt.SortOrder;";

            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", sketch.SketchID);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var seg = ReadSegmentFromRow(conn, reader);
                        if (isProfile)
                            sketch.AddProfileSegment(seg);
                        else
                            sketch.AddSegment(seg);
                    }
                }
            }
        }

        private void LoadSketchCoordinateSystems(SQLiteConnection conn, CAD_Sketch sketch)
        {
            const string sql =
                @"SELECT cs.*
                  FROM CAD_Sketch_CoordinateSystem scs
                  JOIN CoordinateSystem cs ON scs.CoordinateSystemID = cs.CoordinateSystemID
                  WHERE scs.SketchID = @id
                  ORDER BY scs.SortOrder;";

            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", sketch.SketchID);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var cs = ReadCoordinateSystemFromRow(conn, reader);
                        sketch.AddCoordinateSystem(cs);
                    }
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

        private Segment ReadSegmentFromRow(SQLiteConnection conn, SQLiteDataReader reader)
        {
            var seg = new Segment
            {
                SegmentID = reader["SegmentID"] as string,
                SegmentType = (Segment.SegmentTypeEnum)Convert.ToInt32(reader["SegmentType"]),
                IsEdge = Convert.ToInt32(reader["IsEdge"]) != 0,
                Length = Convert.ToDouble(reader["Length"])
            };

            var startPtId = reader["StartPointID"] as string;
            if (startPtId != null)
                seg.StartPoint = LoadPoint(conn, startPtId);

            var endPtId = reader["EndPointID"] as string;
            if (endPtId != null)
                seg.EndPoint = LoadPoint(conn, endPtId);

            var midPtId = reader["MidPointID"] as string;
            if (midPtId != null)
                seg.MidPoint = LoadPoint(conn, midPtId);

            var csId = reader["MyCoordinateSystemID"] as string;
            if (csId != null)
                seg.MyCoordinateSystem = LoadCoordinateSystem(conn, csId);

            var vecId = reader["CurrentVectorID"] as string;
            if (vecId != null)
                seg.CurrentVector = LoadVector(conn, vecId);

            return seg;
        }

        private CoordinateSystem ReadCoordinateSystemFromRow(SQLiteConnection conn, SQLiteDataReader reader)
        {
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

        private CAD_SketchPlane LoadSketchPlane(SQLiteConnection conn, string planeId)
        {
            const string sql = "SELECT * FROM CAD_SketchPlane WHERE SketchPlaneID = @id;";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", planeId);
                using (var reader = cmd.ExecuteReader())
                {
                    if (!reader.Read()) return null;

                    var plane = new CAD_SketchPlane(
                        reader["Name"] as string,
                        (CAD_SketchPlane.FunctionalTypeEnum)Convert.ToInt32(reader["FunctionalType"]),
                        (CAD_SketchPlane.GeometryTypeEnum)Convert.ToInt32(reader["GeometryType"]))
                    {
                        Version = reader["Version"] as string,
                        Path = reader["Path"] as string,
                        IsWorkplane = Convert.ToInt32(reader["IsWorkplane"]) != 0
                    };

                    var csId = reader["MyCoordinateSystemID"] as string;
                    if (csId != null)
                        plane.MyCoordinateSystem = LoadCoordinateSystem(conn, csId);

                    var normalId = reader["NormalVectorID"] as string;
                    if (normalId != null)
                    {
                        var normalVec = LoadVector(conn, normalId);
                        if (normalVec != null)
                            plane.SetNormal(normalVec.X_Value, normalVec.Y_Value, normalVec.Z_Value, false);
                    }

                    return plane;
                }
            }
        }

        // ================================================================
        // Database: Shared entity loaders
        // ================================================================

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

        private void EnsureSketchPlane(SQLiteConnection conn, CAD_SketchPlane plane,
            string planeId, string modelId)
        {
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

        private void EnsureSegment(SQLiteConnection conn, Segment segment, string segmentId)
        {
            // Persist start/end/mid points first
            string startPtId = null;
            if (segment.StartPoint != null)
            {
                startPtId = segment.StartPoint.PointID ?? GenerateId();
                EnsurePoint(conn, segment.StartPoint, startPtId);
            }

            string endPtId = null;
            if (segment.EndPoint != null)
            {
                endPtId = segment.EndPoint.PointID ?? GenerateId();
                EnsurePoint(conn, segment.EndPoint, endPtId);
            }

            string midPtId = null;
            if (segment.MidPoint != null)
            {
                midPtId = segment.MidPoint.PointID ?? GenerateId();
                EnsurePoint(conn, segment.MidPoint, midPtId);
            }

            string focal1Id = null;
            if (segment.FocalPoint1 != null)
            {
                focal1Id = segment.FocalPoint1.PointID ?? GenerateId();
                EnsurePoint(conn, segment.FocalPoint1, focal1Id);
            }

            string focal2Id = null;
            if (segment.FocalPoint2 != null)
            {
                focal2Id = segment.FocalPoint2.PointID ?? GenerateId();
                EnsurePoint(conn, segment.FocalPoint2, focal2Id);
            }

            string vertexId = null;
            if (segment.Vertex != null)
            {
                vertexId = segment.Vertex.PointID ?? GenerateId();
                EnsurePoint(conn, segment.Vertex, vertexId);
            }

            string vecId = null;
            if (segment.CurrentVector != null)
            {
                vecId = segment.CurrentVector.VectorID ?? GenerateId();
                EnsureVector(conn, segment.CurrentVector, vecId);
            }

            string csId = null;
            if (segment.MyCoordinateSystem != null)
            {
                csId = segment.MyCoordinateSystem.CoordinateSystemID
                       ?? segment.MyCoordinateSystem.Name
                       ?? GenerateId();
                EnsureCoordinateSystem(conn, segment.MyCoordinateSystem, csId);
            }

            const string sql =
                @"INSERT OR IGNORE INTO Segment
                  (SegmentID, SegmentType, IsEdge, Length,
                   StartPointID, EndPointID, MidPointID,
                   FocalPoint1ID, FocalPoint2ID, VertexID,
                   CurrentVectorID, MyCoordinateSystemID)
                  VALUES
                  (@id, @type, @edge, @len,
                   @startId, @endId, @midId,
                   @fp1, @fp2, @vtx,
                   @vecId, @csId);";

            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", segmentId);
                cmd.Parameters.AddWithValue("@type", (int)segment.SegmentType);
                cmd.Parameters.AddWithValue("@edge", segment.IsEdge ? 1 : 0);
                cmd.Parameters.AddWithValue("@len", segment.Length);
                cmd.Parameters.AddWithValue("@startId", (object)startPtId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@endId", (object)endPtId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@midId", (object)midPtId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@fp1", (object)focal1Id ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@fp2", (object)focal2Id ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@vtx", (object)vertexId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@vecId", (object)vecId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@csId", (object)csId ?? DBNull.Value);
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

        // -------------------------------------------
        // Sketch Session Management
        // -------------------------------------------

        public static void BeginSketch(ModelDoc2 swModelDoc, string planeName)
        {
            swModelDoc.Extension.SelectByID2(planeName, "PLANE", 0, 0, 0, false, 0, null, 0);
            swModelDoc.SketchManager.InsertSketch(true);
        }

        public static void EndSketch(ModelDoc2 swModelDoc)
        {
            swModelDoc.SketchManager.InsertSketch(true);
        }

        // -------------------------------------------
        // Lines
        // -------------------------------------------

        public static object CreateLine(SketchManager sketchMgr, double x1, double y1, double x2, double y2)
        {
            return sketchMgr.CreateLine(x1, y1, 0, x2, y2, 0);
        }

        public static object CreateCenterLine(SketchManager sketchMgr, double x1, double y1, double x2, double y2)
        {
            return sketchMgr.CreateCenterLine(x1, y1, 0, x2, y2, 0);
        }

        // -------------------------------------------
        // Circles
        // -------------------------------------------

        public static object CreateCircle(SketchManager sketchMgr, double centerX, double centerY, double radius)
        {
            return sketchMgr.CreateCircle(centerX, centerY, 0, centerX + radius, centerY, 0);
        }

        public static object CreateCircleByRadius(SketchManager sketchMgr, double centerX, double centerY, double radius)
        {
            return sketchMgr.CreateCircleByRadius(centerX, centerY, 0, radius);
        }

        public static object CreatePerimeterCircle(SketchManager sketchMgr,
            double x1, double y1, double x2, double y2, double x3, double y3)
        {
            return sketchMgr.PerimeterCircle(x1, y1, x2, y2, x3, y3);
        }

        // -------------------------------------------
        // Arcs
        // -------------------------------------------

        public static object CreateArc(SketchManager sketchMgr, double centerX, double centerY,
            double startX, double startY, double endX, double endY, short direction)
        {
            return sketchMgr.CreateArc(centerX, centerY, 0,
                startX, startY, 0, endX, endY, 0, direction);
        }

        public static object Create3PointArc(SketchManager sketchMgr, double startX, double startY,
            double endX, double endY, double midX, double midY)
        {
            return sketchMgr.Create3PointArc(startX, startY, 0,
                endX, endY, 0, midX, midY, 0);
        }

        public static object CreateTangentArc(SketchManager sketchMgr, double startX, double startY,
            double endX, double endY, int arcType)
        {
            return sketchMgr.CreateTangentArc(startX, startY, 0,
                endX, endY, 0, arcType);
        }

        // -------------------------------------------
        // Ellipses
        // -------------------------------------------

        public static object CreateEllipse(SketchManager sketchMgr, double centerX, double centerY,
            double majorX, double majorY, double minorX, double minorY)
        {
            return sketchMgr.CreateEllipse(centerX, centerY, 0,
                majorX, majorY, 0, minorX, minorY, 0);
        }

        public static object CreateEllipticalArc(SketchManager sketchMgr, double centerX, double centerY,
            double majorX, double majorY, double minorX, double minorY,
            double startX, double startY, double endX, double endY,
            short direction)
        {
            return sketchMgr.CreateEllipticalArc(centerX, centerY, 0,
                majorX, majorY, 0, minorX, minorY, 0,
                startX, startY, 0, endX, endY, 0, direction);
        }

        // -------------------------------------------
        // Rectangles
        // -------------------------------------------

        public static object CreateCornerRectangle(SketchManager sketchMgr,
            double x1, double y1, double x2, double y2)
        {
            return sketchMgr.CreateCornerRectangle(x1, y1, 0, x2, y2, 0);
        }

        public static object CreateCenterRectangle(SketchManager sketchMgr,
            double centerX, double centerY, double cornerX, double cornerY)
        {
            return sketchMgr.CreateCenterRectangle(centerX, centerY, 0,
                cornerX, cornerY, 0);
        }

        public static object Create3PointCornerRectangle(SketchManager sketchMgr,
            double x1, double y1, double x2, double y2, double x3, double y3)
        {
            return sketchMgr.Create3PointCornerRectangle(x1, y1, 0,
                x2, y2, 0, x3, y3, 0);
        }

        public static object Create3PointCenterRectangle(SketchManager sketchMgr,
            double centerX, double centerY, double x2, double y2, double x3, double y3)
        {
            return sketchMgr.Create3PointCenterRectangle(centerX, centerY, 0,
                x2, y2, 0, x3, y3, 0);
        }

        public static object CreateParallelogram(SketchManager sketchMgr,
            double x1, double y1, double x2, double y2, double x3, double y3)
        {
            return sketchMgr.CreateParallelogram(x1, y1, 0,
                x2, y2, 0, x3, y3, 0);
        }

        // -------------------------------------------
        // Polygon
        // -------------------------------------------

        public static object CreatePolygon(SketchManager sketchMgr, double centerX, double centerY,
            double vertexX, double vertexY, int sides, bool inscribed)
        {
            return sketchMgr.CreatePolygon(centerX, centerY, 0,
                vertexX, vertexY, 0, sides, inscribed);
        }

        // -------------------------------------------
        // Slots
        // -------------------------------------------

        public static object CreateStraightSlot(SketchManager sketchMgr,
            double x1, double y1, double x2, double y2, double width)
        {
            return sketchMgr.CreateSketchSlot(
                (int)swSketchSlotCreationType_e.swSketchSlotCreationType_line,
                (int)swSketchSlotLengthType_e.swSketchSlotLengthType_CenterCenter,
                width, x1, y1, 0, x2, y2, 0, 0, 0, 0, 1, false);
        }

        public static object CreateCenterpointStraightSlot(SketchManager sketchMgr,
            double centerX, double centerY, double endX, double endY, double width)
        {
            return sketchMgr.CreateSketchSlot(
                (int)swSketchSlotCreationType_e.swSketchSlotCreationType_line,
                (int)swSketchSlotLengthType_e.swSketchSlotLengthType_FullLength,
                width, centerX, centerY, 0, endX, endY, 0, 0, 0, 0, 1, false);
        }

        public static object Create3PointArcSlot(SketchManager sketchMgr,
            double x1, double y1, double x2, double y2, double x3, double y3, double width)
        {
            return sketchMgr.CreateSketchSlot(
                (int)swSketchSlotCreationType_e.swSketchSlotCreationType_3pointarc,
                (int)swSketchSlotLengthType_e.swSketchSlotLengthType_CenterCenter,
                width, x1, y1, 0, x2, y2, 0, x3, y3, 0, 1, false);
        }

        public static object CreateCenterpointArcSlot(SketchManager sketchMgr,
            double centerX, double centerY, double startX, double startY,
            double endX, double endY, double width)
        {
            return sketchMgr.CreateSketchSlot(
                (int)swSketchSlotCreationType_e.swSketchSlotCreationType_arc,
                (int)swSketchSlotLengthType_e.swSketchSlotLengthType_CenterCenter,
                width, centerX, centerY, 0, startX, startY, 0, endX, endY, 0, 1, false);
        }

        // -------------------------------------------
        // Splines
        // -------------------------------------------

        public static object CreateSpline(SketchManager sketchMgr, double[,] points)
        {
            int numPoints = points.GetLength(0);
            Array pointArray = new double[numPoints * 3];
            for (int i = 0; i < numPoints; i++)
            {
                ((double[])pointArray)[i * 3] = points[i, 0];
                ((double[])pointArray)[i * 3 + 1] = points[i, 1];
                ((double[])pointArray)[i * 3 + 2] = 0;
            }
            return sketchMgr.CreateSpline2(pointArray, false);
        }

        // -------------------------------------------
        // Parabola & Conic
        // -------------------------------------------

        public static object CreateParabola(SketchManager sketchMgr, double focusX, double focusY,
            double apexX, double apexY, double startX, double startY,
            double endX, double endY)
        {
            return sketchMgr.CreateParabola(focusX, focusY, 0,
                apexX, apexY, 0, startX, startY, 0, endX, endY, 0);
        }

        public static object CreateConic(SketchManager sketchMgr, double startX, double startY,
            double endX, double endY, double apexX, double apexY, double rho)
        {
            return sketchMgr.CreateConic(startX, startY, 0,
                endX, endY, 0, apexX, apexY, 0, rho, 0, 0);
        }

        // -------------------------------------------
        // Point
        // -------------------------------------------

        public static object CreatePoint(SketchManager sketchMgr, double x, double y)
        {
            return sketchMgr.CreatePoint(x, y, 0);
        }

        // -------------------------------------------
        // Text
        // -------------------------------------------

        public static object CreateText(ModelDoc2 swModelDoc, string text, double x, double y,
            int fontHeight, int fontAngle, int centerAlign, int flip, int vFlip)
        {
            return swModelDoc.InsertSketchText(x, y, 0, text,
                fontHeight, fontAngle, centerAlign, flip, vFlip);
        }

        // -------------------------------------------
        // Fillet & Chamfer
        // -------------------------------------------

        public static object CreateFillet(SketchManager sketchMgr, double radius)
        {
            return sketchMgr.CreateFillet(radius, (int)swConstraintType_e.swConstraintType_TANGENT);
        }

        public static object CreateChamfer(SketchManager sketchMgr, int type, double distance1, double distance2)
        {
            return sketchMgr.CreateChamfer(type, distance1, distance2);
        }

        // -------------------------------------------
        // Offset & Mirror
        // -------------------------------------------

        public static bool CreateOffset(SketchManager sketchMgr, double offset, bool bothDirections, bool chain,
            int capEnds, int makeConstruction, bool addDimensions)
        {
            return sketchMgr.SketchOffset2(offset, bothDirections, chain,
                capEnds, makeConstruction, addDimensions);
        }

        public static void CreateMirror(ModelDoc2 swModelDoc)
        {
            swModelDoc.SketchMirror();
        }

        // -------------------------------------------
        // Linear & Circular Pattern
        // -------------------------------------------

        public static bool CreateLinearPattern(SketchManager sketchMgr, int numX, int numY,
            double spacingX, double spacingY, double angleX, double angleY,
            string deleteInstances, bool xSpacingDim, bool ySpacingDim,
            bool angleDim, bool createNumDimX, bool createNumDimY)
        {
            return sketchMgr.CreateLinearSketchStepAndRepeat(
                numX, numY, spacingX, spacingY, angleX, angleY,
                deleteInstances, xSpacingDim, ySpacingDim, angleDim,
                createNumDimX, createNumDimY);
        }

        public static object CreateCircularPattern(SketchManager sketchMgr, int count, double radius,
            double spacing, double arcAngle)
        {
            return sketchMgr.CreateCircularSketchStepAndRepeat(
                arcAngle, spacing, count, radius, true, "", true, true, true);
        }
    }
}
