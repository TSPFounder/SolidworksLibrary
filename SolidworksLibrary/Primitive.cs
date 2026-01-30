using System;
using System.Collections.Generic;


using System.Drawing;

namespace Mathematics
{
    public class Primitive
    {
        // -----------------------------
        // Enums (unchanged)
        // -----------------------------
        public enum TwoDPrimitiveTypeEnum
        {
            Square = 0,
            Circle,
            Triangle,
            Rectangle,
            Pentagon,
            Hexagon,
            Septagon,
            Octagon,
            Other
        }

        public enum ThreeDPrimitiveTypeEnum
        {
            Sphere = 0,
            Cube,
            Cone,
            Cylinder,
            Prism,
            Tetrahedron,
            Torus,
            Other
        }

        // -----------------------------
        // Constructor
        // -----------------------------
        public Primitive()
        {
            MyPoints = new List<Point>();
            Vertices = new List<Point>();
            MySegments = new List<Segment>();
        }

        // -----------------------------
        // Identification
        // -----------------------------
        public string Name { get; set; }
        public string Version { get; set; }

        // -----------------------------
        // Primitive properties
        // -----------------------------
        /// <summary>True if this primitive is treated as 2D; false if 3D.</summary>
        public bool Is2D { get; set; }

        /// <summary>2D classification (used when <see cref="Is2D"/> is true).</summary>
        public TwoDPrimitiveTypeEnum TwoDType { get; set; } = TwoDPrimitiveTypeEnum.Other;

        /// <summary>3D classification (used when <see cref="Is2D"/> is false).</summary>
        public ThreeDPrimitiveTypeEnum ThreeDType { get; set; } = ThreeDPrimitiveTypeEnum.Other;

        // -----------------------------
        // Geometry – points
        // -----------------------------
        public Point CurrentPoint { get; set; }
        public Point NextPoint { get; set; }
        public Point PreviousPoint { get; set; }
        public Point CenterPoint { get; set; }

        public List<Point> MyPoints { get; set; }
        public List<Point> Vertices { get; set; }

        // -----------------------------
        // Geometry – segments
        // -----------------------------
        public Segment CurrentSegment { get; set; }
        public Segment NextSegment { get; set; }
        public Segment PreviousSegment { get; set; }
        public List<Segment> MySegments { get; set; }

        // -----------------------------
        // Helpers (optional)
        // -----------------------------
        public void AddPoint(Point pt)
        {
            if (pt is null) throw new ArgumentNullException(nameof(pt));
            MyPoints.Add(pt);
            PreviousPoint = CurrentPoint;
            CurrentPoint = pt;
            if (Vertices.Count == 0) Vertices.Add(pt); // simple seed; caller can manage vertices explicitly
        }

        public void AddVertex(Point pt)
        {
            if (pt is null) throw new ArgumentNullException(nameof(pt));
            Vertices.Add(pt);
        }

        public void AddSegment(Segment seg)
        {
            if (seg is null) throw new ArgumentNullException(nameof(seg));
            MySegments.Add(seg);
            PreviousSegment = CurrentSegment;
            CurrentSegment = seg;
        }

        public override string ToString()
            => $"Primitive(Name={Name ?? "<null>"}, Is2D={Is2D}, 2D={TwoDType}, 3D={ThreeDType}, " +
               $"Pts={MyPoints.Count}, Verts={Vertices.Count}, Segs={MySegments.Count})";
    }
}

