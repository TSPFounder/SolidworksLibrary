using System;
using CAD;
using SldWorks;
using SwConst;

namespace SolidworksLibrary
{
    internal class SketchBuilder
    {
        public ModelDoc2 SwModelDoc { get; set; }
        public SolidworksModel MyModel { get; set; }
        public SketchManager SketchMgr { get; set; }
        public string PlaneName { get; set; }

        private bool _isBatching;

        public SketchBuilder(SolidworksModel model, string planeName = "Front Plane")
        {
            MyModel = model;
            SwModelDoc = (ModelDoc2)model.SwModelObject;
            SketchMgr = SwModelDoc.SketchManager;
            PlaneName = planeName;
        }

        // -------------------------------------------
        // Batch Mode
        // -------------------------------------------

        public void BeginBatch()
        {
            if (!_isBatching)
            {
                _isBatching = true;
                BeginSketch();
            }
        }

        public void EndBatch()
        {
            if (_isBatching)
            {
                _isBatching = false;
                EndSketch();
            }
        }

        // -------------------------------------------
        // Sketch Session Management
        // -------------------------------------------

        private void BeginSketch()
        {
            SwModelDoc.Extension.SelectByID2(PlaneName, "PLANE", 0, 0, 0, false, 0, null, 0);
            SketchMgr.InsertSketch(true);
        }

        private void EndSketch()
        {
            SketchMgr.InsertSketch(true);
        }

        // -------------------------------------------
        // Lines
        // -------------------------------------------

        public object CreateLine(double x1, double y1, double x2, double y2)
        {
            if (!_isBatching) BeginSketch();
            object result = SketchMgr.CreateLine(x1, y1, 0, x2, y2, 0);
            if (!_isBatching) EndSketch();
            return result;
        }

        public object CreateCenterLine(double x1, double y1, double x2, double y2)
        {
            if (!_isBatching) BeginSketch();
            object result = SketchMgr.CreateCenterLine(x1, y1, 0, x2, y2, 0);
            if (!_isBatching) EndSketch();
            return result;
        }

        // -------------------------------------------
        // Circles
        // -------------------------------------------

        public object CreateCircle(double centerX, double centerY, double radius)
        {
            if (!_isBatching) BeginSketch();
            object result = SketchMgr.CreateCircle(centerX, centerY, 0, centerX + radius, centerY, 0);
            if (!_isBatching) EndSketch();
            return result;
        }

        public object CreateCircleByRadius(double centerX, double centerY, double radius)
        {
            if (!_isBatching) BeginSketch();
            object result = SketchMgr.CreateCircleByRadius(centerX, centerY, 0, radius);
            if (!_isBatching) EndSketch();
            return result;
        }

        public object CreatePerimeterCircle(double x1, double y1, double x2, double y2, double x3, double y3)
        {
            if (!_isBatching) BeginSketch();
            object result = SketchMgr.PerimeterCircle(x1, y1, x2, y2, x3, y3);
            if (!_isBatching) EndSketch();
            return result;
        }

        // -------------------------------------------
        // Arcs
        // -------------------------------------------

        public object CreateArc(double centerX, double centerY,
            double startX, double startY, double endX, double endY, short direction)
        {
            if (!_isBatching) BeginSketch();
            object result = SketchMgr.CreateArc(centerX, centerY, 0,
                startX, startY, 0, endX, endY, 0, direction);
            if (!_isBatching) EndSketch();
            return result;
        }

        public object Create3PointArc(double startX, double startY,
            double endX, double endY, double midX, double midY)
        {
            if (!_isBatching) BeginSketch();
            object result = SketchMgr.Create3PointArc(startX, startY, 0,
                endX, endY, 0, midX, midY, 0);
            if (!_isBatching) EndSketch();
            return result;
        }

        public object CreateTangentArc(double startX, double startY,
            double endX, double endY, int arcType)
        {
            if (!_isBatching) BeginSketch();
            object result = SketchMgr.CreateTangentArc(startX, startY, 0,
                endX, endY, 0, arcType);
            if (!_isBatching) EndSketch();
            return result;
        }

        // -------------------------------------------
        // Ellipses
        // -------------------------------------------

        public object CreateEllipse(double centerX, double centerY,
            double majorX, double majorY, double minorX, double minorY)
        {
            if (!_isBatching) BeginSketch();
            object result = SketchMgr.CreateEllipse(centerX, centerY, 0,
                majorX, majorY, 0, minorX, minorY, 0);
            if (!_isBatching) EndSketch();
            return result;
        }

