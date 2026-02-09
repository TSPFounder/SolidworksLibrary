
using Mathematics;
using Newtonsoft.Json;
using SE_Library;
using System;
using System.Collections.Generic;

namespace CAD
{
    public class CAD_Feature
    {
        // -----------------------------
        // Enums (unchanged)
        // -----------------------------
        public enum GeometricFeatureTypeEnum
        {
            Hole = 0,
            Joint,
            Thread,
            Chamfer,
            Fillet,
            CounterBore,
            CounterSink,
            Bead,
            Boss,
            Keyway,
            Leg,
            Arm,
            Mirror,
            Embossment,
            Rib,
            RoundedSlot,
            Gusset,
            Taper,
            SquareSlot,
            Shell,
            Web,
            Tab,
            Coil,
            Helicoil,
            RectangularPattern,
            CircularPattern,
            OtherPattern,
            Other
        }

        public enum Feature3DOperationEnum
        {
            Extrude = 0,
            Revolve,
            Sweep,
            Loft
        }

        // -----------------------------
        // Constructor
        // -----------------------------
        public CAD_Feature()
        {
            ThreeDimOperations = new List<Feature3DOperationEnum>();
            MyDimensions = new List<CAD_Dimension>();
            Sketches = new List<CAD_Sketch>();
            Stations = new List<CAD_Station>();
            MyFeatures = new List<CAD_Feature>();
            MyLibraries = new List<CAD_Library>();
        }

        // -----------------------------
        // Identification
        // -----------------------------
        public string Name { get; set; }
        public string Version { get; set; }

        // -----------------------------
        // Data
        // -----------------------------
        public GeometricFeatureTypeEnum GeometricFeatureType { get; set; }

        // -----------------------------
        // Dimensions
        // -----------------------------
        public CAD_Dimension CurrentDimension { get; set; }
        public List<CAD_Dimension> MyDimensions { get; set; }

        // -----------------------------
        // Owned & Owning Objects
        // -----------------------------
        public CAD_Feature CurrentFeature { get; set; }
        public List<CAD_Feature> MyFeatures { get; set; }

        // -----------------------------
        // Sketches
        // -----------------------------
        public CAD_Sketch CurrentCAD_Sketch { get; set; }
        public List<CAD_Sketch> Sketches { get; set; }

        // -----------------------------
        // Stations
        // -----------------------------
        public CAD_Station CurrentCAD_Station { get; set; }
        public List<CAD_Station> Stations { get; set; }

        // -----------------------------
        // Model & CSYS
        // -----------------------------
        public CAD_Model MyModel { get; set; }
        public CoordinateSystem Origin { get; set; }

        // -----------------------------
        // 3-D Operations
        // -----------------------------
        public List<Feature3DOperationEnum> ThreeDimOperations { get; set; }

        // -----------------------------
        // Libraries
        // -----------------------------
        public CAD_Library CurrentLibrary { get; set; }
        public List<CAD_Library> MyLibraries { get; set; }

        // -----------------------------
        // Methods (kept, with minor polish)
        // -----------------------------
        public bool CreateHole()
        {
            try
            {
                // placeholder for implementation
                return true;
            }
            catch
            {
                return false;
            }
        }

        // -----------------------------
        // Helpers (optional)
        // -----------------------------
        public void AddDimension(CAD_Dimension dim)
        {
            if (dim is null) throw new ArgumentNullException(nameof(dim));
            MyDimensions.Add(dim);
            CurrentDimension = dim;
        }

        public void AddSketch(CAD_Sketch sketch)
        {
            if (sketch is null) throw new ArgumentNullException(nameof(sketch));
            Sketches.Add(sketch);
            CurrentCAD_Sketch = sketch;
        }

        public void AddStation(CAD_Station station)
        {
            if (station is null) throw new ArgumentNullException(nameof(station));
            Stations.Add(station);
            CurrentCAD_Station = station;
        }

