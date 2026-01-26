using System;
using System.Collections.Generic;
using CAD;
using Mathematics;
using SldWorks;
using SwConst;

namespace SolidworksLibrary
{
    internal class AssemblyBuilder : CAD_Assembly
    {
        private readonly SldWorks.SldWorks _swApp;
        private AssemblyDoc _assemblyDoc;
        private ModelDoc2 _modelDoc;

        // -------------------------------------------
        // Constructor
        // -------------------------------------------

        public AssemblyBuilder(SldWorks.SldWorks swApp, CoordinateSystem coordinateSystem,
            SW_Model basePart, SW_Model otherPart)
        {
            _swApp = swApp ?? throw new ArgumentNullException(nameof(swApp));

            if (basePart == null) throw new ArgumentNullException(nameof(basePart));
            if (otherPart == null) throw new ArgumentNullException(nameof(otherPart));

            // Set the assembly's coordinate system
            CurrentCS = coordinateSystem ?? new CoordinateSystem();
            AddCoordinateSystem(CurrentCS);

            // Create the assembly document
            CreateAssemblyDocument();

            // Add the base part (fixed at origin)
            var baseComponent = InsertComponent(basePart, true, 0, 0, 0);
            if (baseComponent != null)
            {
                AddComponent(baseComponent);
            }

            // Add the other part (positioned relative to coordinate system)
            double offsetX = coordinateSystem?.OriginLocation?.X_Value ?? 0;
            double offsetY = coordinateSystem?.OriginLocation?.Y_Value ?? 0;
            double offsetZ = coordinateSystem?.OriginLocation?.Z_Value_Cartesian ?? 0;

            var otherComponent = InsertComponent(otherPart, false, offsetX, offsetY, offsetZ);
            if (otherComponent != null)
            {
                AddComponent(otherComponent);
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

            Console.WriteLine("Created assembly document successfully.");
        }

        // -------------------------------------------
        // Component Insertion
        // -------------------------------------------

        public CAD_Component InsertComponent(SW_Model partModel, bool fixedPosition,
            double x, double y, double z)
        {
            if (partModel?.SwModelObject == null)
            {
                Console.WriteLine("Invalid part model provided.");
                return null;
            }

            // Get the file path of the part
            ModelDoc2 partDoc = (ModelDoc2)partModel.SwModelObject;
            string partPath = partDoc.GetPathName();

            if (string.IsNullOrEmpty(partPath))
            {
                Console.WriteLine("Part has no file path. Save the part first.");
                return null;
            }

            // Insert the component into the assembly
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

            // Fix the component if required
            if (fixedPosition)
            {
                swComponent.Select4(false, null, false);
                _assemblyDoc.FixComponent();
            }

            // Create CAD_Component wrapper
            var cadComponent = new CAD_Component
            {
                Name = swComponent.Name2,
                Path = partPath,
                IsAssembly = false,
                MyPart = partModel.MyCADModel?.CurrentPart
            };

            Console.WriteLine($"Inserted component: {swComponent.Name2} at ({x}, {y}, {z})");

            return cadComponent;
        }

        // -------------------------------------------
        // Component Insertion with Transform
        // -------------------------------------------

        public CAD_Component InsertComponentWithTransform(SW_Model partModel,
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
                // Apply rotation transform if axes are defined
                ApplyComponentTransform(component.Name, localCS);
            }

            return component;
        }

        // -------------------------------------------
        // Transform Application
        // -------------------------------------------

        private void ApplyComponentTransform(string componentName, CoordinateSystem cs)
        {
            if (string.IsNullOrEmpty(componentName) || cs == null) return;

            // Select the component
            _modelDoc.Extension.SelectByID2(componentName, "COMPONENT", 0, 0, 0, false, 0, null, 0);

            SelectionMgr selMgr = (SelectionMgr)_modelDoc.SelectionManager;
            Component2 comp = (Component2)selMgr.GetSelectedObject6(1, -1);

            if (comp == null) return;

            // Create transformation matrix from coordinate system
            MathUtility mathUtil = (MathUtility)_swApp.GetMathUtility();

            double[] transformData = new double[16];

            // Get axes from Vectors list (X=0, Y=1, Z=2 if available)
            Vector xAxis = cs.Vectors.Count > 0 ? cs.Vectors[0] : null;
            Vector yAxis = cs.Vectors.Count > 1 ? cs.Vectors[1] : null;
            Vector zAxis = cs.Vectors.Count > 2 ? cs.Vectors[2] : null;

            // Rotation matrix (3x3) from coordinate system axes
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

            // Translation (position)
            transformData[9] = cs.OriginLocation?.X_Value ?? 0;
            transformData[10] = cs.OriginLocation?.Y_Value ?? 0;
            transformData[11] = cs.OriginLocation?.Z_Value_Cartesian ?? 0;

            // Scale factor
            transformData[12] = 1.0;

            // Padding
            transformData[13] = 0;
            transformData[14] = 0;
            transformData[15] = 0;

            MathTransform transform = (MathTransform)mathUtil.CreateTransform(transformData);
            comp.Transform2 = transform;

            _modelDoc.ClearSelection2(true);
        }

