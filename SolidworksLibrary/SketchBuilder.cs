using CAD;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CAD;
using SldWorks;

namespace SolidworksLibrary
{
    internal class SketchBuilder
    {
        public SketchBuilder(SW_Model model) {
            myModel = model;
            SldWorks.ModelDoc2 swModelDoc = (SldWorks.ModelDoc2)model;
            sketches = new List<CAD_Sketch>();
            sketchMgr = new SketchManager();
        }
        public SldWorks.ModelDoc2 swModelDoc { get; set; }
        public SW_Model myModel { get; set; }
        public List<CAD_Sketch> sketches { get; set; }
        public SketchManager sketchMgr { get; set; }

        public Sketch createCircleSketch(double centerX, double centerY, double radius)
        {
            // Select the Front Plane (Standard name in most templates)
            swModelDoc.Extension.SelectByID2("Front Plane", "PLANE", 0, 0, 0, false, 0, null, 0);

            // Start the Sketch
            sketchMgr.InsertSketch(true);

            // Draw a Circle (Center X, Center Y, Center Z, Radius X, Radius Y, Radius Z)
            // Units are in meters by default (0.05 = 50mm)
            Sketch sketch = (Sketch)sketchMgr.CreateCircle(centerX, centerY, 0, radius, 0, 0);

            // Exit the sketch to save changes
            sketchMgr.InsertSketch(true);

            return sketch;
        }
    }
}
