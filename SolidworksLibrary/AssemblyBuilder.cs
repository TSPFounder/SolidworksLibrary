using System;
using System.Collections.Generic;
using SldWorks;
using SwConst;
using CAD;
using Mathematics;
using SolidworksLibrary.Automation;
using System.IO;
using System.Web.Script.Serialization;

namespace SolidworksLibrary
{
    internal class AssemblyBuilder 
    {
        private readonly SldWorks.SldWorks _swApp;
        private AssemblyDoc _assemblyDoc;
        private ModelDoc2 _modelDoc;
        private CAD_Assembly _myCAD_Assy;
        private readonly Dictionary<string, string> _componentAliasToName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // -------------------------------------------
        // Constructor
        // -------------------------------------------
        public AssemblyBuilder(SldWorks.SldWorks swApp) { 
            _swApp = swApp ?? throw new ArgumentNullException(nameof(swApp));
            _myCAD_Assy = new CAD_Assembly();
            CreateAssemblyDocument();
        }
        public AssemblyBuilder(SldWorks.SldWorks swApp, CoordinateSystem coordinateSystem,
            SolidworksModel basePart, SolidworksModel otherPart)
        {
            _swApp = swApp ?? throw new ArgumentNullException(nameof(swApp));

            if (basePart == null) throw new ArgumentNullException(nameof(basePart));
            if (otherPart == null) throw new ArgumentNullException(nameof(otherPart));

            _myCAD_Assy = new CAD_Assembly();
            

            // Create the assembly document
            CreateAssemblyDocument();

            // Set the assembly's coordinate system
            CoordinateSystem CurrentCS = coordinateSystem ?? new CoordinateSystem();
            _myCAD_Assy.AddCoordinateSystem(CurrentCS);

            

            // Add the base part (fixed at origin)
            var baseComponent = InsertComponent(basePart, true, 0, 0, 0);
            if (baseComponent != null)
            {
                //AddComponent(baseComponent);
            }

            // Add the other part (positioned relative to coordinate system)
            double offsetX = coordinateSystem?.OriginLocation?.X_Value ?? 0;
            double offsetY = coordinateSystem?.OriginLocation?.Y_Value ?? 0;
            double offsetZ = coordinateSystem?.OriginLocation?.Z_Value_Cartesian ?? 0;

            var otherComponent = InsertComponent(otherPart, false, offsetX, offsetY, offsetZ);
            if (otherComponent != null)
            {
                //AddComponent(otherComponent);
            }
        }

        // -------------------------------------------
        // Properties
        // -------------------------------------------

        public AssemblyDoc SwAssemblyDoc => _assemblyDoc;
        public ModelDoc2 SwModelDoc => _modelDoc;

        // -------------------------------------------
        // Assembly Document Creation
        // -------------------------------------------

        private void CreateAssemblyDocument()
        {
            string assemblyTemplate = _swApp.GetUserPreferenceStringValue(
                (int)swUserPreferenceStringValue_e.swDefaultTemplateAssembly);

            if (string.IsNullOrEmpty(assemblyTemplate))
            {
                throw new InvalidOperationException("Assembly template not found in SolidWorks settings.");
            }

            object model = _swApp.NewDocument(assemblyTemplate, 0, 0, 0);
            if (model == null)
            {
                throw new InvalidOperationException("Failed to create assembly document.");
            }

            _modelDoc = (ModelDoc2)model;
            _assemblyDoc = (AssemblyDoc)model;
        }

        // -------------------------------------------
        // Component Insertion
        // -------------------------------------------

