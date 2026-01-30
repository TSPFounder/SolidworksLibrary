using System;
using System.Collections.Generic;



namespace Mathematics
{
    /// <summary>
    /// 3D prism primitive with three orthogonal edge segments and five faces
    /// (2 triangular ends + 3 quadrilateral side faces).
    /// </summary>
    public sealed class Prism : Primitive
    {
        // ------------------------------------------------------------------
        // Constructors
        // ------------------------------------------------------------------

        public Prism()
        {
            Is2D = false;
            ThreeDType = ThreeDPrimitiveTypeEnum.Prism;

            // Edges
            LengthSegment = new Segment();
            WidthSegment = new Segment();
            HeightSegment = new Segment();

            // Faces
            Triangle1 = new Triangle();
            Triangle2 = new Triangle();
            Quadrilateral1 = new Quadrilateral();
            Quadrilateral2 = new Quadrilateral();
            Quadrilateral3 = new Quadrilateral();

            // Location
            CenterPoint = new Point();
        }

        // ------------------------------------------------------------------
        // Identification
        // ------------------------------------------------------------------

        public string PrismID { get; set; }

        // ------------------------------------------------------------------
        // Dimensions (modeled as segments)
        // ------------------------------------------------------------------

        /// <summary>Primary length edge segment.</summary>
        public Segment LengthSegment { get; set; } = new Segment();

        /// <summary>Primary width edge segment.</summary>
        public Segment WidthSegment { get; set; } = new Segment();

        /// <summary>Primary height edge segment.</summary>
        public Segment HeightSegment { get; set; } = new Segment();

        // ------------------------------------------------------------------
        // Faces
        // ------------------------------------------------------------------

        public Triangle Triangle1 { get; set; } = new Triangle();
        public Triangle Triangle2 { get; set; } = new Triangle();
        public Quadrilateral Quadrilateral1 { get; set; } = new Quadrilateral();
        public Quadrilateral Quadrilateral2 { get; set; } = new Quadrilateral();
        public Quadrilateral Quadrilateral3 { get; set; } = new Quadrilateral();

        // ------------------------------------------------------------------
        // Location
        // ------------------------------------------------------------------

        /// <summary>Geometric center (reference) of the prism.</summary>
        public Point CenterPoint { get; set; } = new Point();

        // ------------------------------------------------------------------
        // Validation / Utilities
        // ------------------------------------------------------------------

        /// <summary>
        /// Basic structural validation (checks for non-null members).
        /// </summary>
        public bool IsValid(out string reason)
        {
            if (LengthSegment is null) { reason = "LengthSegment is null."; return false; }
            if (WidthSegment is null) { reason = "WidthSegment is null."; return false; }
            if (HeightSegment is null) { reason = "HeightSegment is null."; return false; }

            if (Triangle1 is null) { reason = "Triangle1 is null."; return false; }
            if (Triangle2 is null) { reason = "Triangle2 is null."; return false; }
            if (Quadrilateral1 is null) { reason = "Quadrilateral1 is null."; return false; }
            if (Quadrilateral2 is null) { reason = "Quadrilateral2 is null."; return false; }
            if (Quadrilateral3 is null) { reason = "Quadrilateral3 is null."; return false; }

            if (CenterPoint is null) { reason = "CenterPoint is null."; return false; }

            reason = null;
            return true;
        }

        /// <summary>
        /// Recreates face objects (useful if topology changes).
        /// </summary>
        public void ResetFaces()
        {
            Triangle1 = new Triangle();
            Triangle2 = new Triangle();
            Quadrilateral1 = new Quadrilateral();
            Quadrilateral2 = new Quadrilateral();
            Quadrilateral3 = new Quadrilateral();
        }

        public override string ToString()
        {
            var id = PrismID ?? "—";
            return $"Prism(ID={id})";
        }
    }
}

