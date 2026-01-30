using System;
using System.Collections.Generic;
using Mathematics;
using SE_Library;
using Documents;
using System.Linq.Expressions;

namespace CAD
{
    /// <summary>
    /// A named CAD parameter with a value, units, optional expression, and links to
    /// dimensions, math parameters, and CAD models.
    /// </summary>
    public sealed class CAD_Parameter
    {
        // -----------------------------
        // Types
        // -----------------------------
        public enum ParameterType
        {
            Double = 0,
            Integer,
            String,
            Vector,
            Other
        }

        // -----------------------------
        // Identity / description
        // -----------------------------
        public string Name { get; set; }
        public string Id { get; set; }                    // was PartNumber
        public string Description { get; set; }
        public string Comments { get; set; }

        // -----------------------------
        // Core data
        // -----------------------------
        public ParameterType MyParameterType { get; set; }
        public CAD_ParameterValue Value { get; set; }
        public UnitOfMeasure MyUnits { get; set; }
        public Expression MyExpression { get; set; }

        // -----------------------------
        // CAD app bindings
        // -----------------------------
        public string SolidWorksParameterName { get; set; }
        public string Fusion360ParameterName { get; set; }

        // -----------------------------
        // Associations
        // -----------------------------
        public CAD_Dimension CurrentDimension { get; set; }
        public CAD_Model CurrentModel { get; set; }
        public CAD_Parameter CurrentMathParameter { get; set; }

        public SE_Table DesignTable { get; set; }

        // Backing collections
        private readonly List<CAD_Dimension> _dimensions = new List<CAD_Dimension>();
        private readonly List<CAD_Parameter> _mathParameters = new List<CAD_Parameter>();
        private readonly List<CAD_Model> _models = new List<CAD_Model>();
        private readonly List<CAD_Parameter> _dependencies = new List<CAD_Parameter>();
        private readonly List<CAD_Parameter> _dependents = new List<CAD_Parameter>();

        public IReadOnlyList<CAD_Dimension> MyDimensions => _dimensions;
        public IReadOnlyList<CAD_Parameter> MyMathParameters => _mathParameters;
        public IReadOnlyList<CAD_Model> MyModels => _models;
        public IReadOnlyList<CAD_Parameter> DependencyParameters => _dependencies;
        public IReadOnlyList<CAD_Parameter> DependentParameters => _dependents;

        // -----------------------------
        // Construction
        // -----------------------------
        public CAD_Parameter() { }

        public CAD_Parameter(string name, ParameterType type, CAD_ParameterValue value = null,UnitOfMeasure units = null)
        {
            Name = name;
            MyParameterType = type;
            Value = value;
            MyUnits = units;
        }

        // -----------------------------
        // Mutators / helpers
        // -----------------------------
        public void LinkDimension(CAD_Dimension dimension, bool setAsCurrent = true)
        {
            if (dimension is null) throw new ArgumentNullException(nameof(dimension));
            if (!_dimensions.Contains(dimension)) _dimensions.Add(dimension);
            if (setAsCurrent) CurrentDimension = dimension;
        }

        public void AttachToModel(CAD_Model model, bool setAsCurrent = true)
        {
            if (model is null) throw new ArgumentNullException(nameof(model));
            if (!_models.Contains(model)) _models.Add(model);
            if (setAsCurrent) CurrentModel = model;
        }

        public void AddMathParameter(CAD_Parameter mathParameter, bool setAsCurrent = false)
        {
            if (mathParameter is null) throw new ArgumentNullException(nameof(mathParameter));
            if (!_mathParameters.Contains(mathParameter)) _mathParameters.Add(mathParameter);
            if (setAsCurrent) CurrentMathParameter = mathParameter;
        }

        public void AddDependency(CAD_Parameter prerequisite)
        {
            if (prerequisite is null) throw new ArgumentNullException(nameof(prerequisite));
            if (ReferenceEquals(prerequisite, this)) return;
            if (!_dependencies.Contains(prerequisite)) _dependencies.Add(prerequisite);
            if (!prerequisite._dependents.Contains(this)) prerequisite._dependents.Add(this);
        }

        public bool RemoveDependency(CAD_Parameter prerequisite)
        {
            if (prerequisite is null) return false;
            var removed = _dependencies.Remove(prerequisite);
            if (removed) prerequisite._dependents.Remove(this);
            return removed;
        }

        public void SetExpression(Expression expression) => MyExpression = expression;

        /// <summary>Try to extract a double value if this parameter represents a scalar.</summary>
        public bool TryGetDouble(out double value)
        {
            value = 0;
            if (Value is null) return false;

            // Extend this as your CAD_ParameterValue API requires.
            if (Value.TryGetDouble(out var v))
            {
                value = v;
                return true;
            }
            return false;
        }

        public override string ToString()
        {
            var idPart = string.IsNullOrWhiteSpace(Id) ? "" : $"[{Id}] ";
            var val = Value?.ToString() ?? "<null>";
            var unit = MyUnits?.ToString();
            return $"{idPart}{Name}: {val}{(unit is null ? "" : " " + unit)}";
        }

        public static CAD_Parameter CreateIntegerParameter(string name, int initialValue = 0)
        {
            var parameter = new CAD_Parameter(name, CAD_Parameter.ParameterType.Integer);
            parameter.Value = new CAD_ParameterValue(initialValue, parameter);
            return parameter;
        }

        public static CAD_Parameter CreateDoubleParameter(string name, double initialValue = 0d)
        {
            var parameter = new CAD_Parameter(name, CAD_Parameter.ParameterType.Double);
            parameter.Value = new CAD_ParameterValue(initialValue, parameter);
            return parameter;
        }

        public static CAD_Parameter CreateBooleanParameter(string name, bool initialValue)
        {
            var parameter = new CAD_Parameter(name, CAD_Parameter.ParameterType.Other);
            parameter.Value = new CAD_ParameterValue(initialValue, parameter);
            return parameter;
        }

        public static CAD_Parameter CreateEnumParameter(string name, int initialValue) => CAD_Parameter.CreateIntegerParameter(name, initialValue);
    }
}

