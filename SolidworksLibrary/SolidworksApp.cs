using CAD;
using CosmosWorksLib;
using SldWorks;
using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using SldWorks;
using CAD;
using SolidworksLibrary.Automation;


namespace SolidworksLibrary
{
    public sealed class SolidworksApp : IDisposable
    {
        // -----------------------------
        // Properties
        // -----------------------------
        /// <summary>
        /// Gets the SolidWorks application object.
        /// </summary>  
        /// 

        private SldWorks.SldWorks _SwApp;
        /// <summary>
        /// Creates a new SolidworksModel instance to hold the current model.
        /// </summary>
        private SolidworksModel _currentSw_Model;
        /// <summary>
        /// Creates a new AssemblyBuilder instance to hold the current assembly document.
        /// </summary>
        private AssemblyBuilder _currentAssyDoc;
        /// <summary>
        /// Creates a new PartBuilder instance to hold the current part document.
        /// </summary>
        private PartBuilder _currentPartDoc;
        /// <summary>
        /// Creates a new DrawingBuilder instance to hold the current drawing document.
        /// </summary>
        private DrawingBuilder _currentDrawingDoc;
        /// <summary>
        /// Creates a new FeatureBuilder instance to hold the current feature.
        /// </summary>
        private FeatureBuilder _currentFeatureDoc;
        private StationBuilder _currentStationBuilder;
        private ConstructionBuilder _currentConstructionBuilder;

        // -----------------------------
        // Current SolidworksDocuments
        // -----------------------------
        /// <summary>
        ///  Current Assembly Document
        /// </summary>
        private SldWorks.AssemblyDoc _currentAssyDocSw;
        /// <summary>
        ///  Current Model Document
        /// </summary>
        private SldWorks.ModelDoc2 _currentModel2Doc;
        /// <summary>
        ///  Current Part Document
        /// </summary>
        private SldWorks.PartDoc _currentPartDoc_Sw;
        /// <summary>
        /// Current Drawing Document
        /// </summary>
        private SldWorks.DrawingDoc _currentDrawingDoc_Sw;
        private SldWorks.FeatureManager _currentFeatureManager;
        private SldWorks.SketchManager _currentSketchManager;

        private SolidworksApp(SldWorks.SldWorks swApp)
        {
            _SwApp = swApp; //  throw new ArgumentNullException(nameof(swApp));
            _currentSw_Model= new SolidworksModel();
            _currentAssyDoc = new AssemblyBuilder();
            _currentPartDoc = new PartBuilder();
            _currentDrawingDoc = new DrawingBuilder();
            _currentFeatureDoc = new FeatureBuilder();
            //currentStationBuilder = new StationBuilder();
            //currentFeatureManager = swApp.ActiveDoc.FeatureManager;
            _currentSketchManager = swApp.ActiveDoc.SketchManager;


        }
        

        private static readonly ConcurrentDictionary<string, SolidworksApp> _sessions = new ConcurrentDictionary<string, SolidworksApp>();

        public static SolidworksApp Connect(bool visible)
        {
            SldWorks.SldWorks swApp = null;

            try
            {
                /*
                swApp = (SldWorks.SldWorks)Marshal.GetActiveObject("SldWorks.Application");
                SldWorks.ModelDoc2 currentModel2Doc = (SldWorks.ModelDoc2)swApp.ActiveDoc;
                */

                Type swType = Type.GetTypeFromProgID("SldWorks.Application");
                if (swType == null)
                {
                    throw new InvalidOperationException("Failed to get SOLIDWORKS application type.");
                }
                swApp = Activator.CreateInstance(swType) as SldWorks.SldWorks;
                if (swApp == null)
                {
                    throw new InvalidOperationException("Failed to create SOLIDWORKS application.");
                }

            }
            catch
            {
                swApp = new SldWorks.SldWorks();
                swApp.Visible = visible;
                
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
