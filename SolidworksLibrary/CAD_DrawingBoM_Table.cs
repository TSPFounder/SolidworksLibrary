using Mathematics;
using Newtonsoft.Json;
using SE_Library;
using System;
using System.Collections.Generic;


namespace CAD
{
    /// <summary>
    /// Drawing Bill-of-Materials (BoM) table element.
    /// Wraps a generic drawing table with BoM-specific columns and rows.
    /// </summary>
    public class CAD_DrawingBoM_Table : CAD_DrawingElement
    {
        // -----------------------------
        // Construction
        // -----------------------------
        public CAD_DrawingBoM_Table(CAD_Drawing drawing, string name)
        {
            //MyType = DrawingElementType.Table;
            MyDrawing = drawing ?? throw new ArgumentNullException(nameof(drawing));
            MyTable = new SE_Table(name ?? throw new ArgumentNullException(nameof(name)));
            _configurations = new List<CAD_Configuration>();
            _bomRows = new List<SE_TableRow>();
        }

        // -----------------------------
        // Ownership
        // -----------------------------
        /// <summary>The drawing that owns this BoM table.</summary>
        public CAD_Drawing MyDrawing { get; set; }

        // -----------------------------
        // Configurations
        // -----------------------------
        private readonly List<CAD_Configuration> _configurations;
        public CAD_Configuration CurrentConfiguration { get; set; }
        public IReadOnlyList<CAD_Configuration> MyConfigurations => _configurations;

        // -----------------------------
        // Underlying data table
        // -----------------------------
        /// <summary>Backed data table for the BoM.</summary>
        public SE_Table MyTable { get; set; }

        // -----------------------------
        // Columns (optional references to typed columns)
        // -----------------------------
        public SE_TableColumn ItemNumberColumn { get; set; }
        public SE_TableColumn PartNumberColumn { get; set; }
        public SE_TableColumn DrawingNumberColumn { get; set; }
        public SE_TableColumn RevisionColumn { get; set; }
        public SE_TableColumn QuantityColumn { get; set; }
        public SE_TableColumn DescriptionColumn { get; set; }
        public SE_TableColumn MaterialColumn { get; set; }
        public SE_TableColumn SpecificationColumn { get; set; }

        // -----------------------------
        // Rows
        // -----------------------------
        public SE_TableRow HeaderRow { get; set; }
        public SE_TableRow PartRow { get; set; } // convenience handle for last/active row

        private readonly List<SE_TableRow> _bomRows;
        public IReadOnlyList<SE_TableRow> MyBoMRows => _bomRows;

        // -----------------------------
        // Metadata
        // -----------------------------
        public string ChangeOrderID { get; set; }

        // -----------------------------
        // Placement on sheet (optional)
        // -----------------------------
        public Point MyLocation { get; set; }

        // -----------------------------
        // Helpers
        // -----------------------------
        /// <summary>Add a configuration reference to this BoM (no duplicates).</summary>
        public bool AddConfiguration(CAD_Configuration cfg)
        {
            if (cfg is null) throw new ArgumentNullException(nameof(cfg));
            if (_configurations.Contains(cfg)) return false;
            _configurations.Add(cfg);
            return true;
        }

        /// <summary>Create and append a new row to the BoM and return it.</summary>
        public SE_TableRow AddRow()
        {
            var row = new SE_TableRow(MyTable);
            _bomRows.Add(row);
            PartRow = row;
            return row;
        }

        /// <summary>Remove a row from the BoM.</summary>
        public bool RemoveRow(SE_TableRow row)
        {
            return row != null && _bomRows.Remove(row);
        }

        /// <summary>Clear all rows from the BoM (columns remain unchanged).</summary>
        public void ClearRows() => _bomRows.Clear();

        /// <summary>
        /// Ensure a column reference exists by name; if not present in the table, create it.
        /// Returns the column reference.
        /// </summary>
        public SE_TableColumn EnsureColumn(ref SE_TableColumn slot, string name, Type systemType)
        {
            /*
            if (slot is not null) return slot;

            var existing = MyTable.FindColumn(name);
            if (existing is not null)
            {
                slot = existing;
                return existing;
            }
             */
            var col = new SE_TableColumn
            {
                ColumnName = name,
               // ColumnTypeEnum = systemType
            };
            MyTable.AddColumn(col);
           
            slot = col;
            return col;
        }

        // JSON Serialization
        public new string ToJson() => JsonConvert.SerializeObject(this, Formatting.Indented,
            new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });
        public static new CAD_DrawingBoM_Table FromJson(string json) => JsonConvert.DeserializeObject<CAD_DrawingBoM_Table>(json);
    }
}

