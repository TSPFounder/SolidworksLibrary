using System;
using System.Collections.Generic;

namespace CAD
{
    /// <summary>
    /// Represents a single drawing sheet (title block, views, notes, dimensions, tables, etc.).
    /// </summary>
    public sealed class CAD_DrawingSheet
    {
        // -----------------------------
        // Types
        // -----------------------------
        public enum Orientation
        {
            Landscape = 0,
            Portrait
        }

        // Reuse the size enum from CAD_Drawing if available; otherwise you can copy it here.
        // public enum DrawingSize { E=0, D, C, B, A, A1, A2, A3 }

        // -----------------------------
        // Backing collections
        // -----------------------------
        private readonly List<CAD_DrawingView> _drawingViews = new List<CAD_DrawingView>();
        private readonly List<CAD_Dimension> _dimensions = new List<CAD_Dimension>();
        private readonly List<CAD_DrawingNote> _drawingNotes = new List<CAD_DrawingNote>();
        private readonly List<CAD_ConstructionGeometry> _constructionGeometry = new List<CAD_ConstructionGeometry>();
        private readonly List<CAD_DrawingPMI> _pmi = new List<CAD_DrawingPMI>();
        private readonly List<CAD_DrawingTable> _drawingTables = new List<CAD_DrawingTable>();

        // -----------------------------
        // Construction
        // -----------------------------
        public CAD_DrawingSheet() { }

        public CAD_DrawingSheet(
            string sheetId,
            int sheetNumber = 1,
            CAD_Drawing.DrawingSize size = CAD_Drawing.DrawingSize.A,
            Orientation orientation = Orientation.Landscape)
        {
            SheetID = sheetId;
            SheetNumber = sheetNumber;
            Size = size;
            SheetOrientation = orientation;
        }

        // -----------------------------
        // Identification / metadata
        // -----------------------------
        public string SheetID { get; set; }
        public int SheetNumber { get; set; } = 1;

        /// <summary>ANSI/ISO size (reuses <see cref="CAD_Drawing.DrawingSize"/>).</summary>
        public CAD_Drawing.DrawingSize Size { get; set; } = CAD_Drawing.DrawingSize.A;

        /// <summary>Landscape or Portrait.</summary>
        public Orientation SheetOrientation { get; set; } = Orientation.Landscape;

        // -----------------------------
        // Ownership
        // -----------------------------
        public CAD_Drawing MyDrawing { get; set; }
        public CAD_BoM MyBoM { get; set; }

        // -----------------------------
        // Current cursors (optional)
        // -----------------------------
        public CAD_DrawingView CurrentDrawingView { get; private set; }
        public CAD_Dimension CurrentDimension { get; private set; }
        public CAD_DrawingNote CurrentDrawingNote { get; private set; }
        public CAD_ConstructionGeometry CurrentConstructionGeometry { get; private set; }
        public CAD_DrawingPMI CurrentPMI { get; private set; }
        public CAD_DrawingTable CurrentDrawingTable { get; private set; }

        // -----------------------------
        // Collections (read-only views)
        // -----------------------------
        public IReadOnlyList<CAD_DrawingView> DrawingViews => _drawingViews;
        public IReadOnlyList<CAD_Dimension> Dimensions => _dimensions;
        public IReadOnlyList<CAD_DrawingNote> DrawingNotes => _drawingNotes;
        public IReadOnlyList<CAD_ConstructionGeometry> ConstructionGeometry => _constructionGeometry;
        public IReadOnlyList<CAD_DrawingPMI> PMI => _pmi;
        public IReadOnlyList<CAD_DrawingTable> DrawingTables => _drawingTables;

        // -----------------------------
        // Mutators / helpers
        // -----------------------------
        public void AddView(CAD_DrawingView view)
        {
            if (view is null) throw new ArgumentNullException(nameof(view));
            _drawingViews.Add(view);
            CurrentDrawingView = view;
        }

        public bool RemoveView(CAD_DrawingView view) => _drawingViews.Remove(view);

        public void AddDimension(CAD_Dimension dim)
        {
            if (dim is null) throw new ArgumentNullException(nameof(dim));
            _dimensions.Add(dim);
            CurrentDimension = dim;
        }

        public bool RemoveDimension(CAD_Dimension dim) => _dimensions.Remove(dim);

        public void AddNote(CAD_DrawingNote note)
        {
            if (note is null) throw new ArgumentNullException(nameof(note));
            _drawingNotes.Add(note);
            CurrentDrawingNote = note;
        }

        public bool RemoveNote(CAD_DrawingNote note) => _drawingNotes.Remove(note);

        public void AddConstructionGeometry(CAD_ConstructionGeometry geom)
        {
            if (geom is null) throw new ArgumentNullException(nameof(geom));
            _constructionGeometry.Add(geom);
            CurrentConstructionGeometry = geom;
        }

        public bool RemoveConstructionGeometry(CAD_ConstructionGeometry geom) => _constructionGeometry.Remove(geom);

        public void AddPMI(CAD_DrawingPMI pmi)
        {
            if (pmi is null) throw new ArgumentNullException(nameof(pmi));
            _pmi.Add(pmi);
            CurrentPMI = pmi;
        }

        public bool RemovePMI(CAD_DrawingPMI pmi) => _pmi.Remove(pmi);

        public void AddTable(CAD_DrawingTable table)
        {
            if (table is null) throw new ArgumentNullException(nameof(table));
            _drawingTables.Add(table);
            CurrentDrawingTable = table;
        }

        public bool RemoveTable(CAD_DrawingTable table) => _drawingTables.Remove(table);

        // -----------------------------
        // Convenience
        // -----------------------------
        public bool IsLandscape => SheetOrientation == Orientation.Landscape;
        public bool IsPortrait => SheetOrientation == Orientation.Portrait;

        public override string ToString()
            => $"Sheet {SheetNumber} ({Size}, {SheetOrientation})" + (string.IsNullOrWhiteSpace(SheetID) ? "" : $" - {SheetID}");
    }
}
