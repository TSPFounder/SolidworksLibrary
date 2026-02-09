using System;
using System.Collections.Generic;
using System.Linq;
using Mathematics;
using System.Xml;
using Newtonsoft.Json;

namespace CAD
{
    /// <summary>
    /// Lightweight, modernized refactor of the original CAD_Sketch class.
    /// - Uses readonly backing collections and read-only exposures (IReadOnlyList).
    /// - Adds small helper mutators (AddPoint / AddSegment) and validation helpers.
    /// - Improves the original WriteSketchToJSON to validate contiguity and produce JSON.
    /// - Keeps type references (Point, Segment, CAD_Parameter, etc.) from the original model.
    /// </summary>
    public class CAD_Sketch
    {
        // -----------------------
        // Backing fields
        // -----------------------
        private readonly List<Point> _points = new List<Point>();
        private readonly List<Segment> _segments = new List<Segment>();
        private readonly List<CAD_Dimension> _dimensions = new List<CAD_Dimension>();
        private readonly List<CAD_Parameter> _parameters = new List<CAD_Parameter>();
        private readonly List<CAD_Constraint> _constraints = new List<CAD_Constraint>();
        private readonly List<Segment> _profile = new List<Segment>();
        private readonly List<TwoDGeometry> _twoDGeometry = new List<TwoDGeometry>();
        private readonly List<CoordinateSystem> _coordinateSystems = new List<CoordinateSystem>();
        private readonly List<CAD_SketchElement> _sketchElements = new List<CAD_SketchElement>();

        // -----------------------
        // Construction
        // -----------------------
        public CAD_Sketch() { }

        public CAD_Sketch(string sketchId) => SketchID = sketchId;

        // -----------------------
        // Identification / metadata
        // -----------------------
        public string SketchID { get; set; }
        public string Version { get; set; }

        // -----------------------
        // Geometry summary parameters
        // -----------------------
        public CAD_Parameter Area { get; set; }
        public CAD_Parameter PerimeterLength { get; set; }

        // -----------------------
        // Ownership
        // -----------------------
        public CAD_Model MyModel { get; set; }
        public CAD_SketchPlane MySketchPlane { get; set; }

        // -----------------------
        // Flags / mode
        // -----------------------
        /// <summary>True when sketch is strictly 2D.</summary>
        public bool IsTwoD { get; set; }

        // -----------------------
        // Current cursors (optional helpers for editing workflows)
        // -----------------------
        public Point CurrentPoint { get; private set; }
        public Segment CurrentSegment { get; private set; }
        public Segment PreviousSegment { get; private set; }
        public CAD_SketchElement CurrentSketchElem { get; set; }
        public CAD_Parameter CurrentParameter { get; set; }
        public CAD_Dimension CurrentDimension { get; set; }
        public CAD_Constraint CurrentConstraint { get; set; }
        public CoordinateSystem CurrentCoordinateSystem { get; set; }
        public CoordinateSystem BaseCoordinateSystem { get; set; }

        // -----------------------
        // Collections (read-only exposure)
        // -----------------------
        public IReadOnlyList<Point> MyPoints => _points;
        public IReadOnlyList<Segment> MySegments => _segments;
        public IReadOnlyList<Segment> MyProfile => _profile;
        public IReadOnlyList<TwoDGeometry> My2DGeometry => _twoDGeometry;
        public IReadOnlyList<CoordinateSystem> MyCoordinateSystems => _coordinateSystems;
        public IReadOnlyList<CAD_SketchElement> MySketchElements => _sketchElements;
        public IReadOnlyList<CAD_Parameter> MyParameters => _parameters;
        public IReadOnlyList<CAD_Dimension> MyDimensions => _dimensions;
        public IReadOnlyList<CAD_Constraint> MyConstraints => _constraints;

        // -----------------------
        // Mutators / helpers
        // -----------------------

        /// <summary>Add a point to the sketch and move the CurrentPoint cursor to it.</summary>
        public void AddPoint(Point point)
        {
            if (point is null) throw new ArgumentNullException(nameof(point));
            _points.Add(point);
            PreviousSegment = null;
            CurrentPoint = point;
        }

        /// <summary>Add a segment to the sketch and update segment cursors (CurrentSegment / PreviousSegment).</summary>
        public void AddSegment(Segment segment)
        {
            if (segment is null) throw new ArgumentNullException(nameof(segment));
            _segments.Add(segment);
            PreviousSegment = _segments.Count >= 2 ? _segments[_segments.Count - 2] : null;
            CurrentSegment = segment;
        }

        public void AddDimension(CAD_Dimension dim)
        {
            if (dim is null) throw new ArgumentNullException(nameof(dim));
            _dimensions.Add(dim);
            CurrentDimension = dim;
        }

        public void AddParameter(CAD_Parameter parameter)
        {
            if (parameter is null) throw new ArgumentNullException(nameof(parameter));
            _parameters.Add(parameter);
            CurrentParameter = parameter;
        }

        public void AddConstraint(CAD_Constraint constraint)
        {
            if (constraint is null) throw new ArgumentNullException(nameof(constraint));
            _constraints.Add(constraint);
            CurrentConstraint = constraint;
        }

        public void AddSketchElement(CAD_SketchElement elem)
        {
            if (elem is null) throw new ArgumentNullException(nameof(elem));
            _sketchElements.Add(elem);
            CurrentSketchElem = elem;
        }