        // -------------------------------------------
        // Mate Creation
        // -------------------------------------------

        public bool CreateCoincidentMate(string component1Name, string face1Name,
            string component2Name, string face2Name)
        {
            // Select first face
            bool selected1 = _modelDoc.Extension.SelectByID2(
                $"{face1Name}@{component1Name}@{_modelDoc.GetTitle()}",
                "FACE", 0, 0, 0, false, 1, null, 0);

            // Select second face
            bool selected2 = _modelDoc.Extension.SelectByID2(
                $"{face2Name}@{component2Name}@{_modelDoc.GetTitle()}",
                "FACE", 0, 0, 0, true, 1, null, 0);

            if (!selected1 || !selected2)
            {
                Console.WriteLine("Failed to select faces for mate.");
                return false;
            }

            int errors = 0;
            Mate2 mate = _assemblyDoc.AddMate5(
                (int)swMateType_e.swMateCOINCIDENT,
                (int)swMateAlign_e.swMateAlignALIGNED,
                false, 0, 0, 0, 0, 0, 0, 0, 0,
                false, false, 0, out errors);

            _modelDoc.ClearSelection2(true);

            if (mate != null && errors == 0)
            {
                Console.WriteLine("Created coincident mate successfully.");
                return true;
            }

            Console.WriteLine($"Failed to create mate. Error code: {errors}");
            return false;
        }

        public bool CreateConcentricMate(string component1Name, string face1Name,
            string component2Name, string face2Name)
        {
            bool selected1 = _modelDoc.Extension.SelectByID2(
                $"{face1Name}@{component1Name}@{_modelDoc.GetTitle()}",
                "FACE", 0, 0, 0, false, 1, null, 0);

            bool selected2 = _modelDoc.Extension.SelectByID2(
                $"{face2Name}@{component2Name}@{_modelDoc.GetTitle()}",
                "FACE", 0, 0, 0, true, 1, null, 0);

            if (!selected1 || !selected2) return false;

            int errors = 0;
            Mate2 mate = _assemblyDoc.AddMate5(
                (int)swMateType_e.swMateCONCENTRIC,
                (int)swMateAlign_e.swMateAlignALIGNED,
                false, 0, 0, 0, 0, 0, 0, 0, 0,
                false, false, 0, out errors);

            _modelDoc.ClearSelection2(true);
            return mate != null && errors == 0;
        }

        public bool CreateDistanceMate(string component1Name, string face1Name,
            string component2Name, string face2Name, double distance)
        {
            bool selected1 = _modelDoc.Extension.SelectByID2(
                $"{face1Name}@{component1Name}@{_modelDoc.GetTitle()}",
                "FACE", 0, 0, 0, false, 1, null, 0);

            bool selected2 = _modelDoc.Extension.SelectByID2(
                $"{face2Name}@{component2Name}@{_modelDoc.GetTitle()}",
                "FACE", 0, 0, 0, true, 1, null, 0);

            if (!selected1 || !selected2) return false;

            int errors = 0;
            Mate2 mate = _assemblyDoc.AddMate5(
                (int)swMateType_e.swMateDISTANCE,
                (int)swMateAlign_e.swMateAlignALIGNED,
                false, distance, 0, 0, 0, 0, 0, 0, 0,
                false, false, 0, out errors);

            _modelDoc.ClearSelection2(true);
            return mate != null && errors == 0;
        }

        // -------------------------------------------
        // JointBuilder Functions
        // Each function takes two parts and a CAD_Joint to create
        // the appropriate SolidWorks mate(s)
        // -------------------------------------------

