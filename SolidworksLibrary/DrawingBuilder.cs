using System;
using System.Collections.Generic;
using CAD;
using SldWorks;
using SwConst;

namespace SolidworksLibrary
{
    public class DrawingBuilder
    {
        // -------------------------------------------
        // Drawing Document Creation
        // -------------------------------------------

        public static DrawingDoc CreateDrawingDocument(SldWorks.SldWorks swApp, out ModelDoc2 modelDoc)
        {
            string drawingTemplate = swApp.GetUserPreferenceStringValue(
                (int)swUserPreferenceStringValue_e.swDefaultTemplateDrawing);

            if (string.IsNullOrEmpty(drawingTemplate))
            {
                throw new InvalidOperationException("Drawing template not found in SolidWorks settings.");
            }

            object model = swApp.NewDocument(drawingTemplate, 0, 0, 0);
            if (model == null)
            {
                throw new InvalidOperationException("Failed to create drawing document.");
            }

            modelDoc = (ModelDoc2)model;
            return (DrawingDoc)model;
        }

        // -------------------------------------------
        // Sheet Management
        // -------------------------------------------

        public static bool ActivateSheet(DrawingDoc drawingDoc, string sheetName)
        {
            if (string.IsNullOrEmpty(sheetName)) return false;
            return drawingDoc.ActivateSheet(sheetName);
        }

        /// <summary>
        /// Adds a new sheet to the drawing.
        /// </summary>
        public static bool AddSheet(DrawingDoc drawingDoc, string sheetName,
            swDwgPaperSizes_e paperSize,
            swDwgTemplates_e sheetTemplate,
            double scaleNumerator, double scaleDenominator,
            bool firstAngleProjection = true,
            string templatePath = "",
            double customWidth = 0, double customHeight = 0,
            string propertyViewName = "")
        {
            return drawingDoc.NewSheet3(
                sheetName,
                (int)paperSize,
                (int)sheetTemplate,
                scaleNumerator,
                scaleDenominator,
                firstAngleProjection,
                string.IsNullOrEmpty(templatePath) ? "" : templatePath,
                customWidth,
                customHeight,
                propertyViewName ?? "");
        }

        // -------------------------------------------
        // Standard Views
        // -------------------------------------------

        public static View CreateFrontView(DrawingDoc drawingDoc, string modelPath, double x, double y)
        {
            return CreateNamedView(drawingDoc, modelPath, x, y, (int)swStandardViews_e.swFrontView);
        }

        public static View CreateBackView(DrawingDoc drawingDoc, string modelPath, double x, double y)
        {
            return CreateNamedView(drawingDoc, modelPath, x, y, (int)swStandardViews_e.swBackView);
        }

        public static View CreateTopView(DrawingDoc drawingDoc, string modelPath, double x, double y)
        {
            return CreateNamedView(drawingDoc, modelPath, x, y, (int)swStandardViews_e.swTopView);
        }

        public static View CreateBottomView(DrawingDoc drawingDoc, string modelPath, double x, double y)
        {
            return CreateNamedView(drawingDoc, modelPath, x, y, (int)swStandardViews_e.swBottomView);
        }

        public static View CreateLeftView(DrawingDoc drawingDoc, string modelPath, double x, double y)
        {
            return CreateNamedView(drawingDoc, modelPath, x, y, (int)swStandardViews_e.swLeftView);
        }

        public static View CreateRightView(DrawingDoc drawingDoc, string modelPath, double x, double y)
        {
            return CreateNamedView(drawingDoc, modelPath, x, y, (int)swStandardViews_e.swRightView);
        }

        public static View CreateIsometricView(DrawingDoc drawingDoc, string modelPath, double x, double y)
        {
            return CreateNamedView(drawingDoc, modelPath, x, y, (int)swStandardViews_e.swIsometricView);
        }

        public static View CreateTrimetricView(DrawingDoc drawingDoc, string modelPath, double x, double y)
        {
            return CreateNamedView(drawingDoc, modelPath, x, y, (int)swStandardViews_e.swTrimetricView);
        }

