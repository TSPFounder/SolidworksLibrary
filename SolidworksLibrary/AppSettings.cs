using System;
using System.Configuration;
using System.IO;

namespace SolidworksLibrary
{
    public static class AppSettings
    {
        public static string DatabasePath =>
            ConfigurationManager.AppSettings["DatabasePath"]
            ?? Path.Combine(DefaultDataFolder, "SolidworksLibrary.db");

        public static string SqlSchemaFolder =>
            ConfigurationManager.AppSettings["SqlSchemaFolder"]
            ?? Path.Combine(DefaultDataFolder, "Schemas");

        private static string DefaultDataFolder =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SolidworksLibrary");
    }
}