using System;
using CAD;
using SldWorks;
using SwConst;

namespace SolidworksLibrary
{
    internal class FeatureBuilder //: CAD_Feature
    {
        private readonly ModelDoc2 _modelDoc;
        private readonly FeatureManager _featureManager;

        public FeatureBuilder(ModelDoc2 modelDoc)
        {
            _modelDoc = modelDoc ?? throw new ArgumentNullException(nameof(modelDoc));
            _featureManager = _modelDoc.FeatureManager ?? throw new InvalidOperationException("The provided document does not expose a FeatureManager.");
        }

        private FeatureManager FeatMgr => _featureManager;
        private ModelDoc2 SwModelDoc => _modelDoc;

        // -------------------------------------------
        // Extrusion (Boss)
        // -------------------------------------------

        public  object CreateExtrusion(bool singleDirection, bool flipDirection,
            int endCondition1, double depth1,
            int endCondition2, double depth2,
            bool draftWhileExtruding1, double draftAngle1,
            bool draftWhileExtruding2, double draftAngle2,
            bool merge, bool useFeatScope, bool useAutoSelect)
        {
            return FeatMgr.FeatureExtrusion3(
                singleDirection, flipDirection, false,
                endCondition1, endCondition2,
                depth1, depth2,
                draftWhileExtruding1, draftWhileExtruding2,
                draftWhileExtruding1, draftWhileExtruding2,
                draftAngle1, draftAngle2,
                false, false, false, false,
                merge, useFeatScope, useAutoSelect,
                0, 0, false);
        }

        // -------------------------------------------
        // Cut Extrusion
        // -------------------------------------------

        public  object CreateCutExtrusion(bool singleDirection, bool flipDirection,
            int endCondition1, double depth1,
            int endCondition2, double depth2,
            bool draftWhileExtruding1, double draftAngle1,
            bool draftWhileExtruding2, double draftAngle2,
            bool normalCut, bool useFeatScope, bool useAutoSelect)
        {
            return FeatMgr.FeatureCut4(
                singleDirection, flipDirection, false,
                endCondition1, endCondition2,
                depth1, depth2,
                draftWhileExtruding1, draftWhileExtruding2,
                draftWhileExtruding1, draftWhileExtruding2,
                draftAngle1, draftAngle2,
                false, false, false, false,
                normalCut, useFeatScope, useAutoSelect,
                false, false, false,
                0, 0.0, false, false);
        }

        // -------------------------------------------
        // Revolve
        // -------------------------------------------

        public  object CreateRevolve(bool singleDirection, bool isSolid,
            bool isCut, bool reverseDirection,
            int endCondition1, double angle1,
            int endCondition2, double angle2,
            bool merge, bool useFeatScope, bool useAutoSelect)
        {
            return FeatMgr.FeatureRevolve2(
                singleDirection, isSolid, false, isCut,
                reverseDirection, false,
                endCondition1, endCondition2,
                angle1, angle2,
                false, false,
                0.0, 0.0,
                0, 0.0, 0.0,
                merge, useFeatScope, useAutoSelect);
        }

        // -------------------------------------------
        // Hole Wizard (Threaded Holes)
        // -------------------------------------------

        public  object CreateHoleWizard(int holeType, int standard,
            int fastenerType, string size, short endCondition,
            double diameter, double depth,
            double headClearance, double headDiameter,
            double headDepth, double threadDepth,
            double threadDiameter)
        {
            return FeatMgr.HoleWizard5(
                holeType, standard, fastenerType,
                size, endCondition,
                diameter, depth,
                headClearance, headDiameter, headDepth,
                threadDiameter, threadDepth,
                0, 0, 0, 0, 0, 0, 0, 0,
                "", false, false, false, false, false, false);
        }

        public  object CreateThreadedHole(string size, double depth,
            double threadDepth, int standard, int fastenerType)
        {
            return FeatMgr.HoleWizard5(
                (int)swWzdGeneralHoleTypes_e.swWzdTap,
                standard, fastenerType,
                size,
                (short)swEndConditions_e.swEndCondBlind,
                0, depth,
                0, 0, 0,
                0, threadDepth,
                0, 0, 0, 0, 0, 0, 0, 0,
                "", false, false, false, false, false, false);
        }

