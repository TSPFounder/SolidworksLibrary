
using System;
using System.Collections.Generic;
using SE_Library;

namespace CAD
{
    /// <summary>
    /// Base class for elements that can appear on a CAD drawing
    /// (views, dimensions, notes, construction geometry, etc.).
    /// </summary>
    public class CAD_DrawingElement
    {
        // -----------------------------
        // Types
        // -----------------------------
        public enum DrawingElementType
        {
            DrawingView = 0,
            Dimension,
            Table,
            BoM,
            PMI,
            ConstructionGeometry,
            Note,
            Other
        }

        // -----------------------------
        // Constructor
        // -----------------------------
        public CAD_DrawingElement()
        {
            MyConstructionGeometry = new List<CAD_ConstructionGeometry>();
        }

        // -----------------------------
        // Identification
        // -----------------------------
        /// <summary>Display name of the drawing element.</summary>
        public string Name { get; set; }

        // -----------------------------
        // Data
        // -----------------------------
        /// <summary>Element classification.</summary>
        public DrawingElementType MyType { get; set; }

        // -----------------------------
        // Owned & Owning Objects
        // -----------------------------
        /// <summary>Owning drawing (if any).</summary>
        public CAD_Drawing MyDrawing { get; set; }

        // -----------------------------
        // Construction Geometry
        // -----------------------------
        /// <summary>The currently active construction geometry (if any).</summary>
        public CAD_ConstructionGeometry CurrentConstructionGeometry { get; set; }

        /// <summary>All construction geometry associated with this element.</summary>
        public List<CAD_ConstructionGeometry> MyConstructionGeometry { get; set; }

        // -----------------------------
        // Helpers (optional)
        // -----------------------------
        /// <summary>Adds a construction geometry item.</summary>
        public void AddConstructionGeometry(CAD_ConstructionGeometry geom)
        {
            if (geom is null) throw new ArgumentNullException(nameof(geom));
            MyConstructionGeometry.Add(geom);
            CurrentConstructionGeometry = geom;
        }

        /// <summary>Clears all construction geometry and current selection.</summary>
        public void ClearConstructionGeometry()
        {
            MyConstructionGeometry.Clear();
            CurrentConstructionGeometry = null;
        }

        public override string ToString()
            => $"DrawingElement(Name={Name ?? "<null>"}, Type={MyType}, CG Count={MyConstructionGeometry.Count})";
    }
}
