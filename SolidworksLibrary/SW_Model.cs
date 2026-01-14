using CAD;
using Mathematics;
using SolidWorks;
using SwConst;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static CAD.CAD_Model;

namespace SolidworksLibrary
{
    internal class SW_Model
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
        public object SwModelObject { get; set; }
        
        public SW_Model() {

            MyCADModel = new CAD_Model();
            Origin = new CoordinateSystem();
            MyCADModel.CAD_AppName = CAD_AppEnum.Solidworks;
        }

        
    }
}
