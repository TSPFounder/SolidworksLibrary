using System;
using System.Collections.Generic;
using System.Linq;
using CAD;

namespace SolidworksLibrary.Automation
{
    public sealed class SolidworksParameterSink : ICadParameterSink
    {
        public BridgeSyncResult ApplyParameters(SolidworksModel solidworksModel, IEnumerable<CAD_Parameter> parameters, BridgeSyncOptions options)
        {
            if (solidworksModel is null) throw new ArgumentNullException(nameof(solidworksModel));
            if (parameters is null) throw new ArgumentNullException(nameof(parameters));
            options ??= new BridgeSyncOptions();

            var result = new BridgeSyncResult();
            var targetPart = solidworksModel.GetOrCreateTargetPart(options.TargetPartName);

            foreach (var parameter in parameters)
            {
                if (parameter is null)
                {
                    result.ParametersSkipped++;
                    continue;
                }

                var existing = targetPart.MyParameters.FirstOrDefault(p => string.Equals(p.Name, parameter.Name, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    if (options.OverwriteExisting)
                    {
                        existing.Value = parameter.Value;
                        existing.MyParameterType = parameter.MyParameterType;
                        existing.Description = parameter.Description ?? existing.Description;
                        result.ParametersUpdated++;
                    }
                    else
                    {
                        result.ParametersSkipped++;
                    }

                    continue;
                }

                if (options.AllowNewParameters)
                {
                    targetPart.AddParameter(parameter);
                    result.ParametersApplied++;
                }
                else
                {
                    result.ParametersSkipped++;
                }
            }

            return result;
        }
    }
}