        /// <summary>
        /// Creates a joint between two parts based on the CAD_Joint type.
        /// Dispatches to the appropriate joint builder method.
        /// </summary>
        public CAD_Joint BuildJoint(SW_Model part1, SW_Model part2, CAD_Joint joint)
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
                    success = BuildInPlaneJoint(comp1Name, comp2Name, joint);
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

        /// <summary>
        /// Creates a Rigid (Lock) joint - no degrees of freedom.
        /// Uses Lock mate to fully constrain relative motion.
        /// </summary>
        public bool BuildRigidJoint(string comp1Name, string comp2Name, CAD_Joint joint)
        {
            // Select both components
            bool selected1 = _modelDoc.Extension.SelectByID2(
                comp1Name, "COMPONENT", 0, 0, 0, false, 1, null, 0);
            bool selected2 = _modelDoc.Extension.SelectByID2(
                comp2Name, "COMPONENT", 0, 0, 0, true, 1, null, 0);

            if (!selected1 || !selected2)
            {
                Console.WriteLine("Failed to select components for rigid joint.");
                return false;
            }

            int errors = 0;
            Mate2 mate = _assemblyDoc.AddMate5(
                (int)swMateType_e.swMateLOCK,
                (int)swMateAlign_e.swMateAlignALIGNED,
                false, 0, 0, 0, 0, 0, 0, 0, 0,
                false, false, 0, out errors);

            _modelDoc.ClearSelection2(true);

            if (mate != null && errors == 0)
            {
                Console.WriteLine("Created Lock mate for Rigid joint.");
                return true;
            }

            Console.WriteLine($"Failed to create Rigid joint. Error: {errors}");
            return false;
        }

        /// <summary>
        /// Creates a Revolute joint - 1 rotational DOF.
        /// Uses Hinge mate or Concentric + Coincident mates.
        /// </summary>
        public bool BuildRevoluteJoint(string comp1Name, string comp2Name, CAD_Joint joint)
        {
            // Select cylindrical faces for concentric mate (axis of rotation)
            bool selected1 = _modelDoc.Extension.SelectByID2(
                comp1Name, "COMPONENT", 0, 0, 0, false, 1, null, 0);
            bool selected2 = _modelDoc.Extension.SelectByID2(
                comp2Name, "COMPONENT", 0, 0, 0, true, 1, null, 0);

            if (!selected1 || !selected2)
            {
                Console.WriteLine("Failed to select components for revolute joint.");
                return false;
            }

            int errors = 0;
            // Use Hinge mate for revolute joint (allows rotation about one axis)
            Mate2 mate = _assemblyDoc.AddMate5(
                (int)swMateType_e.swMateHINGE,
                (int)swMateAlign_e.swMateAlignALIGNED,
                false, 0, 0, 0, 0, 0, 0, 0, 0,
                false, false, 0, out errors);

            _modelDoc.ClearSelection2(true);

            if (mate != null && errors == 0)
            {
                Console.WriteLine("Created Hinge mate for Revolute joint.");
                return true;
            }

            // Fallback: try concentric mate if hinge fails
            Console.WriteLine("Hinge mate failed, attempting Concentric mate...");
            return CreateConcentricMateForJoint(comp1Name, comp2Name);
        }

        /// <summary>
        /// Creates a Slider joint - 1 translational DOF.
        /// Uses Distance mate with linear motion constraint.
        /// </summary>
        public bool BuildSliderJoint(string comp1Name, string comp2Name, CAD_Joint joint)
        {
            bool selected1 = _modelDoc.Extension.SelectByID2(
                comp1Name, "COMPONENT", 0, 0, 0, false, 1, null, 0);
            bool selected2 = _modelDoc.Extension.SelectByID2(
                comp2Name, "COMPONENT", 0, 0, 0, true, 1, null, 0);

            if (!selected1 || !selected2)
            {
                Console.WriteLine("Failed to select components for slider joint.");
                return false;
            }

            int errors = 0;
            // Use Slot mate for slider (allows linear motion along a path)
            Mate2 mate = _assemblyDoc.AddMate5(
                (int)swMateType_e.swMateSLOT,
                (int)swMateAlign_e.swMateAlignALIGNED,
                false, 0, 0, 0, 0, 0, 0, 0, 0,
                false, false, 0, out errors);

            _modelDoc.ClearSelection2(true);

            if (mate != null && errors == 0)
            {
                Console.WriteLine("Created Slot mate for Slider joint.");
                return true;
            }

            // Fallback: use parallel + coincident for linear constraint
            Console.WriteLine("Slot mate failed, attempting Parallel mate...");
            return CreateParallelMateForJoint(comp1Name, comp2Name);
        }

