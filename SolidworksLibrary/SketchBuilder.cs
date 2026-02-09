using System;
using CAD;
using SldWorks;
using SwConst;

namespace SolidworksLibrary
{
    public class SketchBuilder
    {
        // -------------------------------------------
        // Sketch Session Management
        // -------------------------------------------

        public static void BeginSketch(ModelDoc2 swModelDoc, string planeName)
        {
            swModelDoc.Extension.SelectByID2(planeName, "PLANE", 0, 0, 0, false, 0, null, 0);
            swModelDoc.SketchManager.InsertSketch(true);
        }

        public static void EndSketch(ModelDoc2 swModelDoc)
        {
            swModelDoc.SketchManager.InsertSketch(true);
        }

        // -------------------------------------------
        // Lines
        // -------------------------------------------

        public static object CreateLine(SketchManager sketchMgr, double x1, double y1, double x2, double y2)
        {
            return sketchMgr.CreateLine(x1, y1, 0, x2, y2, 0);
        }

        public static object CreateCenterLine(SketchManager sketchMgr, double x1, double y1, double x2, double y2)
        {
            return sketchMgr.CreateCenterLine(x1, y1, 0, x2, y2, 0);
        }

        // -------------------------------------------
        // Circles
        // -------------------------------------------

        public static object CreateCircle(SketchManager sketchMgr, double centerX, double centerY, double radius)
        {
            return sketchMgr.CreateCircle(centerX, centerY, 0, centerX + radius, centerY, 0);
        }

        public static object CreateCircleByRadius(SketchManager sketchMgr, double centerX, double centerY, double radius)
        {
            return sketchMgr.CreateCircleByRadius(centerX, centerY, 0, radius);
        }

        public static object CreatePerimeterCircle(SketchManager sketchMgr,
            double x1, double y1, double x2, double y2, double x3, double y3)
        {
            return sketchMgr.PerimeterCircle(x1, y1, x2, y2, x3, y3);
        }

        // -------------------------------------------
        // Arcs
        // -------------------------------------------

        public static object CreateArc(SketchManager sketchMgr, double centerX, double centerY,
            double startX, double startY, double endX, double endY, short direction)
        {
            return sketchMgr.CreateArc(centerX, centerY, 0,
                startX, startY, 0, endX, endY, 0, direction);
        }

        public static object Create3PointArc(SketchManager sketchMgr, double startX, double startY,
            double endX, double endY, double midX, double midY)
        {
            return sketchMgr.Create3PointArc(startX, startY, 0,
                endX, endY, 0, midX, midY, 0);
        }

        public static object CreateTangentArc(SketchManager sketchMgr, double startX, double startY,
            double endX, double endY, int arcType)
        {
            return sketchMgr.CreateTangentArc(startX, startY, 0,
                endX, endY, 0, arcType);
        }

        // -------------------------------------------
        // Ellipses
        // -------------------------------------------

        public static object CreateEllipse(SketchManager sketchMgr, double centerX, double centerY,
            double majorX, double majorY, double minorX, double minorY)
        {
            return sketchMgr.CreateEllipse(centerX, centerY, 0,
                majorX, majorY, 0, minorX, minorY, 0);
        }

        public static object CreateEllipticalArc(SketchManager sketchMgr, double centerX, double centerY,
            double majorX, double majorY, double minorX, double minorY,
            double startX, double startY, double endX, double endY,
            short direction)
        {
            return sketchMgr.CreateEllipticalArc(centerX, centerY, 0,
                majorX, majorY, 0, minorX, minorY, 0,
                startX, startY, 0, endX, endY, 0, direction);
        }

        // -------------------------------------------
        // Rectangles
        // -------------------------------------------

        public static object CreateCornerRectangle(SketchManager sketchMgr,
            double x1, double y1, double x2, double y2)
        {
            return sketchMgr.CreateCornerRectangle(x1, y1, 0, x2, y2, 0);
        }

        public static object CreateCenterRectangle(SketchManager sketchMgr,
            double centerX, double centerY, double cornerX, double cornerY)
        {
            return sketchMgr.CreateCenterRectangle(centerX, centerY, 0,
                cornerX, cornerY, 0);
        }

        public static object Create3PointCornerRectangle(SketchManager sketchMgr,
            double x1, double y1, double x2, double y2, double x3, double y3)
        {
            return sketchMgr.Create3PointCornerRectangle(x1, y1, 0,
                x2, y2, 0, x3, y3, 0);
        }

        public static object Create3PointCenterRectangle(SketchManager sketchMgr,
            double centerX, double centerY, double x2, double y2, double x3, double y3)
        {
            return sketchMgr.Create3PointCenterRectangle(centerX, centerY, 0,
                x2, y2, 0, x3, y3, 0);
        }

        public static object CreateParallelogram(SketchManager sketchMgr,
            double x1, double y1, double x2, double y2, double x3, double y3)
        {
            return sketchMgr.CreateParallelogram(x1, y1, 0,
                x2, y2, 0, x3, y3, 0);
        }

        // -------------------------------------------
        // Polygon
        // -------------------------------------------

        public static object CreatePolygon(SketchManager sketchMgr, double centerX, double centerY,
            double vertexX, double vertexY, int sides, bool inscribed)
        {
            return sketchMgr.CreatePolygon(centerX, centerY, 0,
                vertexX, vertexY, 0, sides, inscribed);
        }

        // -------------------------------------------
        // Slots
        // -------------------------------------------