        public void AddFeature(CAD_Feature feature)
        {
            if (feature is null) throw new ArgumentNullException(nameof(feature));
            MyFeatures.Add(feature);
            CurrentFeature = feature;
        }

        public void AddLibrary(CAD_Library lib)
        {
            if (lib is null) throw new ArgumentNullException(nameof(lib));
            MyLibraries.Add(lib);
            CurrentLibrary = lib;
        }

        public override string ToString()
            => $"CAD_Feature(Name={Name ?? "<null>"}, Type={GeometricFeatureType}, Dims={MyDimensions.Count}, Sketches={Sketches.Count})";

        // -----------------------------
        // Virtual Feature Creation Methods
        // (Override in CAD-specific implementations)
        // -----------------------------

        /// <summary>
        /// Creates an extrusion (boss) feature.
        /// </summary>
        public virtual object CreateExtrusion(bool singleDirection, bool flipDirection,
            int endCondition1, double depth1,
            int endCondition2, double depth2,
            bool draftWhileExtruding1, double draftAngle1,
            bool draftWhileExtruding2, double draftAngle2,
            bool merge, bool useFeatScope, bool useAutoSelect)
        {
            throw new NotImplementedException("CreateExtrusion must be implemented in a derived class.");
        }

        /// <summary>
        /// Creates a cut extrusion feature.
        /// </summary>
        public virtual object CreateCutExtrusion(bool singleDirection, bool flipDirection,
            int endCondition1, double depth1,
            int endCondition2, double depth2,
            bool draftWhileExtruding1, double draftAngle1,
            bool draftWhileExtruding2, double draftAngle2,
            bool normalCut, bool useFeatScope, bool useAutoSelect)
        {
            throw new NotImplementedException("CreateCutExtrusion must be implemented in a derived class.");
        }

        /// <summary>
        /// Creates a revolve feature.
        /// </summary>
        public virtual object CreateRevolve(bool singleDirection, bool isSolid,
            bool isCut, bool reverseDirection,
            int endCondition1, double angle1,
            int endCondition2, double angle2,
            bool merge, bool useFeatScope, bool useAutoSelect)
        {
            throw new NotImplementedException("CreateRevolve must be implemented in a derived class.");
        }

        /// <summary>
        /// Creates a hole using the Hole Wizard.
        /// </summary>
        public virtual object CreateHoleWizard(int holeType, int standard,
            int fastenerType, string size, short endCondition,
            double diameter, double depth,
            double headClearance, double headDiameter,
            double headDepth, double threadDepth,
            double threadDiameter)
        {
            throw new NotImplementedException("CreateHoleWizard must be implemented in a derived class.");
        }

        /// <summary>
        /// Creates a threaded hole.
        /// </summary>
        public virtual object CreateThreadedHole(string size, double depth,
            double threadDepth, int standard, int fastenerType)
        {
            throw new NotImplementedException("CreateThreadedHole must be implemented in a derived class.");
        }

        /// <summary>
        /// Creates a counterbore hole.
        /// </summary>
        public virtual object CreateCounterboreHole(string size, double depth,
            double cboreDiameter, double cboreDepth,
            int standard, int fastenerType)
        {
            throw new NotImplementedException("CreateCounterboreHole must be implemented in a derived class.");
        }

        /// <summary>
        /// Creates a countersink hole.
        /// </summary>
        public virtual object CreateCountersinkHole(string size, double depth,
            double csinkDiameter, double csinkAngle,
            int standard, int fastenerType)
        {
            throw new NotImplementedException("CreateCountersinkHole must be implemented in a derived class.");
        }

        /// <summary>
        /// Creates a chamfer on selected edges.
        /// </summary>
        public virtual void CreateChamfer(double width, double angle, bool flipDirection)
        {
            throw new NotImplementedException("CreateChamfer must be implemented in a derived class.");
        }

