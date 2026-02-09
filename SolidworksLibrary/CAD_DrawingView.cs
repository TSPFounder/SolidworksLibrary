
using Mathematics;
using Newtonsoft.Json;
using System;

namespace CAD
{
    /// <summary>
    /// A placed drawing view (orthographic, isometric, section, etc.) on a drawing sheet.
    /// </summary>
    public class CAD_DrawingView : CAD_DrawingElement
    {
        // -----------------------------
        // Types
        // -----------------------------
        public enum ViewType
        {
            OrthoTop,
            OrthoFront,
            OrthoRightSide,
            OrthoBottom,
            OrthoBack,
            OrthoLeftSide,
            Isometric,
            CrossSection,
            Detail,
            Other
        }

        // -----------------------------
        // Construction
        // -----------------------------
        public CAD_DrawingView()
        {
            MyType = DrawingElementType.DrawingView;
        }

        public CAD_DrawingView(
            string id,
            string title,
            ViewType viewType,
            Point centerPoint = null,
            Quadrilateral viewRectangle = null,
            string description = null) : this()
        {
            ID = id;
            Title = title;
            Type = viewType;
            CenterPoint = centerPoint;
            ViewRectangle = viewRectangle;
            Description = description;
        }

        // -----------------------------
        // Identification
        // -----------------------------
        public string ID { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }

        // -----------------------------
        // Data
        // -----------------------------
        /// <summary>The canonical type/kind of this view (orthographic, isometric, section, etc.).</summary>
        public ViewType Type { get; set; } = ViewType.Other;

        /// <summary>Optional center point of the view on the sheet (drawing coordinates).</summary>
        public Point CenterPoint { get; set; }

        /// <summary>Optional view bounding rectangle on the sheet (drawing coordinates).</summary>
        public Quadrilateral ViewRectangle { get; set; }

        // -----------------------------
        // Overrides
        // -----------------------------
        public override string ToString()
            => $"{Title ?? Type.ToString()} ({Type})#{ID}";

        // JSON Serialization
        public new string ToJson() => JsonConvert.SerializeObject(this, Formatting.Indented,
            new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });
        public static new CAD_DrawingView FromJson(string json) => JsonConvert.DeserializeObject<CAD_DrawingView>(json);
    }
}
