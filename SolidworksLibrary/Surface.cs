using System;
using System.Collections.Generic;
using System.Linq;


namespace Mathematics
{
    /// <summary>
    /// Generic geometric surface with lightweight bookkeeping for points, segments,
    /// perimeter, meshes and a few helpers (perimeter/area recomputation).
    /// </summary>
    public class Surface
    {
        // -----------------------------
        // Enums
        // -----------------------------
        public enum SurfacePrimitive
        {
            Circle = 0,
            Square,
            Rectangle,
            Triangle,
            Parallelogram,
            Rhombus,
            CylinderWall,
            Sphere,
            PartialSphere,
            Other
        }

        // -----------------------------
        // Construction
        // -----------------------------
        public Surface()
        {
            Points = new List<Point>();
            Segments = new List<Segment>();
            Perimeter = new List<Segment>();
            Meshes = new List<Mesh>();
        }

        // -----------------------------
        // Identity & metadata
        // -----------------------------
        public string Name { get; set; }
        public string ID { get; set; }
        public string Version { get; set; }

        // -----------------------------
        // Data
        // -----------------------------
        /// <summary>Total area (units depend on model units). For 2D planar shapes, you can
        /// call <see cref="TryComputeArea2D(out double)"/> to recompute.</summary>
        public double Area { get; set; }

        /// <summary>Total perimeter length. Call <see cref="RecalculatePerimeterLength"/> to update from <see cref="Perimeter"/>.</summary>
        public double PerimeterLength { get; set; }

        /// <summary>If true, the surface is treated as 2D (planar) for certain helpers.</summary>
        public bool Is2D { get; set; }

        /// <summary>High-level primitive classification (optional hint).</summary>
        public SurfacePrimitive Primitive { get; set; } = SurfacePrimitive.Other;

        // -----------------------------
        // Owned & owning objects
        // -----------------------------
        /// <summary>All points belonging to the surface (not necessarily only perimeter).</summary>
        public List<Point> Points { get; }

        /// <summary>All segments belonging to the surface (not necessarily only perimeter).</summary>
        public List<Segment> Segments { get; }

        /// <summary>Segments describing the perimeter loop. Assumed to be ordered and contiguous.</summary>
        public List<Segment> Perimeter { get; }

        /// <summary>Active mesh representation (if any).</summary>
        public Mesh CurrentMesh { get; set; }

        /// <summary>All meshes associated with the surface.</summary>
        public List<Mesh> Meshes { get; }

        // -----------------------------
        // Helpers
        // -----------------------------
        /// <summary>Replace the perimeter with the provided ordered, contiguous loop.</summary>
        public void SetPerimeter(IEnumerable<Segment> segments)
        {
            if (segments is null) throw new ArgumentNullException(nameof(segments));
            Perimeter.Clear();
            Perimeter.AddRange(segments);
        }

        /// <summary>Recomputes <see cref="PerimeterLength"/> from the current <see cref="Perimeter"/> collection.</summary>
        public double RecalculatePerimeterLength()
        {
            PerimeterLength = Perimeter.Sum(s => s?.Length ?? 0.0);
            return PerimeterLength;
        }

        /// <summary>
        /// Attempts to compute the signed 2D area using the shoelace formula from the perimeter loop.
        /// Assumes the <see cref="Perimeter"/> segments are ordered and coplanar in 2D.
        /// </summary>
        /// <param name="area">Computed absolute area (non-negative) if successful.</param>
        /// <returns>True if an area could be computed.</returns>
        public bool TryComputeArea2D(out double area)
        {
            area = 0.0;
            if (!Is2D) return false;
            if (Perimeter.Count < 3) return false;

            // Build the polygon vertex list from segment start points.
            // Assumes the Perimeter is an ordered contiguous loop.
            var verts = new List<Point>(Perimeter.Count);
            foreach (var seg in Perimeter)
            {
                if (seg?.StartPoint is null) return false;
                verts.Add(seg.StartPoint);
            }

            // Close loop if needed
            if (!ReferenceEquals(Perimeter.Last().EndPoint, verts.First()))
                verts.Add(Perimeter.Last().EndPoint);

            // Basic validation
            if (verts.Count < 3 || verts.Any(v => v is null)) return false;

            double sum = 0.0;
            for (int i = 0; i < verts.Count - 1; i++)
            {
                var a = verts[i];
                var b = verts[i + 1];
                sum += a.X_Value * b.Y_Value - b.X_Value * a.Y_Value;
            }

            area = Math.Abs(sum) * 0.5;
            Area = area;
            return true;
        }

        /// <summary>Clears all geometry (points, segments, perimeter) and meshes.</summary>
        public void ClearGeometry()
        {
            Points.Clear();
            Segments.Clear();
            Perimeter.Clear();
            Meshes.Clear();
            CurrentMesh = null;
            Area = 0.0;
            PerimeterLength = 0.0;
        }
    }
}

