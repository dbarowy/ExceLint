﻿// ------------------------------------------------------------------------------
//  <auto-generated>
//      This code was generated by coded UI test builder.
//      Version: 14.0.0.0
//
//      Changes to this file may cause incorrect behavior and will be lost if
//      the code is regenerated.
//  </auto-generated>
// ------------------------------------------------------------------------------

namespace ExceLintTests
{
    using System;
    using System.CodeDom.Compiler;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Text.RegularExpressions;
    using System.Windows.Input;
    using Microsoft.VisualStudio.TestTools.UITest.Extension;
    using Microsoft.VisualStudio.TestTools.UITesting;
    using Microsoft.VisualStudio.TestTools.UITesting.WinControls;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Keyboard = Microsoft.VisualStudio.TestTools.UITesting.Keyboard;
    using Mouse = Microsoft.VisualStudio.TestTools.UITesting.Mouse;
    using MouseButtons = System.Windows.Forms.MouseButtons;
    
    
    [GeneratedCode("Coded UITest Builder", "14.0.23107.0")]
    public partial class UIMap
    {
        
        /// <summary>
        /// AuditRoundTripPerformance - Use 'AuditRoundTripPerformanceParams' to pass parameters into this method.
        /// </summary>
        public void AuditRoundTripPerformance()
        {
            #region Variable Declarations
            WinHyperlink uIItem01sumdatxlsHyperlink = this.UIExcelWindow.UIItem01sumdatxlsListItem.UIItem01sumdatxlsHyperlink;
            WinTabPage uIExceLintTabPage = this.UIExcelWindow.UIItemWindow.UIRibbonClient.UIExceLintTabPage;
            WinButton uIAuditButton = this.UIExcelWindow.UIItemWindow.UIItemToolBar.UIAuditButton;
            WinButton uIStartOverButton = this.UIExcelWindow.UIItemWindow.UIItemToolBar.UIStartOverButton;
            WinButton uICloseButton = this.UIExcelWindow.UIItemWindow.UIRibbonPropertyPage.UICloseButton;
            WinButton uIDontSaveButton = this.UIMicrosoftExcelWindow.UIMicrosoftExcelDialog.UIDontSaveButton;
            #endregion

            // Launch '%ProgramFiles(x86)%\Microsoft Office\root\Office16\EXCEL.EXE'
            ApplicationUnderTest eXCELApplication = ApplicationUnderTest.Launch(this.AuditRoundTripPerformanceParams.ExePath, this.AuditRoundTripPerformanceParams.AlternateExePath);

            // Click '01sumdat.xls' link
            Mouse.Click(uIItem01sumdatxlsHyperlink, new Point(186, 14));

            // Click 'ExceLint' tab
            Mouse.Click(uIExceLintTabPage, new Point(21, 13));

            // Click 'Audit' button
            Mouse.Click(uIAuditButton, new Point(27, 29));

            // Click 'Start Over' button
            Mouse.Click(uIStartOverButton, new Point(20, 30));

            // Click 'Close' button
            Mouse.Click(uICloseButton, new Point(20, 10));

            // Click 'Don't Save' button
            Mouse.Click(uIDontSaveButton, new Point(50, 18));
        }
        
        #region Properties
        public virtual AuditRoundTripPerformanceParams AuditRoundTripPerformanceParams
        {
            get
            {
                if ((this.mAuditRoundTripPerformanceParams == null))
                {
                    this.mAuditRoundTripPerformanceParams = new AuditRoundTripPerformanceParams();
                }
                return this.mAuditRoundTripPerformanceParams;
            }
        }
        
        public UIExcelWindow UIExcelWindow
        {
            get
            {
                if ((this.mUIExcelWindow == null))
                {
                    this.mUIExcelWindow = new UIExcelWindow();
                }
                return this.mUIExcelWindow;
            }
        }
        
