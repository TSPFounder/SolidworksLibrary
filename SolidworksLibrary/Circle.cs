using System;
using System.Collections.Generic;



namespace Mathematics
{
    /// <summary>
    /// Represents a 2D circle primitive defined by a center point and a radius.
    /// </summary>
    public sealed class Circle : Primitive
    {
        // ------------------------------------------------------------
        // Constructors
        // ------------------------------------------------------------
        public Circle()
        {
            Is2D = true;
            TwoDType = TwoDPrimitiveTypeEnum.Circle;
            CenterPoint = new Point();
        }

        public Circle(Point center, double radius)
            : this()
        {
            CenterPoint = center;
            Radius = radius;
        }

        // ------------------------------------------------------------
        // Identification
        // ------------------------------------------------------------
        public string CircleID { get; set; }

        // ------------------------------------------------------------
        // Dimensions
        // ------------------------------------------------------------
        /// <summary>Radius of the circle (must be ≥ 0).</summary>
        public double Radius { get; set; }

        /// <summary>2D center point of the circle.</summary>
        public Point CenterPoint { get; set; }

        // ------------------------------------------------------------
        // Helpers / Geometry
        // ------------------------------------------------------------

        /// <summary>Returns true if the geometry is valid.</summary>
        public bool IsValid(out string reason)
        {
            if (Radius < 0)
            {
                reason = "Radius cannot be negative.";
                return false;
            }

            if (CenterPoint == null)
            {
                reason = "CenterPoint must be defined.";
                return false;
            }

            reason = null;
            return true;
        }

        /// <summary>Computes area: πr².</summary>
        public double GetArea()
        {
            return Radius < 0 ? double.NaN : System.Math.PI * Radius * Radius;
        }

        /// <summary>Computes circumference: 2πr.</summary>
        public double GetCircumference()
        {
            return Radius < 0 ? double.NaN : 2.0 * System.Math.PI * Radius;
        }

        public override string ToString()
        {
            var id = CircleID ?? "—";
            return $"{nameof(Circle)}(R={Radius:0.###}, Center={CenterPoint}, ID={id})";
        }
    }
}
