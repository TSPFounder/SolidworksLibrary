

using System;
using System.Collections.Generic;
using Mathematics;
using SE_Library;

namespace CAD
{
    public class CAD_Assembly // : CAD_Part
    {
        // -----------------------------
        // Constructor
        // -----------------------------
        public CAD_Assembly()
        {
            MyCoordinateSystems = new List<CoordinateSystem>();

            MyComponents = new List<CAD_Component>();
            MyConfigurations = new List<CAD_Configuration>();
            MissionRequirements = new List<MissionRequirement>();
            SystemRequirements = new List<SystemRequirement>();
            MyInterfaces = new List<CAD_Interface>();

            AxialStations = new List<CAD_Station>();
            RadialStations = new List<CAD_Station>();
            AngularStations = new List<CAD_Station>();
            WingStations = new List<CAD_Station>();
        }

        // -----------------------------
        // Identification
        // -----------------------------
        public string Name { get; set; }
        public string Version { get; set; }
        public string Description { get; set; }

        // -----------------------------
        // Flags
        // -----------------------------
        public bool IsSubAssembly { get; set; }
        public bool IsConfigurationItem { get; set; }

        // -----------------------------
        // Pose (position & orientation)
        // -----------------------------
        public Point MyPosition { get; set; }
        public Vector MyOrientation { get; set; }

        // -----------------------------
        // Coordinate systems
        // -----------------------------
        public CoordinateSystem CurrentCS { get; set; }
        public List<CoordinateSystem> MyCoordinateSystems { get; set; }

        // -----------------------------
        // Ownership / associations
        // -----------------------------
        //public DWM_System MySystem { get; set; }

        // -----------------------------
        // Components
        // -----------------------------
        public CAD_Component CurrentComponent { get; set; }
        public CAD_Component PreviousComponent { get; set; }
        public CAD_Component NextComponent { get; set; }
        public List<CAD_Component> MyComponents { get; set; }

        // -----------------------------
        // Model
        // -----------------------------
        public CAD_Model MyModel { get; set; }

        // -----------------------------
        // Configurations
        // -----------------------------
        public CAD_Configuration CurrentConfiguration { get; set; }
        public List<CAD_Configuration> MyConfigurations { get; set; }

        // -----------------------------
        // Requirements
        // -----------------------------
        public List<MissionRequirement> MissionRequirements { get; set; }
        public List<SystemRequirement> SystemRequirements { get; set; }

        // -----------------------------
        // Part (optional)
        // -----------------------------
        public CAD_Part MyPart { get; set; }

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
        // Helpers (optional)
        // -----------------------------
        public void AddComponent(CAD_Component component)
        {
            if (component is null) throw new ArgumentNullException(nameof(component));
            PreviousComponent = CurrentComponent;
            MyComponents.Add(component);
            CurrentComponent = component;
        }

        public void AddConfiguration(CAD_Configuration config)
        {
            if (config is null) throw new ArgumentNullException(nameof(config));
            MyConfigurations.Add(config);
            CurrentConfiguration = config;
        }

        public void AddInterface(CAD_Interface iface)
        {
            if (iface is null) throw new ArgumentNullException(nameof(iface));
            MyInterfaces.Add(iface);
            CurrentInterface = iface;
        }

        public void AddCoordinateSystem(CoordinateSystem cs)
        {
            if (cs is null) throw new ArgumentNullException(nameof(cs));
            MyCoordinateSystems.Add(cs);
            CurrentCS = cs;
        }

        public  string ToString()
            => $"CAD_Assembly(Name={Name ?? "<null>"}, SubAsm={IsSubAssembly}, Components={MyComponents.Count}, Configs={MyConfigurations.Count})";
    }
}


