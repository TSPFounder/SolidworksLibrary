using System;
using System.IO;

namespace SolidworksLibrary.Automation
{
    public static class MatlabAssemblyScriptExporter
    {
        public static void WriteTemplateScript(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("File path is required.", nameof(filePath));

            var script = string.Join(Environment.NewLine, new[]
            {
                "function requestFile = solidworksAssemblyRequestTemplate(outputFolder)",
                "% SOLIDWORKSASSEMBLYREQUESTTEMPLATE Writes a request file understood by AssemblyBuilder.",
                "if nargin == 0",
                "    outputFolder = pwd;",
                "end",
                "requestFile = fullfile(outputFolder, 'assembly_request.csv');",
                "fid = fopen(requestFile,'w');",
                "if fid < 0",
                "    error('Unable to create request file: %s', requestFile);",
                "end",
                "cleanup = onCleanup(@() fclose(fid));",
                "fprintf(fid, '# MATLAB GUI -> AssemblyBuilder bridge request\\n');",
                "fprintf(fid, 'ASSEMBLY,ExampleAssembly\\n');",
                "fprintf(fid, 'COMPONENT,Base,true,0,0,0\\n');",
                "fprintf(fid, 'COMPONENT,Actuator,false,0.1,0.0,0.02\\n');",
                "fprintf(fid, 'MATE,Base,Actuator,swMateCONCENTRIC,swMateAlignALIGNED,0\\n');",
                "end"
            });

            File.WriteAllText(filePath, script);
        }
    }
}
