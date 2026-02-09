using System;
using System.Collections.Generic;

namespace SolidworksLibrary.Automation
{
    public interface IParameterMapper
    {
        string MapToCadName(SimulationParameter parameter);
    }

    public sealed class ParameterMappingProfile : IParameterMapper
    {
        private readonly Dictionary<string, string> _explicitMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public string Prefix { get; set; }
        public string Suffix { get; set; }

        public void AddMapping(string simulationName, string cadName)
        {
            if (string.IsNullOrWhiteSpace(simulationName))
            {
                throw new ArgumentException("Simulation parameter name is required.", nameof(simulationName));
            }

            if (string.IsNullOrWhiteSpace(cadName))
            {
                throw new ArgumentException("CAD parameter name is required.", nameof(cadName));
            }

            _explicitMappings[simulationName] = cadName;
        }

        public string MapToCadName(SimulationParameter parameter)
        {
            if (parameter is null) throw new ArgumentNullException(nameof(parameter));

            if (_explicitMappings.TryGetValue(parameter.Name, out var mapped))
            {
                return mapped;
            }

            return string.Concat(Prefix ?? string.Empty, parameter.Name, Suffix ?? string.Empty);
        }
    }
}
