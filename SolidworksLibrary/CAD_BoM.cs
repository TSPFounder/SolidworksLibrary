using CAD;
using System;
using System.Collections.Generic;
using static CAD.CAD_DrawingElement;

namespace CAD
{
    /// <summary>
    /// Bill of Materials (BoM) drawing element.
    /// Specializes <see cref="CAD_DrawingTable"/> and tracks configurations and a BoM-specific table reference.
    /// </summary>
    public class CAD_BoM : CAD_DrawingElement
    {
        // -----------------------------
        // Types
        // -----------------------------
        public enum BoM_TypeEnum
        {
            Design = 0,
            Manufacturing,
            Estimating,
            Other
        }

        // -----------------------------
        // State
        // -----------------------------
        private readonly List<CAD_Configuration> _configurations = new List<CAD_Configuration>();

        // -----------------------------
        // Construction
        // -----------------------------
        public CAD_BoM()
        {
            MyType = DrawingElementType.BoM;
        }

        // -----------------------------
        // Properties
        // -----------------------------
        /// <summary>Optional classification for this BoM.</summary>
        public BoM_TypeEnum BoMType { get; set; }

        /// <summary>The configuration currently used to generate this BoM (if any).</summary>
        public CAD_Configuration CurrentConfiguration { get; set; }

        /// <summary>All configurations associated with this BoM.</summary>
        public IReadOnlyList<CAD_Configuration> Configurations => _configurations;

        /// <summary>Optional pointer to a BoM-dedicated drawing table definition.</summary>
        public CAD_DrawingBoM_Table DrawingBoMTable { get; set; }

        // -----------------------------
        // Helpers
        // -----------------------------
        /// <summary>Add a configuration if it's not already present.</summary>
        public bool AddConfiguration(CAD_Configuration configuration)
        {
            if (configuration is null) throw new ArgumentNullException(nameof(configuration));
            if (_configurations.Contains(configuration)) return false;
            _configurations.Add(configuration);
            return true;
        }

        /// <summary>Remove a configuration.</summary>
        public bool RemoveConfiguration(CAD_Configuration configuration)
        {
            return configuration != null && _configurations.Remove(configuration);
        }

        /// <summary>Clear all configurations.</summary>
        public void ClearConfigurations() => _configurations.Clear();
    }
}
