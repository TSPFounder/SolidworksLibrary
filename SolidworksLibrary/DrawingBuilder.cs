using System;
using System.Collections.Generic;
using CAD;
using SldWorks;
using SwConst;

namespace SolidworksLibrary
{
    internal class DrawingBuilder
    {
        private readonly SldWorks.SldWorks _swApp;
        private DrawingDoc _drawingDoc;
        private ModelDoc2 _modelDoc;
        private RevisionTableAnnotation revTableAnno;

        // -------------------------------------------
        // Constructor
        // -------------------------------------------

        public DrawingBuilder(SldWorks.SldWorks swApp, List<SolidworksModel> models)
        {
            _swApp = swApp ?? throw new ArgumentNullException(nameof(swApp));
            Models = models ?? throw new ArgumentNullException(nameof(models));

            CreateDrawingDocument();
        }

        // -------------------------------------------
        // Properties
        // -------------------------------------------

        public DrawingDoc SwDrawingDoc => _drawingDoc;
        public ModelDoc2 SwModelDoc => _modelDoc;
        public List<SolidworksModel> Models { get; private set; }

        // -------------------------------------------
        // Drawing Document Creation
        // -------------------------------------------

        private void CreateDrawingDocument()
        {
            string drawingTemplate = _swApp.GetUserPreferenceStringValue(
                (int)swUserPreferenceStringValue_e.swDefaultTemplateDrawing);

            if (string.IsNullOrEmpty(drawingTemplate))
            {
                throw new InvalidOperationException("Drawing template not found in SolidWorks settings.");
            }

            object model = _swApp.NewDocument(drawingTemplate, 0, 0, 0);
            if (model == null)
            {
                throw new InvalidOperationException("Failed to create drawing document.");
            }

            _modelDoc = (ModelDoc2)model;
            _drawingDoc = (DrawingDoc)model;
        }

        // -------------------------------------------
        // Sheet Management
        // -------------------------------------------

        public bool ActivateSheet(string sheetName)
        {
            if (string.IsNullOrEmpty(sheetName)) return false;
            return _drawingDoc.ActivateSheet(sheetName);
        }

        /// <summary>
        /// Adds a new sheet to the drawing.
        /// </summary>
        /// <param name="sheetName">Name for the new sheet.</param>
        /// <param name="paperSize">Paper size (<see cref="swDwgPaperSizes_e"/>).
        /// Use <c>swDwgPapersUserDefined</c> with <paramref name="customWidth"/>
        /// and <paramref name="customHeight"/> for a custom paper size.</param>
        /// <param name="sheetTemplate">Sheet format template (<see cref="swDwgTemplates_e"/>).
        /// Use <c>swDwgTemplateCustom</c> together with <paramref name="templatePath"/>
        /// to load a specific .slddrt file. Use <c>swDwgTemplateNone</c> for no format.
        /// Use <c>swDwgTemplateAsize</c>–<c>swDwgTemplateEsize</c> for standard formats.</param>
        /// <param name="scaleNumerator">Scale numerator (e.g. 1 for 1:2).</param>
        /// <param name="scaleDenominator">Scale denominator (e.g. 2 for 1:2).</param>
        /// <param name="firstAngleProjection">True for first-angle, false for third-angle projection.</param>
        /// <param name="templatePath">Full path to a sheet format file (.slddrt).
        /// Only used when <paramref name="sheetTemplate"/> is <c>swDwgTemplateCustom</c>.</param>
        /// <param name="customWidth">Custom paper width in meters.
        /// Only used when <paramref name="paperSize"/> is <c>swDwgPapersUserDefined</c>.</param>
        /// <param name="customHeight">Custom paper height in meters.
        /// Only used when <paramref name="paperSize"/> is <c>swDwgPapersUserDefined</c>.</param>
        /// <param name="propertyViewName">Name of the view whose model supplies
        /// custom property values for the sheet format. Empty string for none.</param>
        public bool AddSheet(string sheetName,
            swDwgPaperSizes_e paperSize,
            swDwgTemplates_e sheetTemplate,
            double scaleNumerator, double scaleDenominator,
            bool firstAngleProjection = true,
            string templatePath = "",
            double customWidth = 0, double customHeight = 0,
            string propertyViewName = "")
        {
            return _drawingDoc.NewSheet3(
                sheetName,                                                    // 1  Name
                (int)paperSize,                                               // 2  PaperSize
                (int)sheetTemplate,                                           // 3  TemplateIn
                scaleNumerator,                                               // 4  Scale1
                scaleDenominator,                                             // 5  Scale2
                firstAngleProjection,                                         // 6  FirstAngleProjection
                string.IsNullOrEmpty(templatePath) ? "" : templatePath,       // 7  TemplateName
                customWidth,                                                  // 8  Width
                customHeight,                                                 // 9  Height
                propertyViewName ?? "");                                      // 10 PropertyViewName
        }

