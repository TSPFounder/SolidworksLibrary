using System;
using System.Collections.Generic;



namespace Mathematics
{
    /// <summary>
    /// 3D torus primitive defined by a center (major) radius R and a tube (minor) radius r.
    /// </summary>
    public sealed class Torus : Primitive
    {
        public Torus()
        {
            Is2D = false;
            ThreeDType = ThreeDPrimitiveTypeEnum.Torus;
            CenterPoint = new Point();
        }

        // ------------------------------------------------------------------
        // Identification
        // ------------------------------------------------------------------
        public string TorusID { get; set; }

        // ------------------------------------------------------------------
        // Geometry (R = CenterRadius, r = TubeRadius)
        // ------------------------------------------------------------------
        /// <summary>Major radius (distance from torus center to center of tube).</summary>
        public double CenterRadius { get; set; }

        /// <summary>Minor radius (tube radius).</summary>
        public double TubeRadius { get; set; }

        // ------------------------------------------------------------------
        // Location
        // ------------------------------------------------------------------
        public Point CenterPoint { get; set; } = new Point();

        // ------------------------------------------------------------------
        // Derived quantities (pure functions; do not mutate state)
        // ------------------------------------------------------------------
        /// <summary>Surface area = 4 * π² * R * r.</summary>
        public double SurfaceArea => 4.0 * System.Math.PI * System.Math.PI * CenterRadius * TubeRadius;

        /// <summary>Volume = 2 * π² * R * r².</summary>
        public double Volume => 2.0 * System.Math.PI * System.Math.PI * CenterRadius * TubeRadius * TubeRadius;

        // ------------------------------------------------------------------
        // Validation / Utilities
        // ------------------------------------------------------------------
        /// <summary>
        /// Checks geometric soundness. Requires R &gt; 0 and r &gt; 0. 
        /// Optionally enforces r ≤ R (set <paramref name="enforceMinorLessOrEqualMajor"/> to true).
        /// </summary>
        public bool IsValid(out string reason, bool enforceMinorLessOrEqualMajor = false)
        {
            if (CenterRadius <= 0) { reason = "CenterRadius must be > 0."; return false; }
            if (TubeRadius <= 0) { reason = "TubeRadius must be > 0."; return false; }
            if (enforceMinorLessOrEqualMajor && TubeRadius > CenterRadius)
            {
                reason = "TubeRadius must be ≤ CenterRadius.";
                return false;
            }
            reason = null;
            return true;
        }

        public override string ToString()
            => $"Torus(ID={TorusID ?? "—"}, R={CenterRadius:G}, r={TubeRadius:G})";
    }
}

