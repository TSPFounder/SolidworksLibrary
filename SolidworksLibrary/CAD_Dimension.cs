using System;
using System.Collections.Generic;
using Mathematics;
using SE_Library;
using Newtonsoft.Json;

namespace CAD
{
    public class CAD_Dimension : CAD_DrawingElement
    {
        public enum DimensionType
        {
            Length = 0,
            Diameter,
            Radius,
            Angle,
            Distance,
            Ordinal,
            Other
        }

        public CAD_Dimension()
        {
            MyType = DrawingElementType.Dimension;
            CenterPoint = new Point();
        }

        // Identification
        public string DimensionID { get; set; }
        public string Description { get; set; }
        public bool IsOrdinate { get; set; }

        // Geometry / Locating Points
        public Point CenterPoint { get; set; }
        public Point LeaderLineEndPoint { get; set; }
        public Point LeaderLineBendPoint { get; set; }
        public Point DimensionPoint { get; set; }
        public Point ReferencePoint { get; set; }

        // Associations
        public CAD_Model MyModel { get; set; }
        public Segment MySegment { get; set; }

        // Dimension Values
        public double DimensionNominalValue { get; set; }
        public double DimensionUpperLimitValue { get; set; }
        public double DimensionLowerLimitValue { get; set; }
        public DimensionType MyDimensionType { get; set; }
        public UnitOfMeasure EngineeringUnit { get; set; }

        // Parameters
        public CAD_Parameter CurrentParameter { get; set; }
        public List<CAD_Parameter> MyParameters { get; set; }

        // JSON Serialization
        public string ToJson() => JsonConvert.SerializeObject(this, Formatting.Indented,
            new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });
        public static CAD_Dimension FromJson(string json) => JsonConvert.DeserializeObject<CAD_Dimension>(json);
    }
}
