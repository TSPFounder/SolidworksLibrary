using System;
using System.Collections.Generic;
using System.Drawing;
using Mathematics;
using SE_Library;

namespace CAD
{
    public class CAD_Part
    {
        // -----------------------------
        // Constructor
        // -----------------------------
        public CAD_Part()
        {
            // Collections
            MySketches = new List<CAD_Sketch>();
            MyFeatures = new List<CAD_Feature>();
            MyBodies = new List<CAD_Body>();
            MyDrawings = new List<CAD_Drawing>();
            MyDimensions = new List<CAD_Dimension>();
            MyParameters = new List<CAD_Parameter>();
            MyModels = new List<CAD_Model>();
            MyOrigin = new CoordinateSystem();
            MyCoordinateSystems = new List<CoordinateSystem>();
            //MyLibraries = new List<CAD_Library>();
            MyInterfaces = new List<CAD_Interface>();
            //MyMaterials = new List<Material>();
            AxialStations = new List<CAD_Station>();
            RadialStations = new List<CAD_Station>();
            AngularStations = new List<CAD_Station>();
            WingStations = new List<CAD_Station>();
            MyMassPropertiesList = new List<MassProperties>();

            // Mass properties object commonly kept around
            MyMassProperties = new MassProperties();
            try
            {
                // Preserve original intent if this back-reference exists
                //MyMassProperties.MyCAD_Part = this;
            }
            catch
            {
                // Ignore if not supported by your MassProperties type
            }
        }

        // -----------------------------
        // Identification
        // -----------------------------
        public string Name { get; set; }
        public string Version { get; set; }
        public string PartNumber { get; set; }
        public string Description { get; set; }

        // -----------------------------
        // Mass properties & center of mass
        // -----------------------------
        public MassProperties CurrentMassProperties { get; set; }
        public List<MassProperties> MyMassPropertiesList { get; set; }
        public Mathematics.Point CenterOfMass { get; set; }
        public MassProperties MyMassProperties { get; set; }

        // -----------------------------
        // Models
        // -----------------------------
        public CAD_Model CurrentModel { get; set; }
        public List<CAD_Model> MyModels { get; set; }

        // -----------------------------
        // Coordinate systems
        // -----------------------------
        public CoordinateSystem MyOrigin { get; set; }
        public List<CoordinateSystem> MyCoordinateSystems { get; set; }

        // -----------------------------
        // Sketches
        // -----------------------------
        public CAD_Sketch CurrentSketch { get; set; }
        public List<CAD_Sketch> MySketches { get; set; }

        // -----------------------------
        // Features
        // -----------------------------
        public CAD_Feature CurrentFeature { get; set; }
        public List<CAD_Feature> MyFeatures { get; set; }

        // -----------------------------
        // Bodies
        // -----------------------------
        public CAD_Body CurrentBody { get; set; }
        public List<CAD_Body> MyBodies { get; set; }

        // -----------------------------
        // Drawings
        // -----------------------------
        public CAD_Drawing CurrentDrawing { get; set; }
        public List<CAD_Drawing> MyDrawings { get; set; }

        // -----------------------------
        // Dimensions
        // -----------------------------
        public CAD_Dimension CurrentDimension { get; set; }
        public List<CAD_Dimension> MyDimensions { get; set; }

        // -----------------------------
        // Parameters
        // -----------------------------
        public CAD_Parameter CurrentParameter { get; set; }
        public List<CAD_Parameter> MyParameters { get; set; }

        // -----------------------------
        // Assembly
        // -----------------------------
        public CAD_Assembly MyAssembly { get; set; }

        // -----------------------------
        // Materials
        // -----------------------------
        //public List<Material> MyMaterials { get; set; }

        // -----------------------------
        // CAD application
        // -----------------------------
       // public CAD_App CAD_Application { get; set; }

        // -----------------------------
        // Libraries
        // -----------------------------
        public CAD_Library CurrentLibrary { get; set; }
        public List<CAD_Library> MyLibraries { get; set; }

        // -----------------------------
        // Interfaces
        // -----------------------------
        public CAD_Interface CurrentInterface { get; set; }
        public List<CAD_Interface> MyInterfaces { get; set; }

        // -----------------------------
        // Stations
        // -----------------------------
        public List<CAD_Station> AxialStations { get; set; }
        public List<CAD_Station> RadialStations { get; set; }
        public List<CAD_Station> AngularStations { get; set; }
        public List<CAD_Station> WingStations { get; set; }

        // -----------------------------
        // Methods
        // -----------------------------
        /// <summary>
        /// Extrudes the current sketch (placeholder). Returns true on success.
        /// </summary>
        public bool ExtrudeSketch()
        {
            try
            {
                // Implementation placeholder
                return true;
            }
            catch
            {
                // Avoid UI here; let caller decide how to report errors
                return false;
            }
        }

        // -----------------------------
        // Tiny helpers (optional)
        // -----------------------------
        public void AddSketch(CAD_Sketch sketch)
        {
            if (sketch is null) throw new ArgumentNullException(nameof(sketch));
            MySketches.Add(sketch);
            CurrentSketch = sketch;
        }

        public void AddFeature(CAD_Feature feature)
        {
            if (feature is null) throw new ArgumentNullException(nameof(feature));
            MyFeatures.Add(feature);
            CurrentFeature = feature;
        }

        public void AddBody(CAD_Body body)
        {
            if (body is null) throw new ArgumentNullException(nameof(body));
            MyBodies.Add(body);
            CurrentBody = body;
        }

        public void AddDimension(CAD_Dimension dim)
        {
            if (dim is null) throw new ArgumentNullException(nameof(dim));
            MyDimensions.Add(dim);
            CurrentDimension = dim;
        }

        public void AddParameter(CAD_Parameter param)
        {
            if (param is null) throw new ArgumentNullException(nameof(param));
            MyParameters.Add(param);
            CurrentParameter = param;
        }

        public override string ToString()
            => $"CAD_Part(Name={Name ?? "<null>"}, PN={PartNumber ?? "<null>"}, Features={MyFeatures.Count}, Bodies={MyBodies.Count})";
    }
}

