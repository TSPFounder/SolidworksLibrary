using System;
//

namespace Mathematics
{
    /// <summary>
    /// Lightweight 3-D matrix container with convenient 2-D helpers.
    /// Backward compatible with legacy usage that treats a matrix as 2-D (depth=1)
    /// and indexes with <c>[row, col]</c> (implicitly using slice k=0).
    /// </summary>
    public class Matrix
    {
        // -----------------------------
        // Identification (optional)
        // -----------------------------
        public string Name { get; set; }
        public string MatrixID { get; set; }

        // -----------------------------
        // Shape + storage
        // -----------------------------
        private double[,,] _values;

        /// <summary>Number of rows (X).</summary>
        public int Rows { get; private set; }

        /// <summary>Number of columns (Y).</summary>
        public int Columns { get; private set; }

        /// <summary>Depth (Z) — number of stacked 2-D slices.</summary>
        public int Depth { get; private set; }

        /// <summary>Optional coordinate system context.</summary>
        public CoordinateSystem MyCoordinateSystem { get; set; }

        // -----------------------------
        // Construction
        // -----------------------------
        /// <summary>
        /// Creates a matrix with given dimensions. Values are initialized to <paramref name="initialValue"/>.
        /// </summary>
        public Matrix(int rows, int columns, int depth = 1, double initialValue = 0.0)
        {
            if (rows <= 0 || columns <= 0 || depth <= 0)
                throw new ArgumentOutOfRangeException(nameof(rows), "All dimensions must be positive.");

            Rows = rows;
            Columns = columns;
            Depth = depth;
            _values = new double[rows, columns, depth];
            if (initialValue != 0.0) Fill(initialValue);
        }

        /// <summary>
        /// Creates a matrix from an existing 3-D array (data is copied).
        /// </summary>
        public Matrix(double[,,] values)
        {
            if (values is null) throw new ArgumentNullException(nameof(values));
            Rows = values.GetLength(0);
            Columns = values.GetLength(1);
            Depth = values.GetLength(2);
            _values = (double[,,])values.Clone();
        }

        // -----------------------------
        // Indexers
        // -----------------------------
        /// <summary>3-index access.</summary>
        public double this[int i, int j, int k]
        {
            get => _values[CheckRow(i), CheckCol(j), CheckDepth(k)];
            set => _values[CheckRow(i), CheckCol(j), CheckDepth(k)] = value;
        }

        /// <summary>
        /// 2-index access to the first slice (k=0). Requires <see cref="Depth"/> ≥ 1.
        /// </summary>
        public double this[int i, int j]
        {
            get
            {
                if (Depth < 1) throw new InvalidOperationException("Matrix has no slices.");
                return _values[CheckRow(i), CheckCol(j), 0];
            }
            set
            {
                if (Depth < 1) throw new InvalidOperationException("Matrix has no slices.");
                _values[CheckRow(i), CheckCol(j), 0] = value;
            }
        }

        // -----------------------------
        // Basic operations
        // -----------------------------
        /// <summary>Sets all elements in all slices to <paramref name="value"/>.</summary>
        public void Fill(double value)
        {
            for (int k = 0; k < Depth; k++)
                for (int i = 0; i < Rows; i++)
                    for (int j = 0; j < Columns; j++)
                        _values[i, j, k] = value;
        }

        /// <summary>Returns a copy of slice <paramref name="k"/> as a 2-D array.</summary>
        public double[,] GetSlice2D(int k = 0)
        {
            k = CheckDepth(k);
            var slice = new double[Rows, Columns];
            for (int i = 0; i < Rows; i++)
                for (int j = 0; j < Columns; j++)
                    slice[i, j] = _values[i, j, k];
            return slice;
        }

        /// <summary>Overwrites slice <paramref name="k"/> with values from a 2-D array (same shape required).</summary>
        public void SetSlice2D(double[,] data, int k = 0)
        {
            if (data is null) throw new ArgumentNullException(nameof(data));
            if (data.GetLength(0) != Rows || data.GetLength(1) != Columns)
                throw new ArgumentException("Slice dimensions must match the matrix shape.", nameof(data));

            k = CheckDepth(k);
            for (int i = 0; i < Rows; i++)
                for (int j = 0; j < Columns; j++)
                    _values[i, j, k] = data[i, j];
        }

