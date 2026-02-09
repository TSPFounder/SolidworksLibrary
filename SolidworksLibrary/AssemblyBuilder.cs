using System;
using System.Collections.Generic;
using SldWorks;
using SwConst;
using CAD;
using Mathematics;

namespace SolidworksLibrary
{
    public class AssemblyBuilder
    {
        // -------------------------------------------
        // Assembly Document Creation
        // -------------------------------------------

        public static AssemblyDoc CreateAssemblyDocument(SldWorks.SldWorks swApp, out ModelDoc2 modelDoc)
        {
            string assemblyTemplate = swApp.GetUserPreferenceStringValue(
                (int)swUserPreferenceStringValue_e.swDefaultTemplateAssembly);

            if (string.IsNullOrEmpty(assemblyTemplate))
            {
                throw new InvalidOperationException("Assembly template not found in SolidWorks settings.");
            }

            object model = swApp.NewDocument(assemblyTemplate, 0, 0, 0);
            if (model == null)
            {
                throw new InvalidOperationException("Failed to create assembly document.");
            }

            modelDoc = (ModelDoc2)model;
            return (AssemblyDoc)model;
        }

        public static AssemblyDoc CreateAssemblyWithComponents(SldWorks.SldWorks swApp,
            CoordinateSystem coordinateSystem, SolidworksModel basePart, SolidworksModel otherPart,
            out ModelDoc2 modelDoc)
        {
            if (basePart == null) throw new ArgumentNullException(nameof(basePart));
            if (otherPart == null) throw new ArgumentNullException(nameof(otherPart));

            var assemblyDoc = CreateAssemblyDocument(swApp, out modelDoc);

            var cadAssy = new CAD_Assembly();
            CoordinateSystem currentCS = coordinateSystem ?? new CoordinateSystem();
            cadAssy.AddCoordinateSystem(currentCS);

            InsertComponent(assemblyDoc, modelDoc, basePart, true, 0, 0, 0);

            double offsetX = coordinateSystem?.OriginLocation?.X_Value ?? 0;
            double offsetY = coordinateSystem?.OriginLocation?.Y_Value ?? 0;
            double offsetZ = coordinateSystem?.OriginLocation?.Z_Value_Cartesian ?? 0;

            InsertComponent(assemblyDoc, modelDoc, otherPart, false, offsetX, offsetY, offsetZ);

            return assemblyDoc;
        }

        // -------------------------------------------
        // Component Insertion
        // -------------------------------------------

        public static CAD_Component InsertComponent(AssemblyDoc assemblyDoc, ModelDoc2 modelDoc,
            SolidworksModel partModel, bool fixedPosition,
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

            Component2 swComponent = assemblyDoc.AddComponent5(
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
                assemblyDoc.FixComponent();
            }

            var cadComponent = new CAD_Component
            {
                Name = swComponent.Name2,
                Path = partPath,
                IsAssembly = false,
            };

            return cadComponent;
        }

        // -------------------------------------------
        // Component Insertion with Transform
        // -------------------------------------------

        public static CAD_Component InsertComponentWithTransform(SldWorks.SldWorks swApp,
            AssemblyDoc assemblyDoc, ModelDoc2 modelDoc,
            SolidworksModel partModel, CoordinateSystem localCS)
        {
            if (partModel?.SwModelObject == null || localCS == null)
            {
                return null;
            }

            double x = localCS.OriginLocation?.X_Value ?? 0;
            double y = localCS.OriginLocation?.Y_Value ?? 0;
            double z = localCS.OriginLocation?.Z_Value_Cartesian ?? 0;

            var component = InsertComponent(assemblyDoc, modelDoc, partModel, false, x, y, z);

            if (component != null && localCS.Vectors != null && localCS.Vectors.Count >= 2)
            {
                ApplyComponentTransform(swApp, modelDoc, component.Name, localCS);
            }

            return component;
        }

        // -------------------------------------------
        // Transform Application
        // -------------------------------------------