        public object CreateEllipticalArc(double centerX, double centerY,
            double majorX, double majorY, double minorX, double minorY,
            double startX, double startY, double endX, double endY,
            short direction)
        {
            if (!_isBatching) BeginSketch();
            object result = SketchMgr.CreateEllipticalArc(centerX, centerY, 0,
                majorX, majorY, 0, minorX, minorY, 0,
                startX, startY, 0, endX, endY, 0, direction);
            if (!_isBatching) EndSketch();
            return result;
        }

        // -------------------------------------------
        // Rectangles
        // -------------------------------------------

        public object CreateCornerRectangle(double x1, double y1, double x2, double y2)
        {
            if (!_isBatching) BeginSketch();
            object result = SketchMgr.CreateCornerRectangle(x1, y1, 0, x2, y2, 0);
            if (!_isBatching) EndSketch();
            return result;
        }

        public object CreateCenterRectangle(double centerX, double centerY,
            double cornerX, double cornerY)
        {
            if (!_isBatching) BeginSketch();
            object result = SketchMgr.CreateCenterRectangle(centerX, centerY, 0,
                cornerX, cornerY, 0);
            if (!_isBatching) EndSketch();
            return result;
        }

        public object Create3PointCornerRectangle(double x1, double y1,
            double x2, double y2, double x3, double y3)
        {
            if (!_isBatching) BeginSketch();
            object result = SketchMgr.Create3PointCornerRectangle(x1, y1, 0,
                x2, y2, 0, x3, y3, 0);
            if (!_isBatching) EndSketch();
            return result;
        }

        public object Create3PointCenterRectangle(double centerX, double centerY,
            double x2, double y2, double x3, double y3)
        {
            if (!_isBatching) BeginSketch();
            object result = SketchMgr.Create3PointCenterRectangle(centerX, centerY, 0,
                x2, y2, 0, x3, y3, 0);
            if (!_isBatching) EndSketch();
            return result;
        }

        public object CreateParallelogram(double x1, double y1,
            double x2, double y2, double x3, double y3)
        {
            if (!_isBatching) BeginSketch();
            object result = SketchMgr.CreateParallelogram(x1, y1, 0,
                x2, y2, 0, x3, y3, 0);
            if (!_isBatching) EndSketch();
            return result;
        }

        // -------------------------------------------
        // Polygon
        // -------------------------------------------

        public object CreatePolygon(double centerX, double centerY,
            double vertexX, double vertexY, int sides, bool inscribed)
        {
            if (!_isBatching) BeginSketch();
            object result = SketchMgr.CreatePolygon(centerX, centerY, 0,
                vertexX, vertexY, 0, sides, inscribed);
            if (!_isBatching) EndSketch();
            return result;
        }

        // -------------------------------------------
        // Slots
        // -------------------------------------------

        public object CreateStraightSlot(double x1, double y1, double x2, double y2, double width)
        {
            if (!_isBatching) BeginSketch();
            object result = SketchMgr.CreateSketchSlot(
                (int)swSketchSlotCreationType_e.swSketchSlotCreationType_line,
                (int)swSketchSlotLengthType_e.swSketchSlotLengthType_CenterCenter,
                width, x1, y1, 0, x2, y2, 0, 0, 0, 0, 1, false);
            if (!_isBatching) EndSketch();
            return result;
        }

        public object CreateCenterpointStraightSlot(double centerX, double centerY,
            double endX, double endY, double width)
        {
            if (!_isBatching) BeginSketch();
            object result = SketchMgr.CreateSketchSlot(
                (int)swSketchSlotCreationType_e.swSketchSlotCreationType_line,
                (int)swSketchSlotLengthType_e.swSketchSlotLengthType_FullLength,
                width, centerX, centerY, 0, endX, endY, 0, 0, 0, 0, 1, false);
            if (!_isBatching) EndSketch();
            return result;
        }

        public object Create3PointArcSlot(double x1, double y1,
            double x2, double y2, double x3, double y3, double width)
        {
            if (!_isBatching) BeginSketch();
            object result = SketchMgr.CreateSketchSlot(
                (int)swSketchSlotCreationType_e.swSketchSlotCreationType_3pointarc,
                (int)swSketchSlotLengthType_e.swSketchSlotLengthType_CenterCenter,
                width, x1, y1, 0, x2, y2, 0, x3, y3, 0, 1, false);
            if (!_isBatching) EndSketch();
            return result;
        }

        public object CreateCenterpointArcSlot(double centerX, double centerY,
            double startX, double startY, double endX, double endY, double width)
        {
            if (!_isBatching) BeginSketch();
            object result = SketchMgr.CreateSketchSlot(
                (int)swSketchSlotCreationType_e.swSketchSlotCreationType_arc,
                (int)swSketchSlotLengthType_e.swSketchSlotLengthType_CenterCenter,
                width, centerX, centerY, 0, startX, startY, 0, endX, endY, 0, 1, false);
            if (!_isBatching) EndSketch();
            return result;
        }

