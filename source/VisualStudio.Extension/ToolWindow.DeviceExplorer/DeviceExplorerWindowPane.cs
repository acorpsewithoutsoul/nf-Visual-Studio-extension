//
// Copyright (c) 2017 The nanoFramework project contributors
// See LICENSE file in the project root for full license information.
//

namespace nanoFramework.Tools.VisualStudio.Extension
{
    using System;
    using System.Runtime.InteropServices;
    using Microsoft.VisualStudio.Shell;
    using System.ComponentModel.Design;
    using Microsoft.VisualStudio.Shell.Interop;

    /// <summary>
    /// This class implements the tool window exposed by this package and hosts the 
    /// Device Explorer Toolbar and Device Explorer Control.
    /// </summary>
    /// <remarks>
    /// In Visual Studio tool windows are composed of a frame (implemented by the shell) and a pane,
    /// usually implemented by the package implementer.
    /// <para>
    /// This class derives from the ToolWindowPane class provided from the MPF in order to use its
    /// implementation of the IVsUIElementPane interface.
    /// </para>
    /// </remarks>
    [Guid("65ff0124-880b-4bf4-9441-08a10b4e4c06")]
    public class DeviceExplorerWindowPane : ToolWindowPane
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DeviceExplorerWindowPane"/> class.
        /// </summary>
        public DeviceExplorerWindowPane() : base(null)
        {
            Caption = "Device Explorer - Home";

            // set the toolbar for this control
            ToolBar = DeviceExplorerToolbar.CommandID;
            ToolBarLocation = (int)VSTWT_LOCATION.VSTWT_TOP;

            // This is the user control hosted by the tool window; Note that, even if this class implements IDisposable,
            // we are not calling Dispose on this object. This is because ToolWindowPane calls Dispose on
            // the object returned by the Content property.
            base.Content = new DeviceExplorerControl();
            BitmapImageMoniker = NanoFrameworkMonikers.NanoFramework;
        }
    }
}