        public CAD_Component InsertComponent(SolidworksModel partModel, bool fixedPosition,
            double x, double y, double z)
        {
            if (partModel?.SwModelObject == null)
            {
                Console.WriteLine("Invalid part model provided.");
                return null;
            }

            ModelDoc2 partDoc = (ModelDoc2)partModel.SwModelObject;
            string partPath = partDoc.GetPathName();

            if (string.IsNullOrEmpty(partPath))
            {
                Console.WriteLine("Part has no file path. Save the part first.");
                return null;
            }

            Component2 swComponent = _assemblyDoc.AddComponent5(
                partPath,
                (int)swAddComponentConfigOptions_e.swAddComponentConfigOptions_CurrentSelectedConfig,
                "",
                false,
                "",
                x, y, z);

            if (swComponent == null)
            {
                Console.WriteLine($"Failed to insert component: {partPath}");
                return null;
            }

            if (fixedPosition)
            {
                swComponent.Select4(false, null, false);
                _assemblyDoc.FixComponent();
            }

            var cadComponent = new CAD_Component
            {
                Name = swComponent.Name2,
                Path = partPath,
                IsAssembly = false,
                //MyPart = (CAD_Part)partModel.MyCADModel?.CurrentPart
            };

            _myCAD_Assy.AddComponent(cadComponent);
            return cadComponent;
        }

        public CAD_Component InsertComponent(string alias, SolidworksModel partModel, bool fixedPosition,
            double x, double y, double z)
        {
            var component = InsertComponent(partModel, fixedPosition, x, y, z);
            if (component != null && !string.IsNullOrWhiteSpace(alias))
            {
                _componentAliasToName[alias] = component.Name;
            }

            return component;
        }

        // -------------------------------------------
        // Component Insertion with Transform
        // -------------------------------------------

        public CAD_Component InsertComponentWithTransform(SolidworksModel partModel,
            CoordinateSystem localCS)
        {
            if (partModel?.SwModelObject == null || localCS == null)
            {
                return null;
            }

            double x = localCS.OriginLocation?.X_Value ?? 0;
            double y = localCS.OriginLocation?.Y_Value ?? 0;
            double z = localCS.OriginLocation?.Z_Value_Cartesian ?? 0;

            var component = InsertComponent(partModel, false, x, y, z);

            if (component != null && localCS.Vectors != null && localCS.Vectors.Count >= 2)
            {
                ApplyComponentTransform(component.Name, localCS);
            }

            return component;
        }

        public BridgeSyncResult BuildFromMatlabRequest(MatlabAssemblyRequest request, IDictionary<string, SolidworksModel> modelByAlias)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (modelByAlias == null) throw new ArgumentNullException(nameof(modelByAlias));

            var result = new BridgeSyncResult();
            if (!request.IsValid(out var reason))
            {
                result.Warnings.Add(reason);
                return result;
            }

            _componentAliasToName.Clear();

            foreach (var component in request.Components)
            {
                if (!modelByAlias.TryGetValue(component.Alias, out var model) || model == null)
                {
                    result.Warnings.Add($"No model found for alias '{component.Alias}'.");
                    result.ParametersSkipped++;
                    continue;
                }

                var created = InsertComponent(component.Alias, model, component.IsFixed, component.X, component.Y, component.Z);
                if (created == null)
                {
                    result.Warnings.Add($"Failed to insert alias '{component.Alias}'.");
                    result.ParametersSkipped++;
                }
                else
                {
                    result.ParametersApplied++;
                }
            }

            foreach (var mate in request.Mates)
            {
                if (!TryResolveAlias(mate.Component1Alias, out var comp1) || !TryResolveAlias(mate.Component2Alias, out var comp2))
                {
                    result.Warnings.Add($"Cannot resolve mate aliases '{mate.Component1Alias}' and '{mate.Component2Alias}'.");
                    result.ParametersSkipped++;
                    continue;
                }

                if (CreateComponentMate(comp1, comp2, mate.MateType, mate.Alignment, mate.Value))
                {
                    result.ParametersUpdated++;
                }
                else
                {
                    result.Warnings.Add($"Failed to create mate {mate.MateType} between '{comp1}' and '{comp2}'.");
                    result.ParametersSkipped++;
                }
            }

            return result;
        }

        public BridgeSyncResult BuildFromMatlabRequestFile(string requestFilePath, IDictionary<string, SolidworksModel> modelByAlias)
        {
            var request = MatlabAssemblyRequestFile.Load(requestFilePath);
            return BuildFromMatlabRequest(request, modelByAlias);
        }

        public void WriteMatlabRequestTemplate(string requestFilePath)
        {
            MatlabAssemblyRequestFile.SaveTemplate(requestFilePath);
        }

