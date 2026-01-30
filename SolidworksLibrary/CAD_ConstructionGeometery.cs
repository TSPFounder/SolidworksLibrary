
using System;

namespace CAD
{
    /// <summary>
    /// Lightweight model of construction geometry (datum entities like points, lines, planes, circles).
    /// </summary>
    public class CAD_ConstructionGeometry
    {
        // -----------------------------
        // Types
        // -----------------------------
        public enum ConstructionGeometryTypeEnum
        {
            Point = 0,
            Line,
            Plane,
            Circle
        }

        // -----------------------------
        // Construction
        // -----------------------------
        public CAD_ConstructionGeometry() { }

        public CAD_ConstructionGeometry(
            string name,
            ConstructionGeometryTypeEnum geometryType,
            string version = null,
            CAD_Model ownerModel = null)
        {
            Name = name;
            GeometryType = geometryType;
            Version = version ?? Version;
            MyCAD_Model = ownerModel;
        }

        // -----------------------------
        // Identification / metadata
        // -----------------------------
        /// <summary>User-visible name for the datum/geometry.</summary>
        public string Name { get; set; }

        /// <summary>Semantic version of the definition.</summary>
        public string Version { get; set; } = "1.0";

        /// <summary>The construction geometry kind (point, line, plane, circle).</summary>
        public ConstructionGeometryTypeEnum GeometryType { get; set; }

        // -----------------------------
        // Ownership
        // -----------------------------
        /// <summary>The owning CAD model, if applicable.</summary>
        public CAD_Model MyCAD_Model { get; set; }

        // -----------------------------
        // Overrides
        // -----------------------------
        public override string ToString() =>
            $"{Name ?? GeometryType.ToString()} [{GeometryType}] v{Version}";
    }

    
}