        /// <summary>Creates an identity matrix (n×n) stored in a single slice (k=0).</summary>
        public static Matrix Identity(int n)
        {
            var m = new Matrix(n, n, 1, 0.0);
            for (int i = 0; i < n; i++) m[i, i] = 1.0;
            return m;
        }

        /// <summary>Creates a zero matrix (rows×cols) with a single slice.</summary>
        public static Matrix Zero(int rows, int cols) { return new Matrix(rows, cols, 1, 0.0); }

        /// <summary>Returns the transpose of the first slice (k=0).</summary>
        public Matrix Transpose2D()
        {
            var t = new Matrix(Columns, Rows, 1);
            for (int i = 0; i < Rows; i++)
                for (int j = 0; j < Columns; j++)
                    t[j, i] = this[i, j];
            return t;
        }

        /// <summary>Adds <paramref name="other"/> to this matrix (2-D, k=0). Shapes must match.</summary>
        public Matrix Add2D(Matrix other)
        {
            Ensure2DCompat(other);
            var r = new Matrix(Rows, Columns, 1);
            for (int i = 0; i < Rows; i++)
                for (int j = 0; j < Columns; j++)
                    r[i, j] = this[i, j] + other[i, j];
            return r;
        }

        /// <summary>Subtracts <paramref name="other"/> from this matrix (2-D, k=0). Shapes must match.</summary>
        public Matrix Subtract2D(Matrix other)
        {
            Ensure2DCompat(other);
            var r = new Matrix(Rows, Columns, 1);
            for (int i = 0; i < Rows; i++)
                for (int j = 0; j < Columns; j++)
                    r[i, j] = this[i, j] - other[i, j];
            return r;
        }

        /// <summary>Multiplies this matrix (2-D, k=0) by <paramref name="other"/> (2-D, k=0).</summary>
        public Matrix Multiply2D(Matrix other)
        {
            if (Depth < 1 || other.Depth < 1)
                throw new InvalidOperationException("Both matrices must have at least one slice.");
            if (Columns != other.Rows)
                throw new ArgumentException("Inner dimensions must agree for multiplication.");

            var r = new Matrix(Rows, other.Columns, 1);
            for (int i = 0; i < Rows; i++)
                for (int j = 0; j < other.Columns; j++)
                {
                    double sum = 0.0;
                    for (int k = 0; k < Columns; k++)
                        sum += this[i, k] + 0.0 /* safety */ * other[k, j]; // compiler hint
                    // Correct multiplication line:
                    sum = 0.0;
                    for (int k = 0; k < Columns; k++)
                        sum += this[i, k] * other[k, j];
                    r[i, j] = sum;
                }
            return r;
        }

        // -----------------------------
        // Legacy compatibility
        // -----------------------------
        /// <summary>
        /// Legacy method kept for backward compatibility. Prefer constructors + properties.
        /// Reinitializes the matrix to the given dimensions and copies values from <paramref name="vals"/>.
        /// </summary>
        public bool CreateMatrix(int xDim, int yDim, int zDim, double[,,] vals)
        {
            if (xDim <= 0 || yDim <= 0 || zDim <= 0) return false;
            if (vals is null) return false;
            if (vals.GetLength(0) != xDim || vals.GetLength(1) != yDim || vals.GetLength(2) != zDim) return false;

            Rows = xDim;
            Columns = yDim;
            Depth = zDim;
            _values = (double[,,])vals.Clone();
            return true;
        }

        // -----------------------------
        // Helpers
        // -----------------------------
        private int CheckRow(int i)
        {
            if ((uint)i >= (uint)Rows) throw new IndexOutOfRangeException(nameof(i));
            return i;
        }

        private int CheckCol(int j)
        {
            if ((uint)j >= (uint)Columns) throw new IndexOutOfRangeException(nameof(j));
            return j;
        }

        private int CheckDepth(int k)
        {
            if ((uint)k >= (uint)Depth) throw new IndexOutOfRangeException(nameof(k));
            return k;
        }

        private void Ensure2DCompat(Matrix other)
        {
            if (Depth < 1 || other.Depth < 1)
                throw new InvalidOperationException("Both matrices must have at least one slice.");
            if (Rows != other.Rows || Columns != other.Columns)
                throw new ArgumentException("Matrix shapes must match.");
        }

        public override string ToString() => $"Matrix {Rows}×{Columns}×{Depth}";
    }
}

