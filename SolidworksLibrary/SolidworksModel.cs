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
                return MyCADModel.CurrentPart;
            }

            if (!string.IsNullOrWhiteSpace(targetName))
            {
                var existing = MyCADModel.MyParts.Find(p => string.Equals(p.Name, targetName, StringComparison.OrdinalIgnoreCase));
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