        /// <summary>
        /// Creates a Cylindrical joint - 1 rotational + 1 translational DOF.
        /// Uses Concentric mate allowing rotation and translation along axis.
        /// </summary>
        public bool BuildCylindricalJoint(string comp1Name, string comp2Name, CAD_Joint joint)
        {
            bool selected1 = _modelDoc.Extension.SelectByID2(
                comp1Name, "COMPONENT", 0, 0, 0, false, 1, null, 0);
            bool selected2 = _modelDoc.Extension.SelectByID2(
                comp2Name, "COMPONENT", 0, 0, 0, true, 1, null, 0);

            if (!selected1 || !selected2)
            {
                Console.WriteLine("Failed to select components for cylindrical joint.");
                return false;
            }

            int errors = 0;
            // Concentric mate allows rotation and translation along shared axis
            Mate2 mate = _assemblyDoc.AddMate5(
                (int)swMateType_e.swMateCONCENTRIC,
                (int)swMateAlign_e.swMateAlignALIGNED,
                false, 0, 0, 0, 0, 0, 0, 0, 0,
                false, false, 0, out errors);

            _modelDoc.ClearSelection2(true);

            if (mate != null && errors == 0)
            {
                Console.WriteLine("Created Concentric mate for Cylindrical joint.");
                return true;
            }

            Console.WriteLine($"Failed to create Cylindrical joint. Error: {errors}");
            return false;
        }

        /// <summary>
        /// Creates a PinSlot joint - 2 DOF (rotation + linear translation).
        /// Uses Slot mate with pin constraint.
        /// </summary>
        public bool BuildPinSlotJoint(string comp1Name, string comp2Name, CAD_Joint joint)
        {
            bool selected1 = _modelDoc.Extension.SelectByID2(
                comp1Name, "COMPONENT", 0, 0, 0, false, 1, null, 0);
            bool selected2 = _modelDoc.Extension.SelectByID2(
                comp2Name, "COMPONENT", 0, 0, 0, true, 1, null, 0);

            if (!selected1 || !selected2)
            {
                Console.WriteLine("Failed to select components for pin-slot joint.");
                return false;
            }

            int errors = 0;
            // Slot mate for pin-slot mechanism
            Mate2 mate = _assemblyDoc.AddMate5(
                (int)swMateType_e.swMateSLOT,
                (int)swMateAlign_e.swMateAlignALIGNED,
                false, 0, 0, 0, 0, 0, 0, 0, 0,
                false, false, 0, out errors);

            _modelDoc.ClearSelection2(true);

            if (mate != null && errors == 0)
            {
                Console.WriteLine("Created Slot mate for PinSlot joint.");
                return true;
            }

            Console.WriteLine($"Failed to create PinSlot joint. Error: {errors}");
            return false;
        }

        /// <summary>
        /// Creates a Planar joint - 3 DOF (2 translations + 1 rotation in plane).
        /// Uses Coincident mate on planar faces.
        /// </summary>
        public bool BuildPlanarJoint(string comp1Name, string comp2Name, CAD_Joint joint)
        {
            bool selected1 = _modelDoc.Extension.SelectByID2(
                comp1Name, "COMPONENT", 0, 0, 0, false, 1, null, 0);
            bool selected2 = _modelDoc.Extension.SelectByID2(
                comp2Name, "COMPONENT", 0, 0, 0, true, 1, null, 0);

            if (!selected1 || !selected2)
            {
                Console.WriteLine("Failed to select components for planar joint.");
                return false;
            }

            int errors = 0;
            // Coincident mate on planes allows 3 DOF in the plane
            Mate2 mate = _assemblyDoc.AddMate5(
                (int)swMateType_e.swMateCOINCIDENT,
                (int)swMateAlign_e.swMateAlignALIGNED,
                false, 0, 0, 0, 0, 0, 0, 0, 0,
                false, false, 0, out errors);

            _modelDoc.ClearSelection2(true);

            if (mate != null && errors == 0)
            {
                Console.WriteLine("Created Coincident mate for Planar joint.");
                return true;
            }

            Console.WriteLine($"Failed to create Planar joint. Error: {errors}");
            return false;
        }

