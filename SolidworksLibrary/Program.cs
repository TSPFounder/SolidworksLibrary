using System;

namespace SolidworksLibrary
{
    public class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            SolidworksApp swApplication = SolidworksApp.Connect();
        }
    }
}
