
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace CAD
{
    /// <summary>
    /// A solid (or surface) body composed of sketches and features.
    /// Inherits 3D operation capabilities from <see cref="CAD_Feature"/>.
    /// </summary>
    public class CAD_Body : CAD_Feature
    {
        // -----------------------------
        // Backing fields
        // -----------------------------
        private readonly List<CAD_Sketch> _sketches = new List<CAD_Sketch>();
        private readonly List<CAD_Feature> _features = new List<CAD_Feature>();

        // -----------------------------
        // Identification
        // -----------------------------
        public string Name { get; set; }
        public string Version { get; set; }
        public string PartNumber { get; set; }

        // -----------------------------
        // Owned & Owning Objects
        // -----------------------------
        /// <summary>The active sketch used for the most recent/next feature.</summary>
        public CAD_Sketch CurrentSketch { get; private set; }

        /// <summary>The active feature being edited or most recently created.</summary>
        public CAD_Feature CurrentFeature { get; private set; }

        /// <summary>All sketches referenced by this body.</summary>
        public IReadOnlyList<CAD_Sketch> Sketches => _sketches;

        /// <summary>All features that compose this body.</summary>
        public IReadOnlyList<CAD_Feature> Features => _features;

        // -----------------------------
        // Construction
        // -----------------------------
        public CAD_Body()
        {
            // NOTE:
            // 3D operation list is inherited from CAD_Feature via ThreeDimOperations.
            // It is initialized in CAD_Feature's constructor.
        }

        // -----------------------------
        // Mutators / helpers
        // -----------------------------
        public void AddSketch(CAD_Sketch sketch, bool setCurrent = true)
        {
            if (sketch is null) throw new ArgumentNullException(nameof(sketch));
            _sketches.Add(sketch);
            if (setCurrent) CurrentSketch = sketch;
        }

        public void SetCurrentSketch(CAD_Sketch sketch) => CurrentSketch = sketch;

        public void AddFeature(CAD_Feature feature, bool setCurrent = true)
        {
            if (feature is null) throw new ArgumentNullException(nameof(feature));
            _features.Add(feature);
            if (setCurrent) CurrentFeature = feature;
        }

        public void SetCurrentFeature(CAD_Feature feature) => CurrentFeature = feature;

        /// <summary>
        /// Convenience helper to declare the next 3D operation this body intends to perform.
        /// Uses the inherited <see cref="CAD_Feature.Feature3DOperationEnum"/>.
        /// </summary>
        public void QueueOperation(Feature3DOperationEnum op) => ThreeDimOperations.Add(op);

        /// <summary>Clears all sketches and features (does not alter identification fields).</summary>
        public void Clear()
        {
            _sketches.Clear();
            _features.Clear();
            CurrentSketch = null;
            CurrentFeature = null;
            ThreeDimOperations.Clear();
        }

        // JSON Serialization
        public new string ToJson() => JsonConvert.SerializeObject(this, Formatting.Indented,
            new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });
        public static new CAD_Body FromJson(string json) => JsonConvert.DeserializeObject<CAD_Body>(json);
    }
}