        /// <summary>
        /// Creates an InPlane joint - same as Planar (3 DOF).
        /// Uses Coincident mate on planar faces.
        /// </summary>
        public bool BuildInPlaneJoint(string comp1Name, string comp2Name, CAD_Joint joint)
        {
            // InPlane is functionally the same as Planar
            return BuildPlanarJoint(comp1Name, comp2Name, joint);
        }

        /// <summary>
        /// Creates a Ball (Spherical) joint - 3 rotational DOF.
        /// Uses Point-to-point coincident or Lock with rotation freedom.
        /// </summary>
        public bool BuildBallJoint(string comp1Name, string comp2Name, CAD_Joint joint)
        {
            bool selected1 = _modelDoc.Extension.SelectByID2(
                comp1Name, "COMPONENT", 0, 0, 0, false, 1, null, 0);
            bool selected2 = _modelDoc.Extension.SelectByID2(
                comp2Name, "COMPONENT", 0, 0, 0, true, 1, null, 0);

            if (!selected1 || !selected2)
            {
                Console.WriteLine("Failed to select components for ball joint.");
                return false;
            }

            int errors = 0;
            // Use Universal Joint mate for ball/spherical joint
            Mate2 mate = _assemblyDoc.AddMate5(
                (int)swMateType_e.swMateUNIVERSALJOINT,
                (int)swMateAlign_e.swMateAlignALIGNED,
                false, 0, 0, 0, 0, 0, 0, 0, 0,
                false, false, 0, out errors);

            _modelDoc.ClearSelection2(true);

            if (mate != null && errors == 0)
            {
                Console.WriteLine("Created Universal Joint mate for Ball joint.");
                return true;
            }

            // Fallback: Point coincident for spherical constraint
            Console.WriteLine("Universal Joint mate failed, attempting point coincident...");
            return CreatePointCoincidentForJoint(comp1Name, comp2Name);
        }

        /// <summary>
        /// Creates a LeadScrew joint - 1 DOF (coupled rotation/translation).
        /// Uses Screw mate with specified pitch.
        /// </summary>
        public bool BuildLeadScrewJoint(string comp1Name, string comp2Name, CAD_Joint joint)
        {
            bool selected1 = _modelDoc.Extension.SelectByID2(
                comp1Name, "COMPONENT", 0, 0, 0, false, 1, null, 0);
            bool selected2 = _modelDoc.Extension.SelectByID2(
                comp2Name, "COMPONENT", 0, 0, 0, true, 1, null, 0);

            if (!selected1 || !selected2)
            {
                Console.WriteLine("Failed to select components for lead screw joint.");
                return false;
            }

            int errors = 0;
            // Screw mate couples rotation with translation
            // Default pitch of 0.01 meters per revolution
            double pitch = 0.01;
            Mate2 mate = _assemblyDoc.AddMate5(
                (int)swMateType_e.swMateSCREW,
                (int)swMateAlign_e.swMateAlignALIGNED,
                false, pitch, 0, 0, 0, 0, 0, 0, 0,
                false, false, 0, out errors);

            _modelDoc.ClearSelection2(true);

            if (mate != null && errors == 0)
            {
                Console.WriteLine("Created Screw mate for LeadScrew joint.");
                return true;
            }

            Console.WriteLine($"Failed to create LeadScrew joint. Error: {errors}");
            return false;
        }

        // -------------------------------------------
        // Additional SolidWorks-Specific Joint Builders
        // -------------------------------------------

        /// <summary>
        /// Creates a Gear joint - coupled rotation between two components.
        /// </summary>
        public bool BuildGearJoint(string comp1Name, string comp2Name, double ratio)
        {
            bool selected1 = _modelDoc.Extension.SelectByID2(
                comp1Name, "COMPONENT", 0, 0, 0, false, 1, null, 0);
            bool selected2 = _modelDoc.Extension.SelectByID2(
                comp2Name, "COMPONENT", 0, 0, 0, true, 1, null, 0);

            if (!selected1 || !selected2) return false;

            int errors = 0;
            Mate2 mate = _assemblyDoc.AddMate5(
                (int)swMateType_e.swMateGEAR,
                (int)swMateAlign_e.swMateAlignALIGNED,
                false, ratio, 0, 0, 0, 0, 0, 0, 0,
                false, false, 0, out errors);

            _modelDoc.ClearSelection2(true);
            return mate != null && errors == 0;
        }

