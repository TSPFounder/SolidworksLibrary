
using System;
using System.Collections.Generic;



namespace Mathematics
{
    public class Point
    {
        // -----------------------------
        // Types (unchanged)
        // -----------------------------
        public enum PointTypeEnum
        {
            Cartesian = 0,
            Cylindrical,
            Spherical,
            Complex
        }

        // -----------------------------
        // Constructors / factories
        // -----------------------------
        public Point()
        {
            MyCoordinateSystems = new List<CoordinateSystem>();
            MyConnectedPoints = new List<Point>();
        }

        public static Point FromCartesian(double x, double y, double z = 0.0)
            => new Point
            {
                MyType = PointTypeEnum.Cartesian,
                X_Value = x,
                Y_Value = y,
                Z_Value_Cartesian = z,
                Is2D = z == 0.0
            };

        public static Point FromCylindrical(double r, double thetaRadians, double z = 0.0)
            => new Point
            {
                MyType = PointTypeEnum.Cylindrical,
                R_Value_Cylindrical = r,
                Theta_Value_Cylindrical = thetaRadians,
                Z_Value_Cylindrical = z,
                Is2D = z == 0.0
            };

        public static Point FromSpherical(double r, double thetaRadians, double phiRadians)
            => new Point
            {
                MyType = PointTypeEnum.Spherical,
                R_Value_Spherical = r,
                Theta_Value_Spherical = thetaRadians,
                Phi_Value = phiRadians,
                Is2D = false
            };

        // -----------------------------
        // Identification
        // -----------------------------
        public string PointID { get; set; }
        public bool IsWeightPoint { get; set; }

        // -----------------------------
        // Kind / flags
        // -----------------------------
        public PointTypeEnum MyType { get; set; } = PointTypeEnum.Cartesian;
        public bool Is2D { get; set; }

        // -----------------------------
        // Geometry – Cartesian
        // -----------------------------
        public double X_Value { get; set; }
        public double Y_Value { get; set; }
        public double Z_Value_Cartesian { get; set; }

        // -----------------------------
        // Geometry – Cylindrical (r, θ, z)
        // -----------------------------
        public double R_Value_Cylindrical { get; set; }
        public double Theta_Value_Cylindrical { get; set; }   // radians
        public double Z_Value_Cylindrical { get; set; }

        // -----------------------------
        // Geometry – Spherical (r, θ, φ)
        // θ: azimuth about +Z from +X; φ: polar angle from +Z (0..π)
        // -----------------------------
        public double R_Value_Spherical { get; set; }
        public double Theta_Value_Spherical { get; set; }   // radians
        public double Phi_Value { get; set; }   // radians

        // -----------------------------
        // GPS (optional)
        // -----------------------------
        public double Longitude { get; set; }
        public double Latitude { get; set; }
        public double Altitude { get; set; }

        // -----------------------------
        // Complex (optional)
        // -----------------------------
        public double Real_Value { get; set; }
        public double Complex_Value { get; set; }

        // -----------------------------
        // Coordinate systems / connectivity
        // -----------------------------
        public CoordinateSystem CurrentCoordinateSystem { get; set; }
        public List<CoordinateSystem> MyCoordinateSystems { get; set; }
        public Point CurrentConnectedPoint { get; set; }
        public List<Point> MyConnectedPoints { get; set; }

        // -----------------------------
        // Conversions (in-place)
        // -----------------------------

        /// <summary>Convert cylindrical (r,θ,z) to Cartesian, if MyType == Cylindrical.</summary>
        public bool CylindricalToCartesian(double r, double thetaRadians)
        {
            try
            {
                if (MyType != PointTypeEnum.Cylindrical) return false;

                X_Value = r * Math.Cos(thetaRadians);
                Y_Value = r * Math.Sin(thetaRadians);
                Z_Value_Cartesian = Z_Value_Cylindrical;
                return true;
            }
            catch { return false; }
        }

        /// <summary>Convert spherical (r,θ,φ) to Cartesian, if MyType == Spherical.
        /// θ = azimuth, φ = polar angle from +Z.</summary>
        public bool SphericalToCartesian(double r, double thetaRadians, double phiRadians)
        {
            try
            {
                if (MyType != PointTypeEnum.Spherical) return false;

                X_Value = r * Math.Cos(thetaRadians) * Math.Sin(phiRadians);
                Y_Value = r * Math.Sin(thetaRadians) * Math.Sin(phiRadians);
                Z_Value_Cartesian = r * Math.Cos(phiRadians); // fixed: cos(phi) for Z
                return true;
            }
            catch { return false; }
        }

        /// <summary>Convert Cartesian (x,y) to cylindrical (r,θ,z), if MyType == Cartesian.</summary>
        public bool CartesianToCylindrical(double x, double y)
        {
            try
            {
                if (MyType != PointTypeEnum.Cartesian) return false;

                R_Value_Cylindrical = Math.Sqrt(x * x + y * y);
                Theta_Value_Cylindrical = Math.Atan2(y, x);    // fixed: use atan2
                Z_Value_Cylindrical = Z_Value_Cartesian;
                return true;
            }
            catch { return false; }
        }

        /// <summary>Convert spherical (r,φ) to cylindrical (r,θ,z), if MyType == Spherical.</summary>
        public bool SphericalToCylindrical(double r, double phiRadians)
        {
            try
            {
                if (MyType != PointTypeEnum.Spherical) return false;

                R_Value_Cylindrical = r * Math.Sin(phiRadians);
                Z_Value_Cylindrical = r * Math.Cos(phiRadians);
                Theta_Value_Cylindrical = Theta_Value_Spherical;
                return true;
            }
            catch { return false; }
        }

        /// <summary>Convert Cartesian (x,y,z) to spherical (r,θ,φ), if MyType == Cartesian.</summary>
        public bool CartesianToSpherical(double x, double y, double z)
        {
            try
            {
                if (MyType != PointTypeEnum.Cartesian) return false;

                R_Value_Spherical = Math.Sqrt(x * x + y * y + z * z);
                Theta_Value_Spherical = Math.Atan2(y, x);        // azimuth
                // φ = atan2(ρ, z) with ρ = sqrt(x^2 + y^2)
                Phi_Value = Math.Atan2(Math.Sqrt(x * x + y * y), z);
                return true;
            }
            catch { return false; }
        }

        /// <summary>Convert cylindrical (r,z) to spherical (r,θ,φ), if MyType == Cylindrical.</summary>
        public bool CylindricalToSpherical(double r, double z)
        {
            try
            {
                if (MyType != PointTypeEnum.Cylindrical) return false;

                R_Value_Spherical = Math.Sqrt(r * r + z * z);
                Phi_Value = Math.Atan2(r, z);                    // polar from +Z
                Theta_Value_Spherical = Theta_Value_Cylindrical;
                return true;
            }
            catch { return false; }
        }

        // -----------------------------
        // Utilities
        // -----------------------------
        public static double DegreesToRadians(double angleInDegrees) => angleInDegrees * Math.PI / 180.0;
        public static double RadiansToDegrees(double angleInRadians) => angleInRadians * 180.0 / Math.PI;

        public void ConnectTo(Point other)
        {
            if (other is null) throw new ArgumentNullException(nameof(other));
            MyConnectedPoints.Add(other);
            if (CurrentConnectedPoint == null)
            {
                CurrentConnectedPoint = other;
            }
        }

        public override string ToString()
            => $"Point(ID={PointID ?? "<null>"}, Type={MyType}, " +
               $"Cart=({X_Value:F3},{Y_Value:F3},{Z_Value_Cartesian:F3}))";
    }
}
