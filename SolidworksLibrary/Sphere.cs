using System;
using System.Collections.Generic;



namespace Mathematics
{
    /// <summary>
    /// Represents a three-dimensional spherical primitive.
    /// </summary>
    public sealed class Sphere : Primitive
    {
        // -----------------------------
        // Constructors
        // -----------------------------
        public Sphere()
        {
            Is2D = false;
            ThreeDType = ThreeDPrimitiveTypeEnum.Sphere;
            CenterPoint = new Point();
        }

        /// <summary>
        /// Creates a sphere with center and radius.
        /// </summary>
        public Sphere(Point center, double radius) : this()
        {
            CenterPoint = center;
            Radius = radius;
        }

        // -----------------------------
        // Properties
        // -----------------------------

        /// <summary>
        /// Optional user-defined sphere identifier.
        /// </summary>
        public string SphereID { get; set; }

        /// <summary>
        /// Sphere center point in 3-D space.
        /// </summary>
        public Point CenterPoint { get; set; }

        /// <summary>
        /// Sphere radius; must be non-negative.
        /// </summary>
        public double Radius { get; set; }

        // -----------------------------
        // Helpers
        // -----------------------------

        /// <summary>
        /// Checks sphere geometry.
        /// </summary>
        public bool IsValid(out string reason)
        {
            if (Radius < 0)
            {
                reason = "Radius cannot be negative.";
                return false;
            }

            if (CenterPoint is null)
            {
                reason = "CenterPoint must be defined.";
                return false;
            }

            reason = null;
            return true;
        }

        public override string ToString() =>
            $"{nameof(Sphere)}(Center={CenterPoint}, Radius={Radius:0.###}, ID={SphereID ?? "—"})";
    }
}