        // -------------------------------------------
        // Splines
        // -------------------------------------------

        public object CreateSpline(double[,] points)
        {
            if (!_isBatching) BeginSketch();
            int numPoints = points.GetLength(0);
            Array pointArray = new double[numPoints * 3];
            for (int i = 0; i < numPoints; i++)
            {
                ((double[])pointArray)[i * 3] = points[i, 0];
                ((double[])pointArray)[i * 3 + 1] = points[i, 1];
                ((double[])pointArray)[i * 3 + 2] = 0;
            }
            object result = SketchMgr.CreateSpline2(pointArray, false);
            if (!_isBatching) EndSketch();
            return result;
        }

        // -------------------------------------------
        // Parabola & Conic
        // -------------------------------------------

        public object CreateParabola(double focusX, double focusY,
            double apexX, double apexY, double startX, double startY,
            double endX, double endY)
        {
            if (!_isBatching) BeginSketch();
            object result = SketchMgr.CreateParabola(focusX, focusY, 0,
                apexX, apexY, 0, startX, startY, 0, endX, endY, 0);
            if (!_isBatching) EndSketch();
            return result;
        }

        public object CreateConic(double startX, double startY,
            double endX, double endY, double apexX, double apexY, double rho)
        {
            if (!_isBatching) BeginSketch();
            object result = SketchMgr.CreateConic(startX, startY, 0,
                endX, endY, 0, apexX, apexY, 0, rho, 0, 0);
            if (!_isBatching) EndSketch();
            return result;
        }

        // -------------------------------------------
        // Point
        // -------------------------------------------

        public object CreatePoint(double x, double y)
        {
            if (!_isBatching) BeginSketch();
            object result = SketchMgr.CreatePoint(x, y, 0);
            if (!_isBatching) EndSketch();
            return result;
        }

        // -------------------------------------------
        // Text
        // -------------------------------------------

        public object CreateText(string text, double x, double y,
            int fontHeight, int fontAngle, int centerAlign, int flip, int vFlip)
        {
            if (!_isBatching) BeginSketch();
            object result = SwModelDoc.InsertSketchText(x, y, 0, text,
                fontHeight, fontAngle, centerAlign, flip, vFlip);
            if (!_isBatching) EndSketch();
            return result;
        }

        // -------------------------------------------
        // Fillet & Chamfer
        // -------------------------------------------

        public object CreateFillet(double radius)
        {
            if (!_isBatching) BeginSketch();
            object result = SketchMgr.CreateFillet(radius, (int)swConstraintType_e.swConstraintType_TANGENT);
            if (!_isBatching) EndSketch();
            return result;
        }

        public object CreateChamfer(int type, double distance1, double distance2)
        {
            if (!_isBatching) BeginSketch();
            object result = SketchMgr.CreateChamfer(type, distance1, distance2);
            if (!_isBatching) EndSketch();
            return result;
        }

        // -------------------------------------------
        // Offset & Mirror
        // -------------------------------------------

        public bool CreateOffset(double offset, bool bothDirections, bool chain,
            int capEnds, int makeConstruction, bool addDimensions)
        {
            if (!_isBatching) BeginSketch();
            bool result = SketchMgr.SketchOffset2(offset, bothDirections, chain,
                capEnds, makeConstruction, addDimensions);
            if (!_isBatching) EndSketch();
            return result;
        }

        public void CreateMirror()
        {
            if (!_isBatching) BeginSketch();
            SwModelDoc.SketchMirror();
            if (!_isBatching) EndSketch();
        }

        // -------------------------------------------
        // Linear & Circular Pattern
        // -------------------------------------------

        public bool CreateLinearPattern(int numX, int numY,
            double spacingX, double spacingY, double angleX, double angleY,
            string deleteInstances, bool xSpacingDim, bool ySpacingDim,
            bool angleDim, bool createNumDimX, bool createNumDimY)
        {
            if (!_isBatching) BeginSketch();
            bool result = SketchMgr.CreateLinearSketchStepAndRepeat(
                numX, numY, spacingX, spacingY, angleX, angleY,
                deleteInstances, xSpacingDim, ySpacingDim, angleDim,
                createNumDimX, createNumDimY);
            if (!_isBatching) EndSketch();
            return result;
        }

        public object CreateCircularPattern(int count, double radius,
            double spacing, double arcAngle)
        {
            if (!_isBatching) BeginSketch();
            object result = SketchMgr.CreateCircularSketchStepAndRepeat(
                arcAngle, spacing, count, radius, true, "", true, true, true);
            if (!_isBatching) EndSketch();
            return result;
        }
    }
}
