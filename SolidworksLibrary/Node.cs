using System;
using System.Collections.Generic;


namespace Mathematics
{
    /// <summary>
    /// Finite-element node that extends <see cref="Point"/> with mesh connectivity.
    /// </summary>
    public class Node : Point
    {
        // -----------------------------
        // Backing fields
        // -----------------------------
        private readonly List<MeshElement> _elements = new List<MeshElement>();

        // -----------------------------
        // Construction
        // -----------------------------
        public Node() { }

        public Node(double x, double y, double z = 0.0, bool isInner = false)
            //: base(x, y, z)
        {
            IsInner = isInner;
        }

        // -----------------------------
        // Properties
        // -----------------------------
        /// <summary>
        /// True if this node is an interior node (not on a boundary).
        /// </summary>
        public bool IsInner { get; set; }

        /// <summary>
        /// Read-only view of mesh elements that reference this node.
        /// </summary>
        public IReadOnlyList<MeshElement> Elements => _elements;

        // -----------------------------
        // Connectivity helpers
        // -----------------------------
        /// <summary>Adds a reference to a mesh element if not already present.</summary>
        public void AttachElement(MeshElement element)
        {
            if (element is null) throw new ArgumentNullException(nameof(element));
            if (!_elements.Contains(element))
                _elements.Add(element);
        }

        /// <summary>Removes a reference to a mesh element.</summary>
        public bool DetachElement(MeshElement element)
            => element != null && _elements.Remove(element);

        /// <summary>Clears all attached element references.</summary>
        public void ClearElements() => _elements.Clear();
    }
}

