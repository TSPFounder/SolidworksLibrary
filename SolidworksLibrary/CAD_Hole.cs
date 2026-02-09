using System;
using System.Collections.Generic;
using Mathematics;
using Newtonsoft.Json;

namespace CAD
{
    public class CAD_Hole : CAD_Feature
    {
        public enum HoleGeometryTypeEnum
        {
            Straight = 0,
            CounterSink,
            CounterBore,
            Other
        }

        public CAD_Hole()
        {
            NominalDiameter = new CAD_Dimension { MyDimensionType = CAD_Dimension.DimensionType.Diameter };
            NominalDepth = new CAD_Dimension { MyDimensionType = CAD_Dimension.DimensionType.Length };
            NominalTaperAngle = new CAD_Dimension { MyDimensionType = CAD_Dimension.DimensionType.Angle };
            MyThreads = new List<Thread>();
            CenterPoint = new Point();
        }

        // General Dimensions
        public CAD_Dimension NominalDiameter { get; set; }
        public CAD_Dimension NominalDepth { get; set; }
        public CAD_Dimension NominalTaperAngle { get; set; }
        public Point CenterPoint { get; set; }

        // CounterSink
        public CAD_Dimension CounterSinkAngle { get; set; }
        public CAD_Dimension CounterSinkDepth { get; set; }

        // CounterBore
        public CAD_Dimension CounterBoreOuterDiameter { get; set; }
        public CAD_Dimension CounterBoreDepth { get; set; }

        // Keyway
        public bool HasKeyway { get; set; }
        public CAD_Feature MyKeyway { get; set; }

        // Threads
        public bool HasThreads { get; set; }
        public Thread CurrentThread { get; set; }
        public List<Thread> MyThreads { get; set; }

        // Associations
        public CAD_Feature MyFeature { get; set; }
        public CAD_Sketch MySketch { get; set; }

        // JSON Serialization
        public new string ToJson() => JsonConvert.SerializeObject(this, Formatting.Indented,
            new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });
        public static new CAD_Hole FromJson(string json) => JsonConvert.DeserializeObject<CAD_Hole>(json);
    }
}