        public UIMicrosoftExcelWindow UIMicrosoftExcelWindow
        {
            get
            {
                if ((this.mUIMicrosoftExcelWindow == null))
                {
                    this.mUIMicrosoftExcelWindow = new UIMicrosoftExcelWindow();
                }
                return this.mUIMicrosoftExcelWindow;
            }
        }
        #endregion
        
        #region Fields
        private AuditRoundTripPerformanceParams mAuditRoundTripPerformanceParams;
        
        private UIExcelWindow mUIExcelWindow;
        
        private UIMicrosoftExcelWindow mUIMicrosoftExcelWindow;
        #endregion
    }
    
    /// <summary>
    /// Parameters to be passed into 'AuditRoundTripPerformance'
    /// </summary>
    [GeneratedCode("Coded UITest Builder", "14.0.23107.0")]
    public class AuditRoundTripPerformanceParams
    {
        
        #region Fields
        /// <summary>
        /// Launch '%ProgramFiles(x86)%\Microsoft Office\root\Office16\EXCEL.EXE'
        /// </summary>
        public string ExePath = "C:\\Program Files (x86)\\Microsoft Office\\root\\Office16\\EXCEL.EXE";
        
        /// <summary>
        /// Launch '%ProgramFiles(x86)%\Microsoft Office\root\Office16\EXCEL.EXE'
        /// </summary>
        public string AlternateExePath = "%ProgramFiles(x86)%\\Microsoft Office\\root\\Office16\\EXCEL.EXE";
        #endregion
    }
    
    [GeneratedCode("Coded UITest Builder", "14.0.23107.0")]
    public class UIExcelWindow : WinWindow
    {
        
        public UIExcelWindow()
        {
            #region Search Criteria
            this.SearchProperties[WinWindow.PropertyNames.Name] = "Excel";
            this.SearchProperties[WinWindow.PropertyNames.ClassName] = "XLMAIN";
            this.WindowTitles.Add("Excel");
            this.WindowTitles.Add("01sumdat.xls  [Compatibility Mode] - Excel");
            #endregion
        }
        
        #region Properties
        public UIItem01sumdatxlsListItem UIItem01sumdatxlsListItem
        {
            get
            {
                if ((this.mUIItem01sumdatxlsListItem == null))
                {
                    this.mUIItem01sumdatxlsListItem = new UIItem01sumdatxlsListItem(this);
                }
                return this.mUIItem01sumdatxlsListItem;
            }
        }
        
        public UIItemWindow UIItemWindow
        {
            get
            {
                if ((this.mUIItemWindow == null))
                {
                    this.mUIItemWindow = new UIItemWindow(this);
                }
                return this.mUIItemWindow;
            }
        }
        #endregion
        
        #region Fields
        private UIItem01sumdatxlsListItem mUIItem01sumdatxlsListItem;
        
        private UIItemWindow mUIItemWindow;
        #endregion
    }
    
    [GeneratedCode("Coded UITest Builder", "14.0.23107.0")]
    public class UIItem01sumdatxlsListItem : WinListItem
    {
        
        public UIItem01sumdatxlsListItem(UITestControl searchLimitContainer) : 
                base(searchLimitContainer)
        {
            #region Search Criteria
            this.SearchProperties[WinListItem.PropertyNames.Name] = "01sumdat.xls";
            this.WindowTitles.Add("Excel");
            #endregion
        }
        
        #region Properties
        public WinHyperlink UIItem01sumdatxlsHyperlink
        {
            get
            {
                if ((this.mUIItem01sumdatxlsHyperlink == null))
                {
                    this.mUIItem01sumdatxlsHyperlink = new WinHyperlink(this);
                    #region Search Criteria
                    this.mUIItem01sumdatxlsHyperlink.SearchProperties[WinHyperlink.PropertyNames.Name] = "01sumdat.xls";
                    this.mUIItem01sumdatxlsHyperlink.WindowTitles.Add("Excel");
                    #endregion
                }
                return this.mUIItem01sumdatxlsHyperlink;
            }
        }
        #endregion
        
