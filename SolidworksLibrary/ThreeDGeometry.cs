using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
//

namespace Mathematics
{
    /// <summary>
    /// Represents lightweight 3-D geometry (points + segments), with ownership/context and flags.
    /// Exposes read-only views for collections; use helper mutators to modify contents.
    /// </summary>
    public class ThreeDGeometry
    {
        // -----------------------------
        // Types
        // -----------------------------
        public enum GeometryTypeEnum
        {
            Curve = 0,
            Helix,
            Prism,
            Sphere,
            Cylinder,
            Cone,
            Tetrahedron,
            Octahedron,
            Conic,
            Spline
        }

        // -----------------------------
        // Backing state
        // -----------------------------
        private readonly List<Point> _points = new List<Point>();
        private readonly List<Segment> _segments = new List<Segment>();

        private readonly ReadOnlyCollection<Point> _pointsView;
        private readonly ReadOnlyCollection<Segment> _segmentsView;

        // -----------------------------
        // Construction
        // -----------------------------
        public ThreeDGeometry()
        {
            _pointsView = _points.AsReadOnly();
            _segmentsView = _segments.AsReadOnly();
        }

        public ThreeDGeometry(string segmentId) : this()
        {
            SegmentID = segmentId;
        }

        // -----------------------------
        // Identification / metadata
        // -----------------------------
        /// <summary>Stable identifier for this geometry element.</summary>
        public string SegmentID { get; set; }

        /// <summary>Optional semantic type (e.g., Cylinder, Cone, Prism).</summary>
        public GeometryTypeEnum? GeometryType { get; set; }

        // -----------------------------
        // Flags
        // -----------------------------
        public bool IsClosed { get; private set; }
        public bool IsConstructionGeometry { get; set; }
        public bool IsSolid { get; set; }

        // -----------------------------
        // Ownership / context
        // -----------------------------
        /// <summary>Optional owning sketch, when the 3-D geometry is derived from a sketch.</summary>
        //public CAD_Sketch? MySketch { get; set; }

        /// <summary>Coordinate system used to interpret this geometry.</summary>
        public CoordinateSystem MyCoordinateSystem { get; set; }

        // -----------------------------
        // Cursors (optional)
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
        public IReadOnlyList<Point> MyPoints => _pointsView;
        public IReadOnlyList<Segment> MySegments => _segmentsView;

        // -----------------------------
        // Mutators / helpers
        // -----------------------------
        public ThreeDGeometry AddPoint(Point point)
        {
            if (point is null) throw new ArgumentNullException(nameof(point));
            _points.Add(point);

            if (_points.Count == 1) CurrentPoint = point;
            PreviousPoint = _points.Count >= 2 ? _points[_points.Count - 2] : null;
            NextPoint = null;

            return this;
        }

        public ThreeDGeometry AddPoints(IEnumerable<Point> points)
        {
            if (points is null) throw new ArgumentNullException(nameof(points));
            foreach (var p in points) AddPoint(p);
            return this;
        }

        public ThreeDGeometry AddSegment(Segment segment)
        {
            if (segment is null) throw new ArgumentNullException(nameof(segment));
            _segments.Add(segment);

            if (_segments.Count == 1) CurrentSegment = segment;
            PreviousSegment = _segments.Count >= 2 ? _segments[_segments.Count - 2] : null;
            NextSegment = null;

            return this;
        }

        public ThreeDGeometry AddSegments(IEnumerable<Segment> segments)
        {
            if (segments is null) throw new ArgumentNullException(nameof(segments));
            foreach (var s in segments) AddSegment(s);
            return this;
        }

        public ThreeDGeometry ClearPoints()
        {
            _points.Clear();
            CurrentPoint = NextPoint = PreviousPoint = CenterPoint = null;
            IsClosed = false;
            return this;
        }

        public ThreeDGeometry ClearSegments()
        {
            _segments.Clear();
            CurrentSegment = NextSegment = PreviousSegment = null;
            IsClosed = false;
            return this;
        }

        /// <summary>
        /// Marks geometry as closed if first and last points coincide within tolerance (3-D distance).
        /// </summary>
        public bool TryClose(double tolerance = 1e-9)
        {
            if (_points.Count < 3) { IsClosed = false; return false; }

            var a = _points[0];
            var b = _points[_points.Count - 1]; // Replaced ^1 with explicit index for C# 7.3 compatibility

            var dx = a.X_Value - b.X_Value;
            var dy = a.Y_Value - b.Y_Value;
            var dz = a.Z_Value_Cartesian - b.Z_Value_Cartesian;

            IsClosed = (dx * dx + dy * dy + dz * dz) <= tolerance * tolerance;
            return IsClosed;
        }

        /// <summary>
        /// Ensures a closed loop by appending a duplicate of the first point when needed.
        /// </summary>
        public bool EnsureClosed(bool appendClosingPointIfNeeded = true, double tolerance = 1e-9)
        {
            if (TryClose(tolerance)) return true;
            if (!appendClosingPointIfNeeded || _points.Count == 0) return false;

            var first = _points[0];
            var closing = new Point
            {
                X_Value = first.X_Value,
                Y_Value = first.Y_Value,
                Z_Value_Cartesian = first.Z_Value_Cartesian
            };
            AddPoint(closing);
            return TryClose(tolerance);
        }

        /// <summary>
        /// Computes an axis-aligned 3-D bounding box of all points. Returns null if there are no points.
        /// </summary>
        public (double minX, double minY, double minZ, double maxX, double maxY, double maxZ)? GetBounds3D()
        {
            if (_points.Count == 0) return null;

            double minX = _points[0].X_Value, maxX = minX;
            double minY = _points[0].Y_Value, maxY = minY;
            double minZ = _points[0].Z_Value_Cartesian, maxZ = minZ;

            for (int i = 1; i < _points.Count; i++)
            {
                var p = _points[i];
                if (p.X_Value < minX) minX = p.X_Value;
                if (p.X_Value > maxX) maxX = p.X_Value;

                if (p.Y_Value < minY) minY = p.Y_Value;
                if (p.Y_Value > maxY) maxY = p.Y_Value;

                if (p.Z_Value_Cartesian < minZ) minZ = p.Z_Value_Cartesian;
                if (p.Z_Value_Cartesian > maxZ) maxZ = p.Z_Value_Cartesian;
            }

            return (minX, minY, minZ, maxX, maxY, maxZ);
        }
    }
}
