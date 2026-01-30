//
using System;
using System.Collections.Generic;
using Mathematics;
using SE_Library;
using Documents;
//using MissionsNamespace;
using System.IO.Compression;

namespace CAD
{
    public class CAD_Model 
    {
        // -----------------------------
        // Enums (unchanged)
        // -----------------------------
        public enum CAD_ModelTypeEnum
        {
            Component = 0,
            Assembly,
            Drawing,
            Mesh,
            Body,
            Other
        }

        public enum CAD_AppEnum
        {
            Fusion360 = 0,
            Solidworks,
            Blender,
            UnReal4,
            UnReal5,
            Unity,
            Other
        }

        public enum CAD_FileTypeEnum
        {
            f3d = 0,
            f3z,
            sldprt,
            sldasm,
            slddrw,
            step,
            stl,
            sat,
            dxf,
            iges,
            fbx,
            obj,
            dae,
            x3d,
            wrl,
            other
        }

        // -----------------------------
        // Constructor
        // -----------------------------
        public CAD_Model()
        {
            // Base / system wiring retained
            //MySystem = mySystem;
            //MySystemModelType = ModelTypeEnum.CAD;

            // Collections
            MyStations = new List<CAD_Station>();
            MySketches = new List<CAD_Sketch>();
            MyFeatures = new List<CAD_Feature>();
            MyParts = new List<CAD_Part>();
            MyDrawings = new List<CAD_Drawing>();
            MyAssemblies = new List<CAD_Assembly>();
        }

        // -----------------------------
        // Properties (auto-properties)
        // -----------------------------
        // Identification / metadata
        public string Name { get; set; }
        public string Version { get; set; }
        public string Description { get; set; }
        public string FilePath { get; set; }

        // Enumerations
        public CAD_AppEnum CAD_AppName { get; set; }
        public CAD_ModelTypeEnum ModelType { get; set; }
        public CAD_FileTypeEnum FileType { get; set; }

        // Owned & Owning
        //public CAD_Manager MyCAD_Manager { get; set; }

        // Stations
        public CAD_Station CurrentStation { get; set; }
        public List<CAD_Station> MyStations { get; set; }

        // Sketches
        public CAD_Sketch CurrentSketch { get; set; }
        public List<CAD_Sketch> MySketches { get; set; }

        // Features
        public CAD_Feature CurrentFeature { get; set; }
        public List<CAD_Feature> MyFeatures { get; set; }

        // Parts
        public CAD_Part CurrentPart { get; set; }
        public List<CAD_Part> MyParts { get; set; }

        // Drawings
        public CAD_Drawing CurrentDrawing { get; set; }
        public List<CAD_Drawing> MyDrawings { get; set; }

        // Assemblies
        public CAD_Assembly CurrentAssembly { get; set; }
        public List<CAD_Assembly> MyAssemblies { get; set; }

        //  Systems
        public SE_System MySystem { get; set; }

        // CAD application models
        /*
        public AppFile Fusion360Model { get; set; }
        public AppFile SolidWorksModel { get; set; }
        public AppFile MechanicalDesktopModel { get; set; }
        public AppFile BlenderModel { get; set; }
        public AppFile UnityModel { get; set; }
        public AppFile UnrealEngine4Model { get; set; }
        public AppFile UnrealEngine5Model { get; set; }
        public AppFile OtherCAD_Model { get; set; }

        // Design tables
        public CAD_DesignTable SolidWorksDesignTable { get; set; }
        public CAD_DesignTable MechanicalDesktopDesignTable { get; set; }
        public CAD_DesignTable Fusion360DesignTable { get; set; }
        */

        // BoM
        public CAD_BoM MyBoM { get; set; }

        // -----------------------------
        // Methods
        // -----------------------------
        public bool CreateFusion360Model()
        {
            try
            {
                //Fusion360Model = new AppFile();
                return true;
            }
            catch
            {
                return false;
            }
        }

        // Small helpers (optional)
        public void AddStation(CAD_Station station)
        {
            if (station is null) throw new ArgumentNullException(nameof(station));
            MyStations.Add(station);
            CurrentStation = station;
        }

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

        public void AddPart(CAD_Part part)
        {
            if (part is null) throw new ArgumentNullException(nameof(part));
            MyParts.Add(part);
            CurrentPart = part;
        }

        public void AddDrawing(CAD_Drawing drawing)
        {
            if (drawing is null) throw new ArgumentNullException(nameof(drawing));
            MyDrawings.Add(drawing);
            CurrentDrawing = drawing;
        }

        public void AddAssembly(CAD_Assembly assembly)
        {
            if (assembly is null) throw new ArgumentNullException(nameof(assembly));
            MyAssemblies.Add(assembly);
            CurrentAssembly = assembly;
        }

        public override string ToString()
            => $"CAD_Model(Name={(Name ?? "<null>")}, App={CAD_AppName}, Type={ModelType}, FileType={FileType}, Parts={MyParts.Count}, Features={MyFeatures.Count})";
    }
}