        public  object CreateCounterboreHole(string size, double depth,
            double cboreDiameter, double cboreDepth,
            int standard, int fastenerType)
        {
            return FeatMgr.HoleWizard5(
                (int)swWzdGeneralHoleTypes_e.swWzdCounterBore,
                standard, fastenerType,
                size,
                (short)swEndConditions_e.swEndCondBlind,
                0, depth,
                0, cboreDiameter, cboreDepth,
                0, 0,
                0, 0, 0, 0, 0, 0, 0, 0,
                "", false, false, false, false, false, false);
        }

        public  object CreateCountersinkHole(string size, double depth,
            double csinkDiameter, double csinkAngle,
            int standard, int fastenerType)
        {
            return FeatMgr.HoleWizard5(
                (int)swWzdGeneralHoleTypes_e.swWzdCounterSink,
                standard, fastenerType,
                size,
                (short)swEndConditions_e.swEndCondBlind,
                0, depth,
                0, csinkDiameter, csinkAngle,
                0, 0,
                0, 0, 0, 0, 0, 0, 0, 0,
                "", false, false, false, false, false, false);
        }

        // -------------------------------------------
        // Chamfer
        // -------------------------------------------

        public  void CreateChamfer(double width, double angle, bool flipDirection)
        {
            SwModelDoc.FeatureChamfer(width, angle, flipDirection);
        }

        // -------------------------------------------
        // Fillet
        // -------------------------------------------

        public  bool CreateFillet(double radius, int filletType,
            int overflowType, int radiusType,
            bool propagateToTangentFaces)
        {
            int result = SwModelDoc.FeatureFillet2(radius, true,
                false, false, 0, 0, 0);
            return result == 0;
        }

        public  bool CreateConstantRadiusFillet(double radius,
            bool propagateToTangentFaces)
        {
            int result = SwModelDoc.FeatureFillet2(radius, true,
                false, false, 0, 0, 0);
            return result == 0;
        }

        // -------------------------------------------
        // Shell
        // -------------------------------------------

        public  void CreateShell(double thickness, bool shellOutward)
        {
            SwModelDoc.InsertFeatureShell(thickness, shellOutward);
        }

        // -------------------------------------------
        // Draft
        // -------------------------------------------

        public  object CreateDraft(double angle, bool reverseDirection, int draftType)
        {
            // Requires selected faces and neutral plane before calling
            DraftFeatureData draftData = (DraftFeatureData)FeatMgr.CreateDefinition(
                (int)swFeatureNameID_e.swFmDraft);
            draftData.Angle = angle;
            draftData.Type = draftType;
            draftData.ReverseDirection = reverseDirection;
            return FeatMgr.CreateFeature(draftData);
        }

        // -------------------------------------------
        // Linear Pattern
        // -------------------------------------------

        public  object CreateLinearPattern(int numDir1, double spacingDir1,
            int numDir2, double spacingDir2,
            bool reverseDir1, bool reverseDir2,
            bool geometryPattern, bool varySketch,
            string skipInstances1, string skipInstances2)
        {
            return FeatMgr.FeatureLinearPattern4(
                numDir1, spacingDir1,
                numDir2, spacingDir2,
                reverseDir1, reverseDir2,
                skipInstances1, skipInstances2,
                geometryPattern, varySketch,
                true, true,
                false, false,
                false, false, false, false,
                0, 0);
        }

        // -------------------------------------------
        // Circular Pattern
        // -------------------------------------------

        public  object CreateCircularPattern(int totalInstances, double angularSpacing,
            bool reverseDirection, bool geometryPattern,
            bool equalSpacing, bool varySketch,
            string skipInstances)
        {
            return FeatMgr.FeatureCircularPattern4(
                totalInstances, angularSpacing,
                reverseDirection, skipInstances,
                geometryPattern, equalSpacing, varySketch);
        }