        /// <summary>
        /// Creates a fillet on selected edges.
        /// </summary>
        public virtual bool CreateFillet(double radius, int filletType,
            int overflowType, int radiusType,
            bool propagateToTangentFaces)
        {
            throw new NotImplementedException("CreateFillet must be implemented in a derived class.");
        }

        /// <summary>
        /// Creates a constant radius fillet on selected edges.
        /// </summary>
        public virtual bool CreateConstantRadiusFillet(double radius,
            bool propagateToTangentFaces)
        {
            throw new NotImplementedException("CreateConstantRadiusFillet must be implemented in a derived class.");
        }

        /// <summary>
        /// Creates a shell feature on selected faces.
        /// </summary>
        public virtual void CreateShell(double thickness, bool shellOutward)
        {
            throw new NotImplementedException("CreateShell must be implemented in a derived class.");
        }

        /// <summary>
        /// Creates a draft feature on selected faces.
        /// </summary>
        public virtual object CreateDraft(double angle, bool reverseDirection, int draftType)
        {
            throw new NotImplementedException("CreateDraft must be implemented in a derived class.");
        }

        /// <summary>
        /// Creates a linear pattern of selected features.
        /// </summary>
        public virtual object CreateLinearPattern(int numDir1, double spacingDir1,
            int numDir2, double spacingDir2,
            bool reverseDir1, bool reverseDir2,
            bool geometryPattern, bool varySketch,
            string skipInstances1, string skipInstances2)
        {
            throw new NotImplementedException("CreateLinearPattern must be implemented in a derived class.");
        }

        /// <summary>
        /// Creates a circular pattern of selected features.
        /// </summary>
        public virtual object CreateCircularPattern(int totalInstances, double angularSpacing,
            bool reverseDirection, bool geometryPattern,
            bool equalSpacing, bool varySketch,
            string skipInstances)
        {
            throw new NotImplementedException("CreateCircularPattern must be implemented in a derived class.");
        }

        /// <summary>
        /// Creates a mirror feature of selected features.
        /// </summary>
        public virtual object CreateMirrorFeature(bool geometryPattern, bool propagateVisualProps)
        {
            throw new NotImplementedException("CreateMirrorFeature must be implemented in a derived class.");
        }

        /// <summary>
        /// Creates a rib feature from a sketch.
        /// </summary>
        public virtual void CreateRib(double thickness, int ribType, bool flipMaterial,
            bool reverseThickness, bool naturalDraft, double draftAngle)
        {
            throw new NotImplementedException("CreateRib must be implemented in a derived class.");
        }

        /// <summary>
        /// Creates a slot cut feature.
        /// </summary>
        public virtual object CreateSlotCut(double depth,
            bool singleDirection, bool flipDirection)
        {
            throw new NotImplementedException("CreateSlotCut must be implemented in a derived class.");
        }

        /// <summary>
        /// Creates a joint feature for connecting components.
        /// </summary>
        public virtual object CreateJoint(int jointType, double clearance,
            bool flipDirection)
        {
            throw new NotImplementedException("CreateJoint must be implemented in a derived class.");
        }

        /// <summary>
        /// Creates a bead feature along selected edges or faces.
        /// </summary>
        public virtual object CreateBead(double beadWidth, double beadHeight,
            int beadType, bool flipDirection)
        {
            throw new NotImplementedException("CreateBead must be implemented in a derived class.");
        }

        /// <summary>
        /// Creates a keyway feature for shaft/hub connections.
        /// </summary>
        public virtual object CreateKeyway(double width, double depth,
            double length, int keywayType, bool flipDirection)
        {
            throw new NotImplementedException("CreateKeyway must be implemented in a derived class.");
        }

        /// <summary>
        /// Creates a leg feature.
        /// </summary>
        public virtual object CreateLeg(double height, double width,
            double thickness, int legType)
        {
            throw new NotImplementedException("CreateLeg must be implemented in a derived class.");
        }

