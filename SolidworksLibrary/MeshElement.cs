using System;
using System.Collections.Generic;
using CAD;

namespace Mathematics
{
    /// <summary>
    /// A finite-element style mesh element with identity, type/order metadata,
    /// optional geometric metrics, and a node collection.
    /// </summary>
    public class MeshElement
    {
        // -----------------------------
        // Enums
        // -----------------------------
        public enum ElementTypeEnum
        {
            Triangle = 0,
            Quadrilateral,
            Tetrahedron,
            Hexahedron,
            Other
        }

        public enum OrderEnum
        {
            First = 0,
            Second,
            Third,
            Other
        }

        // -----------------------------
        // Backing collections
        // -----------------------------
        private readonly List<Node> _nodes = new List<Node>();

        // -----------------------------
        // Construction
        // -----------------------------
        public MeshElement() { }

        public MeshElement(string id, string name = null, ElementTypeEnum type = ElementTypeEnum.Other, OrderEnum order = OrderEnum.First)
        {
            ID = id;
            Name = name;
            ElementType = type;
            Order = order;
        }

        // -----------------------------
        // Identification
        // -----------------------------
        public string Name { get; set; }
        public string ID { get; set; }

        // -----------------------------
        // Element metadata
        // -----------------------------
        public ElementTypeEnum ElementType { get; set; } = ElementTypeEnum.Other;
        public OrderEnum Order { get; set; } = OrderEnum.First;

        // -----------------------------
        // Optional scalar metrics
        // -----------------------------
        public CAD_Parameter Volume { get; set; }
        public CAD_Parameter SurfaceArea { get; set; }
        public CAD_Parameter Area { get; set; }
        public CAD_Parameter PerimeterLength { get; set; }

        // -----------------------------
        // Ownership / relationships
        // -----------------------------
        /// <summary>Mesh that owns this element (if any).</summary>
        public Mesh MyMesh { get; set; }

        /// <summary>Read-only view of the element's nodes (topology).</summary>
        public IReadOnlyList<Node> Nodes => _nodes;

        // -----------------------------
        // Mutators
        // -----------------------------
        public void AddNode(Node node)
        {
            if (node is null) throw new ArgumentNullException(nameof(node));
            _nodes.Add(node);
        }

        public void AddNodes(IEnumerable<Node> nodes)
        {
            if (nodes is null) throw new ArgumentNullException(nameof(nodes));
            foreach (var n in nodes)
            {
                if (n is null) throw new ArgumentException("Nodes collection contains null.", nameof(nodes));
                _nodes.Add(n);
            }
        }

        public bool RemoveNode(Node node) => node != null && _nodes.Remove(node);

        public void ClearNodes() => _nodes.Clear();

        /// <summary>Convenience method to set geometric metrics in one call.</summary>
        public void SetMetrics(double? area = null, double? surfaceArea = null, double? perimeter = null, double? volume = null)
        {
            if (area.HasValue)
            {
                Area = CAD_Parameter.CreateDoubleParameter("Area", area.Value);
            }
            if (surfaceArea.HasValue)
            {
                SurfaceArea = CAD_Parameter.CreateDoubleParameter("SurfaceArea", surfaceArea.Value);
            }
            if (perimeter.HasValue)
            {
                PerimeterLength = CAD_Parameter.CreateDoubleParameter("PerimeterLength", perimeter.Value);
            }
            if (volume.HasValue)
            {
                Volume = CAD_Parameter.CreateDoubleParameter("Volume", volume.Value);
            }
        }
    }
}