        // -------------------------------------------
        // Standard Views
        // -------------------------------------------

        public View CreateFrontView(string modelPath, double x, double y)
        {
            return CreateNamedView(modelPath, x, y, (int)swStandardViews_e.swFrontView);
        }

        public View CreateBackView(string modelPath, double x, double y)
        {
            return CreateNamedView(modelPath, x, y, (int)swStandardViews_e.swBackView);
        }

        public View CreateTopView(string modelPath, double x, double y)
        {
            return CreateNamedView(modelPath, x, y, (int)swStandardViews_e.swTopView);
        }

        public View CreateBottomView(string modelPath, double x, double y)
        {
            return CreateNamedView(modelPath, x, y, (int)swStandardViews_e.swBottomView);
        }

        public View CreateLeftView(string modelPath, double x, double y)
        {
            return CreateNamedView(modelPath, x, y, (int)swStandardViews_e.swLeftView);
        }

        public View CreateRightView(string modelPath, double x, double y)
        {
            return CreateNamedView(modelPath, x, y, (int)swStandardViews_e.swRightView);
        }

        public View CreateIsometricView(string modelPath, double x, double y)
        {
            return CreateNamedView(modelPath, x, y, (int)swStandardViews_e.swIsometricView);
        }

        public View CreateTrimetricView(string modelPath, double x, double y)
        {
            return CreateNamedView(modelPath, x, y, (int)swStandardViews_e.swTrimetricView);
        }

        public View CreateDimetricView(string modelPath, double x, double y)
        {
            return CreateNamedView(modelPath, x, y, (int)swStandardViews_e.swDimetricView);
        }

        // -------------------------------------------
        // Projected & Derived Views
        // -------------------------------------------

        /// <summary>
        /// Creates a projected view from the currently selected parent view.
        /// The parent view must be selected before calling this method.
        /// </summary>
        public View CreateProjectedView(double x, double y)
        {
            return (View)_drawingDoc.CreateUnfoldedViewAt3(x, y, 0, false);
        }

        /// <summary>
        /// Creates an auxiliary view projected from a selected edge in the parent view.
        /// An edge must be selected in the parent view before calling this method.
        /// </summary>
        /// <param name="x">X position for the auxiliary view on the sheet.</param>
        /// <param name="y">Y position for the auxiliary view on the sheet.</param>
        /// <param name="label">Label for the auxiliary view (e.g. "A").</param>
        /// <param name="arrowDirection">Direction of the projection arrow.</param>
        /// <param name="flipArrow">True to flip the direction arrow.</param>
        /// <param name="showLabel">True to display the label on the view.</param>
        /// <param name="flipView">True to flip the resulting view.</param>
        public View CreateAuxiliaryView(double x, double y, string label,
            int arrowDirection = 0, bool flipArrow = false,
            bool showLabel = true, bool flipView = false)
        {
            return (View)_drawingDoc.CreateAuxiliaryViewAt2(
                x,                // 1 X
                y,                // 2 Y
                arrowDirection,   // 3 ArrowDirection
                flipArrow,        // 4 FlipArrow
                label,            // 5 Label
                showLabel,        // 6 ShowLabel
                flipView);        // 7 FlipView
        }

