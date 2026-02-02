using System;
using System.Runtime.InteropServices;
using SldWorks;
using CAD;
using SolidworksLibrary.Automation;

namespace SolidworksLibrary
{
    public class SolidworksApp
    {
        public SldWorks.SldWorks SwApp { get; private set; }
        public SolidworksModel currentSw_Model { get; private set; } = new SolidworksModel();
        public SldWorks.AssemblyDoc currentAssyDoc { get; private set; }
        public SldWorks.ModelDoc2 currentModel2Doc { get; private set; }

        private SolidworksApp(SldWorks.SldWorks swApp)
        {
            SwApp = swApp; //  throw new ArgumentNullException(nameof(swApp));
            currentSw_Model= new SolidworksModel();
        }

        public static SolidworksApp Connect()
        {
            SldWorks.SldWorks swApp = null;

            try
            {
                swApp = (SldWorks.SldWorks)Marshal.GetActiveObject("SldWorks.Application");
                SldWorks.ModelDoc2 currentModel2Doc = (SldWorks.ModelDoc2)swApp.ActiveDoc;
            }
            catch
            {
                swApp = new SldWorks.SldWorks();
                swApp.Visible = true;
            }

            return new SolidworksApp(swApp);
        }
        
        public void createModel() {
            currentSw_Model = new SolidworksModel();
            currentSw_Model.SwModelObject = SwApp.ActiveDoc;
        }

        public CadAutomationBridge CreateAutomationBridge(
            ISimulationAdapter simulationAdapter,
            IParameterMapper parameterMapper = null,
            ICadParameterSink parameterSink = null)
        {
            var mapper = parameterMapper ?? new ParameterMappingProfile();
            var sink = parameterSink ?? new SolidworksParameterSink();
            return new CadAutomationBridge(this, simulationAdapter, mapper, sink);
        }
       
    }
}
