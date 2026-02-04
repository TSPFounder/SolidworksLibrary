using System;
using System.Collections.Generic;
using System.Linq;
using CAD;

namespace SolidworksLibrary.Automation
{
    public sealed class CadAutomationBridge
    {
        private readonly SolidworksApp _solidworksApp;
        private readonly ISimulationAdapter _simulationAdapter;
        private readonly IParameterMapper _parameterMapper;
        private readonly ICadParameterSink _parameterSink;

        public CadAutomationBridge(
            SolidworksApp solidworksApp,
            ISimulationAdapter simulationAdapter,
            IParameterMapper parameterMapper,
            ICadParameterSink parameterSink)
        {
            _solidworksApp = solidworksApp ?? throw new ArgumentNullException(nameof(solidworksApp));
            _simulationAdapter = simulationAdapter ?? throw new ArgumentNullException(nameof(simulationAdapter));
            _parameterMapper = parameterMapper ?? throw new ArgumentNullException(nameof(parameterMapper));
            _parameterSink = parameterSink ?? throw new ArgumentNullException(nameof(parameterSink));
        }

        public BridgeSyncResult SyncParameters(SimulationCaptureRequest request, BridgeSyncOptions options = null)
        {
            var snapshot = _simulationAdapter.CaptureSnapshot(request);
            var mapped = MapParameters(snapshot.Parameters);
            return _parameterSink.ApplyParameters(_solidworksApp.currentSw_Model, mapped, options ?? new BridgeSyncOptions());
        }

        private IEnumerable<CAD_Parameter> MapParameters(IEnumerable<SimulationParameter> parameters)
        {
            return parameters.Select(parameter => parameter.ToCadParameter(_parameterMapper.MapToCadName(parameter)));
        }
    }
}
