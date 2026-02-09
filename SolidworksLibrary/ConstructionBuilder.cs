using CAD;
using SldWorks;
using SwConst;
using System;
using System.Runtime.InteropServices;

namespace SolidworksLibrary
{
    public sealed class ConstructionBuilder
    {
        private readonly ModelDoc2 _modelDoc;
        public SketchManager _sketchManager;
        private readonly FeatureManager _featureManager;


        public ConstructionBuilder() { }
        public ConstructionBuilder(ModelDoc2 modelDoc)
        {
            _modelDoc = modelDoc ?? throw new ArgumentNullException(nameof(modelDoc));
            SldWorks.ModelDoc2 swModel = (SldWorks.ModelDoc2)_modelDoc;
            swModel.Extension.SelectByID2("Front Plane", "PLANE", 0, 0, 0, false, 0, null, 0);
            _sketchManager = swModel.SketchManager ?? throw new InvalidOperationException("SketchManager is required for construction geometry.");
            _featureManager = swModel.FeatureManager ?? throw new InvalidOperationException("FeatureManager is required for reference geometry.");
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

        public static SketchPoint CreateConstructionPoint(double x, double y, double z, SketchManager sketchManager)
        {
            return sketchManager.CreatePoint(x, y, z);
        }

        public static SketchLine CreateConstructionLine(double startX, double startY, double startZ, double endX, double endY, double endZ, SketchManager sketchManager)
        {
            var line = (SketchLine) sketchManager.CreateLine(startX, startY, startZ, endX, endY, endZ);
            MarkAsConstruction(line);
            return line;
        }

        public static SketchLine CreateConstructionCenterline(double startX, double startY, double startZ, double endX, double endY, double endZ, SketchManager sketchManager)
        {
            var centerLine = (SketchLine)sketchManager.CreateCenterLine(startX, startY, startZ, endX, endY, endZ);
            MarkAsConstruction(centerLine);
            return centerLine;
        }

        public static SketchArc CreateConstructionArc(double centerX, double centerY, double centerZ,
            double startX, double startY, double startZ,
            double endX, double endY, double endZ, SketchManager sketchManager, bool clockwise = true)
        {
            var arc = (SketchArc)sketchManager.CreateArc(centerX, centerY, centerZ, startX, startY, startZ,
                endX, endY, endZ, (short)(clockwise ? 1 : 0));
            MarkAsConstruction(arc);
            return arc;
        }



        /*
       
        public static SketchArc CreateConstructionCircle(double centerX, double centerY, double centerZ, double radius)
        {
            var sketchManager = ConstructionBuilder._sketchManager.;
            var circle = (SketchArc)sketchManager.CreateCircleByRadius(centerX, centerY, centerZ, radius);
            //MarkAsConstruction(circle);

            
            return circle;
        }
       
        */


        public static SketchSegment[] CreateConstructionSlot(double centerX, double centerY, double centerZ, double length, double width, SketchManager sketchManager)
        {
            if (length <= width)
            {
                throw new ArgumentException("Length must be greater than width to form a slot.", nameof(length));
            }

            double halfLength = length / 2.0;
            double halfWidth = width / 2.0;
            double leftX = centerX - halfLength + halfWidth;
            double rightX = centerX + halfLength - halfWidth;

            var bottomLine = CreateConstructionLine(leftX, centerY - halfWidth, centerZ, rightX, centerY - halfWidth, centerZ, sketchManager);
            var topLine = CreateConstructionLine(rightX, centerY + halfWidth, centerZ, leftX, centerY + halfWidth, centerZ, sketchManager);
            var leftArc = CreateConstructionArc(leftX, centerY, centerZ,
                leftX, centerY + halfWidth, centerZ,
                leftX, centerY - halfWidth, centerZ, sketchManager, false);
            var rightArc = CreateConstructionArc(rightX, centerY, centerZ,
                rightX, centerY - halfWidth, centerZ,
                rightX, centerY + halfWidth, centerZ, sketchManager, false);

            return new SketchSegment[] { (SketchSegment)bottomLine, (SketchSegment)rightArc, (SketchSegment)topLine, (SketchSegment)leftArc };
        }

        public static Feature CreateReferencePlane(swRefPlaneType_e planeType, double value1, int value2, double value3, int value4, double value5, FeatureManager featureManager)
        {
            return (Feature)featureManager.InsertRefPlane((int)planeType, value1, value2, value3, value4, value5);
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
