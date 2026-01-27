using System;
using System.Runtime.InteropServices;
using SldWorks;

namespace SolidworksLibrary
{
    public class SolidworksApp
    {
        public SldWorks.SldWorks SwApp { get; private set; }

        private SolidworksApp(SldWorks.SldWorks swApp)
        {
            SwApp = swApp ?? throw new ArgumentNullException(nameof(swApp));
        }

        public static SolidworksApp Connect()
        {
            SldWorks.SldWorks swApp = null;

            try
            {
                swApp = (SldWorks.SldWorks)Marshal.GetActiveObject("SldWorks.Application");
            }
            catch
            {
                swApp = new SldWorks.SldWorks();
                swApp.Visible = true;
            }

            return new SolidworksApp(swApp);
        }
    }
}
