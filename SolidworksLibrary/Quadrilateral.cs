using System;
using System.Collections.Generic;
using Mathematics;

namespace Mathematics
{
    /// <summary>
    /// A simple, ordered four-vertex polygon with helpers to classify common quadrilateral types.
    /// Assumes vertices are provided in winding order (CW or CCW) and lie in a plane.
    /// </summary>
    public class Quadrilateral : Primitive
    {
        // -----------------------------
        // Construction
        // -----------------------------
        public Quadrilateral()
        {
            // Ensure 2D primitive semantics by default.
            Is2D = true;
            TwoDType = TwoDPrimitiveTypeEnum.Rectangle; // sensible default; may be reclassified
            _vertices.Add(new Point());
            _vertices.Add(new Point());
            _vertices.Add(new Point());
            _vertices.Add(new Point());
            RebuildSegments();
        }

        public Quadrilateral(Point v1, Point v2, Point v3, Point v4) : this()
        {
            Vertex1 = v1 ?? throw new ArgumentNullException(nameof(v1));
            Vertex2 = v2 ?? throw new ArgumentNullException(nameof(v2));
            Vertex3 = v3 ?? throw new ArgumentNullException(nameof(v3));
            Vertex4 = v4 ?? throw new ArgumentNullException(nameof(v4));
            RebuildSegments();
        }

        // -----------------------------
        // Identification
        // -----------------------------
        public string QuadrilateralID { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }

        // -----------------------------
        // Vertex/edge storage
        // -----------------------------
        private readonly List<Point> _vertices = new List<Point>(4);
        private readonly List<Segment> _edges = new List<Segment>(4);

        /// <summary>Ordered vertices V1..V4 (read-only view).</summary>
        public IReadOnlyList<Point> VerticesRO => _vertices;

        /// <summary>Ordered edges E1..E4 (read-only view): (V1–V2), (V2–V3), (V3–V4), (V4–V1).</summary>
        public IReadOnlyList<Segment> EdgesRO => _edges;

        public Point Vertex1
        {
            get => _vertices[0];
            set { _vertices[0] = value ?? throw new ArgumentNullException(nameof(value)); RebuildSegments(); }
        }

        public Point Vertex2
        {
            get => _vertices[1];
            set { _vertices[1] = value ?? throw new ArgumentNullException(nameof(value)); RebuildSegments(); }
        }

        public Point Vertex3
        {
            get => _vertices[2];
            set { _vertices[2] = value ?? throw new ArgumentNullException(nameof(value)); RebuildSegments(); }
        }

        public Point Vertex4
        {
            get => _vertices[3];
            set { _vertices[3] = value ?? throw new ArgumentNullException(nameof(value)); RebuildSegments(); }
        }

        /// <summary>Convenience accessors mapped to traditional field names.</summary>
        public Segment Segment1 => _edges[0];
        public Segment Segment2 => _edges[1];
        public Segment Segment3 => _edges[2];
        public Segment Segment4 => _edges[3];

        /// <summary>Centroid as the average of four vertices (not area-weighted if self-intersecting).</summary>
        public Point MidPoint
        {
            get
            {
                var p = new Point
                {
                    X_Value = (Vertex1.X_Value + Vertex2.X_Value + Vertex3.X_Value + Vertex4.X_Value) / 4.0,
                    Y_Value = (Vertex1.Y_Value + Vertex2.Y_Value + Vertex3.Y_Value + Vertex4.Y_Value) / 4.0,
                    Z_Value_Cartesian = (Vertex1.Z_Value_Cartesian + Vertex2.Z_Value_Cartesian +
                                         Vertex3.Z_Value_Cartesian + Vertex4.Z_Value_Cartesian) / 4.0
                };
                return p;
            }
        }

        // -----------------------------
        // Classification flags (computed)
        // -----------------------------
        public bool IsDart(double tol = 1e-9) => IsSelfIntersecting(tol);

        public bool IsParallelogram(double tol = 1e-9)
            => Parallel(Segment1, Segment3, tol) && Parallel(Segment2, Segment4, tol);

        public bool IsRectangle(double tol = 1e-9)
            => IsParallelogram(tol) && RightAngle(Vertex1, Vertex2, Vertex3, tol);

        public bool IsSquare(double tol = 1e-9)
            => IsRectangle(tol) && NearlyEqual(Length2(Segment1), Length2(Segment2), tol);

        public bool IsRhombus(double tol = 1e-9)
            => NearlyEqual(Length2(Segment1), Length2(Segment2), tol) &&
               NearlyEqual(Length2(Segment2), Length2(Segment3), tol) &&
               NearlyEqual(Length2(Segment3), Length2(Segment4), tol);

        public bool IsKite(double tol = 1e-9)
            => NearlyEqual(Length2(Segment1), Length2(Segment2), tol) &&
               NearlyEqual(Length2(Segment3), Length2(Segment4), tol);

        public bool IsTrapezoid(double tol = 1e-9)
            => Parallel(Segment1, Segment3, tol) ^ Parallel(Segment2, Segment4, tol);

