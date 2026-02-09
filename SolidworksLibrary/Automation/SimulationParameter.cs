using System;
using CAD;

namespace SolidworksLibrary.Automation
{
    public sealed class SimulationParameter
    {
        public SimulationParameter(string name, string valueText, string units = null, SimulationParameterKind kind = SimulationParameterKind.Scalar)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            ValueText = valueText;
            Units = units;
            Kind = kind;
        }

        public string Name { get; }
        public string ValueText { get; }
        public string Units { get; }
        public string SourcePath { get; set; }
        public SimulationDomain Domain { get; set; }
        public SimulationParameterKind Kind { get; set; }
        public string Description { get; set; }
        public bool IsDesignVariable { get; set; }

        public double? NumericValue
            => double.TryParse(ValueText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value)
                ? value
                : (double?)null;

        public CAD_Parameter ToCadParameter(string cadName = null)
        {
            var targetName = string.IsNullOrWhiteSpace(cadName) ? Name : cadName;
            var parameter = new CAD_Parameter(targetName, ResolveCadParameterType());
            parameter.Description = Description;

            if (NumericValue.HasValue)
            {
                parameter.Value = new CAD_ParameterValue(NumericValue.Value, parameter);
            }
            else
            {
                parameter.Value = new CAD_ParameterValue(ValueText ?? string.Empty, parameter);
                parameter.MyParameterType = CAD_Parameter.ParameterType.String;
            }

            return parameter;
        }

        private CAD_Parameter.ParameterType ResolveCadParameterType()
        {
            if (Kind == SimulationParameterKind.Vector)
            {
                return CAD_Parameter.ParameterType.Vector;
            }

            if (NumericValue.HasValue)
            {
                return CAD_Parameter.ParameterType.Double;
            }

            return CAD_Parameter.ParameterType.String;
        }
    }

    public enum SimulationParameterKind
    {
        Scalar = 0,
        Vector,
        Expression,
        String,
        Enum,
        Other
    }
}
