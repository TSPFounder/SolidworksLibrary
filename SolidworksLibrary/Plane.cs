using System;

namespace Mathematics
{
    /// <summary>
    /// Geometric plane defined by an anchor point and a unit normal.
    /// Plane equation: A*x + B*y + C*z + D = 0, where (A,B,C) = Normal (Cartesian).
    /// </summary>
    public sealed class Plane
    {
        /// <summary>A known point on the plane (anchor).</summary>
        public Point AnchorPoint { get; private set; }

        /// <summary>Unit normal vector (Cartesian).</summary>
        public Vector Normal { get; private set; }

        /// <summary>Plane equation constant: A*x + B*y + C*z + D = 0.</summary>
        public double D { get; private set; }

        /// <summary>Plane equation A coefficient (Normal.X_Value).</summary>
        public double A => Normal.X_Value;

        /// <summary>Plane equation B coefficient (Normal.Y_Value).</summary>
        public double B => Normal.Y_Value;

        /// <summary>Plane equation C coefficient (Normal.Z_Value).</summary>
        public double C => Normal.Z_Value;

        public Plane() { }

        private Plane(Point anchorPoint, Vector unitNormal)
        {
            AnchorPoint = anchorPoint ?? throw new ArgumentNullException(nameof(anchorPoint));
            Normal = unitNormal ?? throw new ArgumentNullException(nameof(unitNormal));

            if (Normal.VectorType != Vector.VectorTypeEnum.Cartesian)
                throw new ArgumentException("Plane normal must be a Cartesian vector.", nameof(unitNormal));

            // D = -n · p0
            D = -Vector.Dot(Normal, ToPositionVector(anchorPoint));
        }

        // -----------------------------
        // Factory / creation
        // -----------------------------

        /// <summary>
        /// Create a plane from three non-collinear points.
        /// </summary>
        public static Plane FromPoints(Point p1, Point p2, Point p3)
        {
            if (p1 is null) throw new ArgumentNullException(nameof(p1));
            if (p2 is null) throw new ArgumentNullException(nameof(p2));
            if (p3 is null) throw new ArgumentNullException(nameof(p3));

            // Two direction vectors on the plane (Cartesian)
            var v1 = new Vector(p1, p2); // p2 - p1
            var v2 = new Vector(p1, p3); // p3 - p1

            var n = Vector.Cross(v1, v2); // perpendicular to plane
            if (n.Length <= 0)
                throw new ArgumentException("Points are collinear or too close to define a plane.");

            var unitNormal = n.Normalize(); // your Vector.Normalize() returns a new Cartesian vector

            return new Plane(p1, unitNormal);
        }

        /// <summary>
        /// Create a plane from two non-parallel lines. Each line is defined by two points.
        /// </summary>
        public static Plane FromLines(Point line1P1, Point line1P2, Point line2P1, Point line2P2)
        {
            if (line1P1 is null) throw new ArgumentNullException(nameof(line1P1));
            if (line1P2 is null) throw new ArgumentNullException(nameof(line1P2));
            if (line2P1 is null) throw new ArgumentNullException(nameof(line2P1));
            if (line2P2 is null) throw new ArgumentNullException(nameof(line2P2));

            var dir1 = new Vector(line1P1, line1P2); // direction of line 1
            var dir2 = new Vector(line2P1, line2P2); // direction of line 2

            var n = Vector.Cross(dir1, dir2);
            if (n.Length <= 0)
                throw new ArgumentException("Lines are parallel or too close to define a plane.");

            var unitNormal = n.Normalize();

            // Anchor at one point from line 1 (any point on plane works)
            return new Plane(line1P1, unitNormal);
        }

        // -----------------------------
        // Core operations
        // -----------------------------

        /// <summary>
        /// Signed distance from a point to the plane. Positive in the direction of the normal.
        /// </summary>
        public double DistanceTo(Point p)
        {
            if (p is null) throw new ArgumentNullException(nameof(p));
            return Vector.Dot(Normal, ToPositionVector(p)) + D;
        }

        /// <summary>
        /// True if point lies on the plane within tolerance.
        /// </summary>
        public bool ContainsPoint(Point p, double tolerance = 1e-9)
        {
            if (p is null) throw new ArgumentNullException(nameof(p));
            return Math.Abs(DistanceTo(p)) <= tolerance;
        }

        /// <summary>
        /// Projects a point orthogonally onto the plane.
        /// NOTE: This assumes Point has settable X_Value, Y_Value, Z_Value_Cartesian.
        /// If your Point is immutable, swap this implementation to call your Point factory/constructor.
        /// </summary>
        public Point ProjectPoint(Point p)
        {
            if (p is null) throw new ArgumentNullException(nameof(p));

            var dist = DistanceTo(p);

            // p_proj = p - n * dist
            var pVec = ToPositionVector(p);
            var correction = Scale(Normal, dist);
            var projVec = Subtract(pVec, correction);

            return new Point
            {
                X_Value = projVec.X_Value,
                Y_Value = projVec.Y_Value,
                Z_Value_Cartesian = projVec.Z_Value
            };
        }

        /// <summary>
        /// Returns the normal vector (unit).
        /// Provided mostly for readability / "define normal" requirement.
        /// </summary>
        public Vector GetNormalVector() => Normal;

        public override string ToString()
            => $"Plane(Anchor=({AnchorPoint.X_Value:F3},{AnchorPoint.Y_Value:F3},{AnchorPoint.Z_Value_Cartesian:F3}), " +
               $"Normal=({A:F6},{B:F6},{C:F6}), D={D:F6})";

        // -----------------------------
        // Helpers (Vector math using your Vector API)
        // -----------------------------

        private static Vector ToPositionVector(Point p)
            => Vector.FromCartesian(p.X_Value, p.Y_Value, p.Z_Value_Cartesian);

        private static Vector Scale(Vector v, double s)
        {
            if (v is null) throw new ArgumentNullException(nameof(v));
            if (v.VectorType != Vector.VectorTypeEnum.Cartesian)
                throw new ArgumentException("Scale expects a Cartesian vector.", nameof(v));

            return Vector.FromCartesian(v.X_Value * s, v.Y_Value * s, v.Z_Value * s);
        }

        private static Vector Subtract(Vector a, Vector b)
        {
            if (a is null) throw new ArgumentNullException(nameof(a));
            if (b is null) throw new ArgumentNullException(nameof(b));
            if (a.VectorType != Vector.VectorTypeEnum.Cartesian || b.VectorType != Vector.VectorTypeEnum.Cartesian)
                throw new ArgumentException("Subtract expects Cartesian vectors.");

            return Vector.FromCartesian(a.X_Value - b.X_Value, a.Y_Value - b.Y_Value, a.Z_Value - b.Z_Value);
        }
    }
}
