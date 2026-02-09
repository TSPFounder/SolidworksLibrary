
using Documents;
using Mathematics;
using Newtonsoft.Json;
using SE_Library;
using System;

namespace CAD
{
    public class CAD_Configuration
    {
        // -----------------------------
        // Constructor
        // -----------------------------
        public CAD_Configuration() { }

        // -----------------------------
        // Identification
        // -----------------------------
        public string Name { get; set; }
        public string Description { get; set; }
        public string ID { get; set; }
        public string Revision { get; set; }

        // -----------------------------
        // Owned & Owning Objects
        // -----------------------------
        public CAD_Part CurrentPart { get; set; }
        public SE_TableRow CurrentPartRow { get; set; }   // surfaced from original field
        public CAD_Assembly MyAssembly { get; set; }

        // -----------------------------
        // Diagnostics
        // -----------------------------
        public override string ToString()
            => $"CAD_Configuration(Name={Name ?? "<null>"}, ID={ID ?? "<null>"}, Rev={Revision ?? "<null>"})";

        // JSON Serialization
        public string ToJson() => JsonConvert.SerializeObject(this, Formatting.Indented,
            new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });
        public static CAD_Configuration FromJson(string json) => JsonConvert.DeserializeObject<CAD_Configuration>(json);
    }
}
