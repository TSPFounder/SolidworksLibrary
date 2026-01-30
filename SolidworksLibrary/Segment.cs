
using System;
using System.Collections.Generic;


using System.Drawing;
using System.Numerics;

namespace Mathematics
{
    public class Segment
    {
        // -----------------------------
        // Types (unchanged)
        // -----------------------------
        public enum SegmentTypeEnum
        {
            Line = 0,
            Arc,
            Circle,
            Quadratic,
            Spline
        }

        // -----------------------------
        // Constructor
        // -----------------------------
        public Segment()
        {
            MyPoints = new List<Point>();
            WeightPoints = new List<Point>();
            MyVectors = new List<Vector>();
            MyConnectedSegments = new List<Segment>();
        }

        // -----------------------------
        // Identification
        // -----------------------------
        public string SegmentID { get; set; }

        // -----------------------------
        // Segment classification
        // -----------------------------
        public SegmentTypeEnum SegmentType { get; set; } = SegmentTypeEnum.Line;

        // -----------------------------
        // Flags
        // -----------------------------
        public bool IsEdge { get; set; }

        // -----------------------------
        // Geometry – points
        // -----------------------------
        public Point StartPoint { get; set; }
        public Point EndPoint { get; set; }
        public Point MidPoint { get; set; }
        public Point FocalPoint1 { get; set; }
        public Point FocalPoint2 { get; set; }
        public Point Vertex { get; set; }

        public List<Point> MyPoints { get; set; }
        public List<Point> WeightPoints { get; set; }

        // -----------------------------
        //  Metrics
        // -----------------------------
        public double Length { get; set; }


        // -----------------------------
        // Geometry – vectors
        // -----------------------------
        public Vector CurrentVector { get; set; }
        public List<Vector> MyVectors { get; set; }

        // -----------------------------
        // Coordinate system / sketch context
        // -----------------------------
        public CoordinateSystem MyCoordinateSystem { get; set; }
        //public CAD_Sketch MySketch { get; set; }

        // -----------------------------
        // Connectivity
        // -----------------------------
        public Segment PreviousSegment { get; set; }
        public Segment NextSegment { get; set; }
        public Segment CurrentConnectedSegment { get; set; }
        public List<Segment> MyConnectedSegments { get; set; }

        // -----------------------------
        // Dimension (uses refactored CAD.Dimension)
        // -----------------------------
       // public Dimension CurrentDimension { get; set; }

        // -----------------------------
        // Helpers (optional)
        // -----------------------------
        public void AddPoint(Point pt)
        {
            if (pt is null) throw new ArgumentNullException(nameof(pt));
            MyPoints.Add(pt);
        }

        public void AddWeightPoint(Point pt)
        {
            if (pt is null) throw new ArgumentNullException(nameof(pt));
            WeightPoints.Add(pt);
        }

        public void ConnectTo(Segment seg)
        {
            if (seg is null) throw new ArgumentNullException(nameof(seg));
            MyConnectedSegments.Add(seg);
            CurrentConnectedSegment = seg;
        }

        public override string ToString()
            => $"Segment(ID={SegmentID ?? "<null>"}, Type={SegmentType}, Points={MyPoints.Count}, Connected={MyConnectedSegments.Count})";
    }
}