        public static View CreateDimetricView(DrawingDoc drawingDoc, string modelPath, double x, double y)
        {
            return CreateNamedView(drawingDoc, modelPath, x, y, (int)swStandardViews_e.swDimetricView);
        }

        // -------------------------------------------
        // Projected & Derived Views
        // -------------------------------------------

        public static View CreateProjectedView(DrawingDoc drawingDoc, double x, double y)
        {
            return (View)drawingDoc.CreateUnfoldedViewAt3(x, y, 0, false);
        }

        public static View CreateAuxiliaryView(DrawingDoc drawingDoc, double x, double y, string label,
            int arrowDirection = 0, bool flipArrow = false,
            bool showLabel = true, bool flipView = false)
        {
            return (View)drawingDoc.CreateAuxiliaryViewAt2(
                x, y, arrowDirection, flipArrow, label, showLabel, flipView);
        }

        public static View CreateDetailView(DrawingDoc drawingDoc, double viewX, double viewY, string detailLabel,
            double scaleNumerator = 2, double scaleDenominator = 1,
            bool fullOutline = true, bool jaggedOutline = false, bool noOutline = false)
        {
            return (View)drawingDoc.CreateDetailViewAt4(
                viewX, viewY, 0,
                (int)swDetViewStyle_e.swDetViewSTANDARD,
                scaleNumerator, scaleDenominator,
                detailLabel,
                (int)swDetCircleShowType_e.swDetCircleCIRCLE,
                fullOutline, jaggedOutline, noOutline, 5);
        }

        public static View CreateSectionView(DrawingDoc drawingDoc, double x, double y, string sectionLabel,
            int options = (int)swCreateSectionViewAtOptions_e.swCreateSectionView_NotAligned,
            object excludedComponents = null, double sectionDepth = 0)
        {
            return (View)drawingDoc.CreateSectionViewAt5(
                x, y, 0, sectionLabel, options, excludedComponents, sectionDepth);
        }

        public static bool CreateBrokenOutSection(DrawingDoc drawingDoc, double depth)
        {
            return drawingDoc.CreateBreakOutSection(depth);
        }

        public static bool CropView(DrawingDoc drawingDoc, View view,
            bool fullOutline = true, bool jaggedOutline = false, int shapeIntensity = 5)
        {
            if (view == null) return false;

            drawingDoc.ActivateView(view.Name);
            int error = view.Crop2(fullOutline, jaggedOutline, shapeIntensity);
            return error == (int)swCropViewErrors_e.swCropViewErrors_NoError;
        }

        public static bool RemoveCropView(DrawingDoc drawingDoc, ModelDoc2 modelDoc, View view)
        {
            if (view == null || !view.IsCropped()) return false;

            drawingDoc.ActivateView(view.Name);
            modelDoc.Extension.SelectByID2(
                view.Name, "DRAWINGVIEW", 0, 0, 0, false, 0, null, 0);
            modelDoc.EditSketch();
            modelDoc.Extension.DeleteSelection2(
                (int)swDeleteSelectionOptions_e.swDelete_Absorbed);
            modelDoc.ClearSelection2(true);
            return !view.IsCropped();
        }

        public static bool IsCropped(View view)
        {
            if (view == null) return false;
            return view.IsCropped();
        }

        public static bool InsertAlternatePositionView(DrawingDoc drawingDoc,
            View parentView, string configName)
        {
            if (parentView == null) return false;

            drawingDoc.ActivateView(parentView.Name);
            var result = parentView.InsertAlternateView(
                string.IsNullOrEmpty(configName) ? "" : configName);
            return result != null;
        }

        public static View CreateRelativeView(DrawingDoc drawingDoc, string modelPath, double x, double y)
        {
            if (string.IsNullOrEmpty(modelPath)) return null;

            return (View)drawingDoc.CreateRelativeView(modelPath, x, y, 0, 1);
        }

