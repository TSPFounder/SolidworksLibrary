using System;
using System.Collections.Generic;



namespace Mathematics
{
    /// <summary>
    /// Geometric triangle primitive with typed vertices and edges.
    /// Provides light validation plus convenience geometry (perimeter/area/centroid).
    /// </summary>
    public class Triangle : Primitive
    {
        // -----------------------------
        // Enums
        // -----------------------------
        public enum TriangleTypeEnum
        {
            Isosceles = 0,
            Right,
            Irregular
        }

        // -----------------------------
        // Backing fields
        // -----------------------------
        private string _triangleId;
        private string _name;
        private bool _isRightTriangle;
        private bool _isIsoscelesTriangle;
        private TriangleTypeEnum _triangleType;

        // Vertices
        private Point _v1 = new Point();
        private Point _v2 = new Point();
        private Point _v3 = new Point();

        // Edges
        private Segment _e1 = new Segment();
        private Segment _e2 = new Segment();
        private Segment _e3 = new Segment();

        // -----------------------------
        // Construction
        // -----------------------------
        public Triangle()
        {
            WireSegments();
        }

        public Triangle(Point v1, Point v2, Point v3, string name = null, string id = null)
        {
            _v1 = v1 ?? throw new ArgumentNullException(nameof(v1));
            _v2 = v2 ?? throw new ArgumentNullException(nameof(v2));
            _v3 = v3 ?? throw new ArgumentNullException(nameof(v3));
            _name = name;
            _triangleId = id;

            WireSegments();
            AutoDetectFlags();
        }

        // -----------------------------
        // Properties
        // -----------------------------
        public string TriangleID
        {
            get => _triangleId;
            set => _triangleId = value;
        }

        public string Name
        {
            get => _name;
            set => _name = value;
        }

        /// <summary>True if the triangle satisfies the Pythagorean condition within a tight tolerance.</summary>
        public bool IsRightTriangle
        {
            get => _isRightTriangle;
            set => _isRightTriangle = value;
        }

        /// <summary>True if two sides are equal (within tolerance).</summary>
        public bool IsIsoscelesTriangle
        {
            get => _isIsoscelesTriangle;
            set => _isIsoscelesTriangle = value;
        }

        public TriangleTypeEnum TriangleType
        {
            get => _triangleType;
            set => _triangleType = value;
        }

        // Vertices
        public Point Vertex1
        {
            get => _v1;
            set
            {
                _v1 = value ?? throw new ArgumentNullException(nameof(value));
                WireSegments();
                AutoDetectFlags();
            }
        }

        public Point Vertex2
        {
            get => _v2;
            set
            {
                _v2 = value ?? throw new ArgumentNullException(nameof(value));
                WireSegments();
                AutoDetectFlags();
            }
        }

        public Point Vertex3
        {
            get => _v3;
            set
            {
                _v3 = value ?? throw new ArgumentNullException(nameof(value));
                WireSegments();
                AutoDetectFlags();
            }
        }

        // Edges (read-only from outside; they are kept consistent with vertices)
        public Segment Segment1 => _e1; // V1 -> V2
        public Segment Segment2 => _e2; // V2 -> V3
        public Segment Segment3 => _e3; // V3 -> V1

        // -----------------------------
        // Geometry helpers
        // -----------------------------
        /// <summary>Perimeter length of the triangle.</summary>
        public double Perimeter =>
            Segment1.Length + Segment2.Length + Segment3.Length;

        /// <summary>Area computed by Heron's formula.</summary>
        public double Area
        {
            get
            {
                var a = Segment1.Length;
                var b = Segment2.Length;
                var c = Segment3.Length;
                var s = 0.5 * (a + b + c);
                var area2 = s * (s - a) * (s - b) * (s - c);
                return area2 <= 0 ? 0 : Math.Sqrt(area2);
            }
        }

        /// <summary>Centroid of the triangle (average of vertices).</summary>
        public Point Centroid =>
            new Point
            {
                X_Value = (Vertex1.X_Value + Vertex2.X_Value + Vertex3.X_Value) / 3.0,
                Y_Value = (Vertex1.Y_Value + Vertex2.Y_Value + Vertex3.Y_Value) / 3.0,
                Z_Value_Cartesian = (Vertex1.Z_Value_Cartesian + Vertex2.Z_Value_Cartesian + Vertex3.Z_Value_Cartesian) / 3.0
            };

        // -----------------------------
        // Private utilities
        // -----------------------------
        private void WireSegments()
        {
            // Keep edges consistent with vertices
            _e1.StartPoint = _v1; _e1.EndPoint = _v2;
            _e2.StartPoint = _v2; _e2.EndPoint = _v3;
            _e3.StartPoint = _v3; _e3.EndPoint = _v1;
        }

        private void AutoDetectFlags()
        {
            // Classify with small tolerance
            const double tol = 1e-9;

            var a = Segment1.Length; // v1-v2
            var b = Segment2.Length; // v2-v3
            var c = Segment3.Length; // v3-v1

            // Isosceles check
            _isIsoscelesTriangle = NearlyEqual(a, b, tol) || NearlyEqual(b, c, tol) || NearlyEqual(c, a, tol);

            // Right-triangle check via Pythagoras on sorted sides
            var sides = new[] { a, b, c };
            Array.Sort(sides);
            _isRightTriangle = NearlyEqual(sides[0] * sides[0] + sides[1] * sides[1], sides[2] * sides[2], 1e-8);

            // Basic typing
            _triangleType = _isRightTriangle ? TriangleTypeEnum.Right
                          : _isIsoscelesTriangle ? TriangleTypeEnum.Isosceles
                          : TriangleTypeEnum.Irregular;
        }

        private static bool NearlyEqual(double x, double y, double tol)
            => Math.Abs(x - y) <= tol * Math.Max(1.0, Math.Max(Math.Abs(x), Math.Abs(y)));
    }
}
