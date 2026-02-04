using System;
using System.Collections.Generic;
using SolidworksLibrary.Automation;

namespace SolidworksLibrary
{
    public class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            SolidworksApp swApplication = SolidworksApp.Connect();

            var adapter = new MatlabSimulinkSimscapeAdapter();
            var bridge = swApplication.CreateAutomationBridge(adapter);

            var simulationParameters = new List<SimulationParameter>
            {
                new SimulationParameter(\"WheelBase\", \"2.75\", \"m\") { Domain = SimulationDomain.Simulink, IsDesignVariable = true },
                new SimulationParameter(\"Mass\", \"1250\", \"kg\") { Domain = SimulationDomain.Simscape, IsDesignVariable = true },
                new SimulationParameter(\"MotorType\", \"PMSM\") { Domain = SimulationDomain.Matlab }
            };

            var captureRequest = new SimulationCaptureRequest(SimulationDomain.Simulink, \"VehicleModel\", simulationParameters)
            {
                SourcePath = \"VehicleModel.slx\"
            };

            var syncResult = bridge.SyncParameters(captureRequest);
            Console.WriteLine($\"Bridge sync complete: {syncResult}\");
        }
    }
}