        public bool IsIsoscelesTrapezoid(double tol = 1e-9)
            => IsTrapezoid(tol) && NearlyEqual(Length2(Segment1), Length2(Segment3), tol);

        // -----------------------------
        // Geometry helpers
        // -----------------------------
        /// <summary>Signed area via shoelace (projected onto XY plane).</summary>
        public double SignedAreaXY()
        {
            double sum = 0;
            for (int i = 0; i < 4; i++)
            {
                var a = _vertices[i];
                var b = _vertices[(i + 1) & 3];
                sum += a.X_Value * b.Y_Value - b.X_Value * a.Y_Value;
            }
            return 0.5 * sum;
        }

        /// <summary>Absolute area in the XY plane.</summary>
        public double AreaXY() => Math.Abs(SignedAreaXY());

        /// <summary>Perimeter length in 3D.</summary>
        public double Perimeter()
            => Math.Sqrt(Length2(Segment1)) + Math.Sqrt(Length2(Segment2)) +
               Math.Sqrt(Length2(Segment3)) + Math.Sqrt(Length2(Segment4));

        // -----------------------------
        // Internal wiring
        // -----------------------------
        private void RebuildSegments()
        {
            _edges.Clear();
            for (int i = 0; i < 4; i++)
            {
                var s = new Segment
                {
                    StartPoint = _vertices[i],
                    EndPoint = _vertices[(i + 1) & 3]
                };
                _edges.Add(s);
            }

            // Keep Primitive's collections roughly in sync for legacy callers.
            // Note: Primitive exposes MyPoints/MySegments; we populate them here.
            MyPoints = new List<Point>(_vertices);
            MySegments = new List<Segment>(_edges);
        }

        // -----------------------------
        // Math utilities
        // -----------------------------
        private static double Length2(Segment s)
        {
            var dx = s.EndPoint.X_Value - s.StartPoint.X_Value;
            var dy = s.EndPoint.Y_Value - s.StartPoint.Y_Value;
            var dz = s.EndPoint.Z_Value_Cartesian - s.StartPoint.Z_Value_Cartesian;
            return dx * dx + dy * dy + dz * dz;
        }

        private static bool Parallel(Segment a, Segment b, double tol)
        {
            // Compare normalized cross product magnitude in XY (planar assumption).
            var ax = a.EndPoint.X_Value - a.StartPoint.X_Value;
            var ay = a.EndPoint.Y_Value - a.StartPoint.Y_Value;
            var bx = b.EndPoint.X_Value - b.StartPoint.X_Value;
            var by = b.EndPoint.Y_Value - b.StartPoint.Y_Value;

            var cross = ax * by - ay * bx;
            var denom = Math.Sqrt((ax * ax + ay * ay) * (bx * bx + by * by));
            if (denom <= tol) return false; // degenerate
            return Math.Abs(cross) / denom <= tol;
        }

        private static bool RightAngle(Point a, Point b, Point c, double tol)
        {
            // Angle at B between BA and BC
            var bax = a.X_Value - b.X_Value;
            var bay = a.Y_Value - b.Y_Value;
            var bcx = c.X_Value - b.X_Value;
            var bcy = c.Y_Value - b.Y_Value;
            var dot = bax * bcx + bay * bcy;
            var denom = Math.Sqrt((bax * bax + bay * bay) * (bcx * bcx + bcy * bcy));
            if (denom <= tol) return false;
            return Math.Abs(dot) / denom <= tol;
        }

        private static bool NearlyEqual(double a2, double b2, double tol)
            => Math.Abs(a2 - b2) <= tol * Math.Max(1.0, Math.Max(Math.Abs(a2), Math.Abs(b2)));

        private static bool SegmentsIntersectXY(Segment a, Segment b, double tol)
        {
            // Exclude shared endpoints by tolerance
            bool ShareEndpoint(Point p, Point q)
                => Math.Abs(p.X_Value - q.X_Value) <= tol && Math.Abs(p.Y_Value - q.Y_Value) <= tol;

            if (ShareEndpoint(a.StartPoint, b.StartPoint) ||
                ShareEndpoint(a.StartPoint, b.EndPoint) ||
                ShareEndpoint(a.EndPoint, b.StartPoint) ||
                ShareEndpoint(a.EndPoint, b.EndPoint))
                return false;

            double Orient(Point p, Point q, Point r)
                => (q.X_Value - p.X_Value) * (r.Y_Value - p.Y_Value) -
                   (q.Y_Value - p.Y_Value) * (r.X_Value - p.X_Value);

            var o1 = Orient(a.StartPoint, a.EndPoint, b.StartPoint);
            var o2 = Orient(a.StartPoint, a.EndPoint, b.EndPoint);
            var o3 = Orient(b.StartPoint, b.EndPoint, a.StartPoint);
            var o4 = Orient(b.StartPoint, b.EndPoint, a.EndPoint);

            return (o1 * o2 < -tol) && (o3 * o4 < -tol);
        }

        private bool IsSelfIntersecting(double tol)
            => SegmentsIntersectXY(Segment1, Segment3, tol) || SegmentsIntersectXY(Segment2, Segment4, tol);
    }
}