        public void ExportMatlabGuiTemplateScript(string scriptFilePath)
        {
            MatlabAssemblyScriptExporter.WriteTemplateScript(scriptFilePath);
        }

        public void ExportAssemblySummaryForMatlab(string filePath)
        {
            var lines = new List<string> { "ComponentName" };
            lines.AddRange(GetComponentNames());
            File.WriteAllLines(filePath, lines);
        }

        public string ExportCadAssemblyJson()
        {
            var serializer = new JavaScriptSerializer();
            return serializer.Serialize(CadAssemblyJsonModel.FromCadAssembly(_myCAD_Assy));
        }

        public void ExportCadAssemblyJsonFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("File path is required.", nameof(filePath));
            File.WriteAllText(filePath, ExportCadAssemblyJson());
        }

        public CAD_Assembly ImportCadAssemblyJson(string json, bool replaceCurrent = true)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("JSON payload is required.", nameof(json));

            var serializer = new JavaScriptSerializer();
            var model = serializer.Deserialize<CadAssemblyJsonModel>(json);
            if (model == null) throw new InvalidOperationException("Unable to deserialize CAD assembly JSON payload.");

            var importedAssembly = model.ToCadAssembly();
            if (replaceCurrent)
            {
                _myCAD_Assy = importedAssembly;
                _componentAliasToName.Clear();
                foreach (var component in _myCAD_Assy.MyComponents)
                {
                    if (!string.IsNullOrWhiteSpace(component?.Name))
                    {
                        _componentAliasToName[component.Name] = component.Name;
                    }
                }
            }

            return importedAssembly;
        }

        public CAD_Assembly ImportCadAssemblyJsonFile(string filePath, bool replaceCurrent = true)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("File path is required.", nameof(filePath));
            if (!File.Exists(filePath)) throw new FileNotFoundException("CAD assembly JSON file not found.", filePath);
            return ImportCadAssemblyJson(File.ReadAllText(filePath), replaceCurrent);
        }

        // -------------------------------------------
        // Transform Application
        // -------------------------------------------

        private void ApplyComponentTransform(string componentName, CoordinateSystem cs)
        {
            if (string.IsNullOrEmpty(componentName) || cs == null) return;

            _modelDoc.Extension.SelectByID2(componentName, "COMPONENT", 0, 0, 0, false, 0, null, 0);

            SelectionMgr selMgr = (SelectionMgr)_modelDoc.SelectionManager;
            Component2 comp = (Component2)selMgr.GetSelectedObject6(1, -1);

            if (comp == null) return;

            MathUtility mathUtil = (MathUtility)_swApp.GetMathUtility();

            double[] transformData = new double[16];

            Vector xAxis = cs.Vectors.Count > 0 ? cs.Vectors[0] : null;
            Vector yAxis = cs.Vectors.Count > 1 ? cs.Vectors[1] : null;
            Vector zAxis = cs.Vectors.Count > 2 ? cs.Vectors[2] : null;

            if (xAxis != null)
            {
                transformData[0] = xAxis.X_Value;
                transformData[1] = xAxis.Y_Value;
                transformData[2] = xAxis.Z_Value;
            }
            else
            {
                transformData[0] = 1; transformData[1] = 0; transformData[2] = 0;
            }

            if (yAxis != null)
            {
                transformData[3] = yAxis.X_Value;
                transformData[4] = yAxis.Y_Value;
                transformData[5] = yAxis.Z_Value;
            }
            else
            {
                transformData[3] = 0; transformData[4] = 1; transformData[5] = 0;
            }

            if (zAxis != null)
            {
                transformData[6] = zAxis.X_Value;
                transformData[7] = zAxis.Y_Value;
                transformData[8] = zAxis.Z_Value;
            }
            else
            {
                transformData[6] = 0; transformData[7] = 0; transformData[8] = 1;
            }

            transformData[9] = cs.OriginLocation?.X_Value ?? 0;
            transformData[10] = cs.OriginLocation?.Y_Value ?? 0;
            transformData[11] = cs.OriginLocation?.Z_Value_Cartesian ?? 0;
            transformData[12] = 1.0;
            transformData[13] = 0;
            transformData[14] = 0;
            transformData[15] = 0;

            MathTransform transform = (MathTransform)mathUtil.CreateTransform(transformData);
            comp.Transform2 = transform;

            _modelDoc.ClearSelection2(true);
        }

        // -------------------------------------------
        // Core Mate Helpers
        // -------------------------------------------

        private bool CreateComponentMate(string comp1Name, string comp2Name,
            swMateType_e mateType, swMateAlign_e alignment = swMateAlign_e.swMateAlignALIGNED,
            double value1 = 0, string entityType = "COMPONENT")
        {
            bool selected1 = _modelDoc.Extension.SelectByID2(
                comp1Name, entityType, 0, 0, 0, false, 1, null, 0);
            bool selected2 = _modelDoc.Extension.SelectByID2(
                comp2Name, entityType, 0, 0, 0, true, 1, null, 0);

            if (!selected1 || !selected2)
            {
                _modelDoc.ClearSelection2(true);
                return false;
            }

            int errors = 0;
            Mate2 mate = _assemblyDoc.AddMate5(
                (int)mateType,
                (int)alignment,
                false, value1, 0, 0, 0, 0, 0, 0, 0,
                false, false, 0, out errors);

            _modelDoc.ClearSelection2(true);
            return mate != null && errors == 0;
        }

        private bool CreateFaceMate(string comp1Name, string face1Name,
            string comp2Name, string face2Name,
            swMateType_e mateType, double value1 = 0)
        {
            string title = _modelDoc.GetTitle();
            bool selected1 = _modelDoc.Extension.SelectByID2(
                $"{face1Name}@{comp1Name}@{title}",
                "FACE", 0, 0, 0, false, 1, null, 0);
            bool selected2 = _modelDoc.Extension.SelectByID2(
                $"{face2Name}@{comp2Name}@{title}",
                "FACE", 0, 0, 0, true, 1, null, 0);

            if (!selected1 || !selected2)
            {
                _modelDoc.ClearSelection2(true);
                return false;
            }

            int errors = 0;
            Mate2 mate = _assemblyDoc.AddMate5(
                (int)mateType,
                (int)swMateAlign_e.swMateAlignALIGNED,
                false, value1, 0, 0, 0, 0, 0, 0, 0,
                false, false, 0, out errors);

            _modelDoc.ClearSelection2(true);
            return mate != null && errors == 0;
        }

        // -------------------------------------------
        // Face-Based Mate Methods
        // -------------------------------------------

        public bool CreateCoincidentMate(string component1Name, string face1Name,
            string component2Name, string face2Name)
        {
            return CreateFaceMate(component1Name, face1Name, component2Name, face2Name,
                swMateType_e.swMateCOINCIDENT);
        }

        public bool CreateConcentricMate(string component1Name, string face1Name,
            string component2Name, string face2Name)
        {
            return CreateFaceMate(component1Name, face1Name, component2Name, face2Name,
                swMateType_e.swMateCONCENTRIC);
        }

        public bool CreateDistanceMate(string component1Name, string face1Name,
            string component2Name, string face2Name, double distance)
        {
            return CreateFaceMate(component1Name, face1Name, component2Name, face2Name,
                swMateType_e.swMateDISTANCE, distance);
        }

        // -------------------------------------------
        // Joint Builder Dispatch
        // -------------------------------------------

        public CAD_Joint BuildJoint(SolidworksModel part1, SolidworksModel part2, CAD_Joint joint)
        {
            if (part1 == null) throw new ArgumentNullException(nameof(part1));
            if (part2 == null) throw new ArgumentNullException(nameof(part2));
            if (joint == null) throw new ArgumentNullException(nameof(joint));

            string comp1Name = GetComponentNameFromModel(part1);
            string comp2Name = GetComponentNameFromModel(part2);

            if (string.IsNullOrEmpty(comp1Name) || string.IsNullOrEmpty(comp2Name))
            {
                Console.WriteLine("Could not find component names for the provided parts.");
                return null;
            }

            bool success;
            switch (joint.JointType)
            {
                case CAD_Joint.JointTypeEnum.Rigid:
                    success = BuildRigidJoint(comp1Name, comp2Name, joint);
                    break;
                case CAD_Joint.JointTypeEnum.Revolute:
                    success = BuildRevoluteJoint(comp1Name, comp2Name, joint);
                    break;
                case CAD_Joint.JointTypeEnum.Slider:
                    success = BuildSliderJoint(comp1Name, comp2Name, joint);
                    break;
                case CAD_Joint.JointTypeEnum.Cylindrical:
                    success = BuildCylindricalJoint(comp1Name, comp2Name, joint);
                    break;
                case CAD_Joint.JointTypeEnum.PinSlot:
                    success = BuildPinSlotJoint(comp1Name, comp2Name, joint);
                    break;
                case CAD_Joint.JointTypeEnum.Planar:
                    success = BuildPlanarJoint(comp1Name, comp2Name, joint);
                    break;
                case CAD_Joint.JointTypeEnum.InPlane:
                    success = BuildPlanarJoint(comp1Name, comp2Name, joint);
                    break;
                case CAD_Joint.JointTypeEnum.Ball:
                    success = BuildBallJoint(comp1Name, comp2Name, joint);
                    break;
                case CAD_Joint.JointTypeEnum.LeadScrew:
                    success = BuildLeadScrewJoint(comp1Name, comp2Name, joint);
                    break;
                default:
                    success = BuildRigidJoint(comp1Name, comp2Name, joint);
                    break;
            }

            if (success)
            {
                Console.WriteLine($"Created {joint.JointType} joint between {comp1Name} and {comp2Name}");
            }

            return success ? joint : null;
        }

        // -------------------------------------------
        // Simple Joint Builders (one-liners via CreateComponentMate)
        // -------------------------------------------

        public bool BuildRigidJoint(string comp1Name, string comp2Name, CAD_Joint joint)
        {
            return CreateComponentMate(comp1Name, comp2Name, swMateType_e.swMateLOCK);
        }

        public bool BuildCylindricalJoint(string comp1Name, string comp2Name, CAD_Joint joint)
        {
            return CreateComponentMate(comp1Name, comp2Name, swMateType_e.swMateCONCENTRIC);
        }

        public bool BuildPinSlotJoint(string comp1Name, string comp2Name, CAD_Joint joint)
        {
            return CreateComponentMate(comp1Name, comp2Name, swMateType_e.swMateSLOT);
        }

        public bool BuildPlanarJoint(string comp1Name, string comp2Name, CAD_Joint joint)
        {
            return CreateComponentMate(comp1Name, comp2Name, swMateType_e.swMateCOINCIDENT);
        }

        public bool BuildLeadScrewJoint(string comp1Name, string comp2Name, CAD_Joint joint)
        {
            return CreateComponentMate(comp1Name, comp2Name, swMateType_e.swMateSCREW, value1: 0.01);
        }

        public bool BuildGearJoint(string comp1Name, string comp2Name, double ratio)
        {
            return CreateComponentMate(comp1Name, comp2Name, swMateType_e.swMateGEAR, value1: ratio);
        }

        public bool BuildRackPinionJoint(string comp1Name, string comp2Name, double pinionPitch)
        {
            return CreateComponentMate(comp1Name, comp2Name, swMateType_e.swMateRACKPINION, value1: pinionPitch);
        }

        public bool BuildCamJoint(string comp1Name, string comp2Name)
        {
            return CreateComponentMate(comp1Name, comp2Name, swMateType_e.swMateTANGENT);
        }

        public bool BuildTangentJoint(string comp1Name, string comp2Name)
        {
            return CreateComponentMate(comp1Name, comp2Name, swMateType_e.swMateTANGENT);
        }

        public bool BuildPerpendicularJoint(string comp1Name, string comp2Name)
        {
            return CreateComponentMate(comp1Name, comp2Name, swMateType_e.swMatePERPENDICULAR);
        }

        public bool BuildAngleJoint(string comp1Name, string comp2Name, double angleRadians)
        {
            return CreateComponentMate(comp1Name, comp2Name, swMateType_e.swMateANGLE, value1: angleRadians);
        }

        public bool BuildParallelJoint(string comp1Name, string comp2Name)
        {
            return CreateComponentMate(comp1Name, comp2Name, swMateType_e.swMatePARALLEL);
        }

        public bool BuildSymmetricJoint(string comp1Name, string comp2Name)
        {
            return CreateComponentMate(comp1Name, comp2Name, swMateType_e.swMateSYMMETRIC);
        }

        public bool BuildWidthJoint(string comp1Name, string comp2Name)
        {
            return CreateComponentMate(comp1Name, comp2Name, swMateType_e.swMateWIDTH);
        }

        public bool BuildPathJoint(string comp1Name, string comp2Name)
        {
            return CreateComponentMate(comp1Name, comp2Name, swMateType_e.swMatePATH);
        }

        public bool BuildLinearCouplerJoint(string comp1Name, string comp2Name, double ratio)
        {
            return CreateComponentMate(comp1Name, comp2Name, swMateType_e.swMateLINEARCOUPLER, value1: ratio);
        }

        // -------------------------------------------
        // Joint Builders with Fallback Logic
        // -------------------------------------------

        public bool BuildRevoluteJoint(string comp1Name, string comp2Name, CAD_Joint joint)
        {
            if (CreateComponentMate(comp1Name, comp2Name, swMateType_e.swMateHINGE))
                return true;

            // Fallback: try concentric mate if hinge fails
            return CreateComponentMate(comp1Name, comp2Name, swMateType_e.swMateCONCENTRIC);
        }

        public bool BuildSliderJoint(string comp1Name, string comp2Name, CAD_Joint joint)
        {
            if (CreateComponentMate(comp1Name, comp2Name, swMateType_e.swMateSLOT))
                return true;

            // Fallback: use parallel for linear constraint
            return CreateComponentMate(comp1Name, comp2Name, swMateType_e.swMatePARALLEL);
        }

        public bool BuildBallJoint(string comp1Name, string comp2Name, CAD_Joint joint)
        {
            if (CreateComponentMate(comp1Name, comp2Name, swMateType_e.swMateUNIVERSALJOINT))
                return true;

            // Fallback: point coincident for spherical constraint
            return CreateComponentMate(comp1Name, comp2Name, swMateType_e.swMateCOINCIDENT);
        }

        // -------------------------------------------
        // Helper Methods
        // -------------------------------------------

        private string GetComponentNameFromModel(SolidworksModel model)
        {
            if (model?.SwModelObject == null) return null;

            ModelDoc2 partDoc = (ModelDoc2)model.SwModelObject;
            string partPath = partDoc.GetPathName();

            object[] components = (object[])_assemblyDoc.GetComponents(true);
            if (components != null)
            {
                foreach (object obj in components)
                {
                    Component2 comp = (Component2)obj;
                    if (comp.GetPathName().Equals(partPath, StringComparison.OrdinalIgnoreCase))
                    {
                        return comp.Name2;
                    }
                }
            }

            return null;
        }

        private bool TryResolveAlias(string alias, out string componentName)
        {
            componentName = null;
            if (string.IsNullOrWhiteSpace(alias)) return false;

            if (_componentAliasToName.TryGetValue(alias, out var mapped) && !string.IsNullOrWhiteSpace(mapped))
            {
                componentName = mapped;
                return true;
            }

            componentName = alias;
            return true;
        }

        // -------------------------------------------
        // Component Operations
        // -------------------------------------------

        public void FixComponent(string componentName)
        {
            if (_modelDoc.Extension.SelectByID2(componentName, "COMPONENT", 0, 0, 0, false, 0, null, 0))
            {
                _assemblyDoc.FixComponent();
                _modelDoc.ClearSelection2(true);
            }
        }

        public void FloatComponent(string componentName)
        {
            if (_modelDoc.Extension.SelectByID2(componentName, "COMPONENT", 0, 0, 0, false, 0, null, 0))
            {
                _assemblyDoc.UnfixComponent();
                _modelDoc.ClearSelection2(true);
            }
        }

        public void MoveComponent(string componentName, double dx, double dy, double dz)
        {
            if (!_modelDoc.Extension.SelectByID2(componentName, "COMPONENT", 0, 0, 0, false, 0, null, 0))
                return;

            SelectionMgr selMgr = (SelectionMgr)_modelDoc.SelectionManager;
            Component2 comp = (Component2)selMgr.GetSelectedObject6(1, -1);

            if (comp == null) return;

            MathTransform currentTransform = comp.Transform2;
            MathUtility mathUtil = (MathUtility)_swApp.GetMathUtility();

            double[] translation = { 1, 0, 0, 0, 1, 0, 0, 0, 1, dx, dy, dz, 1, 0, 0, 0 };
            MathTransform translateTransform = (MathTransform)mathUtil.CreateTransform(translation);

            comp.Transform2 = (MathTransform)currentTransform.Multiply(translateTransform);
            _modelDoc.ClearSelection2(true);
        }

        // -------------------------------------------
        // Assembly Information
        // -------------------------------------------

        public List<string> GetComponentNames()
        {
            var names = new List<string>();
            object[] components = (object[])_assemblyDoc.GetComponents(true);

            if (components != null)
            {
                foreach (object obj in components)
                {
                    Component2 comp = (Component2)obj;
                    names.Add(comp.Name2);
                }
            }

            return names;
        }

        public int GetComponentCount()
        {
            object[] components = (object[])_assemblyDoc.GetComponents(true);
            return components?.Length ?? 0;
        }

        // -------------------------------------------
        // Save Operations
        // -------------------------------------------

        public bool SaveAssembly(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return false;

            int errors = 0, warnings = 0;
            bool result = _modelDoc.Extension.SaveAs(
                filePath,
                (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                (int)swSaveAsOptions_e.swSaveAsOptions_Silent,
                null, ref errors, ref warnings);

            if (!result)
            {
                Console.WriteLine($"Failed to save assembly. Errors: {errors}, Warnings: {warnings}");
            }

            return result;
        }

        // -------------------------------------------
        // Rebuild and Update
        // -------------------------------------------

        public void RebuildAssembly()
        {
            _modelDoc.ForceRebuild3(true);
        }

        public void ZoomToFit()
        {
            _modelDoc.ViewZoomtofit2();
        }

        private sealed class CadAssemblyJsonModel
        {
            public string Name { get; set; }
            public string Version { get; set; }
            public string Description { get; set; }
            public List<CadComponentJsonModel> Components { get; set; } = new List<CadComponentJsonModel>();

            public static CadAssemblyJsonModel FromCadAssembly(CAD_Assembly assembly)
            {
                var model = new CadAssemblyJsonModel();
                if (assembly == null)
                {
                    return model;
                }

                model.Name = assembly.Name;
                model.Version = assembly.Version;
                model.Description = assembly.Description;

                foreach (var component in assembly.MyComponents)
                {
                    model.Components.Add(new CadComponentJsonModel
                    {
                        Name = component?.Name,
                        Version = component?.Version,
                        Path = component?.Path,
                        IsAssembly = component?.IsAssembly ?? false,
                        IsConfigurationItem = component?.IsConfigurationItem ?? false
                    });
                }

                return model;
            }

            public CAD_Assembly ToCadAssembly()
            {
                var assembly = new CAD_Assembly
                {
                    Name = Name,
                    Version = Version,
                    Description = Description
                };

                if (Components != null)
                {
                    foreach (var component in Components)
                    {
                        assembly.AddComponent(new CAD_Component
                        {
                            Name = component?.Name,
                            Version = component?.Version,
                            Path = component?.Path,
                            IsAssembly = component?.IsAssembly ?? false,
                            IsConfigurationItem = component?.IsConfigurationItem ?? false
                        });
                    }
                }

                return assembly;
            }
        }

        private sealed class CadComponentJsonModel
        {
            public string Name { get; set; }
            public string Version { get; set; }
            public string Path { get; set; }
            public bool IsAssembly { get; set; }
            public bool IsConfigurationItem { get; set; }
        }
    }
}