        /// <summary>
        /// Creates a Rack and Pinion joint - coupled linear/rotational motion.
        /// </summary>
        public bool BuildRackPinionJoint(string comp1Name, string comp2Name, double pinionPitch)
        {
            bool selected1 = _modelDoc.Extension.SelectByID2(
                comp1Name, "COMPONENT", 0, 0, 0, false, 1, null, 0);
            bool selected2 = _modelDoc.Extension.SelectByID2(
                comp2Name, "COMPONENT", 0, 0, 0, true, 1, null, 0);

            if (!selected1 || !selected2) return false;

            int errors = 0;
            Mate2 mate = _assemblyDoc.AddMate5(
                (int)swMateType_e.swMateRACKPINION,
                (int)swMateAlign_e.swMateAlignALIGNED,
                false, pinionPitch, 0, 0, 0, 0, 0, 0, 0,
                false, false, 0, out errors);

            _modelDoc.ClearSelection2(true);
            return mate != null && errors == 0;
        }

        /// <summary>
        /// Creates a Cam joint - follower constrained to cam profile.
        /// </summary>
        public bool BuildCamJoint(string comp1Name, string comp2Name)
        {
            bool selected1 = _modelDoc.Extension.SelectByID2(
                comp1Name, "COMPONENT", 0, 0, 0, false, 1, null, 0);
            bool selected2 = _modelDoc.Extension.SelectByID2(
                comp2Name, "COMPONENT", 0, 0, 0, true, 1, null, 0);

            if (!selected1 || !selected2) return false;

            int errors = 0;
            // Cam mate is implemented using tangent relationship
            // to constrain follower to cam profile
            Mate2 mate = _assemblyDoc.AddMate5(
                (int)swMateType_e.swMateTANGENT,
                (int)swMateAlign_e.swMateAlignALIGNED,
                false, 0, 0, 0, 0, 0, 0, 0, 0,
                false, false, 0, out errors);

            _modelDoc.ClearSelection2(true);
            return mate != null && errors == 0;
        }

        /// <summary>
        /// Creates a Tangent joint - surfaces remain tangent.
        /// </summary>
        public bool BuildTangentJoint(string comp1Name, string comp2Name)
        {
            bool selected1 = _modelDoc.Extension.SelectByID2(
                comp1Name, "COMPONENT", 0, 0, 0, false, 1, null, 0);
            bool selected2 = _modelDoc.Extension.SelectByID2(
                comp2Name, "COMPONENT", 0, 0, 0, true, 1, null, 0);

            if (!selected1 || !selected2) return false;

            int errors = 0;
            Mate2 mate = _assemblyDoc.AddMate5(
                (int)swMateType_e.swMateTANGENT,
                (int)swMateAlign_e.swMateAlignALIGNED,
                false, 0, 0, 0, 0, 0, 0, 0, 0,
                false, false, 0, out errors);

            _modelDoc.ClearSelection2(true);
            return mate != null && errors == 0;
        }

        /// <summary>
        /// Creates a Perpendicular joint - faces/axes at 90 degrees.
        /// </summary>
        public bool BuildPerpendicularJoint(string comp1Name, string comp2Name)
        {
            bool selected1 = _modelDoc.Extension.SelectByID2(
                comp1Name, "COMPONENT", 0, 0, 0, false, 1, null, 0);
            bool selected2 = _modelDoc.Extension.SelectByID2(
                comp2Name, "COMPONENT", 0, 0, 0, true, 1, null, 0);

            if (!selected1 || !selected2) return false;

            int errors = 0;
            Mate2 mate = _assemblyDoc.AddMate5(
                (int)swMateType_e.swMatePERPENDICULAR,
                (int)swMateAlign_e.swMateAlignALIGNED,
                false, 0, 0, 0, 0, 0, 0, 0, 0,
                false, false, 0, out errors);

            _modelDoc.ClearSelection2(true);
            return mate != null && errors == 0;
        }

