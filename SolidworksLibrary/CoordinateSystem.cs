using System;
using System.Collections.Generic;

//
using System.Numerics;

namespace Mathematics
{
    public class CoordinateSystem
    {
        // -----------------------------
        // Types (unchanged)
        // -----------------------------
        public enum CoordinateSystemTypeEnum
        {
            Cartesian = 0,
            Cylindrical,
            Spherical,
            Polar
        }

        // -----------------------------
        // Constructors / factories
        // -----------------------------
        public CoordinateSystem()
        {
            Vectors = new List<Vector>();
            //MyCAD_Sketches = new List<CAD_Sketch>();
            My2DGeometry = new List<TwoDGeometry>();
            My3DGeometry = new List<ThreeDGeometry>();
        }

        public static CoordinateSystem CreateWcs(
            CoordinateSystemTypeEnum type = CoordinateSystemTypeEnum.Cartesian)
            => new CoordinateSystem { IsWCS = true, MyType = type, Name = "WCS" };

        public static CoordinateSystem FromOrigin(Point origin,
                                                  Vector baseVector = null,
                                                  CoordinateSystemTypeEnum type = CoordinateSystemTypeEnum.Cartesian,
                                                  bool is2D = false,
                                                  bool isWcs = false)
            => new CoordinateSystem
            {
                OriginLocation = origin ?? throw new ArgumentNullException(nameof(origin)),
                BaseVector = baseVector,
                MyType = type,
                Is2D = is2D,
                IsWCS = isWcs
            };

        // -----------------------------
        // Identification / definitions
        // -----------------------------
        public string CoordinateSystemID { get; set; }
        public string Name { get; set; }
        public CoordinateSystemTypeEnum MyType { get; set; } = CoordinateSystemTypeEnum.Cartesian;

        // -----------------------------
        // Data / flags
        // -----------------------------
        public bool IsWCS { get; set; }
        public bool Is2D { get; set; }

        // -----------------------------
        // Geometry / ownership
        // -----------------------------
        public Point OriginLocation { get; set; }

        /// <summary>
        /// Optional reference axis/direction for this coordinate system (e.g., +X for Cartesian).
        /// </summary>
        public Vector BaseVector { get; set; }

        public List<Vector> Vectors { get; set; }
        //public List<CAD_Sketch> MyCAD_Sketches { get; set; }
        public List<TwoDGeometry> My2DGeometry { get; set; }
        public List<ThreeDGeometry> My3DGeometry { get; set; }

        // -----------------------------
        // Helpers
        // -----------------------------
        public void AddVector(Vector v)
        {
            if (v is null) throw new ArgumentNullException(nameof(v));
            Vectors.Add(v);
            if (BaseVector == null)
            {
                BaseVector = v;
            }
        }

        public override string ToString()
            => $"CS(Name={Name ?? "<unnamed>"}, Type={MyType}, IsWCS={IsWCS}, Is2D={Is2D})";
    }
}
