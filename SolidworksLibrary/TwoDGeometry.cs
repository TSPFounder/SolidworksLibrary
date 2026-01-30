
using System;
using System.Collections.Generic;
//

namespace Mathematics
{
    /// <summary>
    /// Represents 2-D sketch geometry (points + segments) with light ownership and flags.
    /// </summary>
    public class TwoDGeometry
    {
        // -----------------------------
        // Types
        // -----------------------------
        public enum GeometryTypeEnum
        {
            Line = 0,
            Arc,
            Circle,
            Triangle,
            Rectangle,
            Quadratic,
            Spline
        }

        // -----------------------------
        // State / backing fields
        // -----------------------------
        private readonly List<Point> _points = new List<Point>();
        private readonly List<Segment> _segments = new List<Segment>();

        // -----------------------------
        // Construction
        // -----------------------------
        public TwoDGeometry() { }

        public TwoDGeometry(string geometryId) => GeometryID = geometryId;

        // -----------------------------
        // Identification
        // -----------------------------
        public string GeometryID { get; set; }

        // -----------------------------
        // Ownership
        // -----------------------------
        /// <summary>Owning CAD sketch, if any.</summary>
        //public CAD_Sketch? MyCAD_Sketch { get; set; }

        /// <summary>Optional working sketch reference (kept for compatibility).</summary>
        //public CAD_Sketch? MySketch { get; set; }

        /// <summary>Coordinate system used to interpret geometry.</summary>
        public CoordinateSystem MyCoordinateSystem { get; set; }

        // -----------------------------
        // Flags
        // -----------------------------
        /// <summary>True if the geometry forms a closed loop.</summary>
        public bool IsClosed { get; private set; }

        /// <summary>True if this is construction (reference) geometry.</summary>
        public bool IsConstructionGeometry { get; set; }

        // -----------------------------
        // Current cursors (optional)
        // -----------------------------
        public Point CurrentPoint { get; set; }
        public Point NextPoint { get; set; }
        public Point PreviousPoint { get; set; }
        public Point CenterPoint { get; set; }

        public Segment CurrentSegment { get; set; }
        public Segment NextSegment { get; set; }
        public Segment PreviousSegment { get; set; }

        // -----------------------------
        // Collections (read-only views)
        // -----------------------------
        public IReadOnlyList<Point> MyPoints => _points;
        public IReadOnlyList<Segment> MySegments => _segments;

        // -----------------------------
        // Mutators / helpers
        // -----------------------------
        public void AddPoint(Point point)
        {
            if (point is null) throw new ArgumentNullException(nameof(point));
            _points.Add(point);
            if (_points.Count == 1) CurrentPoint = point;
            PreviousPoint = _points.Count >= 2 ? _points[_points.Count - 2] : null;
            NextPoint = null;
        }

        public void AddSegment(Segment segment)
        {
            if (segment is null) throw new ArgumentNullException(nameof(segment));
            _segments.Add(segment);
            if (_segments.Count == 1) CurrentSegment = segment;
            PreviousSegment = _segments.Count >= 2 ? _segments[_segments.Count - 2] : null;
            NextSegment = null;
        }

        /// <summary>
        /// Attempts to mark the geometry as closed if the first and last points coincide within a tolerance.
        /// </summary>
        public bool TryClose(double tolerance = 1e-9)
        {
            if (_points.Count < 3) { IsClosed = false; return false; }

            var a = _points[0];
            var b = _points[_points.Count - 1];
            if (a is null || b is null) { IsClosed = false; return false; }

            var dx = a.X_Value - b.X_Value;
            var dy = a.Y_Value - b.Y_Value;
            var dz = a.Z_Value_Cartesian - b.Z_Value_Cartesian;

            IsClosed = (dx * dx + dy * dy + dz * dz) <= tolerance * tolerance;
            return IsClosed;
        }
    }
}