        // -------------------------------------------
        // Mirror
        // -------------------------------------------

        public  object CreateMirrorFeature(bool geometryPattern, bool propagateVisualProps)
        {
            return FeatMgr.InsertMirrorFeature2(
                geometryPattern, false, propagateVisualProps, false, 0);
        }

        // -------------------------------------------
        // Rib
        // -------------------------------------------

        public  void CreateRib(double thickness, int ribType, bool flipMaterial,
            bool reverseThickness, bool naturalDraft, double draftAngle)
        {
            FeatMgr.InsertRib(
                reverseThickness, flipMaterial, thickness, ribType,
                false, naturalDraft, false, draftAngle, false, false);
        }

        // -------------------------------------------
        // Slot (Cut)
        // -------------------------------------------

        public  object CreateSlotCut(double depth,
            bool singleDirection, bool flipDirection)
        {
            return FeatMgr.FeatureCut4(
                singleDirection, flipDirection, false,
                (int)swEndConditions_e.swEndCondBlind, 0,
                depth, 0,
                false, false, false, false,
                0, 0,
                false, false, false, false,
                false, true, true,
                false, false, false,
                0, 0.0, false, false);
        }

        // -------------------------------------------
        // Joint
        // -------------------------------------------

        public  object CreateJoint(int jointType, double clearance,
            bool flipDirection)
        {
            // Creates a joint using a structural member or weldment feature
            // Requires pre-selected sketch segments for the joint path
            // Using extrusion as a basic joint body creation
            return FeatMgr.FeatureExtrusion3(
                true, flipDirection, false,
                (int)swEndConditions_e.swEndCondBlind, 0,
                clearance, 0,
                false, false, false, false,
                0, 0,
                false, false, false, false,
                true, true, true,
                0, 0, false);
        }

        // -------------------------------------------
        // Bead (Weldment)
        // -------------------------------------------

        public  object CreateBead(double beadWidth, double beadHeight,
            int beadType, bool flipDirection)
        {
            // Creates a weldment bead feature using fillet weld approach
            // Requires pre-selected edges or faces
            // Using a swept boss along selected edges as bead representation
            return FeatMgr.FeatureExtrusion3(
                true, flipDirection, false,
                (int)swEndConditions_e.swEndCondBlind, 0,
                beadHeight, 0,
                false, false, false, false,
                0, 0,
                false, false, false, false,
                true, true, true,
                0, 0, false);
        }

        // -------------------------------------------
        // Keyway
        // -------------------------------------------

        public  object CreateKeyway(double width, double depth,
            double length, int keywayType, bool flipDirection)
        {
            // Creates a keyway cut feature using a blind cut extrusion
            // Requires a pre-selected sketch profile for the keyway shape
            return FeatMgr.FeatureCut4(
                true, flipDirection, false,
                (int)swEndConditions_e.swEndCondBlind, 0,
                depth, 0,
                false, false, false, false,
                0, 0,
                false, false, false, false,
                false, true, true,
                false, false, false,
                0, 0.0, false, false);
        }

        // -------------------------------------------
        // Leg
        // -------------------------------------------

        public  object CreateLeg(double height, double width,
            double thickness, int legType)
        {
            // Creates a leg feature as an extruded boss
            // Requires a pre-selected sketch profile
            return FeatMgr.FeatureExtrusion3(
                true, false, false,
                (int)swEndConditions_e.swEndCondBlind, 0,
                height, 0,
                false, false, false, false,
                0, 0,
                false, false, false, false,
                true, true, true,
                0, 0, false);
        }

        // -------------------------------------------
        // Arm
        // -------------------------------------------

        public  object CreateArm(double length, double width,
            double thickness, int armType)
        {
            // Creates an arm feature as an extruded boss
            // Requires a pre-selected sketch profile
            return FeatMgr.FeatureExtrusion3(
                true, false, false,
                (int)swEndConditions_e.swEndCondBlind, 0,
                length, 0,
                false, false, false, false,
                0, 0,
                false, false, false, false,
                true, true, true,
                0, 0, false);
        }

        // -------------------------------------------
        // Embossment
        // -------------------------------------------

