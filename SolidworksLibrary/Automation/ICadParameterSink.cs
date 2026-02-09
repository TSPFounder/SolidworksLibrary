using System.Collections.Generic;

namespace SolidworksLibrary.Automation
{
    public interface ICadParameterSink
    {
        BridgeSyncResult ApplyParameters(object model, IEnumerable<CadParameter> parameters, BridgeSyncOptions options);
    }

    public class CadParameter
    {
        public string Name { get; set; }
        public double Value { get; set; }
        public string Unit { get; set; }
    }
    /*
    public class BridgeSyncResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
    }

    public class BridgeSyncOptions
    {
        public bool RebuildAfterSync { get; set; } = true;
    }
    */

}
