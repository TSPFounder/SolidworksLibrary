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
    public class DrawingBuilder
    {
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
        public DrawingBuilder() { }

        /// <summary>
        /// Creates a DrawingBuilder configured for database operations.
        /// </summary>
        public DrawingBuilder(string databasePath)
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
        /// Persists one or more <see cref="CAD_Drawing"/> objects
        /// (and their full dependent entity graph) to the database.
        /// </summary>
        public void SaveDrawings(IEnumerable<CAD_Drawing> drawings)
        {
            if (drawings == null) throw new ArgumentNullException(nameof(drawings));

            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                using (var txn = conn.BeginTransaction())
                {
                    foreach (var drawing in drawings)
                    {
                        SaveDrawingCore(conn, drawing);
                    }
                    txn.Commit();
                }
            }
        }

        /// <summary>Convenience overload — persists a single drawing.</summary>
        public void SaveDrawing(CAD_Drawing drawing)
        {
            if (drawing == null) throw new ArgumentNullException(nameof(drawing));
            SaveDrawings(new[] { drawing });
        }

        private void SaveDrawingCore(SQLiteConnection conn, CAD_Drawing drawing)
        {
            var drawingId = drawing.Title ?? drawing.DrawingNumber ?? GenerateId();

            // ---- Ensure FK cursor refs ----

            // CurrentCAD_DrawingSheet
            string curSheetId = null;
            if (drawing.CurrentCAD_DrawingSheet != null)
            {
                curSheetId = drawing.CurrentCAD_DrawingSheet.SheetID ?? GenerateId();
                EnsureDrawingSheet(conn, drawing.CurrentCAD_DrawingSheet, curSheetId);
            }

            // CurrentElement
            string curElementId = null;
            if (drawing.CurrentElement != null)
            {
                curElementId = drawing.CurrentElement.Name ?? GenerateId();
                EnsureDrawingElement(conn, drawing.CurrentElement, curElementId);
            }

            // RevisionTable (also a CAD_DrawingElement)
            string revTableId = null;
            if (drawing.RevisionTable != null)
            {
                revTableId = drawing.RevisionTable.Name ?? GenerateId();
                EnsureDrawingElement(conn, drawing.RevisionTable, revTableId);
            }

            // CurrentSketch
            string curSketchId = null;
            if (drawing.CurrentSketch != null)
            {
                curSketchId = drawing.CurrentSketch.SketchID ?? GenerateId();
                EnsureSketch(conn, drawing.CurrentSketch, curSketchId);
            }

            // CurrentView
            string curViewId = null;
            if (drawing.CurrentView != null)
            {
                curViewId = drawing.CurrentView.ID ?? GenerateId();
                EnsureDrawingView(conn, drawing.CurrentView, curViewId);
            }

            // CurrentPart
            string curPartId = null;
            if (drawing.CurrentPart != null)
            {
                curPartId = drawing.CurrentPart.Name ?? drawing.CurrentPart.PartNumber ?? GenerateId();
                EnsurePart(conn, drawing.CurrentPart, curPartId);
            }

            // CurrentParameter
            string curParamId = null;
            if (drawing.CurrentParameter != null)
            {
                curParamId = drawing.CurrentParameter.Name ?? drawing.CurrentParameter.PartNumber ?? GenerateId();
                EnsureParameter(conn, drawing.CurrentParameter, curParamId);
            }

            // CurrentDimension
            string curDimId = null;
            if (drawing.CurrentDimension != null)
            {
                curDimId = drawing.CurrentDimension.DimensionID ?? GenerateId();
                EnsureDimension(conn, drawing.CurrentDimension, curDimId);
            }

            // CurrentConstructionGeometry
            string curCgId = null;
            if (drawing.CurrentConstructionGeometry != null)
            {
                curCgId = drawing.CurrentConstructionGeometry.Name ?? GenerateId();
                EnsureConstructionGeometry(conn, drawing.CurrentConstructionGeometry, curCgId);
            }

            // MyAssembly
            string asmId = null;
            if (drawing.MyAssembly != null)
            {
                asmId = drawing.MyAssembly.Name ?? GenerateId();
                EnsureAssembly(conn, drawing.MyAssembly, asmId);
            }

            // MyModel
            string modelId = null;
            if (drawing.MyModel != null)
            {
                modelId = drawing.MyModel.Name ?? GenerateId();
                EnsureModel(conn, drawing.MyModel, modelId);
            }

            // ---- INSERT OR REPLACE main row ----
            const string sql =
                @"INSERT OR REPLACE INTO CAD_Drawing
                  (DrawingID, Title, DrawingNumber, Revision,
                   DrawingStandard, MyFormat, MyDrawingSize,
                   CurrentCAD_DrawingSheetID, CurrentElementID, RevisionTableID,
                   CurrentSketchID, CurrentViewID, CurrentPartID,
                   CurrentParameterID, CurrentDimensionID, CurrentConstructionGeometryID,
                   MyAssemblyID, MyModelID)
                  VALUES
                  (@id, @title, @num, @rev,
                   @std, @fmt, @sz,
                   @curSheet, @curElem, @revTbl,
                   @curSketch, @curView, @curPart,
                   @curParam, @curDim, @curCg,
                   @asmId, @modelId);";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", drawingId);
                cmd.Parameters.AddWithValue("@title", (object)drawing.Title ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@num", (object)drawing.DrawingNumber ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@rev", (object)drawing.Revision ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@std", (int)drawing.DrawingStandard);
                cmd.Parameters.AddWithValue("@fmt", (int)drawing.MyFormat);
                cmd.Parameters.AddWithValue("@sz", (int)drawing.MyDrawingSize);
                cmd.Parameters.AddWithValue("@curSheet", (object)curSheetId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@curElem", (object)curElementId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@revTbl", (object)revTableId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@curSketch", (object)curSketchId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@curView", (object)curViewId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@curPart", (object)curPartId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@curParam", (object)curParamId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@curDim", (object)curDimId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@curCg", (object)curCgId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@asmId", (object)asmId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@modelId", (object)modelId ?? DBNull.Value);
                cmd.ExecuteNonQuery();
            }

            // ---- Junction table saves ----
            SaveDrawingSheets(conn, drawing, drawingId);
            SaveDrawingElements(conn, drawing, drawingId);
            SaveDrawingSketches(conn, drawing, drawingId);
            SaveDrawingViews(conn, drawing, drawingId);
            SaveDrawingParts(conn, drawing, drawingId);
            SaveDrawingParameters(conn, drawing, drawingId);
            SaveDrawingDimensions(conn, drawing, drawingId);
            SaveDrawingConstructionGeometries(conn, drawing, drawingId);
        }

        // ================================================================
        // Database: Junction table save helpers
        // ================================================================

        private void SaveDrawingSheets(SQLiteConnection conn, CAD_Drawing drawing, string drawingId)
        {
            DeleteJunction(conn, "CAD_Drawing_Sheet", "DrawingID", drawingId);
            for (int i = 0; i < drawing.MyDrawingSheets.Count; i++)
            {
                var sheet = drawing.MyDrawingSheets[i];
                var sheetId = sheet.SheetID ?? GenerateId();
                EnsureDrawingSheet(conn, sheet, sheetId);
                InsertJunction(conn, "CAD_Drawing_Sheet",
                    "DrawingID", drawingId, "SheetID", sheetId, i);
            }
        }

        private void SaveDrawingElements(SQLiteConnection conn, CAD_Drawing drawing, string drawingId)
        {
            DeleteJunction(conn, "CAD_Drawing_Element", "DrawingID", drawingId);
            for (int i = 0; i < drawing.DrawingElements.Count; i++)
            {
                var elem = drawing.DrawingElements[i];
                var elemId = elem.Name ?? GenerateId();
                EnsureDrawingElement(conn, elem, elemId);
                InsertJunction(conn, "CAD_Drawing_Element",
                    "DrawingID", drawingId, "DrawingElementID", elemId, i);
            }
        }

        private void SaveDrawingSketches(SQLiteConnection conn, CAD_Drawing drawing, string drawingId)
        {
            DeleteJunction(conn, "CAD_Drawing_Sketch", "DrawingID", drawingId);
            for (int i = 0; i < drawing.MyCAD_Sketches.Count; i++)
            {
                var sketch = drawing.MyCAD_Sketches[i];
                var sketchId = sketch.SketchID ?? GenerateId();
                EnsureSketch(conn, sketch, sketchId);
                InsertJunction(conn, "CAD_Drawing_Sketch",
                    "DrawingID", drawingId, "SketchID", sketchId, i);
            }
        }

        private void SaveDrawingViews(SQLiteConnection conn, CAD_Drawing drawing, string drawingId)
        {
            DeleteJunction(conn, "CAD_Drawing_View", "DrawingID", drawingId);
            for (int i = 0; i < drawing.MyViews.Count; i++)
            {
                var view = drawing.MyViews[i];
                var viewId = view.ID ?? GenerateId();
                EnsureDrawingView(conn, view, viewId);
                InsertJunction(conn, "CAD_Drawing_View",
                    "DrawingID", drawingId, "DrawingViewID", viewId, i);
            }
        }

        private void SaveDrawingParts(SQLiteConnection conn, CAD_Drawing drawing, string drawingId)
        {
            DeleteJunction(conn, "CAD_Drawing_Part", "DrawingID", drawingId);
            for (int i = 0; i < drawing.MyParts.Count; i++)
            {
                var part = drawing.MyParts[i];
                var partId = part.Name ?? part.PartNumber ?? GenerateId();
                EnsurePart(conn, part, partId);
                InsertJunction(conn, "CAD_Drawing_Part",
                    "DrawingID", drawingId, "PartID", partId, i);
            }
        }

        private void SaveDrawingParameters(SQLiteConnection conn, CAD_Drawing drawing, string drawingId)
        {
            DeleteJunction(conn, "CAD_Drawing_Parameter", "DrawingID", drawingId);
            for (int i = 0; i < drawing.MyParameters.Count; i++)
            {
                var param = drawing.MyParameters[i];
                var paramId = param.Name ?? param.PartNumber ?? GenerateId();
                EnsureParameter(conn, param, paramId);
                InsertJunction(conn, "CAD_Drawing_Parameter",
                    "DrawingID", drawingId, "MathParameterID", paramId, i);
            }
        }

        private void SaveDrawingDimensions(SQLiteConnection conn, CAD_Drawing drawing, string drawingId)
        {
            DeleteJunction(conn, "CAD_Drawing_Dimension", "DrawingID", drawingId);
            for (int i = 0; i < drawing.MyDimensions.Count; i++)
            {
                var dim = drawing.MyDimensions[i];
                var dimId = dim.DimensionID ?? GenerateId();
                EnsureDimension(conn, dim, dimId);
                InsertJunction(conn, "CAD_Drawing_Dimension",
                    "DrawingID", drawingId, "DimensionID", dimId, i);
            }
        }

        private void SaveDrawingConstructionGeometries(SQLiteConnection conn, CAD_Drawing drawing, string drawingId)
        {
            DeleteJunction(conn, "CAD_Drawing_ConstructionGeometry", "DrawingID", drawingId);
            for (int i = 0; i < drawing.MyConstructionGeometry.Count; i++)
            {
                var cg = drawing.MyConstructionGeometry[i];
                var cgId = cg.Name ?? GenerateId();
                EnsureConstructionGeometry(conn, cg, cgId);
                InsertJunction(conn, "CAD_Drawing_ConstructionGeometry",
                    "DrawingID", drawingId, "ConstructionGeometryID", cgId, i);
            }
        }

        // ================================================================
        // Database: Read operations
        // ================================================================

        /// <summary>
        /// Loads all drawing records from the database.
        /// </summary>
        public List<CAD_Drawing> LoadDrawings()
        {
            var drawings = new List<CAD_Drawing>();

            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();

                using (var cmd = new SQLiteCommand(
                    "SELECT * FROM CAD_Drawing ORDER BY DrawingID;", conn))
                {
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var drawing = ReadDrawingFromRow(reader);
                            drawings.Add(drawing);
                        }
                    }
                }

                foreach (var drawing in drawings)
                {
                    var drawingId = drawing.Title ?? drawing.DrawingNumber;
                    if (drawingId == null) continue;
                    LoadDrawingChildren(conn, drawing, drawingId);
                }
            }

            return drawings;
        }

        /// <summary>Loads a single drawing by its ID.</summary>
        public CAD_Drawing LoadDrawing(string drawingId)
        {
            if (string.IsNullOrWhiteSpace(drawingId))
                throw new ArgumentException("Drawing ID must not be null or empty.", nameof(drawingId));

            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();

                CAD_Drawing drawing = null;
                using (var cmd = new SQLiteCommand(
                    "SELECT * FROM CAD_Drawing WHERE DrawingID = @id;", conn))
                {
                    cmd.Parameters.AddWithValue("@id", drawingId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                            drawing = ReadDrawingFromRow(reader);
                    }
                }

                if (drawing == null) return null;

                LoadDrawingChildren(conn, drawing, drawingId);
                return drawing;
            }
        }

        // ================================================================
        // Database: Private helpers — Read (main entity)
        // ================================================================

        private static CAD_Drawing ReadDrawingFromRow(SQLiteDataReader reader)
        {
            return new CAD_Drawing
            {
                Title = reader["Title"] as string,
                DrawingNumber = reader["DrawingNumber"] as string,
                Revision = reader["Revision"] as string,
                DrawingStandard = (CAD_Drawing.DrawingStandardEnum)Convert.ToInt32(reader["DrawingStandard"]),
                MyFormat = (CAD_Drawing.DocFormatEnum)Convert.ToInt32(reader["MyFormat"]),
                MyDrawingSize = (CAD_Drawing.DrawingSize)Convert.ToInt32(reader["MyDrawingSize"])
            };
        }

        private void LoadDrawingChildren(SQLiteConnection conn, CAD_Drawing drawing, string drawingId)
        {
            // Load cursor IDs from main row
            var curSheetId = GetScalar(conn,
                "SELECT CurrentCAD_DrawingSheetID FROM CAD_Drawing WHERE DrawingID = @id;", drawingId);
            var curViewId = GetScalar(conn,
                "SELECT CurrentViewID FROM CAD_Drawing WHERE DrawingID = @id;", drawingId);
            var curSketchId = GetScalar(conn,
                "SELECT CurrentSketchID FROM CAD_Drawing WHERE DrawingID = @id;", drawingId);
            var curPartId = GetScalar(conn,
                "SELECT CurrentPartID FROM CAD_Drawing WHERE DrawingID = @id;", drawingId);
            var curParamId = GetScalar(conn,
                "SELECT CurrentParameterID FROM CAD_Drawing WHERE DrawingID = @id;", drawingId);
            var curDimId = GetScalar(conn,
                "SELECT CurrentDimensionID FROM CAD_Drawing WHERE DrawingID = @id;", drawingId);
            var curCgId = GetScalar(conn,
                "SELECT CurrentConstructionGeometryID FROM CAD_Drawing WHERE DrawingID = @id;", drawingId);

            // Load public-setter associations
            var asmId = GetScalar(conn,
                "SELECT MyAssemblyID FROM CAD_Drawing WHERE DrawingID = @id;", drawingId);
            if (asmId != null)
                drawing.MyAssembly = LoadAssemblyStub(conn, asmId);

            var modelId = GetScalar(conn,
                "SELECT MyModelID FROM CAD_Drawing WHERE DrawingID = @id;", drawingId);
            if (modelId != null)
                drawing.MyModel = LoadModel(conn, modelId);

            // Load collections via Add* methods (setCurrent matches cursor IDs)
            LoadDrawingSheetCollection(conn, drawing, drawingId, curSheetId);
            LoadDrawingElementCollection(conn, drawing, drawingId);
            LoadDrawingSketchCollection(conn, drawing, drawingId, curSketchId);
            LoadDrawingViewCollection(conn, drawing, drawingId, curViewId);
            LoadDrawingPartCollection(conn, drawing, drawingId, curPartId);
            LoadDrawingParameterCollection(conn, drawing, drawingId, curParamId);
            LoadDrawingDimensionCollection(conn, drawing, drawingId, curDimId);
            LoadDrawingConstructionGeometryCollection(conn, drawing, drawingId, curCgId);
        }

        // ================================================================
        // Database: Collection loaders
        // ================================================================

        private void LoadDrawingSheetCollection(SQLiteConnection conn, CAD_Drawing drawing,
            string drawingId, string curSheetId)
        {
            const string sql =
                @"SELECT s.* FROM CAD_Drawing_Sheet ds
                  JOIN CAD_DrawingSheet s ON ds.SheetID = s.SheetID
                  WHERE ds.DrawingID = @id ORDER BY ds.SortOrder;";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", drawingId);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var sheet = ReadDrawingSheetFromRow(reader);
                        var sheetId = reader["SheetID"] as string;
                        drawing.AddSheet(sheet, setCurrent: sheetId == curSheetId);
                    }
                }
            }
        }

        private void LoadDrawingElementCollection(SQLiteConnection conn, CAD_Drawing drawing,
            string drawingId)
        {
            const string sql =
                @"SELECT e.* FROM CAD_Drawing_Element de
                  JOIN CAD_DrawingElement e ON de.DrawingElementID = e.DrawingElementID
                  WHERE de.DrawingID = @id ORDER BY de.SortOrder;";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", drawingId);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var elem = ReadDrawingElementFromRow(reader);
                        drawing.AddElement(elem, setCurrent: false);
                    }
                }
            }
        }

        private void LoadDrawingSketchCollection(SQLiteConnection conn, CAD_Drawing drawing,
            string drawingId, string curSketchId)
        {
            const string sql =
                @"SELECT sk.* FROM CAD_Drawing_Sketch dsk
                  JOIN CAD_Sketch sk ON dsk.SketchID = sk.SketchID
                  WHERE dsk.DrawingID = @id ORDER BY dsk.SortOrder;";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", drawingId);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var sketch = ReadSketchStubFromRow(reader);
                        var sketchId = reader["SketchID"] as string;
                        drawing.AddSketch(sketch, setCurrent: sketchId == curSketchId);
                    }
                }
            }
        }

        private void LoadDrawingViewCollection(SQLiteConnection conn, CAD_Drawing drawing,
            string drawingId, string curViewId)
        {
            const string sql =
                @"SELECT v.* FROM CAD_Drawing_View dv
                  JOIN CAD_DrawingView v ON dv.DrawingViewID = v.DrawingViewID
                  WHERE dv.DrawingID = @id ORDER BY dv.SortOrder;";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", drawingId);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var view = ReadDrawingViewFromRow(reader);
                        var viewId = reader["DrawingViewID"] as string;
                        drawing.AddView(view, setCurrent: viewId == curViewId);
                    }
                }
            }
        }

        private void LoadDrawingPartCollection(SQLiteConnection conn, CAD_Drawing drawing,
            string drawingId, string curPartId)
        {
            const string sql =
                @"SELECT p.* FROM CAD_Drawing_Part dp
                  JOIN CAD_Part p ON dp.PartID = p.PartID
                  WHERE dp.DrawingID = @id ORDER BY dp.SortOrder;";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", drawingId);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var part = ReadPartStubFromRow(reader);
                        var partId = reader["PartID"] as string;
                        drawing.AddPart(part, setCurrent: partId == curPartId);
                    }
                }
            }
        }

        private void LoadDrawingParameterCollection(SQLiteConnection conn, CAD_Drawing drawing,
            string drawingId, string curParamId)
        {
            const string sql =
                @"SELECT mp.* FROM CAD_Drawing_Parameter dp
                  JOIN MathParameter mp ON dp.MathParameterID = mp.MathParameterID
                  WHERE dp.DrawingID = @id ORDER BY dp.SortOrder;";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", drawingId);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var param = ReadParameterFromRow(reader);
                        var paramId = reader["MathParameterID"] as string;
                        drawing.AddParameter(param, setCurrent: paramId == curParamId);
                    }
                }
            }
        }

        private void LoadDrawingDimensionCollection(SQLiteConnection conn, CAD_Drawing drawing,
            string drawingId, string curDimId)
        {
            const string sql =
                @"SELECT d.* FROM CAD_Drawing_Dimension dd
                  JOIN CAD_Dimension d ON dd.DimensionID = d.DimensionID
                  WHERE dd.DrawingID = @id ORDER BY dd.SortOrder;";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", drawingId);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var dim = ReadDimensionFromRow(reader);
                        var dimId = reader["DimensionID"] as string;
                        drawing.AddDimension(dim, setCurrent: dimId == curDimId);
                    }
                }
            }
        }

        private void LoadDrawingConstructionGeometryCollection(SQLiteConnection conn,
            CAD_Drawing drawing, string drawingId, string curCgId)
        {
            const string sql =
                @"SELECT cg.* FROM CAD_Drawing_ConstructionGeometry dcg
                  JOIN CAD_ConstructionGeometry cg ON dcg.ConstructionGeometryID = cg.ConstructionGeometryID
                  WHERE dcg.DrawingID = @id ORDER BY dcg.SortOrder;";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", drawingId);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var geom = ReadConstructionGeometryFromRow(reader);
                        var cgId = reader["ConstructionGeometryID"] as string;
                        drawing.AddConstructionGeometry(geom, setCurrent: cgId == curCgId);
                    }
                }
            }
        }

        // ================================================================
        // Database: Read helpers — stub entity readers
        // ================================================================

        private static CAD_DrawingSheet ReadDrawingSheetFromRow(SQLiteDataReader reader)
        {
            return new CAD_DrawingSheet
            {
                SheetID = reader["SheetID"] as string,
                SheetNumber = Convert.ToInt32(reader["SheetNumber"]),
                Size = (CAD_Drawing.DrawingSize)Convert.ToInt32(reader["Size"]),
                SheetOrientation = (CAD_DrawingSheet.Orientation)Convert.ToInt32(reader["SheetOrientation"])
            };
        }

        private static CAD_DrawingElement ReadDrawingElementFromRow(SQLiteDataReader reader)
        {
            return new CAD_DrawingElement
            {
                Name = reader["Name"] as string,
                MyType = (CAD_DrawingElement.DrawingElementType)Convert.ToInt32(reader["MyType"])
            };
        }

        private static CAD_DrawingView ReadDrawingViewFromRow(SQLiteDataReader reader)
        {
            return new CAD_DrawingView
            {
                ID = reader["ID"] as string,
                Title = reader["Title"] as string,
                Description = reader["Description"] as string,
                Type = (CAD_DrawingView.ViewType)Convert.ToInt32(reader["ViewType"]),
                // Inherited from CAD_DrawingElement
                Name = reader["Name"] as string,
                MyType = (CAD_DrawingElement.DrawingElementType)Convert.ToInt32(reader["MyType"])
            };
        }

        private static CAD_Sketch ReadSketchStubFromRow(SQLiteDataReader reader)
        {
            return new CAD_Sketch
            {
                SketchID = reader["SketchID"] as string,
                Version = reader["Version"] as string,
                IsTwoD = Convert.ToInt32(reader["IsTwoD"]) != 0
            };
        }

        private static CAD_Part ReadPartStubFromRow(SQLiteDataReader reader)
        {
            return new CAD_Part
            {
                Name = reader["Name"] as string,
                Version = reader["Version"] as string,
                PartNumber = reader["PartNumber"] as string,
                Description = reader["Description"] as string
            };
        }

        private static CAD.Parameter ReadParameterFromRow(SQLiteDataReader reader)
        {
            return new CAD.Parameter
            {
                Name = reader["Name"] as string,
                PartNumber = reader["PartNumber"] as string,
                Description = reader["Description"] as string,
                Comments = reader["Comments"] as string,
                MyParameterType = (CAD.Parameter.ParameterType)Convert.ToInt32(reader["MyParameterType"]),
                SolidWorksParameterName = reader["SolidWorksParameterName"] as string,
                Fusion360ParameterName = reader["Fusion360ParameterName"] as string
            };
        }

        private static CAD.Dimension ReadDimensionFromRow(SQLiteDataReader reader)
        {
            return new CAD.Dimension
            {
                DimensionID = reader["DimensionID"] as string,
                Name = reader["Name"] as string,
                Description = reader["Description"] as string,
                IsOrdinate = Convert.ToInt32(reader["IsOrdinate"]) != 0,
                DimensionNominalValue = Convert.ToDouble(reader["DimensionNominalValue"]),
                DimensionUpperLimitValue = Convert.ToDouble(reader["DimensionUpperLimitValue"]),
                DimensionLowerLimitValue = Convert.ToDouble(reader["DimensionLowerLimitValue"]),
                MyDimensionType = (CAD.Dimension.DimensionType)Convert.ToInt32(reader["MyDimensionType"])
            };
        }

        private static CAD_ConstructionGeometery ReadConstructionGeometryFromRow(SQLiteDataReader reader)
        {
            return new CAD_ConstructionGeometery
            {
                Name = reader["Name"] as string,
                Version = reader["Version"] as string ?? "1.0",
                GeometryType = (CAD_ConstructionGeometry.ConstructionGeometryTypeEnum)
                    Convert.ToInt32(reader["GeometryType"])
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

        // ================================================================
        // Database: Stub entity loaders
        // ================================================================

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

        private CAD_Assembly LoadAssemblyStub(SQLiteConnection conn, string assemblyId)
        {
            const string sql = "SELECT * FROM CAD_Assembly WHERE AssemblyID = @id;";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", assemblyId);
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

        // ================================================================
        // Database: Private helpers — Write (Ensure* methods)
        // ================================================================

        private void EnsureDrawingSheet(SQLiteConnection conn, CAD_DrawingSheet sheet, string sheetId)
        {
            const string sql =
                @"INSERT OR IGNORE INTO CAD_DrawingSheet
                  (SheetID, SheetNumber, Size, SheetOrientation)
                  VALUES (@id, @num, @sz, @orient);";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", sheetId);
                cmd.Parameters.AddWithValue("@num", sheet.SheetNumber);
                cmd.Parameters.AddWithValue("@sz", (int)sheet.Size);
                cmd.Parameters.AddWithValue("@orient", (int)sheet.SheetOrientation);
                cmd.ExecuteNonQuery();
            }
        }

        private static void EnsureDrawingElement(SQLiteConnection conn, CAD_DrawingElement elem, string elemId)
        {
            const string sql =
                @"INSERT OR IGNORE INTO CAD_DrawingElement
                  (DrawingElementID, Name, MyType)
                  VALUES (@id, @name, @type);";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", elemId);
                cmd.Parameters.AddWithValue("@name", (object)elem.Name ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@type", (int)elem.MyType);
                cmd.ExecuteNonQuery();
            }
        }

        private void EnsureDrawingView(SQLiteConnection conn, CAD_DrawingView view, string viewId)
        {
            string centerPtId = null;
            if (view.CenterPoint != null)
            {
                centerPtId = view.CenterPoint.PointID ?? GenerateId();
                EnsurePoint(conn, view.CenterPoint, centerPtId);
            }

            const string sql =
                @"INSERT OR IGNORE INTO CAD_DrawingView
                  (DrawingViewID, Name, MyType, ID, Title, Description, ViewType, CenterPointID)
                  VALUES (@id, @name, @elemType, @uid, @title, @desc, @viewType, @cpId);";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", viewId);
                cmd.Parameters.AddWithValue("@name", (object)view.Name ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@elemType", (int)view.MyType);
                cmd.Parameters.AddWithValue("@uid", (object)view.ID ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@title", (object)view.Title ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@desc", (object)view.Description ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@viewType", (int)view.Type);
                cmd.Parameters.AddWithValue("@cpId", (object)centerPtId ?? DBNull.Value);
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

        private static void EnsurePart(SQLiteConnection conn, CAD_Part part, string partId)
        {
            const string sql =
                @"INSERT OR IGNORE INTO CAD_Part
                  (PartID, Name, Version, PartNumber, Description)
                  VALUES (@id, @name, @ver, @pn, @desc);";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", partId);
                cmd.Parameters.AddWithValue("@name", (object)part.Name ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ver", (object)part.Version ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@pn", (object)part.PartNumber ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@desc", (object)part.Description ?? DBNull.Value);
                cmd.ExecuteNonQuery();
            }
        }

        private static void EnsureParameter(SQLiteConnection conn, CAD.Parameter param, string paramId)
        {
            const string sql =
                @"INSERT OR IGNORE INTO MathParameter
                  (MathParameterID, Name, PartNumber, Description, Comments,
                   MyParameterType, SolidWorksParameterName, Fusion360ParameterName)
                  VALUES (@id, @name, @pn, @desc, @comments, @type, @swName, @f360Name);";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", paramId);
                cmd.Parameters.AddWithValue("@name", (object)param.Name ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@pn", (object)param.PartNumber ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@desc", (object)param.Description ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@comments", (object)param.Comments ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@type", (int)param.MyParameterType);
                cmd.Parameters.AddWithValue("@swName", (object)param.SolidWorksParameterName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@f360Name", (object)param.Fusion360ParameterName ?? DBNull.Value);
                cmd.ExecuteNonQuery();
            }
        }

        private void EnsureDimension(SQLiteConnection conn, CAD.Dimension dim, string dimId)
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

        private static void EnsureConstructionGeometry(SQLiteConnection conn,
            CAD_ConstructionGeometry cg, string cgId)
        {
            const string sql =
                @"INSERT OR IGNORE INTO CAD_ConstructionGeometry
                  (ConstructionGeometryID, Name, Version, GeometryType)
                  VALUES (@id, @name, @ver, @geoType);";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", cgId);
                cmd.Parameters.AddWithValue("@name", (object)cg.Name ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ver", (object)cg.Version ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@geoType", (int)cg.GeometryType);
                cmd.ExecuteNonQuery();
            }
        }

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

        // ================================================================
        // Database: Generic helpers
        // ================================================================

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

        private static string GetScalar(SQLiteConnection conn, string sql, string id)
        {
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                return cmd.ExecuteScalar() as string;
            }
        }

        private static string GenerateId() => Guid.NewGuid().ToString("N");

        // ================================================================
        // Original SolidWorks methods (unchanged)
        // ================================================================

        // -------------------------------------------
        // Drawing Document Creation
        // -------------------------------------------

        public static DrawingDoc CreateDrawingDocument(SldWorks.SldWorks swApp, out ModelDoc2 modelDoc)
        {
            string drawingTemplate = swApp.GetUserPreferenceStringValue(
                (int)swUserPreferenceStringValue_e.swDefaultTemplateDrawing);

            if (string.IsNullOrEmpty(drawingTemplate))
            {
                throw new InvalidOperationException("Drawing template not found in SolidWorks settings.");
            }

            object model = swApp.NewDocument(drawingTemplate, 0, 0, 0);
            if (model == null)
            {
                throw new InvalidOperationException("Failed to create drawing document.");
            }

            modelDoc = (ModelDoc2)model;
            return (DrawingDoc)model;
        }

        // -------------------------------------------
        // Sheet Management
        // -------------------------------------------

        public static bool ActivateSheet(DrawingDoc drawingDoc, string sheetName)
        {
            if (string.IsNullOrEmpty(sheetName)) return false;
            return drawingDoc.ActivateSheet(sheetName);
        }

        /// <summary>
        /// Adds a new sheet to the drawing.
        /// </summary>
        public static bool AddSheet(DrawingDoc drawingDoc, string sheetName,
            swDwgPaperSizes_e paperSize,
            swDwgTemplates_e sheetTemplate,
            double scaleNumerator, double scaleDenominator,
            bool firstAngleProjection = true,
            string templatePath = "",
            double customWidth = 0, double customHeight = 0,
            string propertyViewName = "")
        {
            return drawingDoc.NewSheet3(
                sheetName,
                (int)paperSize,
                (int)sheetTemplate,
                scaleNumerator,
                scaleDenominator,
                firstAngleProjection,
                string.IsNullOrEmpty(templatePath) ? "" : templatePath,
                customWidth,
                customHeight,
                propertyViewName ?? "");
        }

        // -------------------------------------------
        // Standard Views
        // -------------------------------------------

        public static SldWorks.View CreateFrontView(DrawingDoc drawingDoc, string modelPath, double x, double y)
        {
            return CreateNamedView(drawingDoc, modelPath, x, y, (int)swStandardViews_e.swFrontView);
        }

        public static SldWorks.View CreateBackView(DrawingDoc drawingDoc, string modelPath, double x, double y)
        {
            return CreateNamedView(drawingDoc, modelPath, x, y, (int)swStandardViews_e.swBackView);
        }

        public static SldWorks.View CreateTopView(DrawingDoc drawingDoc, string modelPath, double x, double y)
        {
            return CreateNamedView(drawingDoc, modelPath, x, y, (int)swStandardViews_e.swTopView);
        }

        public static SldWorks.View CreateBottomView(DrawingDoc drawingDoc, string modelPath, double x, double y)
        {
            return CreateNamedView(drawingDoc, modelPath, x, y, (int)swStandardViews_e.swBottomView);
        }

        public static SldWorks.View CreateLeftView(DrawingDoc drawingDoc, string modelPath, double x, double y)
        {
            return CreateNamedView(drawingDoc, modelPath, x, y, (int)swStandardViews_e.swLeftView);
        }

        public static SldWorks.View CreateRightView(DrawingDoc drawingDoc, string modelPath, double x, double y)
        {
            return CreateNamedView(drawingDoc, modelPath, x, y, (int)swStandardViews_e.swRightView);
        }

        public static SldWorks.View CreateIsometricView(DrawingDoc drawingDoc, string modelPath, double x, double y)
        {
            return CreateNamedView(drawingDoc, modelPath, x, y, (int)swStandardViews_e.swIsometricView);
        }

        public static SldWorks.View CreateTrimetricView(DrawingDoc drawingDoc, string modelPath, double x, double y)
        {
            return CreateNamedView(drawingDoc, modelPath, x, y, (int)swStandardViews_e.swTrimetricView);
        }

        public static SldWorks.View CreateDimetricView(DrawingDoc drawingDoc, string modelPath, double x, double y)
        {
            return CreateNamedView(drawingDoc, modelPath, x, y, (int)swStandardViews_e.swDimetricView);
        }

        // -------------------------------------------
        // Projected & Derived Views
        // -------------------------------------------

        public static SldWorks.View CreateProjectedView(DrawingDoc drawingDoc, double x, double y)
        {
            return (SldWorks.View)drawingDoc.CreateUnfoldedViewAt3(x, y, 0, false);
        }

        public static SldWorks.View CreateAuxiliaryView(DrawingDoc drawingDoc, double x, double y, string label,
            int arrowDirection = 0, bool flipArrow = false,
            bool showLabel = true, bool flipView = false)
        {
            return (SldWorks.View)drawingDoc.CreateAuxiliaryViewAt2(
                x, y, arrowDirection, flipArrow, label, showLabel, flipView);
        }

        public static SldWorks.View CreateDetailView(DrawingDoc drawingDoc, double viewX, double viewY, string detailLabel,
            double scaleNumerator = 2, double scaleDenominator = 1,
            bool fullOutline = true, bool jaggedOutline = false, bool noOutline = false)
        {
            return (SldWorks.View)drawingDoc.CreateDetailViewAt4(
                viewX, viewY, 0,
                (int)swDetViewStyle_e.swDetViewSTANDARD,
                scaleNumerator, scaleDenominator,
                detailLabel,
                (int)swDetCircleShowType_e.swDetCircleCIRCLE,
                fullOutline, jaggedOutline, noOutline, 5);
        }

        public static SldWorks.View CreateSectionView(DrawingDoc drawingDoc, double x, double y, string sectionLabel,
            int options = (int)swCreateSectionViewAtOptions_e.swCreateSectionView_NotAligned,
            object excludedComponents = null, double sectionDepth = 0)
        {
            return (SldWorks.View)drawingDoc.CreateSectionViewAt5(
                x, y, 0, sectionLabel, options, excludedComponents, sectionDepth);
        }

        public static bool CreateBrokenOutSection(DrawingDoc drawingDoc, double depth)
        {
            return drawingDoc.CreateBreakOutSection(depth);
        }

        public static bool CropView(DrawingDoc drawingDoc, SldWorks.View view,
            bool fullOutline = true, bool jaggedOutline = false, int shapeIntensity = 5)
        {
            if (view == null) return false;

            drawingDoc.ActivateView(view.Name);
            int error = view.Crop2(fullOutline, jaggedOutline, shapeIntensity);
            return error == (int)swCropViewErrors_e.swCropViewErrors_NoError;
        }

        public static bool RemoveCropView(DrawingDoc drawingDoc, ModelDoc2 modelDoc, SldWorks.View view)
        {
            if (view == null || !view.IsCropped()) return false;

            drawingDoc.ActivateView(view.Name);
            modelDoc.Extension.SelectByID2(
                view.Name, "DRAWINGVIEW", 0, 0, 0, false, 0, null, 0);
            modelDoc.EditSketch();
            modelDoc.Extension.DeleteSelection2(
                (int)swDeleteSelectionOptions_e.swDelete_Absorbed);
            modelDoc.ClearSelection2(true);
            return !view.IsCropped();
        }

        public static bool IsCropped(SldWorks.View view)
        {
            if (view == null) return false;
            return view.IsCropped();
        }

        public static bool InsertAlternatePositionView(DrawingDoc drawingDoc,
            SldWorks.View parentView, string configName)
        {
            if (parentView == null) return false;

            drawingDoc.ActivateView(parentView.Name);
            var result = parentView.InsertAlternateView(
                string.IsNullOrEmpty(configName) ? "" : configName);
            return result != null;
        }

        public static SldWorks.View CreateRelativeView(DrawingDoc drawingDoc, string modelPath, double x, double y)
        {
            if (string.IsNullOrEmpty(modelPath)) return null;

            return (SldWorks.View)drawingDoc.CreateRelativeView(modelPath, x, y, 0, 1);
        }

        public static SldWorks.View Create3DDrawingView(DrawingDoc drawingDoc, string modelPath, double x, double y)
        {
            return CreateNamedView(drawingDoc, modelPath, x, y, (int)swStandardViews_e.swIsometricView);
        }

        // -------------------------------------------
        // Flat Pattern & Custom Views
        // -------------------------------------------

        public static SldWorks.View CreateFlatPatternView(DrawingDoc drawingDoc, string modelPath,
            double x, double y, string configName)
        {
            return (SldWorks.View)drawingDoc.CreateFlatPatternViewFromModelView3(
                modelPath, configName, x, y, 0, true, false);
        }

        public static SldWorks.View CreateCustomView(DrawingDoc drawingDoc, string modelPath,
            double x, double y, string viewName)
        {
            return (SldWorks.View)drawingDoc.CreateDrawViewFromModelView3(
                modelPath, viewName, x, y, 0);
        }

        // -------------------------------------------
        // Core Named-View Helper
        // -------------------------------------------

        private static SldWorks.View CreateNamedView(DrawingDoc drawingDoc, string modelPath,
            double x, double y, int standardView)
        {
            if (string.IsNullOrEmpty(modelPath))
            {
                Console.WriteLine("Model path is required to create a drawing view.");
                return null;
            }

            string viewName = GetStandardViewName(standardView);

            SldWorks.View view = (SldWorks.View)drawingDoc.CreateDrawViewFromModelView3(
                modelPath, viewName, x, y, 0);

            if (view == null)
            {
                Console.WriteLine($"Failed to create {viewName} view.");
            }

            return view;
        }

        private static string GetStandardViewName(int standardView)
        {
            switch ((swStandardViews_e)standardView)
            {
                case swStandardViews_e.swFrontView: return "*Front";
                case swStandardViews_e.swBackView: return "*Back";
                case swStandardViews_e.swTopView: return "*Top";
                case swStandardViews_e.swBottomView: return "*Bottom";
                case swStandardViews_e.swLeftView: return "*Left";
                case swStandardViews_e.swRightView: return "*Right";
                case swStandardViews_e.swIsometricView: return "*Isometric";
                case swStandardViews_e.swTrimetricView: return "*Trimetric";
                case swStandardViews_e.swDimetricView: return "*Dimetric";
                default: return "*Front";
            }
        }

        // -------------------------------------------
        // Notes
        // -------------------------------------------

        public static Note InsertNote(ModelDoc2 modelDoc, string text, double x, double y)
        {
            if (string.IsNullOrEmpty(text)) return null;

            modelDoc.SetAddToDB(true);
            Note note = (Note)modelDoc.InsertNote(text);
            modelDoc.SetAddToDB(false);

            if (note != null)
            {
                Annotation annotation = (Annotation)note.GetAnnotation();
                annotation?.SetPosition(x, y, 0);
            }

            return note;
        }

        public static Note InsertNoteWithLeader(ModelDoc2 modelDoc, string text, double x, double y)
        {
            modelDoc.SetAddToDB(true);
            Note note = (Note)modelDoc.InsertNote(text);
            modelDoc.SetAddToDB(false);

            if (note != null)
            {
                note.SetTextJustification((int)swTextJustification_e.swTextJustificationLeft);
                Annotation annotation = (Annotation)note.GetAnnotation();
                if (annotation != null)
                {
                    annotation.SetPosition(x, y, 0);
                    annotation.SetLeader3(
                        (int)swLeaderStyle_e.swSTRAIGHT,
                        (int)swLeaderSide_e.swLS_SMART,
                        true, true, false, false);
                }
            }

            return note;
        }

        // -------------------------------------------
        // Bill of Materials (BoM)
        // -------------------------------------------

        public static BomTableAnnotation InsertBomTable(DrawingDoc drawingDoc,
            double x, double y,
            swBomType_e bomType, string configuration, string templatePath)
        {
            SldWorks.View activeView = (SldWorks.View)drawingDoc.ActiveDrawingView;
            if (activeView == null)
            {
                Console.WriteLine("No active drawing view for BOM insertion.");
                return null;
            }

            BomTableAnnotation bomTable = (BomTableAnnotation)activeView.InsertBomTable4(
                false, x, y, 2,
                (int)bomType,
                configuration,
                string.IsNullOrEmpty(templatePath) ? "" : templatePath,
                false, 0, false);

            if (bomTable == null)
            {
                Console.WriteLine("Failed to insert BOM table.");
            }

            return bomTable;
        }

        public static BomTableAnnotation InsertIndentedBom(DrawingDoc drawingDoc,
            double x, double y, string configuration, string templatePath)
        {
            return InsertBomTable(drawingDoc, x, y, swBomType_e.swBomType_Indented,
                configuration, templatePath);
        }

        // -------------------------------------------
        // Title Block
        // -------------------------------------------

        public static bool FillTitleBlockField(ModelDoc2 modelDoc, string fieldName, string value)
        {
            if (string.IsNullOrEmpty(fieldName)) return false;

            bool selected = modelDoc.Extension.SelectByID2(
                fieldName, "NOTE", 0, 0, 0, false, 0, null, 0);

            if (!selected) return false;

            SelectionMgr selMgr = (SelectionMgr)modelDoc.SelectionManager;
            Note note = (Note)selMgr.GetSelectedObject6(1, -1);

            if (note != null)
            {
                note.SetText(value);
                modelDoc.ClearSelection2(true);
                return true;
            }

            modelDoc.ClearSelection2(true);
            return false;
        }

        public static void FillTitleBlock(ModelDoc2 modelDoc,
            string title, string drawnBy, string checkedBy,
            string approvedBy, string date, string partNumber,
            string revision, string material, string scale)
        {
            FillTitleBlockField(modelDoc, "Title", title);
            FillTitleBlockField(modelDoc, "DrawnBy", drawnBy);
            FillTitleBlockField(modelDoc, "CheckedBy", checkedBy);
            FillTitleBlockField(modelDoc, "ApprovedBy", approvedBy);
            FillTitleBlockField(modelDoc, "DrawnDate", date);
            FillTitleBlockField(modelDoc, "PartNo", partNumber);
            FillTitleBlockField(modelDoc, "Revision", revision);
            FillTitleBlockField(modelDoc, "Material", material);
            FillTitleBlockField(modelDoc, "Scale", scale);
        }

        // -------------------------------------------
        // Revision Table
        // -------------------------------------------

        public static RevisionTableAnnotation InsertRevisionTable(DrawingDoc drawingDoc,
            double x, double y, string templatePath)
        {
            Sheet currentsheet = (Sheet)drawingDoc.GetCurrentSheet();

            RevisionTableAnnotation revTableAnno = currentsheet.InsertRevisionTable2(
                true, x, y,
                (int)swBOMConfigurationAnchorType_e.swBOMConfigurationAnchor_TopLeft,
                string.IsNullOrEmpty(templatePath) ? "" : templatePath,
                (int)swRevisionTableSymbolShape_e.swRevisionTable_CircleSymbol, true);

            if (revTableAnno == null)
            {
                Console.WriteLine("Failed to insert revision table.");
            }

            return revTableAnno;
        }

        public static bool AddRevisionRow(RevisionTableAnnotation revTable,
            string revisionId, string description, string date, string approvedBy)
        {
            if (revTable == null) return false;

            int revMark = revTable.AddRevision(revisionId);
            if (revMark == 0) return false;

            TableAnnotation table = (TableAnnotation)revTable;
            int lastRow = table.RowCount - 1;

            if (table.ColumnCount > 1 && !string.IsNullOrEmpty(description))
                table.Text[lastRow, 1] = description;
            if (table.ColumnCount > 2 && !string.IsNullOrEmpty(date))
                table.Text[lastRow, 2] = date;
            if (table.ColumnCount > 3 && !string.IsNullOrEmpty(approvedBy))
                table.Text[lastRow, 3] = approvedBy;

            return true;
        }

        // -------------------------------------------
        // General Table
        // -------------------------------------------

        public static TableAnnotation InsertGeneralTable(DrawingDoc drawingDoc,
            double x, double y,
            int rows, int columns, double rowHeight, double colWidth,
            string templatePath)
        {
            TableAnnotation table = (TableAnnotation)drawingDoc.InsertTableAnnotation2(
                true, x, y,
                (int)swBOMConfigurationAnchorType_e.swBOMConfigurationAnchor_TopLeft,
                string.IsNullOrEmpty(templatePath) ? "" : templatePath,
                rows, columns);

            if (table == null)
            {
                Console.WriteLine("Failed to insert general table.");
                return null;
            }

            for (int r = 0; r < table.RowCount; r++)
                table.SetRowHeight(r, rowHeight, 0);
            for (int c = 0; c < table.ColumnCount; c++)
                table.SetColumnWidth(c, colWidth, 0);

            return table;
        }

        public static bool SetTableCell(TableAnnotation table, int row, int column, string value)
        {
            if (table == null || row < 0 || column < 0) return false;
            if (row >= table.RowCount || column >= table.ColumnCount) return false;

            table.Text[row, column] = value ?? "";
            return true;
        }

        // -------------------------------------------
        // Dimensions & Annotations
        // -------------------------------------------

        public static object InsertDimension(ModelDoc2 modelDoc, double x, double y)
        {
            return modelDoc.AddDimension2(x, y, 0);
        }

        public static AutoBalloonOptions InsertAutoBalloonsForView(DrawingDoc drawingDoc, SldWorks.View view)
        {
            if (view == null) return null;

            drawingDoc.ActivateView(view.Name);

            AutoBalloonOptions autoballoonParams = drawingDoc.CreateAutoBalloonOptions();
            autoballoonParams.Style = (int)swBalloonStyle_e.swBS_Circular;
            autoballoonParams.Size = (int)swBalloonFit_e.swBF_Tightest;
            autoballoonParams.Layout = (int)swBalloonLayoutType_e.swDetailingBalloonLayout_Top;

            drawingDoc.AutoBalloon5(autoballoonParams);

            return autoballoonParams;
        }

        // -------------------------------------------
        // Save Operations
        // -------------------------------------------

        public static bool SaveDrawingFile(ModelDoc2 modelDoc, string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return false;

            int errors = 0, warnings = 0;
            bool result = modelDoc.Extension.SaveAs(
                filePath,
                (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                (int)swSaveAsOptions_e.swSaveAsOptions_Silent,
                null, ref errors, ref warnings);

            if (!result)
            {
                Console.WriteLine($"Failed to save drawing. Errors: {errors}, Warnings: {warnings}");
            }

            return result;
        }

        public static bool ExportToPdf(ModelDoc2 modelDoc, string pdfPath)
        {
            if (string.IsNullOrEmpty(pdfPath)) return false;

            int errors = 0, warnings = 0;
            return modelDoc.Extension.SaveAs(
                pdfPath,
                (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                (int)swSaveAsOptions_e.swSaveAsOptions_Silent,
                null, ref errors, ref warnings);
        }
    }
}
