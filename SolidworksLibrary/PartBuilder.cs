using CAD;
using SolidWorks;
using SwConst;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using CAD;

namespace SolidworksLibrary
{
    internal class PartBuilder
    {
        public PartBuilder(SldWorks.SldWorks swApp)
        {
            myModel = new SW_Model();
            myModel.SwModelObject = createNewPart(swApp);
            mySketchBuilder = new SketchBuilder(myModel);
            myStationBuilder = new StationBuilder(myModel);

        }
        public SldWorks.PartDoc createNewPart(SldWorks.SldWorks swApp)
        {
            int errors = 0, warnings = 0;

            SldWorks.PartDoc swModel = null;

            //  Fetch the default template path from the system settings
            string partTemplate = swApp.GetUserPreferenceStringValue((int)swUserPreferenceStringValue_e.swDefaultTemplatePart);


            if (partTemplate != null)
            {
                object model = swApp.NewDocument(partTemplate, 0, 0, 0);
                if (model == null)
                {
                    Console.WriteLine("Failed to create document. Template not found at: " + partTemplate);
                }
                else
                {
                    Console.WriteLine("Created part document successfully.");
                    swModel = (SldWorks.PartDoc)model;

                }
            }

            return swModel;

        }

        public string getPartTemplate(SldWorks.SldWorks swApp)
        {
            return swApp.GetUserPreferenceStringValue((int)swUserPreferenceStringValue_e.swDefaultTemplatePart);
        }

        public SW_Model myModel { get; set; }
        public StationBuilder myStationBuilder { get; set; }
        public SketchBuilder mySketchBuilder { get; set; }

    }
}

