using System;
using System.Collections.Generic;
using System.Xml.Linq;
//using CAD_Library;
using Mathematics;


namespace Mathematics
{
    /// <summary>
    /// Lightweight mesh container: topology (nodes/elements) + simple geometry (tri/quad faces)
    /// with optional scalar metrics (area, perimeter, volume).
    /// </summary>
    public class Mesh
    {
        // -----------------------------
        // Backing collections
        // -----------------------------
        private readonly List<Triangle> _triangles = new List<Triangle>();
        private readonly List<Quadrilateral> _quadrilaterals = new List<Quadrilateral>();
        private readonly List<Node> _nodes = new List<Node>();
        private readonly List<MeshElement> _elements = new List<MeshElement>();

        // -----------------------------
        // Construction
        // -----------------------------
        public Mesh() { }

        public Mesh(string id, string name = null, string version = null) : this()
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

        // -----------------------------
        // Scalar metrics (optional)
        // -----------------------------
        public double Volume { get; set; }
        public double SurfaceArea { get; set; }
        public double Area { get; set; }
        public double PerimeterLength { get; set; }

        // -----------------------------
        // Ownership / relationships
        // -----------------------------
        /// <summary>Surface this mesh approximates (if any).</summary>
        public Surface MySurface { get; set; }

        // -----------------------------
        // Geometry/topology (read-only views)
        // -----------------------------
        public IReadOnlyList<Triangle> Triangles => _triangles;
        public IReadOnlyList<Quadrilateral> Quadrilaterals => _quadrilaterals;
        public IReadOnlyList<Node> Nodes => _nodes;
        public IReadOnlyList<MeshElement> Elements => _elements;

        // -----------------------------
        // Mutators
        // -----------------------------
        public void AddTriangle(Triangle triangle)
        {
            if (triangle is null) throw new ArgumentNullException(nameof(triangle));
            _triangles.Add(triangle);
        }

        public void AddQuadrilateral(Quadrilateral quad)
        {
            if (quad is null) throw new ArgumentNullException(nameof(quad));
            _quadrilaterals.Add(quad);
        }

        public void AddNode(Node node)
        {
            if (node is null) throw new ArgumentNullException(nameof(node));
            _nodes.Add(node);
        }

        public void AddElement(MeshElement element)
        {
            if (element is null) throw new ArgumentNullException(nameof(element));
            _elements.Add(element);
        }

        public bool RemoveTriangle(Triangle triangle) => triangle != null && _triangles.Remove(triangle);
        public bool RemoveQuadrilateral(Quadrilateral quad) => quad != null && _quadrilaterals.Remove(quad);
        public bool RemoveNode(Node node) => node != null && _nodes.Remove(node);
        public bool RemoveElement(MeshElement element) => element != null && _elements.Remove(element);

        /// <summary>Clears all topology and face data.</summary>
        public void Clear()
        {
            _triangles.Clear();
            _quadrilaterals.Clear();
            _nodes.Clear();
            _elements.Clear();
        }

        /// <summary>Convenience helper to set scalar metrics at once.</summary>
        public void SetMetrics(double? area = null, double? surfaceArea = null, double? perimeter = null, double? volume = null)
        {
            Area = area ?? Area;
            SurfaceArea = surfaceArea ?? SurfaceArea;
            PerimeterLength = perimeter ?? PerimeterLength;
            Volume = volume ?? Volume;
        }
    }
}

