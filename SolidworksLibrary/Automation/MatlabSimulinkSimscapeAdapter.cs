using System;

namespace SolidworksLibrary.Automation
{
    public sealed class MatlabSimulinkSimscapeAdapter : ISimulationAdapter
    {
        public string AdapterName => "Matlab/Simulink/Simscape";

        public SimulationModelSnapshot CaptureSnapshot(SimulationCaptureRequest request)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));

            var snapshot = new SimulationModelSnapshot(request.Domain, request.ModelName, request.Parameters)
            {
                SourcePath = request.SourcePath
            };

            foreach (var pair in request.Metadata)
            {
                snapshot.Metadata[pair.Key] = pair.Value;
            }

            return snapshot;
        }
    }
}
