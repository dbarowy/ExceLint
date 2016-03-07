﻿using System;
using System.Collections.Generic;
using Microsoft.Office.Tools.Ribbon;
using Microsoft.FSharp.Core;
using Excel = Microsoft.Office.Interop.Excel;

namespace ExceLintUI
{
    public partial class ExceLintRibbon
    {
        Dictionary<Excel.Workbook, WorkbookState> wbstates = new Dictionary<Excel.Workbook, WorkbookState>();
        WorkbookState currentWorkbook;

        #region BUTTON_HANDLERS
        private void AnalyzeButton_Click(object sender, RibbonControlEventArgs e)
        {
            // check for debug easter egg
            if ((System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Alt) > 0)
            {
                currentWorkbook.DebugMode = true;
            }

            var prop = getProportion(this.SensitivityTextBox.Text, this.SensitivityTextBox.Label);
            if (prop == FSharpOption<double>.None)
            {
                return;
            }
            else
            {
                currentWorkbook.toolProportion = prop.Value;
                try
                {
                    currentWorkbook.analyze(WorkbookState.MAX_DURATION_IN_MS, getConfig());
                    currentWorkbook.flag();
                    setUIState(currentWorkbook);
                }
                catch (Parcel.ParseException ex)
                {
                    System.Windows.Forms.Clipboard.SetText(ex.Message);
                    System.Windows.Forms.MessageBox.Show("Could not parse the formula string:\n" + ex.Message);
                    return;
                }
                catch (System.OutOfMemoryException ex)
                {
                    System.Windows.Forms.MessageBox.Show("Insufficient memory to perform analysis.");
                    return;
                }
            }
        }
        
        private void FixErrorButton_Click(object sender, RibbonControlEventArgs e)
        {
            currentWorkbook.fixError(setUIState, getConfig());
        }

        private void MarkAsOKButton_Click(object sender, RibbonControlEventArgs e)
        {
            currentWorkbook.markAsOK();
            setUIState(currentWorkbook);
        }

        private void StartOverButton_Click(object sender, RibbonControlEventArgs e)
        {
            currentWorkbook.resetTool();
            setUIState(currentWorkbook);
        }

        private void button1_Click(object sender, RibbonControlEventArgs e)
        {
            currentWorkbook.getVectors();
        }

        private void button2_Click(object sender, RibbonControlEventArgs e)
        {
            currentWorkbook.getL2NormSum();
        }

        private void button3_Click(object sender, RibbonControlEventArgs e)
        {
            currentWorkbook.getRelativeVectors();
        }

        #endregion BUTTON_HANDLERS

        #region EVENTS
        private void SetUIStateNoWorkbooks()
        {
            this.MarkAsOKButton.Enabled = false;
            this.FixErrorButton.Enabled = false;
            this.StartOverButton.Enabled = false;
            this.AnalyzeButton.Enabled = false;
        }

        private void ExceLintRibbon_Load(object sender, RibbonUIEventArgs e)
        {
            // Callbacks for handling workbook state objects
            //WorkbookOpen(Globals.ThisAddIn.Application.ActiveWorkbook);
            //((Excel.AppEvents_Event)Globals.ThisAddIn.Application).NewWorkbook += WorkbookOpen;
            Globals.ThisAddIn.Application.WorkbookOpen += WorkbookOpen;
            Globals.ThisAddIn.Application.WorkbookActivate += WorkbookActivated;
            Globals.ThisAddIn.Application.WorkbookDeactivate += WorkbookDeactivated;
            Globals.ThisAddIn.Application.WorkbookBeforeClose += WorkbookClose;

            // sometimes the default blank workbook opens *before* the CheckCell
            // add-in is loaded so we have to handle sheet state specially.
            if (currentWorkbook == null)
            {
                var wb = Globals.ThisAddIn.Application.ActiveWorkbook;
                if (wb == null)
                {
                    // the plugin loaded first; there's no active workbook
                    return;
                }
                WorkbookOpen(wb);
                WorkbookActivated(wb);
            }
        }

