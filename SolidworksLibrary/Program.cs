using CAD;
using SolidWorks;
using SwConst;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace SolidworksLibrary
{
    public class Program
    {


        //
        // Method to start or connect to SOLIDWORKS
        // Returns the SldWorks application object
        //
        
         
        public static SldWorks.SldWorks StartSolidworks() // Public static method
        {
            //  Create the SldWorks application object
            SldWorks.SldWorks swApp = null;            

            // Try to connect to a running SOLIDWORKS instance
            try
            {
                swApp = (SldWorks.SldWorks)Marshal.GetActiveObject("SldWorks.Application");
            }
            catch
            {
                // If not running, start it
                swApp = new SldWorks.SldWorks();
                swApp.Visible = true;
            }

            

            return swApp;
        }
        

        
        public static SolidworksApp createSW_App()
        {
            //  Create the SldWorks application object
            SldWorks.SldWorks swApp = null;

            // Try to connect to a running SOLIDWORKS instance
            try
            {
                swApp = (SldWorks.SldWorks)Marshal.GetActiveObject("SldWorks.Application");
            }
            catch
            {
                // If not running, start it
                swApp = new SldWorks.SldWorks();
                swApp.Visible = true;
               
            }

            SolidworksApp myApp = new SolidworksApp();
            myApp.swApp = swApp;

            return myApp;
        }

        [STAThread]
        static void Main(string[] args)
        {
            SolidworksApp swApplication = createSW_App();






        }

        

    }
    
}
