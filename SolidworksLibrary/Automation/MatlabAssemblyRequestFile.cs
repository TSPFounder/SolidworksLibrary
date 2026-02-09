using System;
using System.Globalization;
using System.IO;
using SwConst;

namespace SolidworksLibrary.Automation
{
    public static class MatlabAssemblyRequestFile
    {
        public static MatlabAssemblyRequest Load(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("File path is required.", nameof(filePath));
            if (!File.Exists(filePath)) throw new FileNotFoundException("Matlab request file not found.", filePath);

            var request = new MatlabAssemblyRequest();
            var lineNumber = 0;

            foreach (var rawLine in File.ReadAllLines(filePath))
            {
                lineNumber++;
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                var cells = line.Split(',');
                var recordType = cells[0].Trim();

                if (recordType.Equals("ASSEMBLY", StringComparison.OrdinalIgnoreCase))
                {
                    request.AssemblyName = GetCell(cells, 1, lineNumber, "ASSEMBLY name");
                    continue;
                }

                if (recordType.Equals("COMPONENT", StringComparison.OrdinalIgnoreCase))
                {
                    request.Components.Add(new MatlabAssemblyComponentSpec
                    {
                        Alias = GetCell(cells, 1, lineNumber, "component alias"),
                        IsFixed = ParseBool(GetCell(cells, 2, lineNumber, "isFixed"), lineNumber),
                        X = ParseDouble(GetCell(cells, 3, lineNumber, "x"), lineNumber),
                        Y = ParseDouble(GetCell(cells, 4, lineNumber, "y"), lineNumber),
                        Z = ParseDouble(GetCell(cells, 5, lineNumber, "z"), lineNumber)
                    });
                    continue;
                }

                if (recordType.Equals("MATE", StringComparison.OrdinalIgnoreCase))
                {
                    request.Mates.Add(new MatlabAssemblyMateSpec
                    {
                        Component1Alias = GetCell(cells, 1, lineNumber, "component1 alias"),
                        Component2Alias = GetCell(cells, 2, lineNumber, "component2 alias"),
                        MateType = ParseMateType(GetCell(cells, 3, lineNumber, "mate type"), lineNumber),
                        Alignment = ParseMateAlign(GetCell(cells, 4, lineNumber, "mate alignment"), lineNumber),
                        Value = ParseDouble(GetCell(cells, 5, lineNumber, "mate value"), lineNumber)
                    });
                    continue;
                }

                throw new InvalidDataException($"Line {lineNumber}: Unsupported record type '{recordType}'.");
            }

            return request;
        }

        public static void SaveTemplate(string filePath)
        {
            var template = string.Join(Environment.NewLine, new[]
            {
                "# MATLAB GUI -> AssemblyBuilder bridge request",
                "ASSEMBLY,ExampleAssembly",
                "COMPONENT,Base,true,0,0,0",
                "COMPONENT,Actuator,false,0.1,0.0,0.02",
                "MATE,Base,Actuator,swMateCONCENTRIC,swMateAlignALIGNED,0"
            });

            File.WriteAllText(filePath, template);
        }

        private static string GetCell(string[] cells, int index, int lineNumber, string valueName)
        {
            if (index >= cells.Length || string.IsNullOrWhiteSpace(cells[index]))
            {
                throw new InvalidDataException($"Line {lineNumber}: Missing {valueName}.");
            }

            return cells[index].Trim();
        }

        private static bool ParseBool(string value, int lineNumber)
        {
            if (bool.TryParse(value, out var result)) return result;
            if (value == "1") return true;
            if (value == "0") return false;
            throw new InvalidDataException($"Line {lineNumber}: '{value}' is not a valid bool.");
        }

        private static double ParseDouble(string value, int lineNumber)
        {
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
            {
                return result;
            }

            throw new InvalidDataException($"Line {lineNumber}: '{value}' is not a valid number.");
        }

        private static swMateType_e ParseMateType(string value, int lineNumber)
        {
            if (Enum.TryParse(value, true, out swMateType_e parsed))
            {
                return parsed;
            }

            throw new InvalidDataException($"Line {lineNumber}: Unsupported mate type '{value}'.");
        }

        private static swMateAlign_e ParseMateAlign(string value, int lineNumber)
        {
            if (Enum.TryParse(value, true, out swMateAlign_e parsed))
            {
                return parsed;
            }

            throw new InvalidDataException($"Line {lineNumber}: Unsupported mate alignment '{value}'.");
        }
    }
}