        public static void ApplyComponentTransform(SldWorks.SldWorks swApp,
            ModelDoc2 modelDoc, string componentName, CoordinateSystem cs)
        {
            if (string.IsNullOrEmpty(componentName) || cs == null) return;

            modelDoc.Extension.SelectByID2(componentName, "COMPONENT", 0, 0, 0, false, 0, null, 0);

            SelectionMgr selMgr = (SelectionMgr)modelDoc.SelectionManager;
            Component2 comp = (Component2)selMgr.GetSelectedObject6(1, -1);

            if (comp == null) return;

            MathUtility mathUtil = (MathUtility)swApp.GetMathUtility();

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

            modelDoc.ClearSelection2(true);
        }

        // -------------------------------------------
        // Core Mate Helpers
        // -------------------------------------------

        public static bool CreateComponentMate(AssemblyDoc assemblyDoc, ModelDoc2 modelDoc,
            string comp1Name, string comp2Name,
            swMateType_e mateType, swMateAlign_e alignment = swMateAlign_e.swMateAlignALIGNED,
            double value1 = 0, string entityType = "COMPONENT")
        {
            bool selected1 = modelDoc.Extension.SelectByID2(
                comp1Name, entityType, 0, 0, 0, false, 1, null, 0);
            bool selected2 = modelDoc.Extension.SelectByID2(
                comp2Name, entityType, 0, 0, 0, true, 1, null, 0);

            if (!selected1 || !selected2)
            {
                modelDoc.ClearSelection2(true);
                return false;
            }

            int errors = 0;
            Mate2 mate = assemblyDoc.AddMate5(
                (int)mateType,
                (int)alignment,
                false, value1, 0, 0, 0, 0, 0, 0, 0,
                false, false, 0, out errors);

            modelDoc.ClearSelection2(true);
            return mate != null && errors == 0;
        }

        public static bool CreateFaceMate(AssemblyDoc assemblyDoc, ModelDoc2 modelDoc,
            string comp1Name, string face1Name,
            string comp2Name, string face2Name,
            swMateType_e mateType, double value1 = 0)
        {
            string title = modelDoc.GetTitle();
            bool selected1 = modelDoc.Extension.SelectByID2(
                $"{face1Name}@{comp1Name}@{title}",
                "FACE", 0, 0, 0, false, 1, null, 0);
            bool selected2 = modelDoc.Extension.SelectByID2(
                $"{face2Name}@{comp2Name}@{title}",
                "FACE", 0, 0, 0, true, 1, null, 0);

            if (!selected1 || !selected2)
            {
                modelDoc.ClearSelection2(true);
                return false;
            }

            int errors = 0;
            Mate2 mate = assemblyDoc.AddMate5(
                (int)mateType,
                (int)swMateAlign_e.swMateAlignALIGNED,
                false, value1, 0, 0, 0, 0, 0, 0, 0,
                false, false, 0, out errors);

            modelDoc.ClearSelection2(true);
            return mate != null && errors == 0;
        }

        // -------------------------------------------
        // Face-Based Mate Methods
        // -------------------------------------------

        public static bool CreateCoincidentMate(AssemblyDoc assemblyDoc, ModelDoc2 modelDoc,
            string component1Name, string face1Name,
            string component2Name, string face2Name)
        {
            return CreateFaceMate(assemblyDoc, modelDoc, component1Name, face1Name,
                component2Name, face2Name, swMateType_e.swMateCOINCIDENT);
        }

        public static bool CreateConcentricMate(AssemblyDoc assemblyDoc, ModelDoc2 modelDoc,
            string component1Name, string face1Name,
            string component2Name, string face2Name)
        {
            return CreateFaceMate(assemblyDoc, modelDoc, component1Name, face1Name,
                component2Name, face2Name, swMateType_e.swMateCONCENTRIC);
        }

        public static bool CreateDistanceMate(AssemblyDoc assemblyDoc, ModelDoc2 modelDoc,
            string component1Name, string face1Name,
            string component2Name, string face2Name, double distance)
        {
            return CreateFaceMate(assemblyDoc, modelDoc, component1Name, face1Name,
                component2Name, face2Name, swMateType_e.swMateDISTANCE, distance);
        }

        // -------------------------------------------
        // Joint Builder Dispatch
        // -------------------------------------------