        /// <summary>
        /// Creates a detail view from a pre-selected detail circle sketch.
        /// Draw a circle on the parent view and select it before calling this method.
        /// </summary>
        /// <param name="viewX">X position for the new detail view on the sheet.</param>
        /// <param name="viewY">Y position for the new detail view on the sheet.</param>
        /// <param name="detailLabel">Label for the detail view (e.g. "A").</param>
        /// <param name="scaleNumerator">Scale numerator (e.g. 5 for 5:1).</param>
        /// <param name="scaleDenominator">Scale denominator (e.g. 1 for 5:1).</param>
        /// <param name="fullOutline">True to show a full outline around the detail.</param>
        /// <param name="jaggedOutline">True to show a jagged outline.</param>
        /// <param name="noOutline">True to hide the outline entirely.</param>
        public View CreateDetailView(double viewX, double viewY, string detailLabel,
            double scaleNumerator = 2, double scaleDenominator = 1,
            bool fullOutline = true, bool jaggedOutline = false, bool noOutline = false)
        {
            return (View)_drawingDoc.CreateDetailViewAt4(
                viewX,                                                // 1  X
                viewY,                                                // 2  Y
                0,                                                    // 3  Z
                (int)swDetViewStyle_e.swDetViewSTANDARD,              // 4  Style
                scaleNumerator,                                       // 5  Scale1
                scaleDenominator,                                     // 6  Scale2
                detailLabel,                                          // 7  Label
                (int)swDetCircleShowType_e.swDetCircleCIRCLE,         // 8  ShowType
                fullOutline,                                          // 9  FullOutline
                jaggedOutline,                                        // 10 JaggedOutline
                noOutline,                                            // 11 NoOutline
                5);                                                   // 12 ShapeIntensity
        }

        /// <summary>
        /// Creates a section view at the specified location.
        /// A section line must be selected in the parent view before calling this method.
        /// </summary>
        /// <param name="x">X position for the section view on the sheet.</param>
        /// <param name="y">Y position for the section view on the sheet.</param>
        /// <param name="sectionLabel">Label letter for the section view (e.g. "A").</param>
        /// <param name="options">Options as defined in <see cref="swCreateSectionViewAtOptions_e"/>.
        /// Use <c>swCreateSectionView_NotAligned</c> (0) for a standard section,
        /// <c>swCreateSectionView_DisplaySurfaceCut</c> (1) to show the cut surface, etc.
        /// Values can be combined with bitwise OR.</param>
        /// <param name="excludedComponents">Array of components to exclude, or null for none.</param>
        /// <param name="sectionDepth">Distance from the section line. Use 0 for full depth.</param>
        public View CreateSectionView(double x, double y, string sectionLabel,
            int options = (int)swCreateSectionViewAtOptions_e.swCreateSectionView_NotAligned,
            object excludedComponents = null, double sectionDepth = 0)
        {
            return (View)_drawingDoc.CreateSectionViewAt5(
                x,                      // 1 X
                y,                      // 2 Y
                0,                      // 3 Z
                sectionLabel,           // 4 SectionLabel
                options,                // 5 Options
                excludedComponents,     // 6 ExcludedComponents
                sectionDepth);          // 7 SectionDepth
        }

        /// <summary>
        /// Creates a broken-out section in the currently active view.
        /// A closed sketch profile and depth must be provided.
        /// </summary>
        public bool CreateBrokenOutSection(double depth)
        {
            return _drawingDoc.CreateBreakOutSection(depth);
        }

        /// <summary>
        /// Crops the specified view using a closed sketch profile that has already
        /// been drawn on it. The sketch must be selected before calling.
        /// </summary>
        /// <param name="view">The view to crop.</param>
        /// <param name="fullOutline">True to show a full outline around the crop boundary.</param>
        /// <param name="jaggedOutline">True to show a jagged outline.
        /// Only applies when <paramref name="fullOutline"/> is false.</param>
        /// <param name="shapeIntensity">Intensity of the jagged outline (1 = most, 5 = least).
        /// Only applies when <paramref name="jaggedOutline"/> is true.</param>
        /// <returns>True if the crop succeeded.</returns>
        public bool CropView(View view, bool fullOutline = true,
            bool jaggedOutline = false, int shapeIntensity = 5)
        {
            if (view == null) return false;

            _drawingDoc.ActivateView(view.Name);
            int error = view.Crop2(fullOutline, jaggedOutline, shapeIntensity);
            return error == (int)swCropViewErrors_e.swCropViewErrors_NoError;
        }