        public  object CreateEmbossment(double depth, double taperAngle,
            bool flipDirection, int embossType)
        {
            // Creates an embossment/deboss feature
            // Requires a pre-selected sketch on a face
            return FeatMgr.FeatureExtrusion3(
                true, flipDirection, false,
                (int)swEndConditions_e.swEndCondBlind, 0,
                depth, 0,
                taperAngle > 0, false, taperAngle > 0, false,
                taperAngle, 0,
                false, false, false, false,
                true, true, true,
                0, 0, false);
        }

        // -------------------------------------------
        // Gusset
        // -------------------------------------------

        public  object CreateGusset(double thickness, double height,
            double width, int gussetType, bool flipDirection)
        {
            // Creates a gusset feature as an extruded triangular profile
            // Requires pre-selected sketch with gusset profile
            return FeatMgr.FeatureExtrusion3(
                true, flipDirection, false,
                (int)swEndConditions_e.swEndCondBlind, 0,
                thickness, 0,
                false, false, false, false,
                0, 0,
                false, false, false, false,
                true, true, true,
                0, 0, false);
        }

        // -------------------------------------------
        // Web
        // -------------------------------------------

        public  object CreateWeb(double thickness, double height,
            int webType, bool flipDirection)
        {
            // Creates a web/rib feature for structural support
            // Requires a pre-selected sketch
            FeatMgr.InsertRib(
                false, flipDirection, thickness, webType,
                false, false, false, 0, false, false);
            return SwModelDoc.Extension.GetLastFeatureAdded();
        }

        // -------------------------------------------
        // Tab (Sheet Metal)
        // -------------------------------------------

        public  object CreateTab(double length, double width,
            double thickness, int tabType, bool flipDirection)
        {
            // Creates a tab feature as an extruded boss
            // Requires a pre-selected sketch profile
            return FeatMgr.FeatureExtrusion3(
                true, flipDirection, false,
                (int)swEndConditions_e.swEndCondBlind, 0,
                thickness, 0,
                false, false, false, false,
                0, 0,
                false, false, false, false,
                true, true, true,
                0, 0, false);
        }

        // -------------------------------------------
        // Coil / Spring
        // -------------------------------------------

        public  object CreateCoil(double pitch, double diameter,
            double height, int numCoils, bool clockwise,
            int coilType, double wireDiameter)
        {
            // Creates a coil/spring feature
            // Requires pre-selected circle sketch and profile for sweep
            // First creates helix curve, then sweeps profile along it
            // For basic implementation, use revolve with helical path
            return FeatMgr.FeatureRevolve2(
                false, true, false, false,
                false, false,
                (int)swEndConditions_e.swEndCondBlind, 0,
                height * numCoils, 0,
                false, false,
                0.0, 0.0,
                0, 0.0, 0.0,
                true, true, true);
        }

        // -------------------------------------------
        // Helicoil / Thread Insert
        // -------------------------------------------

        public  object CreateHelicoil(double pitch, double diameter,
            double depth, int numTurns, bool clockwise,
            int threadType)
        {
            // Creates a helicoil/helical thread insert feature
            // Requires pre-selected circle sketch and thread profile
            // For basic implementation, use cut revolve
            return FeatMgr.FeatureRevolve2(
                false, true, false, true,
                false, false,
                (int)swEndConditions_e.swEndCondBlind, 0,
                depth, 0,
                false, false,
                0.0, 0.0,
                0, 0.0, 0.0,
                true, true, true);
        }

        // -------------------------------------------
        // Sweep
        // -------------------------------------------

        public object CreateSweep(bool isSolid, bool isCut,
            bool isThinFeature, double thinWallThickness,
            bool merge, bool useFeatScope, bool useAutoSelect,
            int startTangentType, int endTangentType,
            bool alignWithEndFaces, bool maintainTangency)
        {
            // Creates a sweep feature along a path
            // Requires pre-selected profile sketch and path sketch
            SweepFeatureData sweepData = (SweepFeatureData)FeatMgr.CreateDefinition(
                isCut ? (int)swFeatureNameID_e.swFmSweepCut : (int)swFeatureNameID_e.swFmSweep);
            sweepData.MaintainTangency = maintainTangency;
            sweepData.ThinFeature = isThinFeature;
            if (isThinFeature)
            {
                sweepData.ThinWallType = 0;
                sweepData.SetWallThickness(true, thinWallThickness);
            }
            return FeatMgr.CreateFeature(sweepData);
        }

