using System;
using System.Collections.Generic;
using CAD;
using Mathematics;

namespace CAD
{
    /// <summary>
    /// Lightweight container for mass properties of a CAD part:
    /// mass, center of gravity, inertia tensors, coordinate systems, and principal directions.
    /// </summary>
    public class MassProperties
    {
        // -----------------------------
        // Backing state
        // -----------------------------
        private readonly List<CoordinateSystem> _coordinateSystems = new List<CoordinateSystem>();
        private readonly List<Matrix> _momentsHistory = new List<Matrix>();
        private Vector[] _principalDirections = new Vector[3];

        // -----------------------------
        // Construction
        // -----------------------------
        public MassProperties()
        {
            MyCAD_Part = new CAD_Part();

            // Defaults
            CenterOfGravity = new Point();
            PrincipalMomentsOfInertia = new Matrix(3, 3, 3); // 3x3 tensor
            CurrentMomentsOfInertia = new Matrix(3, 3, 3); // 3x3 tensor
            _principalDirections = new[] { new Vector(new Point(), new Point()),
                                                new Vector(new Point(), new Point()),
                                                new Vector(new Point(), new Point()) };
        }

        // -----------------------------
        // Ownership
        // -----------------------------
        /// <summary>The CAD part these mass properties refer to.</summary>
        public CAD_Part MyCAD_Part { get; set; }

        // -----------------------------
        // Scalars
        // -----------------------------
        /// <summary>Total mass (consistent units with geometry + density inputs).</summary>
        public double Mass { get; set; }

        // -----------------------------
        // Coordinate systems
        // -----------------------------
        /// <summary>Current coordinate system in which values are expressed.</summary>
        public CoordinateSystem CurrentCoordinateSystem { get; set; }

        /// <summary>All relevant coordinate systems associated with this mass snapshot.</summary>
        public IReadOnlyList<CoordinateSystem> CoordinateSystems => _coordinateSystems;

        public void AddCoordinateSystem(CoordinateSystem cs)
        {
            if (cs is null) throw new ArgumentNullException(nameof(cs));
            _coordinateSystems.Add(cs);
            CurrentCoordinateSystem = cs;
        }

        // -----------------------------
        // Center of gravity
        // -----------------------------
        /// <summary>Center of mass in the current coordinate system.</summary>
        public Point CenterOfGravity { get; set; }

        // -----------------------------
        // Inertias
        // -----------------------------
        /// <summary>Principal moments of inertia (diagonalized 3×3 tensor).</summary>
        public Matrix PrincipalMomentsOfInertia { get; set; }

        /// <summary>Current (about the same origin as CoG) 3×3 inertia tensor.</summary>
        public Matrix CurrentMomentsOfInertia { get; set; }

        /// <summary>Historical snapshots of inertia tensors (optional audit trail).</summary>
        public IReadOnlyList<Matrix> MyMomentsOfInertia => _momentsHistory;

        public void SnapshotCurrentInertia()
        {
            _momentsHistory.Add(CurrentMomentsOfInertia);
        }

        /// <summary>Principal axes unit vectors aligned with <see cref="PrincipalMomentsOfInertia"/> (X,Y,Z order).</summary>
        public IReadOnlyList<Vector> PrincipalDirections => _principalDirections;

        /// <summary>
        /// Sets the inertia tensors and principal directions with validation (expects 3×3 matrices and 3 axes).
        /// </summary>
        public void SetInertia(Matrix currentInertia, Matrix principalInertia, IReadOnlyList<Vector> principalAxes)
        {
            if (currentInertia is null) throw new ArgumentNullException(nameof(currentInertia));
            if (principalInertia is null) throw new ArgumentNullException(nameof(principalInertia));
            if (principalAxes is null) throw new ArgumentNullException(nameof(principalAxes));
            if (currentInertia.Rows != 3 || currentInertia.Columns != 3)
                throw new ArgumentException("Current inertia must be 3×3.", nameof(currentInertia));
            if (principalInertia.Rows != 3 || principalInertia.Columns != 3)
                throw new ArgumentException("Principal inertia must be 3×3.", nameof(principalInertia));
            if (principalAxes.Count != 3)
                throw new ArgumentException("Exactly 3 principal axes are required.", nameof(principalAxes));

            CurrentMomentsOfInertia = currentInertia;
            PrincipalMomentsOfInertia = principalInertia;
            _principalDirections = new[] { principalAxes[0], principalAxes[1], principalAxes[2] };
        }

        /// <summary>
        /// Applies the parallel-axis theorem to move inertia from the center of gravity to a new origin offset by <paramref name="r"/>.
        /// Assumes mass is set and <see cref="CurrentMomentsOfInertia"/> is about CoG.
        /// </summary>
        /// <param name="r">Offset vector from CoG to the new origin (in same CS/units).</param>
        public void ShiftInertiaToOffset(Vector r)
        {
            if (r is null) throw new ArgumentNullException(nameof(r));
            // Build the skew-symmetric outer terms: I' = I + m * (||r||^2 * I3 - r r^T)
            var rx = r.X_Value; var ry = r.Y_Value; var rz = r.Z_Value;
            var rr = rx * rx + ry * ry + rz * rz;

            var rrT = new double[,]
            {
                { rx*rx, rx*ry, rx*rz },
                { ry*rx, ry*ry, ry*rz },
                { rz*rx, rz*ry, rz*rz }
            };

            var identityScaled = new double[,]
            {
                { rr, 0, 0 },
                { 0, rr, 0 },
                { 0, 0, rr }
            };

            // ΔI = m * (identityScaled - rrT)
            var delta = new Matrix(3, 3, 3);
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                {
                    var val = Mass * (identityScaled[i, j] - rrT[i, j]);
                    delta[i, j] = val;
                }

            // Apply
            var shifted = new Matrix(3, 3, 3);
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    shifted[i, j] = CurrentMomentsOfInertia[i, j] + delta[i, j];

            SnapshotCurrentInertia(); // keep history
            CurrentMomentsOfInertia = shifted;
        }

        /// <summary>Clears volatile selections (current CS only).</summary>
        public void ClearSelection() => CurrentCoordinateSystem = null;
    }
}

