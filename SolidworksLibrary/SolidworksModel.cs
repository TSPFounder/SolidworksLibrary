using CAD;
using Mathematics;
using System;

namespace SolidworksLibrary
{
    /// <summary>
    /// Wraps a SolidWorks model document (PartDoc or AssemblyDoc) and its
    /// associated CAD metadata.
    /// </summary>
    public class SolidworksModel
    {
        // -----------------------------
        // Enums
        // -----------------------------
        public enum ModelTypeEnum
        {
            Part = 0,
            Assembly,
            Drawing,
            Other
        }

        // -----------------------------
        // Model & CSYS
        // -----------------------------
        public CAD_Model MyCADModel { get; set; }
        public CoordinateSystem Origin { get; set; }
        public ModelTypeEnum Type { get; set; }

        /// <summary>
        /// The underlying SolidWorks COM object. Holds a <c>PartDoc</c> or <c>AssemblyDoc</c>.
        /// </summary>
        public object SwModelObject { get; set; }

        public SolidworksModel()
        {
            MyCADModel = new CAD_Model();
            Origin = new CoordinateSystem();
            MyCADModel.CAD_AppName = CAD_Model.CAD_AppEnum.Solidworks;
        }

        public CAD_Part GetOrCreateTargetPart(string targetName = null)
        {
            if (MyCADModel.CurrentPart != null)
            {
                // Only reuse the current part if no specific target name is requested,
                // or if the requested name matches the current part (case-insensitive).
                if (string.IsNullOrWhiteSpace(targetName) ||
                    string.Equals(MyCADModel.CurrentPart.Name, targetName, StringComparison.OrdinalIgnoreCase))
                {
                    return MyCADModel.CurrentPart;
                }
                return MyCADModel.CurrentPart;
            }

            if (!string.IsNullOrWhiteSpace(targetName))
            {
                var existing = MyCADModel.MyParts.Find(part => string.Equals(part.Name, targetName, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    MyCADModel.CurrentPart = existing;
                    return existing;
                }
            }

            var part = new CAD_Part { Name = string.IsNullOrWhiteSpace(targetName) ? "PrimaryPart" : targetName };
            MyCADModel.AddPart(part);
            return part;
        }
    }
}
