//
// Copyright (c) 2017 The nanoFramework project contributors
// See LICENSE file in the project root for full license information.
//

using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.ComponentModel.Design;
using Task = System.Threading.Tasks.Task;



namespace nanoFramework.Tools.VisualStudio.Extension
{
    internal sealed class DeviceExplorerVisualStudioMenuBarCommand
    {
        public static DeviceExplorerVisualStudioMenuBarCommand Instance { get; private set; }
        private readonly Package _package;

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private System.IServiceProvider ServiceProvider => _package;
            
        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        private static readonly Guid menuGroupID = new Guid("c975c4ec-f229-45dd-b681-e42815641675");

        /// <summary>
        /// Command ID.
        /// </summary>
        private const int CommandId = 0x0100;

        private DeviceExplorerVisualStudioMenuBarCommand(AsyncPackage package)
        {
            _package = package ?? throw new ArgumentNullException("Package can't be null.");

            var commandService = ServiceProvider.GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            Microsoft.Assumes.Present(commandService);

            var menuCommandID = new CommandID(menuGroupID, CommandId);
            var menuItem = new MenuCommand(ShowToolWindow, menuCommandID);
            commandService.AddCommand(menuItem);
        }


        public static async Task InitializeAsync(AsyncPackage package)
        {
            Instance = new DeviceExplorerVisualStudioMenuBarCommand(package);
        }

        /// <summary>
        /// Shows the tool window when the menu item is clicked.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event args.</param>
        private void ShowToolWindow(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            // Get the instance number 0 of this tool window. This window is single instance so this instance
            // is actually the only one.
            // The last flag is set to true so that if the tool window does not exists it will be created.
            ToolWindowPane window = _package.FindToolWindow(typeof(DeviceExplorerWindowPane), 0, true);
            if ((window == null) || (window.Frame == null))
            {
                throw new NotSupportedException("Cannot create nanoFramework Device Explorer tool window.");
            }

            IVsWindowFrame windowFrame = (IVsWindowFrame)window.Frame;
            Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(windowFrame.Show());
        }
    }
}