using System;
using System.Globalization;

namespace CAD
{
    /// <summary>
    /// Strongly-typed parameter value wrapper with safe parsing and typed accessors.
    /// </summary>
    public sealed class CAD_ParameterValue
    {
        // -----------------------------
        // Types
        // -----------------------------
        public enum ParameterValueTypeEnum
        {
            Double = 0,
            Single,
            Int16,
            Int32,
            Int64,
            Boolean,
            String,
            Object
        }

        // -----------------------------
        // State
        // -----------------------------
        private object _value;

        /// <summary>The declared type of this value.</summary>
        public ParameterValueTypeEnum ValueType { get; }

        /// <summary>The owning parameter (optional).</summary>
        public CAD_Parameter Parameter { get; }

        // -----------------------------
        // Construction
        // -----------------------------
        public CAD_ParameterValue(ParameterValueTypeEnum type, CAD_Parameter parameter = null)
        {
            ValueType = type;
            Parameter = parameter;
        }

        public CAD_ParameterValue(double value, CAD_Parameter parameter = null)
            : this(ParameterValueTypeEnum.Double, parameter) => _value = value;

        public CAD_ParameterValue(float value, CAD_Parameter parameter = null)
            : this(ParameterValueTypeEnum.Single, parameter) => _value = value;

        public CAD_ParameterValue(short value, CAD_Parameter parameter = null)
            : this(ParameterValueTypeEnum.Int16, parameter) => _value = value;

        public CAD_ParameterValue(int value, CAD_Parameter parameter = null)
            : this(ParameterValueTypeEnum.Int32, parameter) => _value = value;

        public CAD_ParameterValue(long value, CAD_Parameter parameter = null)
            : this(ParameterValueTypeEnum.Int64, parameter) => _value = value;

        public CAD_ParameterValue(bool value, CAD_Parameter parameter = null)
            : this(ParameterValueTypeEnum.Boolean, parameter) => _value = value;

        public CAD_ParameterValue(string value, CAD_Parameter parameter = null)
            : this(ParameterValueTypeEnum.String, parameter) => _value = value;

        public CAD_ParameterValue(object value, CAD_Parameter parameter = null)
            : this(ParameterValueTypeEnum.Object, parameter) => _value = value;

        // -----------------------------
        // Typed setters
        // -----------------------------
        public void Set(double v) { Ensure(ParameterValueTypeEnum.Double); _value = v; }
        public void Set(float v) { Ensure(ParameterValueTypeEnum.Single); _value = v; }
        public void Set(short v) { Ensure(ParameterValueTypeEnum.Int16); _value = v; }
        public void Set(int v) { Ensure(ParameterValueTypeEnum.Int32); _value = v; }
        public void Set(long v) { Ensure(ParameterValueTypeEnum.Int64); _value = v; }
        public void Set(bool v) { Ensure(ParameterValueTypeEnum.Boolean); _value = v; }
        public void Set(string v) { Ensure(ParameterValueTypeEnum.String); _value = v; }
        public void SetObject(object v) { Ensure(ParameterValueTypeEnum.Object); _value = v; }

        /// <summary>
        /// Parses a string according to <see cref="ValueType"/> using invariant culture.
        /// Returns false if parsing fails; never throws.
        /// </summary>
        public bool TrySetFromString(string value)
        {
            var s = value ?? string.Empty;
            var ci = CultureInfo.InvariantCulture;

            switch (ValueType)
            {
                case ParameterValueTypeEnum.Double:
                    if (double.TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands, ci, out var d)) { _value = d; return true; }
                    return false;

                case ParameterValueTypeEnum.Single:
                    if (float.TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands, ci, out var f)) { _value = f; return true; }
                    return false;

                case ParameterValueTypeEnum.Int16:
                    if (short.TryParse(s, NumberStyles.Integer, ci, out var i16)) { _value = i16; return true; }
                    return false;

                case ParameterValueTypeEnum.Int32:
                    if (int.TryParse(s, NumberStyles.Integer, ci, out var i32)) { _value = i32; return true; }
                    return false;

                case ParameterValueTypeEnum.Int64:
                    if (long.TryParse(s, NumberStyles.Integer, ci, out var i64)) { _value = i64; return true; }
                    return false;

