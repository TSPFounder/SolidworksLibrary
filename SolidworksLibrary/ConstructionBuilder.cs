using CAD;
using SldWorks;
using SwConst;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace SolidworksLibrary
{
    public sealed class ConstructionBuilder
    {
        // ================================================================
        // SolidWorks instance state
        // ================================================================
        private readonly ModelDoc2 _modelDoc;
        public SketchManager _sketchManager;
        private readonly FeatureManager _featureManager;

        // ================================================================
        // Database instance state
        // ================================================================
        private readonly string _databasePath;
        private string ConnectionString => $"Data Source={_databasePath};Version=3;Foreign Keys=True;";

        // ================================================================
        // Constructors
        // ================================================================

        /// <summary>
        /// Parameterless constructor preserved for backward compatibility.
        /// </summary>
        public ConstructionBuilder() { }

        /// <summary>
        /// Creates a ConstructionBuilder configured for database operations.
        /// </summary>
        public ConstructionBuilder(string databasePath)
        {
            if (string.IsNullOrWhiteSpace(databasePath))
                throw new ArgumentException("Database path must not be null or empty.", nameof(databasePath));
            _databasePath = databasePath;
        }

        /// <summary>
        /// Creates a ConstructionBuilder configured for SolidWorks COM operations.
        /// </summary>
        public ConstructionBuilder(ModelDoc2 modelDoc)
        {
            _modelDoc = modelDoc ?? throw new ArgumentNullException(nameof(modelDoc));
            SldWorks.ModelDoc2 swModel = (SldWorks.ModelDoc2)_modelDoc;
            swModel.Extension.SelectByID2("Front Plane", "PLANE", 0, 0, 0, false, 0, null, 0);
            _sketchManager = swModel.SketchManager ?? throw new InvalidOperationException("SketchManager is required for construction geometry.");
            _featureManager = swModel.FeatureManager ?? throw new InvalidOperationException("FeatureManager is required for reference geometry.");
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
        /// Persists one or more <see cref="CAD_ConstructionGeometry"/> objects
        /// (and their dependent entity graph) to the database.
        /// </summary>
        public void SaveConstructionGeometries(IEnumerable<CAD_ConstructionGeometry> geometries)
        {
            if (geometries == null) throw new ArgumentNullException(nameof(geometries));

            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                using (var txn = conn.BeginTransaction())
                {
                    foreach (var geom in geometries)
                    {
                        SaveConstructionGeometryCore(conn, geom);
                    }
                    txn.Commit();
                }
            }
        }

        /// <summary>Convenience overload — persists a single construction geometry.</summary>
        public void SaveConstructionGeometry(CAD_ConstructionGeometry geometry)
        {
            if (geometry == null) throw new ArgumentNullException(nameof(geometry));
            SaveConstructionGeometries(new[] { geometry });
        }

        private void SaveConstructionGeometryCore(SQLiteConnection conn, CAD_ConstructionGeometry geom)
        {
            var geomId = geom.Name ?? GenerateId();

            // Ensure FK: MyCAD_Model
            string modelId = null;
            if (geom.MyCAD_Model != null)
            {
                modelId = geom.MyCAD_Model.Name ?? GenerateId();
                EnsureModel(conn, geom.MyCAD_Model, modelId);
            }

            const string sql =
                @"INSERT OR REPLACE INTO CAD_ConstructionGeometry
                  (ConstructionGeometryID, Name, Version, GeometryType, MyCAD_ModelID)
                  VALUES (@id, @name, @ver, @geoType, @modelId);";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", geomId);
                cmd.Parameters.AddWithValue("@name", (object)geom.Name ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ver", (object)geom.Version ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@geoType", (int)geom.GeometryType);
                cmd.Parameters.AddWithValue("@modelId", (object)modelId ?? DBNull.Value);
                cmd.ExecuteNonQuery();
            }
        }

        // ================================================================
        // Database: Read operations
        // ================================================================

        /// <summary>
        /// Loads all construction geometry records from the database.
        /// Optionally filters by owning model ID.
        /// </summary>
        public List<CAD_ConstructionGeometry> LoadConstructionGeometries(string modelId = null)
        {
            var geometries = new List<CAD_ConstructionGeometry>();

            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();

                var sql = modelId != null
                    ? "SELECT * FROM CAD_ConstructionGeometry WHERE MyCAD_ModelID = @mid ORDER BY ConstructionGeometryID;"
                    : "SELECT * FROM CAD_ConstructionGeometry ORDER BY ConstructionGeometryID;";

                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    if (modelId != null)
                        cmd.Parameters.AddWithValue("@mid", modelId);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var geom = ReadConstructionGeometryFromRow(reader);
                            geometries.Add(geom);
                        }
                    }
                }

                // Load children (MyCAD_Model) for each
                foreach (var geom in geometries)
                {
                    var geomId = geom.Name;
                    if (geomId == null) continue;
                    LoadConstructionGeometryChildren(conn, geom, geomId);
                }
            }

            return geometries;
        }

        /// <summary>Loads a single construction geometry by its ID.</summary>
        public CAD_ConstructionGeometry LoadConstructionGeometry(string constructionGeometryId)
        {
            if (string.IsNullOrWhiteSpace(constructionGeometryId))
                throw new ArgumentException("Construction geometry ID must not be null or empty.",
                    nameof(constructionGeometryId));

            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();

                CAD_ConstructionGeometry geom = null;
                using (var cmd = new SQLiteCommand(
                    "SELECT * FROM CAD_ConstructionGeometry WHERE ConstructionGeometryID = @id;", conn))
                {
                    cmd.Parameters.AddWithValue("@id", constructionGeometryId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                            geom = ReadConstructionGeometryFromRow(reader);
                    }
                }

                if (geom == null) return null;

                LoadConstructionGeometryChildren(conn, geom, constructionGeometryId);
                return geom;
            }
        }

        // ================================================================
        // Database: Private helpers — Read
        // ================================================================

        private static CAD_ConstructionGeometry ReadConstructionGeometryFromRow(SQLiteDataReader reader)
        {
            return new CAD_ConstructionGeometry
            {
                Name = reader["Name"] as string,
                Version = reader["Version"] as string ?? "1.0",
                GeometryType = (CAD_ConstructionGeometry.ConstructionGeometryTypeEnum)
                    Convert.ToInt32(reader["GeometryType"])
            };
        }

        private void LoadConstructionGeometryChildren(SQLiteConnection conn,
            CAD_ConstructionGeometry geom, string geomId)
        {
            // Load MyCAD_Model
            var myModelId = GetScalar(conn,
                "SELECT MyCAD_ModelID FROM CAD_ConstructionGeometry WHERE ConstructionGeometryID = @id;",
                geomId);
            if (myModelId != null)
                geom.MyCAD_Model = LoadModel(conn, myModelId);
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

        private static string GetScalar(SQLiteConnection conn, string sql, string id)
        {
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                return cmd.ExecuteScalar() as string;
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

        private static string GenerateId() => Guid.NewGuid().ToString("N");

        // ================================================================
        // Original SolidWorks methods (unchanged)
        // ================================================================

        public static void StartSketch(ModelDoc2 modelDoc, SketchManager sketchManager, string targetPlane = "Front Plane")
        {
            modelDoc.ClearSelection2(true);
            if (!string.IsNullOrWhiteSpace(targetPlane))
            {
                modelDoc.Extension.SelectByID2(targetPlane, "PLANE", 0, 0, 0, false, 0, null, 0);
            }

            sketchManager.InsertSketch(true);
        }

        public void EndSketch()
        {
            _sketchManager.InsertSketch(true);
        }

        public static SketchPoint CreateConstructionPoint(double x, double y, double z, SketchManager sketchManager)
        {
            return sketchManager.CreatePoint(x, y, z);
        }

        public static SketchLine CreateConstructionLine(double startX, double startY, double startZ, double endX, double endY, double endZ, SketchManager sketchManager)
        {
            var line = (SketchLine) sketchManager.CreateLine(startX, startY, startZ, endX, endY, endZ);
            MarkAsConstruction(line);
            return line;
        }

        public static SketchLine CreateConstructionCenterline(double startX, double startY, double startZ, double endX, double endY, double endZ, SketchManager sketchManager)
        {
            var centerLine = (SketchLine)sketchManager.CreateCenterLine(startX, startY, startZ, endX, endY, endZ);
            MarkAsConstruction(centerLine);
            return centerLine;
        }

        public static SketchArc CreateConstructionArc(double centerX, double centerY, double centerZ,
            double startX, double startY, double startZ,
            double endX, double endY, double endZ, SketchManager sketchManager, bool clockwise = true)
        {
            var arc = (SketchArc)sketchManager.CreateArc(centerX, centerY, centerZ, startX, startY, startZ,
                endX, endY, endZ, (short)(clockwise ? 1 : 0));
            MarkAsConstruction(arc);
            return arc;
        }



        /*

        public static SketchArc CreateConstructionCircle(double centerX, double centerY, double centerZ, double radius)
        {
            var sketchManager = ConstructionBuilder._sketchManager.;
            var circle = (SketchArc)sketchManager.CreateCircleByRadius(centerX, centerY, centerZ, radius);
            //MarkAsConstruction(circle);


            return circle;
        }

        */


        public static SketchSegment[] CreateConstructionSlot(double centerX, double centerY, double centerZ, double length, double width, SketchManager sketchManager)
        {
            if (length <= width)
            {
                throw new ArgumentException("Length must be greater than width to form a slot.", nameof(length));
            }

            double halfLength = length / 2.0;
            double halfWidth = width / 2.0;
            double leftX = centerX - halfLength + halfWidth;
            double rightX = centerX + halfLength - halfWidth;

            var bottomLine = CreateConstructionLine(leftX, centerY - halfWidth, centerZ, rightX, centerY - halfWidth, centerZ, sketchManager);
            var topLine = CreateConstructionLine(rightX, centerY + halfWidth, centerZ, leftX, centerY + halfWidth, centerZ, sketchManager);
            var leftArc = CreateConstructionArc(leftX, centerY, centerZ,
                leftX, centerY + halfWidth, centerZ,
                leftX, centerY - halfWidth, centerZ, sketchManager, false);
            var rightArc = CreateConstructionArc(rightX, centerY, centerZ,
                rightX, centerY - halfWidth, centerZ,
                rightX, centerY + halfWidth, centerZ, sketchManager, false);

            return new SketchSegment[] { (SketchSegment)bottomLine, (SketchSegment)rightArc, (SketchSegment)topLine, (SketchSegment)leftArc };
        }

        public static Feature CreateReferencePlane(swRefPlaneType_e planeType, double value1, int value2, double value3, int value4, double value5, FeatureManager featureManager)
        {
            return (Feature)featureManager.InsertRefPlane((int)planeType, value1, value2, value3, value4, value5);
        }

        private static void MarkAsConstruction(object entity)
        {
            if (entity is SketchSegment segment)
            {
                segment.ConstructionGeometry = true;
            }
        }
    }
}