        public static object CreateStraightSlot(SketchManager sketchMgr,
            double x1, double y1, double x2, double y2, double width)
        {
            return sketchMgr.CreateSketchSlot(
                (int)swSketchSlotCreationType_e.swSketchSlotCreationType_line,
                (int)swSketchSlotLengthType_e.swSketchSlotLengthType_CenterCenter,
                width, x1, y1, 0, x2, y2, 0, 0, 0, 0, 1, false);
        }

        public static object CreateCenterpointStraightSlot(SketchManager sketchMgr,
            double centerX, double centerY, double endX, double endY, double width)
        {
            return sketchMgr.CreateSketchSlot(
                (int)swSketchSlotCreationType_e.swSketchSlotCreationType_line,
                (int)swSketchSlotLengthType_e.swSketchSlotLengthType_FullLength,
                width, centerX, centerY, 0, endX, endY, 0, 0, 0, 0, 1, false);
        }

        public static object Create3PointArcSlot(SketchManager sketchMgr,
            double x1, double y1, double x2, double y2, double x3, double y3, double width)
        {
            return sketchMgr.CreateSketchSlot(
                (int)swSketchSlotCreationType_e.swSketchSlotCreationType_3pointarc,
                (int)swSketchSlotLengthType_e.swSketchSlotLengthType_CenterCenter,
                width, x1, y1, 0, x2, y2, 0, x3, y3, 0, 1, false);
        }

        public static object CreateCenterpointArcSlot(SketchManager sketchMgr,
            double centerX, double centerY, double startX, double startY,
            double endX, double endY, double width)
        {
            return sketchMgr.CreateSketchSlot(
                (int)swSketchSlotCreationType_e.swSketchSlotCreationType_arc,
                (int)swSketchSlotLengthType_e.swSketchSlotLengthType_CenterCenter,
                width, centerX, centerY, 0, startX, startY, 0, endX, endY, 0, 1, false);
        }

        // -------------------------------------------
        // Splines
        // -------------------------------------------

        public static object CreateSpline(SketchManager sketchMgr, double[,] points)
        {
            int numPoints = points.GetLength(0);
            Array pointArray = new double[numPoints * 3];
            for (int i = 0; i < numPoints; i++)
            {
                ((double[])pointArray)[i * 3] = points[i, 0];
                ((double[])pointArray)[i * 3 + 1] = points[i, 1];
                ((double[])pointArray)[i * 3 + 2] = 0;
            }
            return sketchMgr.CreateSpline2(pointArray, false);
        }

        // -------------------------------------------
        // Parabola & Conic
        // -------------------------------------------

        public static object CreateParabola(SketchManager sketchMgr, double focusX, double focusY,
            double apexX, double apexY, double startX, double startY,
            double endX, double endY)
        {
            return sketchMgr.CreateParabola(focusX, focusY, 0,
                apexX, apexY, 0, startX, startY, 0, endX, endY, 0);
        }

        public static object CreateConic(SketchManager sketchMgr, double startX, double startY,
            double endX, double endY, double apexX, double apexY, double rho)
        {
            return sketchMgr.CreateConic(startX, startY, 0,
                endX, endY, 0, apexX, apexY, 0, rho, 0, 0);
        }

        // -------------------------------------------
        // Point
        // -------------------------------------------

        public static object CreatePoint(SketchManager sketchMgr, double x, double y)
        {
            return sketchMgr.CreatePoint(x, y, 0);
        }

        // -------------------------------------------
        // Text
        // -------------------------------------------

        public static object CreateText(ModelDoc2 swModelDoc, string text, double x, double y,
            int fontHeight, int fontAngle, int centerAlign, int flip, int vFlip)
        {
            return swModelDoc.InsertSketchText(x, y, 0, text,
                fontHeight, fontAngle, centerAlign, flip, vFlip);
        }

        // -------------------------------------------
        // Fillet & Chamfer
        // -------------------------------------------

        public static object CreateFillet(SketchManager sketchMgr, double radius)
        {
            return sketchMgr.CreateFillet(radius, (int)swConstraintType_e.swConstraintType_TANGENT);
        }

        public static object CreateChamfer(SketchManager sketchMgr, int type, double distance1, double distance2)
        {
            return sketchMgr.CreateChamfer(type, distance1, distance2);
        }

        // -------------------------------------------
        // Offset & Mirror
        // -------------------------------------------

        public static bool CreateOffset(SketchManager sketchMgr, double offset, bool bothDirections, bool chain,
            int capEnds, int makeConstruction, bool addDimensions)
        {
            return sketchMgr.SketchOffset2(offset, bothDirections, chain,
                capEnds, makeConstruction, addDimensions);
        }

        public static void CreateMirror(ModelDoc2 swModelDoc)
        {
            swModelDoc.SketchMirror();
        }

        // -------------------------------------------
        // Linear & Circular Pattern
        // -------------------------------------------

        public static bool CreateLinearPattern(SketchManager sketchMgr, int numX, int numY,
            double spacingX, double spacingY, double angleX, double angleY,
            string deleteInstances, bool xSpacingDim, bool ySpacingDim,
            bool angleDim, bool createNumDimX, bool createNumDimY)
        {
            return sketchMgr.CreateLinearSketchStepAndRepeat(
                numX, numY, spacingX, spacingY, angleX, angleY,
                deleteInstances, xSpacingDim, ySpacingDim, angleDim,
                createNumDimX, createNumDimY);
        }

        public static object CreateCircularPattern(SketchManager sketchMgr, int count, double radius,
            double spacing, double arcAngle)
        {
            return sketchMgr.CreateCircularSketchStepAndRepeat(
                arcAngle, spacing, count, radius, true, "", true, true, true);
        }
    }
}
