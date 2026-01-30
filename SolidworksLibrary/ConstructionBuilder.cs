using System;
using SldWorks;
using SwConst;
using CAD;

namespace SolidworksLibrary
{
    internal class ConstructionBuilder
    {
        private readonly ModelDoc2 _modelDoc;
        private readonly SketchManager _sketchManager;
        private readonly FeatureManager _featureManager;

        public ConstructionBuilder(ModelDoc2 modelDoc)
        {
            _modelDoc = modelDoc ?? throw new ArgumentNullException(nameof(modelDoc));
            _sketchManager = modelDoc.SketchManager ?? throw new InvalidOperationException("SketchManager is required for construction geometry.");
            _featureManager = modelDoc.FeatureManager ?? throw new InvalidOperationException("FeatureManager is required for reference geometry.");
        }

        public static void StartSketch(ModelDoc2 modelDoc, SketchManager sketchManager, string targetPlane = "Front Plane")
        {
            modelDoc.ClearSelection2(true);
            if (!string.IsNullOrWhiteSpace(targetPlane))
            {
                modelDoc.Extension.SelectByID2(targetPlane, "PLANE", 0, 0, 0, false, 0, null, 0);
            }

            sketchManager.InsertSketch(true);
        }

        public void EndSketch()
        {
            _sketchManager.InsertSketch(true);
        }

        public SketchPoint CreateConstructionPoint(double x, double y, double z)
        {
            return _sketchManager.CreatePoint(x, y, z);
        }

        public SketchLine CreateConstructionLine(double startX, double startY, double startZ, double endX, double endY, double endZ)
        {
            var line = (SketchLine)_sketchManager.CreateLine(startX, startY, startZ, endX, endY, endZ);
            MarkAsConstruction(line);
            return line;
        }

        public SketchLine CreateConstructionCenterline(double startX, double startY, double startZ, double endX, double endY, double endZ)
        {
            var centerLine = (SketchLine)_sketchManager.CreateCenterLine(startX, startY, startZ, endX, endY, endZ);
            MarkAsConstruction(centerLine);
            return centerLine;
        }

        public SketchArc CreateConstructionArc(double centerX, double centerY, double centerZ,
            double startX, double startY, double startZ,
            double endX, double endY, double endZ, bool clockwise = true)
        {
            var arc = (SketchArc)_sketchManager.CreateArc(centerX, centerY, centerZ, startX, startY, startZ,
                endX, endY, endZ, (short)(clockwise ? 1 : 0));
            MarkAsConstruction(arc);
            return arc;
        }
        
        public SketchArc CreateConstructionCircle(double centerX, double centerY, double centerZ, double radius)
        {
            var circle = (SketchArc)_sketchManager.CreateCircleByRadius(centerX, centerY, centerZ, radius);
            MarkAsConstruction(circle);
            return circle;
        }

        public SketchSegment[] CreateConstructionSlot(double centerX, double centerY, double centerZ, double length, double width)
        {
            if (length <= width)
            {
                throw new ArgumentException("Length must be greater than width to form a slot.", nameof(length));
            }

            double halfLength = length / 2.0;
            double halfWidth = width / 2.0;
            double leftX = centerX - halfLength + halfWidth;
            double rightX = centerX + halfLength - halfWidth;

            var bottomLine = CreateConstructionLine(leftX, centerY - halfWidth, centerZ, rightX, centerY - halfWidth, centerZ);
            var topLine = CreateConstructionLine(rightX, centerY + halfWidth, centerZ, leftX, centerY + halfWidth, centerZ);
            var leftArc = CreateConstructionArc(leftX, centerY, centerZ,
                leftX, centerY + halfWidth, centerZ,
                leftX, centerY - halfWidth, centerZ, false);
            var rightArc = CreateConstructionArc(rightX, centerY, centerZ,
                rightX, centerY - halfWidth, centerZ,
                rightX, centerY + halfWidth, centerZ, false);

            return new SketchSegment[] { (SketchSegment)bottomLine, (SketchSegment)rightArc, (SketchSegment)topLine, (SketchSegment)leftArc };
        }

        public Feature CreateReferencePlane(swRefPlaneType_e planeType, double value1, int value2, double value3, int value4, double value5)
        {
            return (Feature)_featureManager.InsertRefPlane((int)planeType, value1, value2, value3, value4, value5);
        }

        private static void MarkAsConstruction(object entity)
        {
            if (entity is SketchSegment segment)
            {
                segment.ConstructionGeometry = true;
            }
        }
    }
}
