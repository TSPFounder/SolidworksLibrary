using System;
using System.Collections.Generic;

namespace SolidworksLibrary.Automation
{
    public sealed class SimulationCaptureRequest
    {
        
        public SimulationCaptureRequest(SimulationDomain domain, string modelName, IReadOnlyList<SimulationParameter> parameters)
        {
            Domain = domain;
            ModelName = modelName ?? throw new ArgumentNullException(nameof(modelName));
            Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        }
        

        public SimulationDomain Domain { get; }
        public string ModelName { get; }
        public string SourcePath { get; set; }
        public IReadOnlyList<SimulationParameter> Parameters { get; }
        public IDictionary<string, string> Metadata { get; } = new Dictionary<string, string>();
    }
}