        /// <summary>
        /// Creates an Angle joint - faces/axes at specified angle.
        /// </summary>
        public bool BuildAngleJoint(string comp1Name, string comp2Name, double angleRadians)
        {
            bool selected1 = _modelDoc.Extension.SelectByID2(
                comp1Name, "COMPONENT", 0, 0, 0, false, 1, null, 0);
            bool selected2 = _modelDoc.Extension.SelectByID2(
                comp2Name, "COMPONENT", 0, 0, 0, true, 1, null, 0);

            if (!selected1 || !selected2) return false;

            int errors = 0;
            Mate2 mate = _assemblyDoc.AddMate5(
                (int)swMateType_e.swMateANGLE,
                (int)swMateAlign_e.swMateAlignALIGNED,
                false, angleRadians, 0, 0, 0, 0, 0, 0, 0,
                false, false, 0, out errors);

            _modelDoc.ClearSelection2(true);
            return mate != null && errors == 0;
        }

        /// <summary>
        /// Creates a Parallel joint - faces/axes remain parallel.
        /// </summary>
        public bool BuildParallelJoint(string comp1Name, string comp2Name)
        {
            bool selected1 = _modelDoc.Extension.SelectByID2(
                comp1Name, "COMPONENT", 0, 0, 0, false, 1, null, 0);
            bool selected2 = _modelDoc.Extension.SelectByID2(
                comp2Name, "COMPONENT", 0, 0, 0, true, 1, null, 0);

            if (!selected1 || !selected2) return false;

            int errors = 0;
            Mate2 mate = _assemblyDoc.AddMate5(
                (int)swMateType_e.swMatePARALLEL,
                (int)swMateAlign_e.swMateAlignALIGNED,
                false, 0, 0, 0, 0, 0, 0, 0, 0,
                false, false, 0, out errors);

            _modelDoc.ClearSelection2(true);
            return mate != null && errors == 0;
        }

        /// <summary>
        /// Creates a Symmetric joint - components symmetric about a plane.
        /// </summary>
        public bool BuildSymmetricJoint(string comp1Name, string comp2Name)
        {
            bool selected1 = _modelDoc.Extension.SelectByID2(
                comp1Name, "COMPONENT", 0, 0, 0, false, 1, null, 0);
            bool selected2 = _modelDoc.Extension.SelectByID2(
                comp2Name, "COMPONENT", 0, 0, 0, true, 1, null, 0);

            if (!selected1 || !selected2) return false;

            int errors = 0;
            Mate2 mate = _assemblyDoc.AddMate5(
                (int)swMateType_e.swMateSYMMETRIC,
                (int)swMateAlign_e.swMateAlignALIGNED,
                false, 0, 0, 0, 0, 0, 0, 0, 0,
                false, false, 0, out errors);

            _modelDoc.ClearSelection2(true);
            return mate != null && errors == 0;
        }

        /// <summary>
        /// Creates a Width joint - component centered between two faces.
        /// </summary>
        public bool BuildWidthJoint(string comp1Name, string comp2Name)
        {
            bool selected1 = _modelDoc.Extension.SelectByID2(
                comp1Name, "COMPONENT", 0, 0, 0, false, 1, null, 0);
            bool selected2 = _modelDoc.Extension.SelectByID2(
                comp2Name, "COMPONENT", 0, 0, 0, true, 1, null, 0);

            if (!selected1 || !selected2) return false;

            int errors = 0;
            Mate2 mate = _assemblyDoc.AddMate5(
                (int)swMateType_e.swMateWIDTH,
                (int)swMateAlign_e.swMateAlignALIGNED,
                false, 0, 0, 0, 0, 0, 0, 0, 0,
                false, false, 0, out errors);

            _modelDoc.ClearSelection2(true);
            return mate != null && errors == 0;
        }

        /// <summary>
        /// Creates a Path joint - component follows a path curve.
        /// </summary>
        public bool BuildPathJoint(string comp1Name, string comp2Name)
        {
            bool selected1 = _modelDoc.Extension.SelectByID2(
                comp1Name, "COMPONENT", 0, 0, 0, false, 1, null, 0);
            bool selected2 = _modelDoc.Extension.SelectByID2(
                comp2Name, "COMPONENT", 0, 0, 0, true, 1, null, 0);

            if (!selected1 || !selected2) return false;

            int errors = 0;
            Mate2 mate = _assemblyDoc.AddMate5(
                (int)swMateType_e.swMatePATH,
                (int)swMateAlign_e.swMateAlignALIGNED,
                false, 0, 0, 0, 0, 0, 0, 0, 0,
                false, false, 0, out errors);

            _modelDoc.ClearSelection2(true);
            return mate != null && errors == 0;
        }