        #region Fields
        private WinHyperlink mUIItem01sumdatxlsHyperlink;
        #endregion
    }
    
    [GeneratedCode("Coded UITest Builder", "14.0.23107.0")]
    public class UIItemWindow : WinWindow
    {
        
        public UIItemWindow(UITestControl searchLimitContainer) : 
                base(searchLimitContainer)
        {
            #region Search Criteria
            this.SearchProperties[WinWindow.PropertyNames.AccessibleName] = "Ribbon";
            this.SearchProperties[WinWindow.PropertyNames.ClassName] = "NetUIHWND";
            this.WindowTitles.Add("01sumdat.xls  [Compatibility Mode] - Excel");
            #endregion
        }
        
        #region Properties
        public UIRibbonClient UIRibbonClient
        {
            get
            {
                if ((this.mUIRibbonClient == null))
                {
                    this.mUIRibbonClient = new UIRibbonClient(this);
                }
                return this.mUIRibbonClient;
            }
        }
        
        public UIItemToolBar UIItemToolBar
        {
            get
            {
                if ((this.mUIItemToolBar == null))
                {
                    this.mUIItemToolBar = new UIItemToolBar(this);
                }
                return this.mUIItemToolBar;
            }
        }
        
        public UIRibbonPropertyPage UIRibbonPropertyPage
        {
            get
            {
                if ((this.mUIRibbonPropertyPage == null))
                {
                    this.mUIRibbonPropertyPage = new UIRibbonPropertyPage(this);
                }
                return this.mUIRibbonPropertyPage;
            }
        }
        #endregion
        
        #region Fields
        private UIRibbonClient mUIRibbonClient;
        
        private UIItemToolBar mUIItemToolBar;
        
        private UIRibbonPropertyPage mUIRibbonPropertyPage;
        #endregion
    }
    
    [GeneratedCode("Coded UITest Builder", "14.0.23107.0")]
    public class UIRibbonClient : WinClient
    {
        
        public UIRibbonClient(UITestControl searchLimitContainer) : 
                base(searchLimitContainer)
        {
            #region Search Criteria
            this.WindowTitles.Add("01sumdat.xls  [Compatibility Mode] - Excel");
            #endregion
        }
        
        #region Properties
        public WinTabPage UIExceLintTabPage
        {
            get
            {
                if ((this.mUIExceLintTabPage == null))
                {
                    this.mUIExceLintTabPage = new WinTabPage(this);
                    #region Search Criteria
                    this.mUIExceLintTabPage.SearchProperties[WinTabPage.PropertyNames.Name] = "ExceLint";
                    this.mUIExceLintTabPage.WindowTitles.Add("01sumdat.xls  [Compatibility Mode] - Excel");
                    #endregion
                }
                return this.mUIExceLintTabPage;
            }
        }
        #endregion
        
        #region Fields
        private WinTabPage mUIExceLintTabPage;
        #endregion
    }
    
    [GeneratedCode("Coded UITest Builder", "14.0.23107.0")]
    public class UIItemToolBar : WinToolBar
    {
        
        public UIItemToolBar(UITestControl searchLimitContainer) : 
                base(searchLimitContainer)
        {
            #region Search Criteria
            this.WindowTitles.Add("01sumdat.xls  [Compatibility Mode] - Excel");
            #endregion
        }
        
        #region Properties
        public WinButton UIAuditButton
        {
            get
            {
                if ((this.mUIAuditButton == null))
                {
                    this.mUIAuditButton = new WinButton(this);
                    #region Search Criteria
                    this.mUIAuditButton.SearchProperties[WinButton.PropertyNames.Name] = "Audit";
                    this.mUIAuditButton.WindowTitles.Add("01sumdat.xls  [Compatibility Mode] - Excel");
                    #endregion
                }
                return this.mUIAuditButton;
            }
        }
        