        public void AddCoordinateSystem(CoordinateSystem cs)
        {
            if (cs is null) throw new ArgumentNullException(nameof(cs));
            _coordinateSystems.Add(cs);
            CurrentCoordinateSystem = cs;
        }

        public void AddProfileSegment(Segment seg)
        {
            if (seg is null) throw new ArgumentNullException(nameof(seg));
            _profile.Add(seg);
        }

        public void AddTwoDGeometry(TwoDGeometry geo)
        {
            if (geo is null) throw new ArgumentNullException(nameof(geo));
            _twoDGeometry.Add(geo);
        }

        public void Clear()
        {
            _points.Clear();
            _segments.Clear();
            _dimensions.Clear();
            _parameters.Clear();
            _constraints.Clear();
            _profile.Clear();
            _twoDGeometry.Clear();
            _sketchElements.Clear();
            CurrentPoint = null;
            CurrentSegment = null;
            PreviousSegment = null;
        }

        // -----------------------
        // Validation helpers
        // -----------------------

        /// <summary>
        /// Validates that the provided segments form a contiguous chain where each segment's start equals the previous segment's end.
        /// </summary>
        /// <param name="segments">Segments to validate (if null uses sketch segments).</param>
        /// <param name="tolerance">Coordinate matching tolerance</param>
        /// <returns>True if contiguous (and non-empty), otherwise false.</returns>
        public bool ValidateContiguous(IReadOnlyList<Segment> segments = null, double tolerance = 1e-9)
        {
            var list = segments ?? MySegments;
            if (list == null || list.Count == 0) return false;
            // Each segment must have a non-null StartPoint and EndPoint
            for (int i = 0; i < list.Count; ++i)
            {
                var seg = list[i];
                if (seg?.StartPoint is null || seg.EndPoint is null) return false;
                if (i > 0)
                {
                    var prev = list[i - 1];
                    if (prev?.EndPoint is null) return false;
                    if (!PointsMatch(prev.EndPoint, seg.StartPoint, tolerance)) return false;
                }
            }
            return true;
        }

        private static bool PointsMatch(Point a, Point b, double tol)
        {
            if (a is null || b is null) return false;
            var dx = a.X_Value - b.X_Value;
            var dy = a.Y_Value - b.Y_Value;
            var dz = a.Z_Value_Cartesian - b.Z_Value_Cartesian;
            return (dx * dx + dy * dy + dz * dz) <= tol * tol;
        }

        /// <summary>
        /// Attempts to determine whether a closed loop exists (first and last point equal within tolerance).
        /// Returns false if there are too few segments/points.
        /// </summary>
        public bool IsClosedLoop(double tolerance = 1e-9)
        {
            if (_segments.Count < 1) return false;
            var first = _segments.First().StartPoint;
            var last = _segments.Last().EndPoint;
            if (first is null || last is null) return false;
            return PointsMatch(first, last, tolerance);
        }

        // -----------------------
        // Serialization / original WriteSketchToJSON replacement
        // -----------------------

        /// <summary>
        /// Validates contiguity of segments and if valid serializes the segments into JSON.
        /// Produces a JSON string in the out parameter. Returns true when validation + serialization succeed.
        /// </summary>
        public bool WriteSketchToJSON(out string json, IReadOnlyList<Segment> segments = null, double tolerance = 1e-9)
        {
            json = string.Empty;
            var list = segments ?? MySegments;
            if (!ValidateContiguous(list, tolerance)) return false;

            // Build a simple serializable representation to avoid attempting to serialize external types directly.
            var serializableSegments = list.Select(s => new
            {
                Start = new { X = s.StartPoint?.X_Value, Y = s.StartPoint?.Y_Value, Z = s.StartPoint?.Z_Value_Cartesian },
                End = new { X = s.EndPoint?.X_Value, Y = s.EndPoint?.Y_Value, Z = s.EndPoint?.Z_Value_Cartesian },
                SegmentID = s.SegmentID
            }).ToList();

            try
            {
                json = JsonConvert.SerializeObject(new
                {
                    SketchID,
                    Version,
                    IsTwoD,
                    Segments = serializableSegments
                }, Newtonsoft.Json.Formatting.Indented);

                return true;
            }
            catch
            {
                json = string.Empty;
                return false;
            }
        }

        /// <summary>
        /// Convenience wrapper matching the older signature: returns true/false based on success (json discarded).
        /// </summary>
        public bool WriteSketchToJSON(List<Segment> MySegments)
        {
            return WriteSketchToJSON(out _, MySegments);
        }

        // -----------------------
        // Simple sketching helper
        // -----------------------
        /// <summary>
        /// Adds a point to the sketch and updates the current point. Optionally performs CAD-app specific actions (kept for compatibility).
        /// </summary>
        public bool SketchAPoint(Point myPoint)
        {
            if (myPoint is null) return false;
            AddPoint(myPoint);

            // Placeholder: app-specific behavior can be added here if necessary.
            // For now we only update internal state and return success.
            return true;
        }

        // JSON Serialization
        public string ToJson() => JsonConvert.SerializeObject(this, Newtonsoft.Json.Formatting.Indented,
            new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });
        public static CAD_Sketch FromJson(string json) => JsonConvert.DeserializeObject<CAD_Sketch>(json);
    }
}

