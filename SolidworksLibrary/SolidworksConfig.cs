using System;
using System.Configuration;
using System.IO;

namespace SolidworksLibrary
{
    public sealed class SolidworksConfig
    {
        // Singleton pattern
        private static readonly Lazy<SolidworksConfig> _instance =
            new Lazy<SolidworksConfig>(() => new SolidworksConfig());

        public static SolidworksConfig Instance => _instance.Value;

        // Configuration values
        public string DatabasePath { get; set; }
        public string SqlSchemaFolder { get; set; }
        public string TemplatesFolder { get; set; }

        private SolidworksConfig()
        {
            // Priority: Environment Variable > App.config > Default
            DatabasePath = GetSetting("SW_DATABASE_PATH", "DatabasePath",
                Path.Combine(DataFolder, "SolidworksLibrary.db"));

            SqlSchemaFolder = GetSetting("SW_SCHEMA_FOLDER", "SqlSchemaFolder",
                Path.Combine(DataFolder, "Schemas"));

            TemplatesFolder = GetSetting("SW_TEMPLATES_FOLDER", "TemplatesFolder",
                Path.Combine(DataFolder, "Templates"));
        }

        private string DataFolder =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SolidworksLibrary");

        private static string GetSetting(string envVar, string appSettingKey, string defaultValue)
        {
            // 1. Check environment variable first
            var envValue = Environment.GetEnvironmentVariable(envVar);
            if (!string.IsNullOrEmpty(envValue))
                return envValue;

            // 2. Check App.config
            var appValue = ConfigurationManager.AppSettings[appSettingKey];
            if (!string.IsNullOrEmpty(appValue))
                return appValue;

            // 3. Return default
            return defaultValue;
        }
    }
}

