
using Mathematics;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace CAD
{
    /// <summary>
    /// Represents a sketch plane (workplane) with a coordinate system, a normal, and owned sketches.
    /// </summary>
    public sealed class CAD_SketchPlane
    {
        // -----------------------------
        // Types
        // -----------------------------
        public enum GeometryTypeEnum
        {
            Cartesian = 0,
            Spherical,
            Cylindrical // fixed spelling
        }

        public enum FunctionalTypeEnum
        {
            Interface = 0,
            Section,
            GeometricBoundary,
            Feature,
            CoordinateSystemOrigin,
            Incremental
        }

        // -----------------------------
        // Backing storage
        // -----------------------------
        private readonly List<CAD_Sketch> _sketches = new List<CAD_Sketch>();

        // -----------------------------
        // Construction
        // -----------------------------
        public CAD_SketchPlane() { }

        public CAD_SketchPlane(string name,
                               FunctionalTypeEnum functionalType = FunctionalTypeEnum.Feature,
                               GeometryTypeEnum geometryType = GeometryTypeEnum.Cartesian)
        {
            Name = name;
            FunctionalType = functionalType;
            GeometryType = geometryType;
        }

        // -----------------------------
        // Identification
        // -----------------------------
        public string Name { get; set; }
        public string Version { get; set; }
        public string Path { get; set; }

        // -----------------------------
        // Data / flags
        // -----------------------------
        public bool IsWorkplane { get; set; } = true;
        public GeometryTypeEnum GeometryType { get; set; } = GeometryTypeEnum.Cartesian;
        public FunctionalTypeEnum FunctionalType { get; set; } = FunctionalTypeEnum.Feature;

        // -----------------------------
        // Ownership
        // -----------------------------
        /// <summary>Owning model, if any.</summary>
        public CAD_Model MyModel { get; set; }

        /// <summary>Coordinate system used by this plane (origin + axes).</summary>
        public CoordinateSystem MyCoordinateSystem { get; set; }

        /// <summary>Unit normal vector (model units) describing plane orientation.</summary>
        public Vector NormalVector { get; private set; }

        /// <summary>Currently active/selected sketch on this plane.</summary>
        public CAD_Sketch CurrentSketch { get; private set; }

        /// <summary>All sketches associated with this plane.</summary>
        public IReadOnlyList<CAD_Sketch> Sketches => _sketches;

        // -----------------------------
        // Mutators / helpers
        // -----------------------------
        public CAD_SketchPlane AddSketch(CAD_Sketch sketch, bool makeCurrent = true)
        {
            if (sketch is null) throw new ArgumentNullException(nameof(sketch));
            _sketches.Add(sketch);
            if (makeCurrent) CurrentSketch = sketch;
            return this;
        }

        public bool TrySetCurrentSketch(CAD_Sketch sketch)
        {
            if (sketch is null) return false;
            if (_sketches.Contains(sketch))
            {
                CurrentSketch = sketch;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Sets the plane normal from raw components. Normalized if <paramref name="normalize"/> is true.
        /// </summary>
        public CAD_SketchPlane SetNormal(double nx, double ny, double nz, bool normalize = true)
        {
            // Build a minimal Vector instance from (0,0,0) -> (nx,ny,nz)
            var start = new Mathematics.Point { X_Value = 0, Y_Value = 0, Z_Value_Cartesian = 0 };
            var end = new Mathematics.Point { X_Value = nx, Y_Value = ny, Z_Value_Cartesian = nz };
            var v = new Vector(start, end) { VectorType = Vector.VectorTypeEnum.Cartesian };

            if (normalize)
            {
                var len = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                if (len > 0)
                {
                    v.X_Value = nx / len;
                    v.Y_Value = ny / len;
                    v.Z_Value = nz / len;
                }
            }
            else
            {
                v.X_Value = nx; v.Y_Value = ny; v.Z_Value = nz;
            }

            NormalVector = v;
            return this;
        }

        public override string ToString()
            => $"{Name ?? "SketchPlane"} [{FunctionalType}, {GeometryType}]  IsWorkplane={IsWorkplane}";

        // JSON Serialization
        public string ToJson() => JsonConvert.SerializeObject(this, Formatting.Indented,
            new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });
        public static CAD_SketchPlane FromJson(string json) => JsonConvert.DeserializeObject<CAD_SketchPlane>(json);
    }
}