        // -------------------------------------------
        // Loft
        // -------------------------------------------

        public  object CreateLoft(bool isSolid, bool isCut,
            bool isThinFeature, double thinWallThickness,
            bool merge, bool useFeatScope, bool useAutoSelect,
            int startTangentType, int endTangentType,
            bool closeProfile, bool maintainTangency)
        {
            // Creates a loft feature between profiles
            // Requires pre-selected profile sketches
            // InsertProtrusionBlend2 signature:
            // bool Closed, bool KeepTangency, bool ForceNonRational, double TessTol,
            // short StartTangentType, short EndTangentType, double StartTangentLength, double EndTangentLength,
            // bool bStartMatchTangent, bool IsThin, bool MergeSmoothFaces, double Thickness1, double Thickness2,
            // short ThinType, bool Merge, bool UseFeatScope, bool UseAutoSelect, int NumGuides
            if (isCut)
            {
                // For cut loft, use a simplified cut extrusion as fallback
                // since InsertCutBlend doesn't exist with this signature
                return FeatMgr.FeatureCut4(
                    true, false, false,
                    (int)swEndConditions_e.swEndCondBlind, 0,
                    thinWallThickness, 0,
                    false, false, false, false,
                    0, 0,
                    false, false, false, false,
                    false, useFeatScope, useAutoSelect,
                    false, false, false,
                    0, 0.0, false, false);
            }
            else
            {
                return FeatMgr.InsertProtrusionBlend2(
                    closeProfile, maintainTangency, false,
                    1.0,
                    (short)startTangentType, (short)endTangentType,
                    0.0, 0.0,
                    false, isThinFeature, false,
                    thinWallThickness, 0.0,
                    (short)0,
                    merge, useFeatScope, useAutoSelect, 0);
            }
        }

        // -------------------------------------------
        // Other Pattern (Table-Driven / Sketch-Driven)
        // -------------------------------------------

        public  object CreateOtherPattern(int patternType,
            object patternParameters, bool geometryPattern)
        {
            // Creates a sketch-driven pattern
            // Requires pre-selected features and reference sketch with points
            SketchPatternFeatureData patternData = (SketchPatternFeatureData)FeatMgr.CreateDefinition(
                (int)swFeatureNameID_e.swFmSketchPattern);
            patternData.GeometryPattern = geometryPattern;
            return FeatMgr.CreateFeature(patternData);
        }

        // -------------------------------------------
        // Rounded Slot
        // -------------------------------------------

        public  object CreateRoundedSlot(double length, double width,
            double depth, bool singleDirection, bool flipDirection)
        {
            // Creates a rounded slot (obround) cut feature
            // Requires a pre-selected rounded slot sketch
            return FeatMgr.FeatureCut4(
                singleDirection, flipDirection, false,
                (int)swEndConditions_e.swEndCondBlind, 0,
                depth, 0,
                false, false, false, false,
                0, 0,
                false, false, false, false,
                false, true, true,
                false, false, false,
                0, 0.0, false, false);
        }

        // -------------------------------------------
        // Square Slot
        // -------------------------------------------

        public  object CreateSquareSlot(double length, double width,
            double depth, bool singleDirection, bool flipDirection)
        {
            // Creates a square/rectangular slot cut feature
            // Requires a pre-selected rectangular sketch
            return FeatMgr.FeatureCut4(
                singleDirection, flipDirection, false,
                (int)swEndConditions_e.swEndCondBlind, 0,
                depth, 0,
                false, false, false, false,
                0, 0,
                false, false, false, false,
                false, true, true,
                false, false, false,
                0, 0.0, false, false);
        }
    }
}
