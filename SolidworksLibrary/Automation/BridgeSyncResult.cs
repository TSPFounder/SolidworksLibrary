using System.Collections.Generic;

namespace SolidworksLibrary.Automation
{
    public sealed class BridgeSyncResult
    {
        public int ParametersApplied { get; set; }
        public int ParametersUpdated { get; set; }
        public int ParametersSkipped { get; set; }
        public IList<string> Warnings { get; } = new List<string>();

        public override string ToString()
            => $"Applied={ParametersApplied}, Updated={ParametersUpdated}, Skipped={ParametersSkipped}, Warnings={Warnings.Count}";
    }
}
