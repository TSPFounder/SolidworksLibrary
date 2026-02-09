using System.Collections.Generic;

namespace SolidworksLibrary.Automation
{
    public class SolidworksParameterSinkAdapter : ICadParameterSink
    {
        private readonly SolidworksParameterSink _sink = new SolidworksParameterSink();

        public BridgeSyncResult ApplyParameters(object model, IEnumerable<CadParameter> parameters, BridgeSyncOptions options)
        {
            return _sink.ApplyParameters(model as SolidworksModel, parameters as IEnumerable<CAD.CAD_Parameter>, options);
        }
    }
}