        public static View Create3DDrawingView(DrawingDoc drawingDoc, string modelPath, double x, double y)
        {
            return CreateNamedView(drawingDoc, modelPath, x, y, (int)swStandardViews_e.swIsometricView);
        }

        // -------------------------------------------
        // Flat Pattern & Custom Views
        // -------------------------------------------

        public static View CreateFlatPatternView(DrawingDoc drawingDoc, string modelPath,
            double x, double y, string configName)
        {
            return (View)drawingDoc.CreateFlatPatternViewFromModelView3(
                modelPath, configName, x, y, 0, true, false);
        }

        public static View CreateCustomView(DrawingDoc drawingDoc, string modelPath,
            double x, double y, string viewName)
        {
            return (View)drawingDoc.CreateDrawViewFromModelView3(
                modelPath, viewName, x, y, 0);
        }

        // -------------------------------------------
        // Core Named-View Helper
        // -------------------------------------------

        private static View CreateNamedView(DrawingDoc drawingDoc, string modelPath,
            double x, double y, int standardView)
        {
            if (string.IsNullOrEmpty(modelPath))
            {
                Console.WriteLine("Model path is required to create a drawing view.");
                return null;
            }

            string viewName = GetStandardViewName(standardView);

            View view = (View)drawingDoc.CreateDrawViewFromModelView3(
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

        public static Note InsertNote(ModelDoc2 modelDoc, string text, double x, double y)
        {
            if (string.IsNullOrEmpty(text)) return null;

            modelDoc.SetAddToDB(true);
            Note note = (Note)modelDoc.InsertNote(text);
            modelDoc.SetAddToDB(false);

            if (note != null)
            {
                Annotation annotation = (Annotation)note.GetAnnotation();
                annotation?.SetPosition(x, y, 0);
            }

            return note;
        }

        public static Note InsertNoteWithLeader(ModelDoc2 modelDoc, string text, double x, double y)
        {
            modelDoc.SetAddToDB(true);
            Note note = (Note)modelDoc.InsertNote(text);
            modelDoc.SetAddToDB(false);

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

        public static BomTableAnnotation InsertBomTable(DrawingDoc drawingDoc,
            double x, double y,
            swBomType_e bomType, string configuration, string templatePath)
        {
            View activeView = (View)drawingDoc.ActiveDrawingView;
            if (activeView == null)
            {
                Console.WriteLine("No active drawing view for BOM insertion.");
                return null;
            }

            BomTableAnnotation bomTable = (BomTableAnnotation)activeView.InsertBomTable4(
                false, x, y, 2,
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

        public static BomTableAnnotation InsertIndentedBom(DrawingDoc drawingDoc,
            double x, double y, string configuration, string templatePath)
        {
            return InsertBomTable(drawingDoc, x, y, swBomType_e.swBomType_Indented,
                configuration, templatePath);
        }

        // -------------------------------------------
        // Title Block
        // -------------------------------------------

        public static bool FillTitleBlockField(ModelDoc2 modelDoc, string fieldName, string value)
        {
            if (string.IsNullOrEmpty(fieldName)) return false;

            bool selected = modelDoc.Extension.SelectByID2(
                fieldName, "NOTE", 0, 0, 0, false, 0, null, 0);

            if (!selected) return false;

            SelectionMgr selMgr = (SelectionMgr)modelDoc.SelectionManager;
            Note note = (Note)selMgr.GetSelectedObject6(1, -1);

            if (note != null)
            {
                note.SetText(value);
                modelDoc.ClearSelection2(true);
                return true;
            }

            modelDoc.ClearSelection2(true);
            return false;
        }

        public static void FillTitleBlock(ModelDoc2 modelDoc,
            string title, string drawnBy, string checkedBy,
            string approvedBy, string date, string partNumber,
            string revision, string material, string scale)
        {
            FillTitleBlockField(modelDoc, "Title", title);
            FillTitleBlockField(modelDoc, "DrawnBy", drawnBy);
            FillTitleBlockField(modelDoc, "CheckedBy", checkedBy);
            FillTitleBlockField(modelDoc, "ApprovedBy", approvedBy);
            FillTitleBlockField(modelDoc, "DrawnDate", date);
            FillTitleBlockField(modelDoc, "PartNo", partNumber);
            FillTitleBlockField(modelDoc, "Revision", revision);
            FillTitleBlockField(modelDoc, "Material", material);
            FillTitleBlockField(modelDoc, "Scale", scale);
        }

        // -------------------------------------------
        // Revision Table
        // -------------------------------------------

        public static RevisionTableAnnotation InsertRevisionTable(DrawingDoc drawingDoc,
            double x, double y, string templatePath)
        {
            Sheet currentsheet = (Sheet)drawingDoc.GetCurrentSheet();

            RevisionTableAnnotation revTableAnno = currentsheet.InsertRevisionTable2(
                true, x, y,
                (int)swBOMConfigurationAnchorType_e.swBOMConfigurationAnchor_TopLeft,
                string.IsNullOrEmpty(templatePath) ? "" : templatePath,
                (int)swRevisionTableSymbolShape_e.swRevisionTable_CircleSymbol, true);

            if (revTableAnno == null)
            {
                Console.WriteLine("Failed to insert revision table.");
            }

            return revTableAnno;
        }

        public static bool AddRevisionRow(RevisionTableAnnotation revTable,
            string revisionId, string description, string date, string approvedBy)
        {
            if (revTable == null) return false;

            int revMark = revTable.AddRevision(revisionId);
            if (revMark == 0) return false;

            TableAnnotation table = (TableAnnotation)revTable;
            int lastRow = table.RowCount - 1;

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

        public static TableAnnotation InsertGeneralTable(DrawingDoc drawingDoc,
            double x, double y,
            int rows, int columns, double rowHeight, double colWidth,
            string templatePath)
        {
            TableAnnotation table = (TableAnnotation)drawingDoc.InsertTableAnnotation2(
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
                table.SetColumnWidth(c, colWidth, 0);

            return table;
        }

        public static bool SetTableCell(TableAnnotation table, int row, int column, string value)
        {
            if (table == null || row < 0 || column < 0) return false;
            if (row >= table.RowCount || column >= table.ColumnCount) return false;

            table.Text[row, column] = value ?? "";
            return true;
        }

        // -------------------------------------------
        // Dimensions & Annotations
        // -------------------------------------------

        public static object InsertDimension(ModelDoc2 modelDoc, double x, double y)
        {
            return modelDoc.AddDimension2(x, y, 0);
        }

        public static AutoBalloonOptions InsertAutoBalloonsForView(DrawingDoc drawingDoc, View view)
        {
            if (view == null) return null;

            drawingDoc.ActivateView(view.Name);

            AutoBalloonOptions autoballoonParams = drawingDoc.CreateAutoBalloonOptions();
            autoballoonParams.Style = (int)swBalloonStyle_e.swBS_Circular;
            autoballoonParams.Size = (int)swBalloonFit_e.swBF_Tightest;
            autoballoonParams.Layout = (int)swBalloonLayoutType_e.swDetailingBalloonLayout_Top;

            drawingDoc.AutoBalloon5(autoballoonParams);

            return autoballoonParams;
        }

        // -------------------------------------------
        // Save Operations
        // -------------------------------------------

        public static bool SaveDrawing(ModelDoc2 modelDoc, string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return false;

            int errors = 0, warnings = 0;
            bool result = modelDoc.Extension.SaveAs(
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

        public static bool ExportToPdf(ModelDoc2 modelDoc, string pdfPath)
        {
            if (string.IsNullOrEmpty(pdfPath)) return false;

            int errors = 0, warnings = 0;
            return modelDoc.Extension.SaveAs(
                pdfPath,
                (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                (int)swSaveAsOptions_e.swSaveAsOptions_Silent,
                null, ref errors, ref warnings);
        }
    }
}
