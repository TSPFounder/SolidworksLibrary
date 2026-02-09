using Mathematics;
using Newtonsoft.Json;
using SE_Library;
using System;
using System.Collections.Generic;

namespace CAD
{
    /// <summary>
    /// Lightweight wrapper for a geometric surface within a CAD body.
    /// Holds scalar properties (area/perimeter/length) and one-or-more triangulated meshes.
    /// </summary>
    public class CAD_Surface : Surface
    {
        // -----------------------------
        // Enums (unchanged)
        // -----------------------------
        public enum SurfaceTypeEnum
        {
            Plane = 0,
            Circle,
            Ellipse,
            Trainangle,
            Square,
            Rectangle,
            Quadrilateral,
            Polygon,
            Cylinder,
            Cone,
            Sphere,
            Torus,
            NURBS,
            TwoDMesh,
            ThreeDMesh,
            Other
        }

        // -----------------------------
        // State
        // -----------------------------
        private readonly List<Mesh> _meshes = new List<Mesh>();

        // -----------------------------
        // Construction
        // -----------------------------
        public CAD_Surface() { }

        public CAD_Surface(string id, string name = null, string version = null) : this()
        {
            ID = id;
            Name = name;
            Version = version;
        }

        // -----------------------------
        // Identification
        // -----------------------------
        public string Name { get; set; }
        public string ID { get; set; }
        public string Version { get; set; }
        public string Description { get; set; }
        public SurfaceTypeEnum SurfaceType { get; set; } 

        // -----------------------------
        // Scalar data
        // -----------------------------
        /// <summary>Total developed length (for curve-like surfaces), optional.</summary>
        public double Length { get; set; }

        /// <summary>Surface area.</summary>
        public double Area { get; set; }

        /// <summary>Boundary perimeter length (sum of edge loop lengths).</summary>
        public double Perimeter { get; set; }

        // -----------------------------
        // Ownership / relationships
        // -----------------------------
        /// <summary>
        /// Optional reference to the underlying analytic/parametric surface
        /// this object was derived from (distinct from this derived type which
        /// already inherits <see cref="Surface"/> for convenience).
        /// </summary>
        public Surface SourceSurface { get; set; }

        /// <summary>Owning CAD body, if any.</summary>
        public CAD_Body MyBody { get; set; }

        // -----------------------------
        // Meshes
        // -----------------------------
        /// <summary>The most recently added or explicitly selected mesh.</summary>
        public Mesh CurrentMesh { get; private set; }

        /// <summary>All meshes associated with this surface (read-only view).</summary>
        public IReadOnlyList<Mesh> Meshes => _meshes;

        /// <summary>Adds a mesh and makes it the <see cref="CurrentMesh"/>.</summary>
        public void AddMesh(Mesh mesh)
        {
            if (mesh is null) throw new ArgumentNullException(nameof(mesh));
            _meshes.Add(mesh);
            CurrentMesh = mesh;
        }

        /// <summary>Removes a mesh; updates <see cref="CurrentMesh"/> if needed.</summary>
        public bool RemoveMesh(Mesh mesh)
        {
            if (mesh is null) return false;
            var removed = _meshes.Remove(mesh);
            if (removed && ReferenceEquals(CurrentMesh, mesh))
                CurrentMesh = _meshes.Count > 0 ? _meshes[_meshes.Count - 1] : null;
            return removed;
        }

        /// <summary>Clears all meshes and resets <see cref="CurrentMesh"/>.</summary>
        public void ClearMeshes()
        {
            _meshes.Clear();
            CurrentMesh = null;
        }

        /// <summary>Explicitly sets which mesh is considered current.</summary>
        public void SetCurrentMesh(Mesh mesh)
        {
            if (mesh is null) throw new ArgumentNullException(nameof(mesh));
            if (!_meshes.Contains(mesh))
                _meshes.Add(mesh);
            CurrentMesh = mesh;
        }

        // JSON Serialization
        public string ToJson() => JsonConvert.SerializeObject(this, Formatting.Indented,
            new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });
        public static CAD_Surface FromJson(string json) => JsonConvert.DeserializeObject<CAD_Surface>(json);
    }
}
