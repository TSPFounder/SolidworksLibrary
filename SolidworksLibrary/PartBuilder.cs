using CAD;
using SwConst;
using System;
using System.Collections.Generic;

namespace SolidworksLibrary
{
    internal class PartBuilder
    {
        public PartBuilder(SldWorks.SldWorks swApp, int qty, double spacing,
            StationBuilder.CoordinateSystemType coordSystem, int axis)
        {
            Model = new SolidworksModel();
            Model.SwModelObject = CreateNewPart(swApp);
            SketchBuilder = new SketchBuilder(Model);
            StationBuilder = new StationBuilder(swApp, Model, qty, spacing, coordSystem, axis);

            StationBuilder.Build();

            StationSketchBuilders = new List<SketchBuilder>(StationBuilder.Stations.Count);
            foreach (CAD_Station station in StationBuilder.Stations)
            {
                string planeName = station.CurrentSketchPlane?.Path ?? station.CurrentSketchPlane?.Name;
                if (!string.IsNullOrEmpty(planeName))
                {
                    StationSketchBuilders.Add(new SketchBuilder(Model, planeName));
                }
            }
        }

        // -------------------------------------------
        // Part Creation
        // -------------------------------------------

        public SldWorks.PartDoc CreateNewPart(SldWorks.SldWorks swApp)
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

        public string GetPartTemplate(SldWorks.SldWorks swApp)
        {
            return swApp.GetUserPreferenceStringValue(
                (int)swUserPreferenceStringValue_e.swDefaultTemplatePart);
        }

        // -------------------------------------------
        // Station Sketch Access
        // -------------------------------------------

        /// <summary>
        /// Returns the <see cref="SketchBuilder"/> for the station at the given index.
        /// </summary>
        public SketchBuilder GetStationSketchBuilder(int stationIndex)
        {
            if (stationIndex < 0 || stationIndex >= StationSketchBuilders.Count)
                throw new ArgumentOutOfRangeException(nameof(stationIndex));

            return StationSketchBuilders[stationIndex];
        }

        // -------------------------------------------
        // Properties
        // -------------------------------------------

        public SolidworksModel Model { get; private set; }
        public StationBuilder StationBuilder { get; private set; }

        /// <summary>
        /// Default sketch builder targeting the base plane ("Front Plane").
        /// </summary>
        public SketchBuilder SketchBuilder { get; private set; }

        /// <summary>
        /// One <see cref="SketchBuilder"/> per station, each targeting
        /// that station's offset plane.
        /// </summary>
        public List<SketchBuilder> StationSketchBuilders { get; private set; }
    }
}
