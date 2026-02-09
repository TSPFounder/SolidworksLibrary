
using Documents;
//using Documents;
using Mathematics;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace CAD
{
    /// <summary>
    /// Represents a CAD drawing document (title block, sheets, views, dims, params, elements).
    /// </summary>
    public class CAD_Drawing //: DWM_Document
    {
        // -----------------------------
        // Enums
        // -----------------------------
        public enum DrawingStandardEnum { ANSI = 0 }
        public enum DocFormatEnum { CAD_File = 0, DWG, PDF, PNG, JPG, Other }
        public enum CAD_AppName { SolidWorks = 0, Fusion360, MechanicalDesktop, AutoCAD, Other }
        public enum DrawingSize { E = 0, D, C, B, A, A1, A2, A3 }

        // -----------------------------
        // Backing state
        // -----------------------------
        private readonly List<CAD_DrawingSheet> _sheets = new List<CAD_DrawingSheet>();
        private readonly List<CAD_DrawingElement> _elements = new List<CAD_DrawingElement>();
        private readonly List<CAD_Sketch> _sketches = new List<CAD_Sketch>();
        private readonly List<CAD_DrawingView> _views = new List<CAD_DrawingView>();
        private readonly List<CAD_Part> _parts = new List<CAD_Part>();
        private readonly List<CAD_Parameter> _parameters = new List<CAD_Parameter>();
        private readonly List<CAD_Dimension> _dimensions = new List<CAD_Dimension>();
        private readonly List<CAD_ConstructionGeometry> _constructionGeometry = new List<CAD_ConstructionGeometry>();

        // -----------------------------
        // Identification
        // -----------------------------
        public string Title { get; set; }
        public string DrawingNumber { get; set; }
        public string Revision { get; set; }

        // -----------------------------
        // Data
        // -----------------------------
        public DrawingStandardEnum DrawingStandard { get; set; } = DrawingStandardEnum.ANSI;
        public DocFormatEnum MyFormat { get; set; } = DocFormatEnum.CAD_File;
        public DrawingSize MyDrawingSize { get; set; } = DrawingSize.A;

        // -----------------------------
        // Owned & Owning Objects
        // -----------------------------
        public CAD_DrawingSheet CurrentCAD_DrawingSheet { get; private set; }
        public IReadOnlyList<CAD_DrawingSheet> MyDrawingSheets => _sheets;

        public CAD_DrawingElement RevisionTable { get; private set; }
        public CAD_DrawingElement CurrentElement { get; private set; }
        public IReadOnlyList<CAD_DrawingElement> DrawingElements => _elements;

        public CAD_Sketch CurrentSketch { get; private set; }
        public IReadOnlyList<CAD_Sketch> MyCAD_Sketches => _sketches;

        public CAD_DrawingView CurrentView { get; private set; }
        public IReadOnlyList<CAD_DrawingView> MyViews => _views;

        public CAD_Part CurrentPart { get; private set; }
        public IReadOnlyList<CAD_Part> MyParts => _parts;

        public CAD_Assembly MyAssembly { get; set; }
        public CAD_Model MyModel { get; set; }
        //public CAD_Manager? TheCAD_Manager { get; set; }

        // Parameters
        public CAD_Parameter CurrentParameter { get; private set; }
        public IReadOnlyList<CAD_Parameter> MyParameters => _parameters;

        // Dimensions
        public CAD_Dimension CurrentDimension { get; private set; }
        public IReadOnlyList<CAD_Dimension> MyDimensions => _dimensions;

        // Construction geometry
        public CAD_ConstructionGeometry CurrentConstructionGeometry { get; private set; }
        public IReadOnlyList<CAD_ConstructionGeometry> MyConstructionGeometry => _constructionGeometry;

        // -----------------------------
        // Construction
        // -----------------------------
        public CAD_Drawing() { }

        // -----------------------------
        // Mutators / helpers
        // -----------------------------
        public void AddSheet(CAD_DrawingSheet sheet, bool setCurrent = true)
        {
            if (sheet is null) throw new ArgumentNullException(nameof(sheet));
            _sheets.Add(sheet);
            if (setCurrent) CurrentCAD_DrawingSheet = sheet;
        }

        public void AddElement(CAD_DrawingElement element, bool setCurrent = true)
        {
            if (element is null) throw new ArgumentNullException(nameof(element));
            _elements.Add(element);
            if (setCurrent) CurrentElement = element;
        }

        public void AddSketch(CAD_Sketch sketch, bool setCurrent = true)
        {
            if (sketch is null) throw new ArgumentNullException(nameof(sketch));
            _sketches.Add(sketch);
            if (setCurrent) CurrentSketch = sketch;
        }

        public void AddView(CAD_DrawingView view, bool setCurrent = true)
        {
            if (view is null) throw new ArgumentNullException(nameof(view));
            _views.Add(view);
            if (setCurrent) CurrentView = view;
        }

        public void AddPart(CAD_Part part, bool setCurrent = true)
        {
            if (part is null) throw new ArgumentNullException(nameof(part));
            _parts.Add(part);
            if (setCurrent) CurrentPart = part;
        }

        public void AddParameter(CAD_Parameter parameter, bool setCurrent = true)
        {
            if (parameter is null) throw new ArgumentNullException(nameof(parameter));
            _parameters.Add(parameter);
            if (setCurrent) CurrentParameter = parameter;
        }

        public void AddDimension(CAD_Dimension dimension, bool setCurrent = true)
        {
            if (dimension is null) throw new ArgumentNullException(nameof(dimension));
            _dimensions.Add(dimension);
            if (setCurrent) CurrentDimension = dimension;
        }

        public void AddConstructionGeometry(CAD_ConstructionGeometry geom, bool setCurrent = true)
        {
            if (geom is null) throw new ArgumentNullException(nameof(geom));
            _constructionGeometry.Add(geom);
            if (setCurrent) CurrentConstructionGeometry = geom;
        }

        // JSON Serialization
        public string ToJson() => JsonConvert.SerializeObject(this, Formatting.Indented,
            new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });
        public static CAD_Drawing FromJson(string json) => JsonConvert.DeserializeObject<CAD_Drawing>(json);

    }
}