                case ParameterValueTypeEnum.Boolean:
                    if (bool.TryParse(s, out var b)) { _value = b; return true; }
                    // accept 0/1 as false/true
                    if (s == "0") { _value = false; return true; }
                    if (s == "1") { _value = true; return true; }
                    return false;

                case ParameterValueTypeEnum.String:
                    _value = s;
                    return true;

                case ParameterValueTypeEnum.Object:
                    _value = s; // caller can deserialize later
                    return true;

                default:
                    return false;
            }
        }

        // -----------------------------
        // Typed getters / TryGet
        // -----------------------------
        public bool TryGetDouble(out double v) { v = 0; return ValueType == ParameterValueTypeEnum.Double && _value is double d && (v = d) == d; }
        public bool TryGetSingle(out float v) { v = 0; return ValueType == ParameterValueTypeEnum.Single && _value is float f && (v = f) == f; }
        public bool TryGetInt16(out short v) { v = 0; return ValueType == ParameterValueTypeEnum.Int16 && _value is short i16 && (v = i16) == i16; }
        public bool TryGetInt32(out int v) { v = 0; return ValueType == ParameterValueTypeEnum.Int32 && _value is int i32 && (v = i32) == i32; }
        public bool TryGetInt64(out long v) { v = 0; return ValueType == ParameterValueTypeEnum.Int64 && _value is long i64 && (v = i64) == i64; }
        public bool TryGetBoolean(out bool v) { v = false; return ValueType == ParameterValueTypeEnum.Boolean && _value is bool b && (v = b) == b; }
        public bool TryGetString(out string v) { v = null; return ValueType == ParameterValueTypeEnum.String && _value is string s && (v = s) == s; }
        public bool TryGetObject(out object v) { v = _value; return ValueType == ParameterValueTypeEnum.Object; }

        public double? AsDouble()
        {
            double v;
            return TryGetDouble(out v) ? (double?)v : null;
        }
        public float? AsSingle()
        {
            float v;
            return TryGetSingle(out v) ? (float?)v : null;
        }
        public short? AsInt16()
        {
            short v;
            return TryGetInt16(out v) ? (short?)v : null;
        }
        public int? AsInt32()
        {
            int v;
            return TryGetInt32(out v) ? (int?)v : null;
        }
        public long? AsInt64()
        {
            long v;
            return TryGetInt64(out v) ? (long?)v : null;
        }
        public bool? AsBoolean()
        {
            bool v;
            return TryGetBoolean(out v) ? (bool?)v : null;
        }
        public string AsString()
        {
            string v;
            return TryGetString(out v) ? v : null;
        }
        public object AsObject() => ValueType == ParameterValueTypeEnum.Object ? _value : null;

        // -----------------------------
        // Utilities
        // -----------------------------
        private void Ensure(ParameterValueTypeEnum expected)
        {
            if (ValueType != expected)
                throw new InvalidOperationException($"Value type mismatch. Expected {expected} but was {ValueType}.");
        }

        public override string ToString()
        {
            switch (ValueType)
            {
                case ParameterValueTypeEnum.Double:
                    return AsDouble()?.ToString("G", CultureInfo.InvariantCulture) ?? "null";
                case ParameterValueTypeEnum.Single:
                    return AsSingle()?.ToString("G", CultureInfo.InvariantCulture) ?? "null";
                case ParameterValueTypeEnum.Int16:
                    return AsInt16()?.ToString(CultureInfo.InvariantCulture) ?? "null";
                case ParameterValueTypeEnum.Int32:
                    return AsInt32()?.ToString(CultureInfo.InvariantCulture) ?? "null";
                case ParameterValueTypeEnum.Int64:
                    return AsInt64()?.ToString(CultureInfo.InvariantCulture) ?? "null";
                case ParameterValueTypeEnum.Boolean:
                    return AsBoolean()?.ToString() ?? "null";
                case ParameterValueTypeEnum.String:
                    return AsString() ?? "null";
                case ParameterValueTypeEnum.Object:
                    return _value?.ToString() ?? "null";
                default:
                    return "null";
            }
        }
    }
}