        // This event is called when Excel opens a workbook
        private void WorkbookOpen(Excel.Workbook workbook)
        {
            wbstates.Add(workbook, new WorkbookState(Globals.ThisAddIn.Application, workbook));
        }

        // This event is called when Excel brings an opened workbook
        // to the foreground
        private void WorkbookActivated(Excel.Workbook workbook)
        {
            // when opening a blank sheet, Excel does not emit
            // a WorkbookOpen event, so we need to call it manually
            if (!wbstates.ContainsKey(workbook))
            {
                WorkbookOpen(workbook);
            }
            currentWorkbook = wbstates[workbook];
            setUIState(currentWorkbook);
        }

        // This even it called when Excel sends an opened workbook
        // to the background
        private void WorkbookDeactivated(Excel.Workbook workbook)
        {
            currentWorkbook = null;
            // WorkbookBeforeClose event does not fire for default workbooks
            // containing no data
            var wbs = new List<Excel.Workbook>();
            foreach (var wb in Globals.ThisAddIn.Application.Workbooks)
            {
                if (wb != workbook)
                {
                    wbs.Add((Excel.Workbook)wb);
                }
            }

            if (wbs.Count == 0)
            {
                wbstates.Clear();
                SetUIStateNoWorkbooks();
            }
        }

        private void WorkbookClose(Excel.Workbook workbook, ref bool Cancel)
        {
            wbstates.Remove(workbook);
            if (wbstates.Count == 0)
            {
                SetUIStateNoWorkbooks();
            }
        }
        #endregion EVENTS

        #region UTILITY_FUNCTIONS
        private static FSharpOption<double> getProportion(string input, string label)
        {
            var errormsg = label + " must be a value between 0 and 100.";

            try
            {
                double prop = Double.Parse(input) / 100.0;

                if (prop <= 0 || prop > 100)
                {
                    System.Windows.Forms.MessageBox.Show(errormsg);
                }

                return FSharpOption<double>.Some(prop);
            }
            catch
            {
                System.Windows.Forms.MessageBox.Show(errormsg);
                return FSharpOption<double>.None;
            }
        }

        private void setUIState(WorkbookState wbs)
        {
            this.MarkAsOKButton.Enabled = wbs.MarkAsOK_Enabled;
            this.FixErrorButton.Enabled = wbs.FixError_Enabled;
            this.StartOverButton.Enabled = wbs.ClearColoringButton_Enabled;
            this.AnalyzeButton.Enabled = wbs.Analyze_Enabled;
        }

        #endregion UTILITY_FUNCTIONS

        public ExceLint.Analysis.FeatureConf getConfig()
        {
            var c = new ExceLint.Analysis.FeatureConf();

            if (this.inDegree.Checked) { c = c.enableInDegree(); }
            if (this.outDegree.Checked) { c = c.enableOutDegree(); }
            if (this.combinedDegree.Checked) { c = c.enableCombinedDegree(); }
            if (this.inVectors.Checked) { c = c.enableFormulaRelativeL2NormSum(); }
            if (this.outVectors.Checked) { c = c.enableDataRelativeL2NormSum(); }
            if (this.inVectorsAbs.Checked) { c = c.enableFormulaAbsoluteL2NormSum(); }
            if (this.outVectorsAbs.Checked) { c = c.enableDataAbsoluteL2NormSum(); }
            if (this.allCellsFreq.Checked) { c = c.analyzeRelativeToAllCells(); }
            if (this.columnCellsFreq.Checked) { c = c.analyzeRelativeToColumns(); }
            if (this.rowCellsFreq.Checked) { c = c.analyzeRelativeToRows(); }

            return c;
        }

        private void ToDOT_Click(object sender, RibbonControlEventArgs e)
        {
            var graphviz = currentWorkbook.ToDOT();
            System.Windows.Clipboard.SetText(graphviz);
            System.Windows.Forms.MessageBox.Show("Done. Graph is in the clipboard.");
        }
    }
}
