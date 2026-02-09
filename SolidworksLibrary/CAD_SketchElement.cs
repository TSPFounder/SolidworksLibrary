using Mathematics;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace CAD
{
    /// <summary>
    /// A geometric element within a sketch (points, lines, arcs, etc.).
    /// </summary>
    public sealed class CAD_SketchElement
    {
        // -----------------------------
        // Types
        // -----------------------------
        public enum SketchElemTypeEnum
        {
            StartPoint = 0,
            EndPoint,
            MidPoint,
            ControlPoint,
            Line,
            Rectangle,
            Circle,
            Parabola,
            Ellipse,
            Contour,
            Arc,
            Spline,
            Slot,
            BreakLine,
            Centerline,
            Centerpoint,
            WorkPoint,
            WorkLine
        }

        // -----------------------------
        // Backing fields
        // -----------------------------
        private readonly List<Point> _points = new List<Point>();
        private readonly List<Primitive> _primitives = new List<Primitive>();

        // -----------------------------
        // Construction
        // -----------------------------
        public CAD_SketchElement() { }

        public CAD_SketchElement(
            SketchElemTypeEnum elementType,
            string name = null,
            string version = null,
            string path = null)
        {
            ElementType = elementType;
            Name = name;
            Version = version;
            Path = path;
        }

        // -----------------------------
        // Identity
        // -----------------------------
        public string Name { get; set; }
        public string Version { get; set; }
        public string Path { get; set; }

        // -----------------------------
        // Data
        // -----------------------------
        public SketchElemTypeEnum ElementType { get; set; }

        /// <summary>Marks this element as a work/reference element.</summary>
        public bool IsWorkElement { get; set; }

        // -----------------------------
        // Geometry (owned)
        // -----------------------------
        public Point CurrentPoint { get; private set; }
        public Point StartPoint { get; set; }
        public Point EndPoint { get; set; }
        public Point MidPoint { get; set; }
        public Point ControlPoint { get; set; }

        public Primitive CurrentPrimitive { get; private set; }

        /// <summary>All points associated with this sketch element.</summary>
        public IReadOnlyList<Point> Points => _points;

        /// <summary>All geometric primitives (lines, arcs, splines) for this element.</summary>
        public IReadOnlyList<Primitive> Primitives => _primitives;

        // -----------------------------
        // Operations
        // -----------------------------
        /// <summary>
        /// Adds a point to this element. Optionally sets it as the current point and/or marks the element as a work element.
        /// </summary>
        public Point AddPoint(Point point = null, bool makeCurrent = true, bool isWorkPoint = false)
        {
            var p = point ?? new Point();
            _points.Add(p);

            if (makeCurrent)
                CurrentPoint = p;

            // Corrected: compare, don't assign.
            if (isWorkPoint)
                IsWorkElement = true;

            return p;
        }

        /// <summary>
        /// Adds a primitive to this element and sets it as current.
        /// </summary>
        public Primitive AddPrimitive(Primitive primitive)
        {
            if (primitive is null) throw new ArgumentNullException(nameof(primitive));
            _primitives.Add(primitive);
            CurrentPrimitive = primitive;
            return primitive;
        }

        /// <summary>
        /// Clears all geometry (points and primitives) and resets current references.
        /// </summary>
        public void ClearGeometry()
        {
            _points.Clear();
            _primitives.Clear();
            CurrentPoint = null;
            CurrentPrimitive = null;
        }

        // JSON Serialization
        public string ToJson() => JsonConvert.SerializeObject(this, Formatting.Indented,
            new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });
        public static CAD_SketchElement FromJson(string json) => JsonConvert.DeserializeObject<CAD_SketchElement>(json);
    }
}