        /// <summary>
        /// Removes the crop from the specified view by selecting the crop sketch
        /// and deleting it.
        /// </summary>
        /// <returns>True if the view is no longer cropped.</returns>
        public bool RemoveCropView(View view)
        {
            if (view == null || !view.IsCropped()) return false;

            _drawingDoc.ActivateView(view.Name);
            _modelDoc.Extension.SelectByID2(
                view.Name, "DRAWINGVIEW", 0, 0, 0, false, 0, null, 0);
            _modelDoc.EditSketch();
            _modelDoc.Extension.DeleteSelection2(
                (int)swDeleteSelectionOptions_e.swDelete_Absorbed);
            _modelDoc.ClearSelection2(true);
            return !view.IsCropped();
        }

        /// <summary>
        /// Returns true if the specified view is currently cropped.
        /// </summary>
        public bool IsCropped(View view)
        {
            if (view == null) return false;
            return view.IsCropped();
        }

        /// <summary>
        /// Inserts an alternate position view on the specified parent view.
        /// The parent view must be activated before calling this method.
        /// </summary>
        public bool InsertAlternatePositionView(View parentView, string configName)
        {
            if (parentView == null) return false;

            _drawingDoc.ActivateView(parentView.Name);
            var result = parentView.InsertAlternateView(
                string.IsNullOrEmpty(configName) ? "" : configName);
            return result != null;
        }

        /// <summary>
        /// Creates a relative-to-model view by pre-selecting two orthogonal planar faces
        /// or planes, then placing the view.
        /// The two faces/planes must be selected (marks 1 and 2)    before calling this method.
        /// </summary>
        public View CreateRelativeView(string modelPath, double x, double y)
        {
            if (string.IsNullOrEmpty(modelPath)) return null;

            return (View)_drawingDoc.CreateRelativeView(modelPath, x, y, 0, 1);
        }

        public View Create3DDrawingView(string modelPath, double x, double y)
        {
            return CreateNamedView(modelPath, x, y, (int)swStandardViews_e.swIsometricView);
        }

        // -------------------------------------------
        // Flat Pattern & Custom Views
        // -------------------------------------------

        public View CreateFlatPatternView(string modelPath, double x, double y, string configName)
        {
            return (View)_drawingDoc.CreateFlatPatternViewFromModelView3(
                modelPath, configName, x, y, 0, true, false);
        }

        public View CreateCustomView(string modelPath, double x, double y, string viewName)
        {
            return (View)_drawingDoc.CreateDrawViewFromModelView3(
                modelPath, viewName, x, y, 0);
        }

        // -------------------------------------------
        // Core Named-View Helper
        // -------------------------------------------

        private View CreateNamedView(string modelPath, double x, double y, int standardView)
        {
            if (string.IsNullOrEmpty(modelPath))
            {
                Console.WriteLine("Model path is required to create a drawing view.");
                return null;
            }

            string viewName = GetStandardViewName(standardView);

            View view = (View)_drawingDoc.CreateDrawViewFromModelView3(
                modelPath, viewName, x, y, 0);

            if (view == null)
            {
                Console.WriteLine($"Failed to create {viewName} view.");
            }

            return view;
        }

        private static string GetStandardViewName(int standardView)
        {
            switch ((swStandardViews_e)standardView)
            {
                case swStandardViews_e.swFrontView: return "*Front";
                case swStandardViews_e.swBackView: return "*Back";
                case swStandardViews_e.swTopView: return "*Top";
                case swStandardViews_e.swBottomView: return "*Bottom";
                case swStandardViews_e.swLeftView: return "*Left";
                case swStandardViews_e.swRightView: return "*Right";
                case swStandardViews_e.swIsometricView: return "*Isometric";
                case swStandardViews_e.swTrimetricView: return "*Trimetric";
                case swStandardViews_e.swDimetricView: return "*Dimetric";
                default: return "*Front";
            }
        }

        // -------------------------------------------
        // Notes
        // -------------------------------------------