        public WinButton UIStartOverButton
        {
            get
            {
                if ((this.mUIStartOverButton == null))
                {
                    this.mUIStartOverButton = new WinButton(this);
                    #region Search Criteria
                    this.mUIStartOverButton.SearchProperties[WinButton.PropertyNames.Name] = "Start Over";
                    this.mUIStartOverButton.WindowTitles.Add("01sumdat.xls  [Compatibility Mode] - Excel");
                    #endregion
                }
                return this.mUIStartOverButton;
            }
        }
        #endregion
        
        #region Fields
        private WinButton mUIAuditButton;
        
        private WinButton mUIStartOverButton;
        #endregion
    }
    
    [GeneratedCode("Coded UITest Builder", "14.0.23107.0")]
    public class UIRibbonPropertyPage : WinControl
    {
        
        public UIRibbonPropertyPage(UITestControl searchLimitContainer) : 
                base(searchLimitContainer)
        {
            #region Search Criteria
            this.SearchProperties[UITestControl.PropertyNames.Name] = "Ribbon";
            this.SearchProperties[UITestControl.PropertyNames.ControlType] = "PropertyPage";
            this.WindowTitles.Add("01sumdat.xls  [Compatibility Mode] - Excel");
            #endregion
        }
        
        #region Properties
        public WinButton UICloseButton
        {
            get
            {
                if ((this.mUICloseButton == null))
                {
                    this.mUICloseButton = new WinButton(this);
                    #region Search Criteria
                    this.mUICloseButton.SearchProperties[WinButton.PropertyNames.Name] = "Close";
                    this.mUICloseButton.WindowTitles.Add("01sumdat.xls  [Compatibility Mode] - Excel");
                    #endregion
                }
                return this.mUICloseButton;
            }
        }
        #endregion
        
        #region Fields
        private WinButton mUICloseButton;
        #endregion
    }
    
    [GeneratedCode("Coded UITest Builder", "14.0.23107.0")]
    public class UIMicrosoftExcelWindow : WinWindow
    {
        
        public UIMicrosoftExcelWindow()
        {
            #region Search Criteria
            this.SearchProperties[WinWindow.PropertyNames.Name] = "Microsoft Excel";
            this.SearchProperties[WinWindow.PropertyNames.ClassName] = "NUIDialog";
            this.WindowTitles.Add("Microsoft Excel");
            #endregion
        }
        
        #region Properties
        public UIMicrosoftExcelDialog UIMicrosoftExcelDialog
        {
            get
            {
                if ((this.mUIMicrosoftExcelDialog == null))
                {
                    this.mUIMicrosoftExcelDialog = new UIMicrosoftExcelDialog(this);
                }
                return this.mUIMicrosoftExcelDialog;
            }
        }
        #endregion
        
        #region Fields
        private UIMicrosoftExcelDialog mUIMicrosoftExcelDialog;
        #endregion
    }
    
    [GeneratedCode("Coded UITest Builder", "14.0.23107.0")]
    public class UIMicrosoftExcelDialog : WinControl
    {
        
        public UIMicrosoftExcelDialog(UITestControl searchLimitContainer) : 
                base(searchLimitContainer)
        {
            #region Search Criteria
            this.SearchProperties[UITestControl.PropertyNames.Name] = "Microsoft Excel";
            this.SearchProperties[UITestControl.PropertyNames.ControlType] = "Dialog";
            this.WindowTitles.Add("Microsoft Excel");
            #endregion
        }
        
        #region Properties
        public WinButton UIDontSaveButton
        {
            get
            {
                if ((this.mUIDontSaveButton == null))
                {
                    this.mUIDontSaveButton = new WinButton(this);
                    #region Search Criteria
                    this.mUIDontSaveButton.SearchProperties[WinButton.PropertyNames.Name] = "Don\'t Save";
                    this.mUIDontSaveButton.WindowTitles.Add("Microsoft Excel");
                    #endregion
                }
                return this.mUIDontSaveButton;
            }
        }
        #endregion
        
        #region Fields
        private WinButton mUIDontSaveButton;
        #endregion
    }
}
