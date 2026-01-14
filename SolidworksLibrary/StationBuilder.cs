using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CAD;
using SldWorks;

namespace SolidworksLibrary
{
    internal class StationBuilder
    {
        public StationBuilder(SW_Model model) 
        {
            myModel = model;
        }

        public SW_Model myModel { get; set; }

    }
}
