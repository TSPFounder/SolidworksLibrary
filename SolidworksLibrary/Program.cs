using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SolidworksLibrary
{
    public class Program
    {
        

        public static void StartSolidworks() // Public static method
        {
             SldWorks.SldWorks swApp = null;

            // Try to connect to a running SOLIDWORKS instance
            try
            {
                swApp = (SldWorks.SldWorks)Marshal.GetActiveObject("SldWorks.Application");
            }
            catch
            {
                // If not running, start it
                swApp = new SldWorks.SldWorks();
                swApp.Visible = true;
            }

            // Example: create a new part
            int errors = 0, warnings = 0;

            object model = null;

            if (swApp != null)
            {
                // 2. Fetch the default template path from the system settings
                string partTemplate = swApp.GetUserPreferenceStringValue((int)swUserPreferenceStringValue_e.swDefaultTemplatePart);

                // 3. Use that specific path to create the document
                // Replacing: var model = swApp.NewDocument("", 0, 0, 0);
                model = swApp.NewDocument(partTemplate, 0, 0, 0);

                if (model == null)
                {
                    Console.WriteLine("Failed to create document. Template not found at: " + partTemplate);
                }
                else
                {
                    Console.WriteLine("Created document successfully.");
                }
            }

            if (model != null)
            {
                // 1. Get the ModelDoc2 interface from the model
                SldWorks.ModelDoc2 swModel = (SldWorks.ModelDoc2)model;

                // 2. Select the Front Plane (Standard name in most templates)
                swModel.Extension.SelectByID2("Front Plane", "PLANE", 0, 0, 0, false, 0, null, 0);

                // 3. Access the Sketch Manager
                SldWorks.SketchManager swSketchMgr = swModel.SketchManager;

                // 4. Start the Sketch
                swSketchMgr.InsertSketch(true);

                // 5. Draw a Circle (Center X, Center Y, Center Z, Radius X, Radius Y, Radius Z)
                // Units are in meters by default (0.05 = 50mm)
                swSketchMgr.CreateCircle(0, 0, 0, 0.05, 0, 0);

                // 6. Exit the sketch to save changes
                swSketchMgr.InsertSketch(true);

                Console.WriteLine("Sketch and circle created successfully.");
            }
        }
        static void Main(string[] args)
        {
            StartSolidworks();
        }
    }
    
}