        public Note InsertNote(string text, double x, double y)
        {
            if (string.IsNullOrEmpty(text)) return null;

            _modelDoc.SetAddToDB(true);
            Note note = (Note)_modelDoc.InsertNote(text);
            _modelDoc.SetAddToDB(false);

            if (note != null)
            {
                Annotation annotation = (Annotation)note.GetAnnotation();
                annotation?.SetPosition(x, y, 0);
            }

            return note;
        }

        public Note InsertNoteWithLeader(string text, double x, double y)
        {
            _modelDoc.SetAddToDB(true);
            Note note = (Note)_modelDoc.InsertNote(text);
            _modelDoc.SetAddToDB(false);

            if (note != null)
            {
                note.SetTextJustification((int)swTextJustification_e.swTextJustificationLeft);
                Annotation annotation = (Annotation)note.GetAnnotation();
                if (annotation != null)
                {
                    annotation.SetPosition(x, y, 0);
                    annotation.SetLeader3(
                        (int)swLeaderStyle_e.swSTRAIGHT,
                        (int)swLeaderSide_e.swLS_SMART,
                        true, true, false, false);
                }
            }

            return note;
        }

        // -------------------------------------------
        // Bill of Materials (BoM)
        // -------------------------------------------

        public BomTableAnnotation InsertBomTable(double x, double y,
            swBomType_e bomType, string configuration, string templatePath)
        {
            View activeView = (View)_drawingDoc.ActiveDrawingView;
            if (activeView == null)
            {
                Console.WriteLine("No active drawing view for BOM insertion.");
                return null;
            }

            BomTableAnnotation bomTable = (BomTableAnnotation)activeView.InsertBomTable4(
                false, x, y,2,
                (int)bomType,
                configuration,
                string.IsNullOrEmpty(templatePath) ? "" : templatePath,
                false, 0, false);

            if (bomTable == null)
            {
                Console.WriteLine("Failed to insert BOM table.");
            }

            return bomTable;
        }

        public BomTableAnnotation InsertIndentedBom(double x, double y,
            string configuration, string templatePath)
        {
            return InsertBomTable(x, y, swBomType_e.swBomType_Indented,
                configuration, templatePath);
        }

        // -------------------------------------------
        // Title Block
        // -------------------------------------------

        public bool FillTitleBlockField(string fieldName, string value)
        {
            if (string.IsNullOrEmpty(fieldName)) return false;

            bool selected = _modelDoc.Extension.SelectByID2(
                fieldName, "NOTE", 0, 0, 0, false, 0, null, 0);

            if (!selected) return false;

            SelectionMgr selMgr = (SelectionMgr)_modelDoc.SelectionManager;
            Note note = (Note)selMgr.GetSelectedObject6(1, -1);

            if (note != null)
            {
                note.SetText(value);
                _modelDoc.ClearSelection2(true);
                return true;
            }

            _modelDoc.ClearSelection2(true);
            return false;
        }

        public void FillTitleBlock(
            string title, string drawnBy, string checkedBy,
            string approvedBy, string date, string partNumber,
            string revision, string material, string scale)
        {
            FillTitleBlockField("Title", title);
            FillTitleBlockField("DrawnBy", drawnBy);
            FillTitleBlockField("CheckedBy", checkedBy);
            FillTitleBlockField("ApprovedBy", approvedBy);
            FillTitleBlockField("DrawnDate", date);
            FillTitleBlockField("PartNo", partNumber);
            FillTitleBlockField("Revision", revision);
            FillTitleBlockField("Material", material);
            FillTitleBlockField("Scale", scale);
        }

        // -------------------------------------------
        // Revision Table
        // -------------------------------------------

        public RevisionTableAnnotation InsertRevisionTable(double x, double y,
            string templatePath)
        {

            Sheet currentsheet = (Sheet)_drawingDoc.GetCurrentSheet();

            revTableAnno = currentsheet.InsertRevisionTable2(true, x, y, (int)swBOMConfigurationAnchorType_e.swBOMConfigurationAnchor_TopLeft, string.IsNullOrEmpty(templatePath) ? "" : templatePath, (int)swRevisionTableSymbolShape_e.swRevisionTable_CircleSymbol, true);

            

            if (revTableAnno == null)
            {
                Console.WriteLine("Failed to insert revision table.");
            }

            return revTableAnno;
        }