        /// <summary>
        /// Creates a Linear Coupler joint - coupled linear motion between components.
        /// </summary>
        public bool BuildLinearCouplerJoint(string comp1Name, string comp2Name, double ratio)
        {
            bool selected1 = _modelDoc.Extension.SelectByID2(
                comp1Name, "COMPONENT", 0, 0, 0, false, 1, null, 0);
            bool selected2 = _modelDoc.Extension.SelectByID2(
                comp2Name, "COMPONENT", 0, 0, 0, true, 1, null, 0);

            if (!selected1 || !selected2) return false;

            int errors = 0;
            Mate2 mate = _assemblyDoc.AddMate5(
                (int)swMateType_e.swMateLINEARCOUPLER,
                (int)swMateAlign_e.swMateAlignALIGNED,
                false, ratio, 0, 0, 0, 0, 0, 0, 0,
                false, false, 0, out errors);

            _modelDoc.ClearSelection2(true);
            return mate != null && errors == 0;
        }

        // -------------------------------------------
        // Helper Methods for Joint Building
        // -------------------------------------------

        private string GetComponentNameFromModel(SW_Model model)
        {
            if (model?.SwModelObject == null) return null;

            ModelDoc2 partDoc = (ModelDoc2)model.SwModelObject;
            string partPath = partDoc.GetPathName();

            // Search for component with matching path
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

        private bool CreateConcentricMateForJoint(string comp1Name, string comp2Name)
        {
            bool selected1 = _modelDoc.Extension.SelectByID2(
                comp1Name, "COMPONENT", 0, 0, 0, false, 1, null, 0);
            bool selected2 = _modelDoc.Extension.SelectByID2(
                comp2Name, "COMPONENT", 0, 0, 0, true, 1, null, 0);

            if (!selected1 || !selected2) return false;

            int errors = 0;
            Mate2 mate = _assemblyDoc.AddMate5(
                (int)swMateType_e.swMateCONCENTRIC,
                (int)swMateAlign_e.swMateAlignALIGNED,
                false, 0, 0, 0, 0, 0, 0, 0, 0,
                false, false, 0, out errors);

            _modelDoc.ClearSelection2(true);
            return mate != null && errors == 0;
        }

        private bool CreateParallelMateForJoint(string comp1Name, string comp2Name)
        {
            bool selected1 = _modelDoc.Extension.SelectByID2(
                comp1Name, "COMPONENT", 0, 0, 0, false, 1, null, 0);
            bool selected2 = _modelDoc.Extension.SelectByID2(
                comp2Name, "COMPONENT", 0, 0, 0, true, 1, null, 0);

            if (!selected1 || !selected2) return false;

            int errors = 0;
            Mate2 mate = _assemblyDoc.AddMate5(
                (int)swMateType_e.swMatePARALLEL,
                (int)swMateAlign_e.swMateAlignALIGNED,
                false, 0, 0, 0, 0, 0, 0, 0, 0,
                false, false, 0, out errors);

            _modelDoc.ClearSelection2(true);
            return mate != null && errors == 0;
        }

        private bool CreatePointCoincidentForJoint(string comp1Name, string comp2Name)
        {
            bool selected1 = _modelDoc.Extension.SelectByID2(
                comp1Name, "COMPONENT", 0, 0, 0, false, 1, null, 0);
            bool selected2 = _modelDoc.Extension.SelectByID2(
                comp2Name, "COMPONENT", 0, 0, 0, true, 1, null, 0);

            if (!selected1 || !selected2) return false;

            int errors = 0;
            Mate2 mate = _assemblyDoc.AddMate5(
                (int)swMateType_e.swMateCOINCIDENT,
                (int)swMateAlign_e.swMateAlignALIGNED,
                false, 0, 0, 0, 0, 0, 0, 0, 0,
                false, false, 0, out errors);

            _modelDoc.ClearSelection2(true);
            return mate != null && errors == 0;
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

            if (result)
            {
                Console.WriteLine($"Assembly saved to: {filePath}");
            }
            else
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
    }
}