        public static CAD_Joint BuildJoint(SldWorks.SldWorks swApp, AssemblyDoc assemblyDoc,
            ModelDoc2 modelDoc, SolidworksModel part1, SolidworksModel part2, CAD_Joint joint)
        {
            if (part1 == null) throw new ArgumentNullException(nameof(part1));
            if (part2 == null) throw new ArgumentNullException(nameof(part2));
            if (joint == null) throw new ArgumentNullException(nameof(joint));

            string comp1Name = GetComponentNameFromModel(assemblyDoc, part1);
            string comp2Name = GetComponentNameFromModel(assemblyDoc, part2);

            if (string.IsNullOrEmpty(comp1Name) || string.IsNullOrEmpty(comp2Name))
            {
                Console.WriteLine("Could not find component names for the provided parts.");
                return null;
            }

            bool success;
            switch (joint.JointType)
            {
                case CAD_Joint.JointTypeEnum.Rigid:
                    success = BuildRigidJoint(assemblyDoc, modelDoc, comp1Name, comp2Name);
                    break;
                case CAD_Joint.JointTypeEnum.Revolute:
                    success = BuildRevoluteJoint(assemblyDoc, modelDoc, comp1Name, comp2Name);
                    break;
                case CAD_Joint.JointTypeEnum.Slider:
                    success = BuildSliderJoint(assemblyDoc, modelDoc, comp1Name, comp2Name);
                    break;
                case CAD_Joint.JointTypeEnum.Cylindrical:
                    success = BuildCylindricalJoint(assemblyDoc, modelDoc, comp1Name, comp2Name);
                    break;
                case CAD_Joint.JointTypeEnum.PinSlot:
                    success = BuildPinSlotJoint(assemblyDoc, modelDoc, comp1Name, comp2Name);
                    break;
                case CAD_Joint.JointTypeEnum.Planar:
                case CAD_Joint.JointTypeEnum.InPlane:
                    success = BuildPlanarJoint(assemblyDoc, modelDoc, comp1Name, comp2Name);
                    break;
                case CAD_Joint.JointTypeEnum.Ball:
                    success = BuildBallJoint(assemblyDoc, modelDoc, comp1Name, comp2Name);
                    break;
                case CAD_Joint.JointTypeEnum.LeadScrew:
                    success = BuildLeadScrewJoint(assemblyDoc, modelDoc, comp1Name, comp2Name);
                    break;
                default:
                    success = BuildRigidJoint(assemblyDoc, modelDoc, comp1Name, comp2Name);
                    break;
            }

            if (success)
            {
                Console.WriteLine($"Created {joint.JointType} joint between {comp1Name} and {comp2Name}");
            }

            return success ? joint : null;
        }

        // -------------------------------------------
        // Simple Joint Builders
        // -------------------------------------------

        public static bool BuildRigidJoint(AssemblyDoc assemblyDoc, ModelDoc2 modelDoc,
            string comp1Name, string comp2Name)
        {
            return CreateComponentMate(assemblyDoc, modelDoc, comp1Name, comp2Name, swMateType_e.swMateLOCK);
        }

        public static bool BuildCylindricalJoint(AssemblyDoc assemblyDoc, ModelDoc2 modelDoc,
            string comp1Name, string comp2Name)
        {
            return CreateComponentMate(assemblyDoc, modelDoc, comp1Name, comp2Name, swMateType_e.swMateCONCENTRIC);
        }

        public static bool BuildPinSlotJoint(AssemblyDoc assemblyDoc, ModelDoc2 modelDoc,
            string comp1Name, string comp2Name)
        {
            return CreateComponentMate(assemblyDoc, modelDoc, comp1Name, comp2Name, swMateType_e.swMateSLOT);
        }

        public static bool BuildPlanarJoint(AssemblyDoc assemblyDoc, ModelDoc2 modelDoc,
            string comp1Name, string comp2Name)
        {
            return CreateComponentMate(assemblyDoc, modelDoc, comp1Name, comp2Name, swMateType_e.swMateCOINCIDENT);
        }

        public static bool BuildLeadScrewJoint(AssemblyDoc assemblyDoc, ModelDoc2 modelDoc,
            string comp1Name, string comp2Name)
        {
            return CreateComponentMate(assemblyDoc, modelDoc, comp1Name, comp2Name, swMateType_e.swMateSCREW, value1: 0.01);
        }

