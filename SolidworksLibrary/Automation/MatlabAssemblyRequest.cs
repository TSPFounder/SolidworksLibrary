using System;
using System.Collections.Generic;
using SwConst;

namespace SolidworksLibrary.Automation
{
    public sealed class MatlabAssemblyRequest
    {
        public string AssemblyName { get; set; }
        public IList<MatlabAssemblyComponentSpec> Components { get; } = new List<MatlabAssemblyComponentSpec>();
        public IList<MatlabAssemblyMateSpec> Mates { get; } = new List<MatlabAssemblyMateSpec>();

        public bool IsValid(out string reason)
        {
            if (Components.Count == 0)
            {
                reason = "At least one component is required.";
                return false;
            }

            foreach (var component in Components)
            {
                if (string.IsNullOrWhiteSpace(component.Alias))
                {
                    reason = "Each component requires an alias.";
                    return false;
                }
            }

            reason = null;
            return true;
        }
    }

    public sealed class MatlabAssemblyComponentSpec
    {
        public string Alias { get; set; }
        public bool IsFixed { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
    }

    public sealed class MatlabAssemblyMateSpec
    {
        public string Component1Alias { get; set; }
        public string Component2Alias { get; set; }
        public swMateType_e MateType { get; set; }
        public swMateAlign_e Alignment { get; set; } = swMateAlign_e.swMateAlignALIGNED;
        public double Value { get; set; }
    }
}
