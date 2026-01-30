using System;
using System.Collections.Generic;
using Mathematics;
using SE_Library;
using Documents;

namespace CAD
{
    /// <summary>
    /// Represents a sketch/feature constraint within a CAD model.
    /// </summary>
    public sealed class CAD_Constraint
    {
        // -----------------------------
        // Types
        // -----------------------------
        public enum ConstraintType
        {
            Horizontal = 0,
            Vertical,
            Distance,
            Coincident,
            Tangent,
            Angle,
            Equal,
            Parallel,
            Perpendicular,
            Fixed,
            Midpoint,
            Midplane,
            Concentric,
            Collinear,
            Symmetry,
            Curvature,
            Other
        }

        // -----------------------------
        // Backing fields (mutable privately; exposed read-only)
        // -----------------------------
        private readonly List<CAD_Feature> _features = new List<CAD_Feature>();
        private readonly List<CAD_Model> _models = new List<CAD_Model>();

        // -----------------------------
        // Construction
        // -----------------------------
        public CAD_Constraint() { }

        public CAD_Constraint(string id, string name = null, ConstraintType type = ConstraintType.Other)
        {
            ID = id;
            Name = name;
            Type = type;
        }

        // -----------------------------
        // Identity / Meta
        // -----------------------------
        public string Name { get; set; }
        public string ID { get; set; }               // (Was "PartNumber" in original; clarified to ID)
        public string Description { get; set; }

        // -----------------------------
        // Data
        // -----------------------------
        public ConstraintType Type { get; set; }      // (Was "MyConstraintType")

        // -----------------------------
        // Owned & Owning Objects
        // -----------------------------
        public CAD_Feature CurrentFeature { get; set; }
        public CAD_Feature PreviousFeature { get; set; }

        public CAD_Model CurrentModel { get; set; }

        /// <summary>All features referenced by this constraint (read-only view).</summary>
        public IReadOnlyList<CAD_Feature> Features => _features;

        /// <summary>All models this constraint participates in (read-only view).</summary>
        public IReadOnlyList<CAD_Model> Models => _models;

        // -----------------------------
        // Helpers (idempotent adds/removes)
        // -----------------------------
        public void AddFeature(CAD_Feature feature)
        {
            if (feature is null) throw new ArgumentNullException(nameof(feature));
            if (!_features.Contains(feature)) _features.Add(feature);
        }

        public bool RemoveFeature(CAD_Feature feature) =>
            feature != null && _features.Remove(feature);

        public void AddModel(CAD_Model model)
        {
            if (model is null) throw new ArgumentNullException(nameof(model));
            if (!_models.Contains(model)) _models.Add(model);
        }

        public bool RemoveModel(CAD_Model model) =>
            model != null && _models.Remove(model);

        // -----------------------------
        // Validation
        // -----------------------------
        /// <summary>Basic sanity check (must have ID or Name).</summary>
        public bool IsValid(out string reason)
        {
            if (string.IsNullOrWhiteSpace(ID) && string.IsNullOrWhiteSpace(Name))
            {
                reason = "Constraint must have an ID or Name.";
                return false;
            }

            reason = null;
            return true;
        }
    }
}