        public static bool BuildGearJoint(AssemblyDoc assemblyDoc, ModelDoc2 modelDoc,
            string comp1Name, string comp2Name, double ratio)
        {
            return CreateComponentMate(assemblyDoc, modelDoc, comp1Name, comp2Name, swMateType_e.swMateGEAR, value1: ratio);
        }

        public static bool BuildRackPinionJoint(AssemblyDoc assemblyDoc, ModelDoc2 modelDoc,
            string comp1Name, string comp2Name, double pinionPitch)
        {
            return CreateComponentMate(assemblyDoc, modelDoc, comp1Name, comp2Name, swMateType_e.swMateRACKPINION, value1: pinionPitch);
        }

        public static bool BuildCamJoint(AssemblyDoc assemblyDoc, ModelDoc2 modelDoc,
            string comp1Name, string comp2Name)
        {
            return CreateComponentMate(assemblyDoc, modelDoc, comp1Name, comp2Name, swMateType_e.swMateTANGENT);
        }

        public static bool BuildTangentJoint(AssemblyDoc assemblyDoc, ModelDoc2 modelDoc,
            string comp1Name, string comp2Name)
        {
            return CreateComponentMate(assemblyDoc, modelDoc, comp1Name, comp2Name, swMateType_e.swMateTANGENT);
        }

        public static bool BuildPerpendicularJoint(AssemblyDoc assemblyDoc, ModelDoc2 modelDoc,
            string comp1Name, string comp2Name)
        {
            return CreateComponentMate(assemblyDoc, modelDoc, comp1Name, comp2Name, swMateType_e.swMatePERPENDICULAR);
        }

        public static bool BuildAngleJoint(AssemblyDoc assemblyDoc, ModelDoc2 modelDoc,
            string comp1Name, string comp2Name, double angleRadians)
        {
            return CreateComponentMate(assemblyDoc, modelDoc, comp1Name, comp2Name, swMateType_e.swMateANGLE, value1: angleRadians);
        }

        public static bool BuildParallelJoint(AssemblyDoc assemblyDoc, ModelDoc2 modelDoc,
            string comp1Name, string comp2Name)
        {
            return CreateComponentMate(assemblyDoc, modelDoc, comp1Name, comp2Name, swMateType_e.swMatePARALLEL);
        }

        public static bool BuildSymmetricJoint(AssemblyDoc assemblyDoc, ModelDoc2 modelDoc,
            string comp1Name, string comp2Name)
        {
            return CreateComponentMate(assemblyDoc, modelDoc, comp1Name, comp2Name, swMateType_e.swMateSYMMETRIC);
        }

        public static bool BuildWidthJoint(AssemblyDoc assemblyDoc, ModelDoc2 modelDoc,
            string comp1Name, string comp2Name)
        {
            return CreateComponentMate(assemblyDoc, modelDoc, comp1Name, comp2Name, swMateType_e.swMateWIDTH);
        }

        public static bool BuildPathJoint(AssemblyDoc assemblyDoc, ModelDoc2 modelDoc,
            string comp1Name, string comp2Name)
        {
            return CreateComponentMate(assemblyDoc, modelDoc, comp1Name, comp2Name, swMateType_e.swMatePATH);
        }

        public static bool BuildLinearCouplerJoint(AssemblyDoc assemblyDoc, ModelDoc2 modelDoc,
            string comp1Name, string comp2Name, double ratio)
        {
            return CreateComponentMate(assemblyDoc, modelDoc, comp1Name, comp2Name, swMateType_e.swMateLINEARCOUPLER, value1: ratio);
        }

        // -------------------------------------------
        // Joint Builders with Fallback Logic
        // -------------------------------------------

        public static bool BuildRevoluteJoint(AssemblyDoc assemblyDoc, ModelDoc2 modelDoc,
            string comp1Name, string comp2Name)
        {
            if (CreateComponentMate(assemblyDoc, modelDoc, comp1Name, comp2Name, swMateType_e.swMateHINGE))
                return true;

            return CreateComponentMate(assemblyDoc, modelDoc, comp1Name, comp2Name, swMateType_e.swMateCONCENTRIC);
        }

