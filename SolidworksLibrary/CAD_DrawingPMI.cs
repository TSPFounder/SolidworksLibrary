namespace CAD
{
    /// <summary>
    /// Product & Manufacturing Information (PMI) placed on a drawing (2D or 3D).
    /// </summary>
    public sealed class CAD_DrawingPMI : CAD_DrawingElement
    {
        public enum PmiType
        {
            Gdt = 0,
            Welding,
            Hole,
            SurfaceFinish,
            Other
        }

        /// <summary>True if this PMI is attached in 3D context; otherwise 2D drawing-only.</summary>
        public bool Is3D { get; set; }

        /// <summary>Kind of PMI (GD&T, welding, etc.).</summary>
        public PmiType Type { get; set; } = PmiType.Other;

        public CAD_DrawingPMI()
        {
            MyType = DrawingElementType.PMI;
        }

        /// <summary>Create a 2D PMI of the given type.</summary>
        public static CAD_DrawingPMI Create2D(PmiType type) => new CAD_DrawingPMI { Is3D = false, Type = type };

        /// <summary>Create a 3D PMI of the given type.</summary>
        public static CAD_DrawingPMI Create3D(PmiType type) => new CAD_DrawingPMI { Is3D = true, Type = type };

        public override string ToString() => $"{(Is3D ? "3D" : "2D")} PMI ({Type})";
    }
}
