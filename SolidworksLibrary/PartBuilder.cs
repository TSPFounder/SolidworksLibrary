using SwConst;
using System;
using System.Collections.Generic;
using CAD;

namespace SolidworksLibrary
{
    public class PartBuilder
    {
        public static SldWorks.PartDoc CreateNewPart(SldWorks.SldWorks swApp)
        {
            string partTemplate = swApp.GetUserPreferenceStringValue(
                (int)swUserPreferenceStringValue_e.swDefaultTemplatePart);

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

        public static string GetPartTemplate(SldWorks.SldWorks swApp)
        {
            return swApp.GetUserPreferenceStringValue(
                (int)swUserPreferenceStringValue_e.swDefaultTemplatePart);
        }

        public static SolidworksModel CreateModel(SldWorks.SldWorks swApp)
        {
            var model = new SolidworksModel();
            model.SwModelObject = CreateNewPart(swApp);
            return model;
        }

        public static List<CAD_Station> BuildStations(SldWorks.SldWorks swApp,
            SolidworksModel model, int qty, double spacing,
            StationBuilder.CoordinateSystemType coordSystem, int axis)
        {
            return StationBuilder.Build(swApp, model, qty, spacing, coordSystem, axis);
        }

        public static List<string> GetStationPlaneNames(List<CAD_Station> stations)
        {
            var planeNames = new List<string>(stations.Count);
            foreach (var station in stations)
            {
                string planeName = station.CurrentSketchPlane?.Path ?? station.CurrentSketchPlane?.Name;
                if (!string.IsNullOrEmpty(planeName))
                {
                    planeNames.Add(planeName);
                }
            }
            return planeNames;
        }
    }
}
