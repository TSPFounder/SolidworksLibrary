using System;
using CAD;
using SldWorks;
using SwConst;

namespace SolidworksLibrary
{
    public class FeatureBuilder
    {
        // -------------------------------------------
        // Extrusion (Boss)
        // -------------------------------------------

        public static object CreateExtrusion(FeatureManager featMgr,
            bool singleDirection, bool flipDirection,
            int endCondition1, double depth1,
            int endCondition2, double depth2,
            bool draftWhileExtruding1, double draftAngle1,
            bool draftWhileExtruding2, double draftAngle2,
            bool merge, bool useFeatScope, bool useAutoSelect)
        {
            return featMgr.FeatureExtrusion3(
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

        public static object CreateCutExtrusion(FeatureManager featMgr,
            bool singleDirection, bool flipDirection,
            int endCondition1, double depth1,
            int endCondition2, double depth2,
            bool draftWhileExtruding1, double draftAngle1,
            bool draftWhileExtruding2, double draftAngle2,
            bool normalCut, bool useFeatScope, bool useAutoSelect)
        {
            return featMgr.FeatureCut4(
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

        public static object CreateRevolve(FeatureManager featMgr,
            bool singleDirection, bool isSolid,
            bool isCut, bool reverseDirection,
            int endCondition1, double angle1,
            int endCondition2, double angle2,
            bool merge, bool useFeatScope, bool useAutoSelect)
        {
            return featMgr.FeatureRevolve2(
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

        public static object CreateHoleWizard(FeatureManager featMgr,
            int holeType, int standard,
            int fastenerType, string size, short endCondition,
            double diameter, double depth,
            double headClearance, double headDiameter,
            double headDepth, double threadDepth,
            double threadDiameter)
        {
            return featMgr.HoleWizard5(
                holeType, standard, fastenerType,
                size, endCondition,
                diameter, depth,
                headClearance, headDiameter, headDepth,
                threadDiameter, threadDepth,
                0, 0, 0, 0, 0, 0, 0, 0,
                "", false, false, false, false, false, false);
        }

        public static object CreateThreadedHole(FeatureManager featMgr,
            string size, double depth,
            double threadDepth, int standard, int fastenerType)
        {
            return featMgr.HoleWizard5(
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

        public static object CreateCounterboreHole(FeatureManager featMgr,
            string size, double depth,
            double cboreDiameter, double cboreDepth,
            int standard, int fastenerType)
        {
            return featMgr.HoleWizard5(
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

        public static object CreateCountersinkHole(FeatureManager featMgr,
            string size, double depth,
            double csinkDiameter, double csinkAngle,
            int standard, int fastenerType)
        {
            return featMgr.HoleWizard5(
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

        public static void CreateChamfer(ModelDoc2 swModelDoc,
            double width, double angle, bool flipDirection)
        {
            swModelDoc.FeatureChamfer(width, angle, flipDirection);
        }

        // -------------------------------------------
        // Fillet
        // -------------------------------------------

        public static bool CreateFillet(ModelDoc2 swModelDoc, double radius,
            int filletType, int overflowType, int radiusType,
            bool propagateToTangentFaces)
        {
            int result = swModelDoc.FeatureFillet2(radius, true,
                false, false, 0, 0, 0);
            return result == 0;
        }

        public static bool CreateConstantRadiusFillet(ModelDoc2 swModelDoc,
            double radius, bool propagateToTangentFaces)
        {
            int result = swModelDoc.FeatureFillet2(radius, true,
                false, false, 0, 0, 0);
            return result == 0;
        }

        // -------------------------------------------
        // Shell
        // -------------------------------------------

        public static void CreateShell(ModelDoc2 swModelDoc,
            double thickness, bool shellOutward)
        {
            swModelDoc.InsertFeatureShell(thickness, shellOutward);
        }

        // -------------------------------------------
        // Draft
        // -------------------------------------------

        public static object CreateDraft(FeatureManager featMgr,
            double angle, bool reverseDirection, int draftType)
        {
            DraftFeatureData draftData = (DraftFeatureData)featMgr.CreateDefinition(
                (int)swFeatureNameID_e.swFmDraft);
            draftData.Angle = angle;
            draftData.Type = draftType;
            draftData.ReverseDirection = reverseDirection;
            return featMgr.CreateFeature(draftData);
        }

        // -------------------------------------------
        // Linear Pattern
        // -------------------------------------------

        public static object CreateLinearPattern(FeatureManager featMgr,
            int numDir1, double spacingDir1,
            int numDir2, double spacingDir2,
            bool reverseDir1, bool reverseDir2,
            bool geometryPattern, bool varySketch,
            string skipInstances1, string skipInstances2)
        {
            return featMgr.FeatureLinearPattern4(
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

        public static object CreateCircularPattern(FeatureManager featMgr,
            int totalInstances, double angularSpacing,
            bool reverseDirection, bool geometryPattern,
            bool equalSpacing, bool varySketch,
            string skipInstances)
        {
            return featMgr.FeatureCircularPattern4(
                totalInstances, angularSpacing,
                reverseDirection, skipInstances,
                geometryPattern, equalSpacing, varySketch);
        }

        // -------------------------------------------
        // Mirror
        // -------------------------------------------

        public static object CreateMirrorFeature(FeatureManager featMgr,
            bool geometryPattern, bool propagateVisualProps)
        {
            return featMgr.InsertMirrorFeature2(
                geometryPattern, false, propagateVisualProps, false, 0);
        }

        // -------------------------------------------
        // Rib
        // -------------------------------------------

        public static void CreateRib(FeatureManager featMgr,
            double thickness, int ribType, bool flipMaterial,
            bool reverseThickness, bool naturalDraft, double draftAngle)
        {
            featMgr.InsertRib(
                reverseThickness, flipMaterial, thickness, ribType,
                false, naturalDraft, false, draftAngle, false, false);
        }

        // -------------------------------------------
        // Slot (Cut)
        // -------------------------------------------

        public static object CreateSlotCut(FeatureManager featMgr,
            double depth, bool singleDirection, bool flipDirection)
        {
            return featMgr.FeatureCut4(
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

        public static object CreateJoint(FeatureManager featMgr,
            int jointType, double clearance, bool flipDirection)
        {
            return featMgr.FeatureExtrusion3(
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

        public static object CreateBead(FeatureManager featMgr,
            double beadWidth, double beadHeight,
            int beadType, bool flipDirection)
        {
            return featMgr.FeatureExtrusion3(
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

        public static object CreateKeyway(FeatureManager featMgr,
            double width, double depth,
            double length, int keywayType, bool flipDirection)
        {
            return featMgr.FeatureCut4(
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

        public static object CreateLeg(FeatureManager featMgr,
            double height, double width,
            double thickness, int legType)
        {
            return featMgr.FeatureExtrusion3(
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

        public static object CreateArm(FeatureManager featMgr,
            double length, double width,
            double thickness, int armType)
        {
            return featMgr.FeatureExtrusion3(
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

        public static object CreateEmbossment(FeatureManager featMgr,
            double depth, double taperAngle,
            bool flipDirection, int embossType)
        {
            return featMgr.FeatureExtrusion3(
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

        public static object CreateGusset(FeatureManager featMgr,
            double thickness, double height,
            double width, int gussetType, bool flipDirection)
        {
            return featMgr.FeatureExtrusion3(
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

        public static object CreateWeb(FeatureManager featMgr, ModelDoc2 swModelDoc,
            double thickness, double height,
            int webType, bool flipDirection)
        {
            featMgr.InsertRib(
                false, flipDirection, thickness, webType,
                false, false, false, 0, false, false);
            return swModelDoc.Extension.GetLastFeatureAdded();
        }

        // -------------------------------------------
        // Tab (Sheet Metal)
        // -------------------------------------------

        public static object CreateTab(FeatureManager featMgr,
            double length, double width,
            double thickness, int tabType, bool flipDirection)
        {
            return featMgr.FeatureExtrusion3(
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

        public static object CreateCoil(FeatureManager featMgr,
            double pitch, double diameter,
            double height, int numCoils, bool clockwise,
            int coilType, double wireDiameter)
        {
            return featMgr.FeatureRevolve2(
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

        public static object CreateHelicoil(FeatureManager featMgr,
            double pitch, double diameter,
            double depth, int numTurns, bool clockwise,
            int threadType)
        {
            return featMgr.FeatureRevolve2(
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

        public static object CreateSweep(FeatureManager featMgr,
            bool isSolid, bool isCut,
            bool isThinFeature, double thinWallThickness,
            bool merge, bool useFeatScope, bool useAutoSelect,
            int startTangentType, int endTangentType,
            bool alignWithEndFaces, bool maintainTangency)
        {
            SweepFeatureData sweepData = (SweepFeatureData)featMgr.CreateDefinition(
                isCut ? (int)swFeatureNameID_e.swFmSweepCut : (int)swFeatureNameID_e.swFmSweep);
            sweepData.MaintainTangency = maintainTangency;
            sweepData.ThinFeature = isThinFeature;
            if (isThinFeature)
            {
                sweepData.ThinWallType = 0;
                sweepData.SetWallThickness(true, thinWallThickness);
            }
            return featMgr.CreateFeature(sweepData);
        }

        // -------------------------------------------
        // Loft
        // -------------------------------------------

        public static object CreateLoft(FeatureManager featMgr,
            bool isSolid, bool isCut,
            bool isThinFeature, double thinWallThickness,
            bool merge, bool useFeatScope, bool useAutoSelect,
            int startTangentType, int endTangentType,
            bool closeProfile, bool maintainTangency)
        {
            if (isCut)
            {
                return featMgr.FeatureCut4(
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
                return featMgr.InsertProtrusionBlend2(
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

        public static object CreateOtherPattern(FeatureManager featMgr,
            int patternType, object patternParameters, bool geometryPattern)
        {
            SketchPatternFeatureData patternData = (SketchPatternFeatureData)featMgr.CreateDefinition(
                (int)swFeatureNameID_e.swFmSketchPattern);
            patternData.GeometryPattern = geometryPattern;
            return featMgr.CreateFeature(patternData);
        }

        // -------------------------------------------
        // Rounded Slot
        // -------------------------------------------

        public static object CreateRoundedSlot(FeatureManager featMgr,
            double length, double width,
            double depth, bool singleDirection, bool flipDirection)
        {
            return featMgr.FeatureCut4(
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

        public static object CreateSquareSlot(FeatureManager featMgr,
            double length, double width,
            double depth, bool singleDirection, bool flipDirection)
        {
            return featMgr.FeatureCut4(
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
