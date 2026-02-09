
using Mathematics;
using Newtonsoft.Json;
using SE_Library;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace CAD
{
    public class CAD_Interface
    {
        // -----------------------------
        // Types (unchanged)
        // -----------------------------
        public enum InterfaceType
        {
            Joint = 0,
            ElectricalConnector,
            Other
        }

        // -----------------------------
        // Constructor
        // -----------------------------
        public CAD_Interface()
        {
            MyContactPoints = new List<Mathematics.Point>();
            MyContactSurfaces = new List<CAD_Surface>();
        }

        // -----------------------------
        // Identification
        // -----------------------------
        public string Name { get; set; }
        public string ID { get; set; }
        public string Version { get; set; }

        // Optional: expose the enum as a property (not present in original API)
        public InterfaceType InterfaceKind { get; set; }

        // -----------------------------
        // Contact geometry
        // -----------------------------
        public Mathematics.Point CurrentContactPoint { get; set; }
        public List<Mathematics.Point> MyContactPoints { get; set; }

        public CAD_Surface CurrentContactSurface { get; set; }
        public List<CAD_Surface> MyContactSurfaces { get; set; }

        // -----------------------------
        // Associations
        // -----------------------------
        public CAD_Joint MyJoint { get; set; }
        public CAD_Component BaseComponent { get; set; }
        public CAD_Component MatingComponent { get; set; }

        // -----------------------------
        // Helpers (optional)
        // -----------------------------
        public void AddContactPoint(Mathematics.Point pt)
        {
            if (pt is null) throw new ArgumentNullException(nameof(pt));
            MyContactPoints.Add(pt);
            CurrentContactPoint = pt;
        }

        public void AddContactSurface(CAD_Surface surface)
        {
            if (surface is null) throw new ArgumentNullException(nameof(surface));
            MyContactSurfaces.Add(surface);
            CurrentContactSurface = surface;
        }

        public override string ToString()
            => $"CAD_Interface(Name={Name ?? "<null>"}," +
               $" Kind={(InterfaceKind.ToString() ?? "<unspecified>")}," +
               $" Points={MyContactPoints.Count}, Surfaces={MyContactSurfaces.Count})";

        // JSON Serialization
        public string ToJson() => JsonConvert.SerializeObject(this, Formatting.Indented,
            new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });
        public static CAD_Interface FromJson(string json) => JsonConvert.DeserializeObject<CAD_Interface>(json);
    }
}
