
using System;

namespace Mathematics
{
    /// <summary>
    /// Geometric vector with multiple coordinate representations and light ownership links.
    /// </summary>
    public class Vector
    {
        // -----------------------------
        // Types (unchanged)
        // -----------------------------
        public enum VectorTypeEnum
        {
            Cartesian = 0,
            Cylindrical,
            Spherical,
            Polar
        }

        // -----------------------------
        // Constructors / factories
        // -----------------------------
        public Vector()
        {
            WorldCoordinateSystem = CoordinateSystem.CreateWcs();
            CurrentCoordinateSystem = WorldCoordinateSystem;
        }

        public Vector(Point startCartesianPoint, Point endCartesianPoint) : this()
        {
            if (startCartesianPoint is null) throw new ArgumentNullException(nameof(startCartesianPoint));
            if (endCartesianPoint is null) throw new ArgumentNullException(nameof(endCartesianPoint));

            StartPoint = startCartesianPoint;
            EndPoint = endCartesianPoint;

            // Components (fixed: Z used X before)
            X_Value = endCartesianPoint.X_Value - startCartesianPoint.X_Value;
            Y_Value = endCartesianPoint.Y_Value - startCartesianPoint.Y_Value;
            Z_Value = endCartesianPoint.Z_Value_Cartesian - startCartesianPoint.Z_Value_Cartesian;

            VectorType = VectorTypeEnum.Cartesian;

            // Set a baseline “direction” into WCS for reference
            WorldCoordinateSystem.BaseVector = this;
        }

        /// <summary>Create a Cartesian vector from components.</summary>
        public static Vector FromCartesian(double x, double y, double z = 0) =>
            new Vector { VectorType = VectorTypeEnum.Cartesian, X_Value = x, Y_Value = y, Z_Value = z };

        /// <summary>Create a Cylindrical/Polar vector (r,θ,z).</summary>
        public static Vector FromCylindrical(double r, double thetaRadians, double z = 0) =>
            new Vector { VectorType = VectorTypeEnum.Cylindrical, Cyl_R = r, Cyl_Theta = thetaRadians, L = z };

        /// <summary>Create a Spherical vector (R,θ,φ). θ=azimuth, φ=polar angle from +Z.</summary>
        public static Vector FromSpherical(double r, double thetaRadians, double phiRadians) =>
            new Vector { VectorType = VectorTypeEnum.Spherical, Sph_R = r, Sph_Theta = thetaRadians, Phi = phiRadians };

        // -----------------------------
        // Identification
        // -----------------------------
        public string Name { get; set; }
        public string VectorID { get; set; }
        public bool IsKnotVector { get; set; }
        public VectorTypeEnum VectorType { get; set; } = VectorTypeEnum.Cartesian;

        // -----------------------------
        // Magnitude (computed)
        // -----------------------------
        public double Length => GetVectorLength();

        // -----------------------------
        // Cartesian components
        // -----------------------------
        public double X_Value { get; set; }
        public double Y_Value { get; set; }
        public double Z_Value { get; set; }

        // -----------------------------
        // Cylindrical / Polar (r,θ,z or r,θ,L)
        // -----------------------------
        public double Cyl_R { get; set; }
        /// <summary>Azimuth angle θ (radians).</summary>
        public double Cyl_Theta { get; set; }
        /// <summary>Axial component (z or L depending on usage).</summary>
        public double L { get; set; }

        // -----------------------------
        // Spherical (R,θ,φ)
        // θ: azimuth about +Z from +X; φ: polar angle from +Z
        // -----------------------------
        public double Sph_R { get; set; }
        public double Sph_Theta { get; set; }
        public double Phi { get; set; }

        // -----------------------------
        // Ownership / context
        // -----------------------------
        public Point StartPoint { get; set; }
        public Point EndPoint { get; set; }

        public CoordinateSystem WorldCoordinateSystem { get; set; } = CoordinateSystem.CreateWcs();
        public CoordinateSystem CurrentCoordinateSystem { get; set; }

        // -----------------------------
        // Methods
        // -----------------------------
        /// <summary>Compute the vector length based on its coordinate representation.</summary>
        public double GetVectorLength()
        {
            switch (VectorType)
            {
                case VectorTypeEnum.Cartesian:
                    return Math.Sqrt(X_Value * X_Value + Y_Value * Y_Value + Z_Value * Z_Value);
                case VectorTypeEnum.Cylindrical:
                    return Math.Sqrt(Cyl_R * Cyl_R + L * L);
                case VectorTypeEnum.Spherical:
                    return Math.Abs(Sph_R);
                case VectorTypeEnum.Polar:
                    return Math.Abs(L);
                default:
                    return Math.Sqrt(X_Value * X_Value + Y_Value * Y_Value + Z_Value * Z_Value);
            }
        }

        /// <summary>
        /// Returns a unit (normalized) Cartesian vector.
        /// </summary>
        /// <exception cref="ArgumentNullException"/>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the vector is not Cartesian.
        /// </exception>
        public Vector Normalize()
        {
            if (VectorType != VectorTypeEnum.Cartesian)
                throw new InvalidOperationException("Normalize requires a Cartesian vector.");

            var len = Length;
            if (len <= 0)
                throw new InvalidOperationException("Cannot normalize a zero-length vector.");

            return FromCartesian(
                X_Value / len,
                Y_Value / len,
                Z_Value / len
            );
        }

        /// <summary>
        /// Dot product of two Cartesian vectors.
        /// </summary>
        /// <exception cref="ArgumentNullException"/>
        /// <exception cref="ArgumentException">
        /// Thrown if either vector is not Cartesian.
        /// </exception>
        public static double Dot(Vector a, Vector b)
        {
            if (a is null) throw new ArgumentNullException(nameof(a));
            if (b is null) throw new ArgumentNullException(nameof(b));

            if (a.VectorType != VectorTypeEnum.Cartesian)
                throw new ArgumentException("Dot product requires Cartesian vectors.", nameof(a));

            if (b.VectorType != VectorTypeEnum.Cartesian)
                throw new ArgumentException("Dot product requires Cartesian vectors.", nameof(b));

            return a.X_Value * b.X_Value
                 + a.Y_Value * b.Y_Value
                 + a.Z_Value * b.Z_Value;
        }

        /// <summary>Cross product (expects both vectors in Cartesian form).</summary>
        public static Vector Cross(Vector a, Vector b)
        {
            if (a is null) throw new ArgumentNullException(nameof(a));
            if (b is null) throw new ArgumentNullException(nameof(b));

            if (a.VectorType != VectorTypeEnum.Cartesian)
                throw new ArgumentException("Cross product requires Cartesian vectors.", nameof(a));

            if (b.VectorType != VectorTypeEnum.Cartesian)
                throw new ArgumentException("Cross product requires Cartesian vectors.", nameof(b));

            return FromCartesian(
                a.Y_Value * b.Z_Value - a.Z_Value * b.Y_Value,
                a.Z_Value * b.X_Value - a.X_Value * b.Z_Value,
                a.X_Value * b.Y_Value - a.Y_Value * b.X_Value
            );
        }

        public override string ToString()
            => $"Vector(Name={Name ?? "<unnamed>"}, Type={VectorType}, Len={Length:F6}, " +
               $"Cart=({X_Value:F3},{Y_Value:F3},{Z_Value:F3}))";
    }
}