using Documents;
using Newtonsoft.Json;
using SE_Library;
using System.Collections.Generic;

namespace CAD
{
    /// <summary>
    /// Title block / metadata table placed on a drawing.
    /// </summary>
    public sealed class CAD_DrawingTable : CAD_DrawingElement
    {
        // -----------------------------
        // Columns (commonly seen in title blocks)
        // -----------------------------
        public SE_TableColumn DrawingNumber { get; set; }
        public SE_TableColumn DrawingTitle { get; set; }
        public SE_TableColumn DrawingStandard { get; set; }
        public SE_TableColumn DrawingSize { get; set; }
        public SE_TableColumn ReleaseDate { get; set; }
        public SE_TableColumn PartNumber { get; set; }
        public SE_TableColumn NextAssembly { get; set; }
        public SE_TableColumn Revision { get; set; }

        // -----------------------------
        // Ownership / associations
        // -----------------------------
        /// <summary>The backing data table rendered on the drawing.</summary>
        public SE_Table Table { get; set; }

        /// <summary>Active configuration selecting which values/rows appear.</summary>
        public CAD_Configuration CurrentConfiguration { get; set; }

        /// <summary>Available configurations for this table.</summary>
        public List<CAD_Configuration> Configurations { get; } = new List<CAD_Configuration>();

        // -----------------------------
        // Construction
        // -----------------------------
        public CAD_DrawingTable()
        {
            MyType = DrawingElementType.Table;
        }

        // -----------------------------
        // Helpers
        // -----------------------------
        /// <summary>
        /// Returns a concise display string using DrawingNumber → DrawingTitle when available.
        /// </summary>
        public override string ToString()
        {
            var num = DrawingNumber?.ColumnName ?? DrawingNumber?.Description ?? "N/A";
            var title = DrawingTitle?.ColumnName ?? DrawingTitle?.Description ?? "";
            return string.IsNullOrWhiteSpace(title) ? $"Table [{num}]" : $"Table [{num}] — {title}";
        }

        /// <summary>
        /// Convenience to set the core title-block fields at once.
        /// </summary>
        public void SetCore(
            SE_TableColumn drawingNumber = null,
            SE_TableColumn drawingTitle = null,
            SE_TableColumn revision = null)
        {
            if (drawingNumber != null) DrawingNumber = drawingNumber;
            if (drawingTitle != null) DrawingTitle = drawingTitle;
            if (revision != null) Revision = revision;
        }

        // JSON Serialization
        public new string ToJson() => JsonConvert.SerializeObject(this, Formatting.Indented,
            new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });
        public static new CAD_DrawingTable FromJson(string json) => JsonConvert.DeserializeObject<CAD_DrawingTable>(json);
    }
}