        /// <summary>
        /// Creates an arm feature.
        /// </summary>
        public virtual object CreateArm(double length, double width,
            double thickness, int armType)
        {
            throw new NotImplementedException("CreateArm must be implemented in a derived class.");
        }

        /// <summary>
        /// Creates an embossment feature on a surface.
        /// </summary>
        public virtual object CreateEmbossment(double depth, double taperAngle,
            bool flipDirection, int embossType)
        {
            throw new NotImplementedException("CreateEmbossment must be implemented in a derived class.");
        }

        /// <summary>
        /// Creates a gusset feature for structural reinforcement.
        /// </summary>
        public virtual object CreateGusset(double thickness, double height,
            double width, int gussetType, bool flipDirection)
        {
            throw new NotImplementedException("CreateGusset must be implemented in a derived class.");
        }

        /// <summary>
        /// Creates a web feature for structural support.
        /// </summary>
        public virtual object CreateWeb(double thickness, double height,
            int webType, bool flipDirection)
        {
            throw new NotImplementedException("CreateWeb must be implemented in a derived class.");
        }

        /// <summary>
        /// Creates a tab feature.
        /// </summary>
        public virtual object CreateTab(double length, double width,
            double thickness, int tabType, bool flipDirection)
        {
            throw new NotImplementedException("CreateTab must be implemented in a derived class.");
        }

        /// <summary>
        /// Creates a coil/spring feature.
        /// </summary>
        public virtual object CreateCoil(double pitch, double diameter,
            double height, int numCoils, bool clockwise,
            int coilType, double wirediameter)
        {
            throw new NotImplementedException("CreateCoil must be implemented in a derived class.");
        }

        /// <summary>
        /// Creates a helicoil/helical thread insert feature.
        /// </summary>
        public virtual object CreateHelicoil(double pitch, double diameter,
            double depth, int numTurns, bool clockwise,
            int threadType)
        {
            throw new NotImplementedException("CreateHelicoil must be implemented in a derived class.");
        }

        /// <summary>
        /// Creates a sweep feature along a path.
        /// </summary>
        public virtual object CreateSweep(bool isSolid, bool isCut,
            bool isThinFeature, double thinWallThickness,
            bool merge, bool useFeatScope, bool useAutoSelect,
            int startTangentType, int endTangentType,
            bool alignWithEndFaces, bool maintainTangency)
        {
            throw new NotImplementedException("CreateSweep must be implemented in a derived class.");
        }

        /// <summary>
        /// Creates a loft feature between profiles.
        /// </summary>
        public virtual object CreateLoft(bool isSolid, bool isCut,
            bool isThinFeature, double thinWallThickness,
            bool merge, bool useFeatScope, bool useAutoSelect,
            int startTangentType, int endTangentType,
            bool closeProfile, bool maintainTangency)
        {
            throw new NotImplementedException("CreateLoft must be implemented in a derived class.");
        }

        /// <summary>
        /// Creates an other/custom pattern feature.
        /// </summary>
        public virtual object CreateOtherPattern(int patternType,
            object patternParameters, bool geometryPattern)
        {
            throw new NotImplementedException("CreateOtherPattern must be implemented in a derived class.");
        }

        /// <summary>
        /// Creates a rounded slot feature.
        /// </summary>
        public virtual object CreateRoundedSlot(double length, double width,
            double depth, bool singleDirection, bool flipDirection)
        {
            throw new NotImplementedException("CreateRoundedSlot must be implemented in a derived class.");
        }

        /// <summary>
        /// Creates a square slot feature.
        /// </summary>
        public virtual object CreateSquareSlot(double length, double width,
            double depth, bool singleDirection, bool flipDirection)
        {
            throw new NotImplementedException("CreateSquareSlot must be implemented in a derived class.");
        }

        // JSON Serialization
        public new string ToJson() => JsonConvert.SerializeObject(this, Formatting.Indented,
            new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });
        public static new CAD_Feature FromJson(string json) => JsonConvert.DeserializeObject<CAD_Feature>(json);
    }
}
