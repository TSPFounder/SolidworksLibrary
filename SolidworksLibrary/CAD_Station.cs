
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace CAD
{
    /// <summary>
    /// A design “station” (axial, radial, angular, or wing) used to place sketch planes and
    /// reference locations in a CAD model (e.g., fuselage stations, wing stations, frames).
    /// </summary>
    public sealed class CAD_Station
    {
        // -----------------------------
        // Types
        // -----------------------------
        public enum StationTypeEnum
        {
            Axial = 0,
            Radial,
            Angular,
            Wing,
            Other
        }

        // -----------------------------
        // Backing storage
        // -----------------------------
        private readonly List<CAD_SketchPlane> _sketchPlanes = new List<CAD_SketchPlane>();

        // -----------------------------
        // Construction
        // -----------------------------
        /*
        public CAD_Station(CAD_SketchPlane myPlane) {
            _sketchPlanes.Add(myPlane);
        }
        */

        public CAD_Station(CAD_SketchPlane myPlane,string id, StationTypeEnum type)
        {
            _sketchPlanes.Add(myPlane);
            ID = id;
            MyType = type;
        }


        
        public CAD_Station(string id, StationTypeEnum type, double value) //: this(id, type)
        {
            SetLocation(type, value);
        }
        


        // -----------------------------
        // Identity
        // -----------------------------
        /// <summary>Display name (optional).</summary>
        public string Name { get; set; }

        /// <summary>Unique identifier (optional but recommended).</summary>
        public string ID { get; set; }

        public string Version { get; set; }

        // -----------------------------
        // Station data
        // -----------------------------
        public StationTypeEnum MyType { get; set; } = StationTypeEnum.Axial;

        /// <summary>Axial location (e.g., along fuselage X), in model units.</summary>
        public double AxialLocation { get; private set; }

        /// <summary>Radial location (e.g., radius from axis), in model units.</summary>
        public double RadialLocation { get; private set; }

        /// <summary>Angular location (e.g., degrees about axis unless your model uses radians).</summary>
        public double AngularLocation { get; private set; }

        /// <summary>Wing station location (e.g., spanwise), in model units.</summary>
        public double WingLocation { get; private set; }

        /// <summary>Floor location (e.g., vertical height), in model units.</summary>
        public double FloorLocation { get; private set; }

        // -----------------------------
        // Ownership
        // -----------------------------
        /// <summary>Owning CAD model (if any).</summary>
        public CAD_Model MyModel { get; set; }

        /// <summary>The currently active sketch plane at this station, if one is selected.</summary>
        public CAD_SketchPlane CurrentSketchPlane { get; private set; }

        /// <summary>All sketch planes associated with this station.</summary>
        public IReadOnlyList<CAD_SketchPlane> MySketchPlanes => _sketchPlanes;

        // -----------------------------
        // Mutators / helpers
        // -----------------------------
        /// <summary>
        /// Sets the primary location value according to <paramref name="type"/>.
        /// Other location fields are left unchanged.
        /// </summary>
        public CAD_Station SetLocation(StationTypeEnum type, double value)
        {
            MyType = type;
            switch (type)
            {
                case StationTypeEnum.Axial: AxialLocation = value; break;
                case StationTypeEnum.Radial: RadialLocation = value; break;
                case StationTypeEnum.Angular: AngularLocation = value; break;
                case StationTypeEnum.Wing: WingLocation = value; break;
                case StationTypeEnum.Other:   /* no-op */              break;
                default: throw new ArgumentOutOfRangeException(nameof(type));
            }
            return this;
        }

        /// <summary>Adds a sketch plane and optionally makes it the current plane.</summary>
        public CAD_Station AddSketchPlane(CAD_SketchPlane plane, bool makeCurrent = true)
        {
            if (plane is null) throw new ArgumentNullException(nameof(plane));
            _sketchPlanes.Add(plane);
            if (makeCurrent) CurrentSketchPlane = plane;
            return this;
        }

        /// <summary>Sets the <see cref="CurrentSketchPlane"/> if it is already associated with this station.</summary>
        public bool TrySetCurrentSketchPlane(CAD_SketchPlane plane)
        {
            if (plane is null) return false;
            if (_sketchPlanes.Contains(plane))
            {
                CurrentSketchPlane = plane;
                return true;
            }
            return false;
        }

        /// <summary>Simple string for debugging/diagnostics.</summary>
        public override string ToString()
            => $"{MyType} Station (ID: {ID ?? "—"}, Axial={AxialLocation}, Radial={RadialLocation}, Angular={AngularLocation}, Wing={WingLocation})";

        // JSON Serialization
        public string ToJson() => JsonConvert.SerializeObject(this, Formatting.Indented,
            new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });
        public static CAD_Station FromJson(string json) => JsonConvert.DeserializeObject<CAD_Station>(json);
    }
}