        public bool AddRevisionRow(RevisionTableAnnotation revTable,
            string revisionId, string description, string date, string approvedBy)
        {
            
            if (revTable == null) return false;

            int revMark = revTable.AddRevision(revisionId);
            if (revMark == 0) return false;

            TableAnnotation table = (TableAnnotation)revTable;
            int lastRow = table.RowCount - 1;

            // Column layout: 0=Rev, 1=Description, 2=Date, 3=Approved
            if (table.ColumnCount > 1 && !string.IsNullOrEmpty(description))
                table.Text[lastRow, 1] = description;
            if (table.ColumnCount > 2 && !string.IsNullOrEmpty(date))
                table.Text[lastRow, 2] = date;
            if (table.ColumnCount > 3 && !string.IsNullOrEmpty(approvedBy))
                table.Text[lastRow, 3] = approvedBy;

            return true;
        }

        // -------------------------------------------
        // General Table
        // -------------------------------------------

        public TableAnnotation InsertGeneralTable(double x, double y,
            int rows, int columns, double rowHeight, double colWidth,
            string templatePath)
        {
            TableAnnotation table = (TableAnnotation)_drawingDoc.InsertTableAnnotation2(
                true, x, y,
                (int)swBOMConfigurationAnchorType_e.swBOMConfigurationAnchor_TopLeft,
                string.IsNullOrEmpty(templatePath) ? "" : templatePath,
                rows, columns);

            if (table == null)
            {
                Console.WriteLine("Failed to insert general table.");
                return null;
            }

            for (int r = 0; r < table.RowCount; r++)
                table.SetRowHeight(r, rowHeight, 0);
            for (int c = 0; c < table.ColumnCount; c++)
                table.SetColumnWidth(c, colWidth,0);

            return table;
        }

        public bool SetTableCell(TableAnnotation table, int row, int column, string value)
        {
            if (table == null || row < 0 || column < 0) return false;
            if (row >= table.RowCount || column >= table.ColumnCount) return false;

            table.Text[row, column] = value ?? "";
            return true;
        }

        // -------------------------------------------
        // Dimensions & Annotations
        // -------------------------------------------

        public object InsertDimension(double x, double y)
        {
            return _modelDoc.AddDimension2(x, y, 0);
        }

        /// <summary>
        /// Inserts auto-balloons for all components visible in the specified view.
        /// </summary>
        public AutoBalloonOptions InsertAutoBalloonsForView(View view)
        {
            if (view == null) return null;

            _drawingDoc.ActivateView(view.Name);

            object vNotes;

            // AutoBalloonOptions 
            AutoBalloonOptions autoballoonParams = _drawingDoc.CreateAutoBalloonOptions();            
            autoballoonParams.Style = (int)swBalloonStyle_e.swBS_Circular;
            autoballoonParams.Size = (int)swBalloonFit_e.swBF_Tightest;
            autoballoonParams.Layout = (int)swBalloonLayoutType_e.swDetailingBalloonLayout_Top;

            //  Create the balloon
            vNotes = _drawingDoc.AutoBalloon5(autoballoonParams);

            return autoballoonParams;
        }

        // -------------------------------------------
        // Save Operations
        // -------------------------------------------

        public bool SaveDrawing(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return false;

            int errors = 0, warnings = 0;
            bool result = _modelDoc.Extension.SaveAs(
                filePath,
                (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                (int)swSaveAsOptions_e.swSaveAsOptions_Silent,
                null, ref errors, ref warnings);

            if (!result)
            {
                Console.WriteLine($"Failed to save drawing. Errors: {errors}, Warnings: {warnings}");
            }

            return result;
        }

        public bool ExportToPdf(string pdfPath)
        {
            if (string.IsNullOrEmpty(pdfPath)) return false;

            int errors = 0, warnings = 0;
            return _modelDoc.Extension.SaveAs(
                pdfPath,
                (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                (int)swSaveAsOptions_e.swSaveAsOptions_Silent,
                null, ref errors, ref warnings);
        }
    }
}
