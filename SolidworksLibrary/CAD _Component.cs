
using System;
using System.Collections.Generic;
using System.Numerics;
using Mathematics;
using SE_Library;

namespace CAD
{
    public class CAD_Component : CAD_Part
    {
        // -----------------------------
        // Constructor
        // -----------------------------
        public CAD_Component()
        {
            MySketches = new List<CAD_Sketch>();
            MyJoints = new List<CAD_Joint>();
            MomentsOfInertia = new List<CAD_Parameter>();
            PrincipleDirections = new List<Mathematics.Vector>();
        }

        // -----------------------------
        // Identification
        // -----------------------------
        public string Name { get; set; }
        public string Version { get; set; }
        public string Path { get; set; }

        // -----------------------------
        // Data
        // -----------------------------
        public CAD_Parameter Weight { get; set; }
        public List<CAD_Parameter> MomentsOfInertia { get; set; }
        /// <remarks>
        /// Kept the original property name <c>PrincipleDirections</c> to preserve API compatibility.
        /// If this was a typo for “PrincipalDirections”, we can add a second read-only alias.
        /// </remarks>
        public List<Mathematics.Vector> PrincipleDirections { get; set; }

        // Optional alias (uncomment if you want a clearer name without breaking the original):
        // public List<Vector> PrincipalDirections => PrincipleDirections;

        // -----------------------------
        // Flags
        // -----------------------------
        public bool IsAssembly { get; set; }
        public bool IsConfigurationItem { get; set; }

        // -----------------------------
        // Ownership / Associations
        // -----------------------------
        public CAD_Part MyPart { get; set; }
        public List<CAD_Sketch> MySketches { get; set; }
        public List<CAD_Joint> MyJoints { get; set; }

        // -----------------------------
        // Component data
        // -----------------------------
        public int WBS_Level { get; set; }

        // -----------------------------
        // Methods (placeholders retained)
        // -----------------------------
        /// <summary>Extrudes a CAD profile (implementation-specific; retained as placeholder).</summary>
        public void ExtrudeCAD_Profile()
        {
            // Implementation goes here
        }

        // -----------------------------
        // Helpers (optional)
        // -----------------------------
        public void AddSketch(CAD_Sketch sketch)
        {
            if (sketch is null) throw new ArgumentNullException(nameof(sketch));
            MySketches.Add(sketch);
        }

        public void AddJoint(CAD_Joint joint)
        {
            if (joint is null) throw new ArgumentNullException(nameof(joint));
            MyJoints.Add(joint);
        }

        public  string ToString()
            => $"CAD_Component(Name={Name ?? "<null>"}, Version={Version ?? "<null>"}, WBS={WBS_Level}, IsAsm={IsAssembly})";
    }
}

