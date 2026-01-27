using CAD;
using SwConst;
using System;

namespace SolidworksLibrary
{
    internal class PartBuilder
    {
        public PartBuilder(SldWorks.SldWorks swApp, int qty, double spacing, StationBuilder.CoordinateSystemType coordSystem, int axis)
        {
            Model = new SolidworksModel();
            Model.SwModelObject = CreateNewPart(swApp);
            SketchBuilder = new SketchBuilder(Model);
            StationBuilder = new StationBuilder(swApp, Model, qty, spacing, coordSystem, axis);
        }

        public SldWorks.PartDoc CreateNewPart(SldWorks.SldWorks swApp)
        {
            string partTemplate = swApp.GetUserPreferenceStringValue((int)swUserPreferenceStringValue_e.swDefaultTemplatePart);

            if (!string.IsNullOrEmpty(partTemplate))
            {
                object model = swApp.NewDocument(partTemplate, 0, 0, 0);
                if (model == null)
                {
                    Console.WriteLine("Failed to create document. Template not found at: " + partTemplate);
                }
                else
                {
                    return (SldWorks.PartDoc)model;
                }
            }

            return null;
        }

        public string GetPartTemplate(SldWorks.SldWorks swApp)
        {
            return swApp.GetUserPreferenceStringValue((int)swUserPreferenceStringValue_e.swDefaultTemplatePart);
        }

        public SolidworksModel Model { get; private set; }
        public StationBuilder StationBuilder { get; private set; }
        public SketchBuilder SketchBuilder { get; private set; }
    }
}
