﻿//
// Copyright (c) 2017 The nanoFramework project contributors
// See LICENSE file in the project root for full license information.
//

namespace nanoFramework.Tools.VisualStudio.Extension
{
    using GalaSoft.MvvmLight.Messaging;
    using Microsoft.VisualStudio.Shell;
    using nanoFramework.Tools.Debugger;
    using nanoFramework.Tools.VisualStudio.Extension.ToolWindow.ViewModel;
    using System;
    using System.Windows.Controls;
    using System.Windows.Threading;

    /// <summary>
    /// Interaction logic for DeviceExplorerControl.
    /// </summary>
    public partial class DeviceExplorerControl : UserControl
    {
        // strongly-typed view models enable x:bind
        private DeviceExplorerControlViewModel DeviceExplorerControlViewModel => DataContext as DeviceExplorerControlViewModel;

        /// <summary>
        /// Initializes a new instance of the <see cref="DeviceExplorerControl"/> class.
        /// </summary>
        public DeviceExplorerControl()
        {
            InitializeComponent();

            Loaded += DeviceExplorerControl_Loaded;

            Messenger.Default.Register<NotificationMessage>(this, DeviceExplorerControlViewModel.MessagingTokens.ForceSelectionOfNanoDevice, (message) => ForceSelectionOfNanoDeviceHandlerAsync());
        }
        
        private void DeviceExplorerControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            // update the status of the control button
            DeviceExplorerControlViewModel.IsViewLoaded = true;
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            NavigateHyperlink(e.Uri);
            e.Handled = true;
        }

        private void NavigateHyperlink(Uri uri)
        {
            string page = uri.AbsoluteUri;
            VsShellUtilities.OpenSystemBrowser(page);

            // TODO: add telemetry for clicks 
        }

        private void DevicesTreeView_SelectedItemChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<object> e)
        {
            // if user has selected the 'devices' TreeViewItem (collapsing the tree view...)
            if (e.NewValue.GetType().Equals(typeof(TreeViewItem)))
            {
                // clear selected device
                // can't select header as the selected device
                DeviceExplorerControlViewModel.SelectedDevice = null;
                return;
            }

            // sanity check for no device in tree view
            if ((sender as TreeView).Items.Count > 0)
            {
                DeviceExplorerControlViewModel.SelectedDevice = (NanoDeviceBase)e.NewValue;
            }
        }

        
        #region MVVM messaging handlers

        private async System.Threading.Tasks.Task ForceSelectionOfNanoDeviceHandlerAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            // make sure the item in the treeview is selected, in case the selected device was changed in the view model
            if (deviceTreeView.SelectedItem != null)
            {
                if (deviceTreeView.SelectedItem.GetType().IsSubclassOf(typeof(NanoDeviceBase)))
                {
                    // check if it's the same so we don't switch 
                    if (((NanoDeviceBase)deviceTreeView.SelectedItem).Description == DeviceExplorerControlViewModel.SelectedDevice.Description)
                    {
                        // nothing to do here
                        return;
                    }
                }
            }

            // select the device
            var deviceItem = DevicesHeaderItem.ItemContainerGenerator.ContainerFromItem(DeviceExplorerControlViewModel.SelectedDevice) as TreeViewItem;
            if (deviceItem != null)
            {
                // need to disable the event handler otherwise it will mess the selection
                deviceTreeView.SelectedItemChanged -= DevicesTreeView_SelectedItemChanged;

                deviceItem.IsSelected = true;

                // enabled it back
                deviceTreeView.SelectedItemChanged += DevicesTreeView_SelectedItemChanged;

                // force redrawing to show selection
                deviceTreeView.InvalidateVisual();
                deviceTreeView.UpdateLayout();
                deviceTreeView.InvalidateVisual();
            }
        }

        #endregion
    }
}