        public static bool BuildSliderJoint(AssemblyDoc assemblyDoc, ModelDoc2 modelDoc,
            string comp1Name, string comp2Name)
        {
            if (CreateComponentMate(assemblyDoc, modelDoc, comp1Name, comp2Name, swMateType_e.swMateSLOT))
                return true;

            return CreateComponentMate(assemblyDoc, modelDoc, comp1Name, comp2Name, swMateType_e.swMatePARALLEL);
        }

        public static bool BuildBallJoint(AssemblyDoc assemblyDoc, ModelDoc2 modelDoc,
            string comp1Name, string comp2Name)
        {
            if (CreateComponentMate(assemblyDoc, modelDoc, comp1Name, comp2Name, swMateType_e.swMateUNIVERSALJOINT))
                return true;

            return CreateComponentMate(assemblyDoc, modelDoc, comp1Name, comp2Name, swMateType_e.swMateCOINCIDENT);
        }

        // -------------------------------------------
        // Helper Methods
        // -------------------------------------------

        public static string GetComponentNameFromModel(AssemblyDoc assemblyDoc, SolidworksModel model)
        {
            if (model?.SwModelObject == null) return null;

            ModelDoc2 partDoc = (ModelDoc2)model.SwModelObject;
            string partPath = partDoc.GetPathName();

            object[] components = (object[])assemblyDoc.GetComponents(true);
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

        // -------------------------------------------
        // Component Operations
        // -------------------------------------------

        public static void FixComponent(AssemblyDoc assemblyDoc, ModelDoc2 modelDoc, string componentName)
        {
            if (modelDoc.Extension.SelectByID2(componentName, "COMPONENT", 0, 0, 0, false, 0, null, 0))
            {
                assemblyDoc.FixComponent();
                modelDoc.ClearSelection2(true);
            }
        }

        public static void FloatComponent(AssemblyDoc assemblyDoc, ModelDoc2 modelDoc, string componentName)
        {
            if (modelDoc.Extension.SelectByID2(componentName, "COMPONENT", 0, 0, 0, false, 0, null, 0))
            {
                assemblyDoc.UnfixComponent();
                modelDoc.ClearSelection2(true);
            }
        }

        public static void MoveComponent(SldWorks.SldWorks swApp, ModelDoc2 modelDoc,
            string componentName, double dx, double dy, double dz)
        {
            if (!modelDoc.Extension.SelectByID2(componentName, "COMPONENT", 0, 0, 0, false, 0, null, 0))
                return;

            SelectionMgr selMgr = (SelectionMgr)modelDoc.SelectionManager;
            Component2 comp = (Component2)selMgr.GetSelectedObject6(1, -1);

            if (comp == null) return;

            MathTransform currentTransform = comp.Transform2;
            MathUtility mathUtil = (MathUtility)swApp.GetMathUtility();

            double[] translation = { 1, 0, 0, 0, 1, 0, 0, 0, 1, dx, dy, dz, 1, 0, 0, 0 };
            MathTransform translateTransform = (MathTransform)mathUtil.CreateTransform(translation);

            comp.Transform2 = (MathTransform)currentTransform.Multiply(translateTransform);
            modelDoc.ClearSelection2(true);
        }

        // -------------------------------------------
        // Assembly Information
        // -------------------------------------------

        public static List<string> GetComponentNames(AssemblyDoc assemblyDoc)
        {
            var names = new List<string>();
            object[] components = (object[])assemblyDoc.GetComponents(true);

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

        public static int GetComponentCount(AssemblyDoc assemblyDoc)
        {
            object[] components = (object[])assemblyDoc.GetComponents(true);
            return components?.Length ?? 0;
        }

        // -------------------------------------------
        // Save Operations
        // -------------------------------------------

        public static bool SaveAssembly(ModelDoc2 modelDoc, string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return false;

            int errors = 0, warnings = 0;
            bool result = modelDoc.Extension.SaveAs(
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

        public static void RebuildAssembly(ModelDoc2 modelDoc)
        {
            modelDoc.ForceRebuild3(true);
        }

        public static void ZoomToFit(ModelDoc2 modelDoc)
        {
            modelDoc.ViewZoomtofit2();
        }
    }
}
