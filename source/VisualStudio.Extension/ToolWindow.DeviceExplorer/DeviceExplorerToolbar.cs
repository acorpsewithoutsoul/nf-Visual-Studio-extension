//
// Copyright (c) 2017 The nanoFramework project contributors
// See LICENSE file in the project root for full license information.
//

using GalaSoft.MvvmLight.Messaging;
using Microsoft.VisualStudio.Shell;
using nanoFramework.Tools.Debugger.Extensions;
using nanoFramework.Tools.VisualStudio.Extension.ToolWindow.ViewModel;
using System;
using System.ComponentModel.Design;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Task = System.Threading.Tasks.Task;



namespace nanoFramework.Tools.VisualStudio.Extension
{

    //TODO separate the ViewModel from the View
    /// <summary>
    /// The toolbar for the Device Explorer Window Pane.
    /// Commands in this toolbar act upon the device selected in the Device Explorer Control.
    /// </summary>
    internal sealed class DeviceExplorerToolbar
    {
        private DeviceExplorerControlViewModel _deviceExplorerControlViewModel;

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly Package _package;

        // command set Guids
        private static readonly Guid menuGroupID = new Guid("DF641D51-1E8C-48E4-B549-CC6BCA9BDE19");
        private const int DeviceExplorerToolbarID = 0x1000;
        public static CommandID CommandID => new CommandID(menuGroupID, DeviceExplorerToolbarID);


        // toolbar command IDs
        private const int PingDeviceCommandID = 0x0210;
        private const int DeviceCapabilitiesID = 0x0220;
        private const int DeviceEraseID = 0x0230;
        private const int RebootID = 0x0240;
        private const int NetworkConfigID = 0x0250;

        // 2nd group
        private const int ShowInternalErrorsCommandID = 0x0300;

        // toolbar command Menu Commands 
        private MenuCommand _pingMenuCommand;
        private MenuCommand _capabilitiesMenuCommand;
        private MenuCommand _eraseMenuCommand;
        private MenuCommand _rebootMenuCommand;
        private MenuCommand _networkConfigMenuCommand;
        private MenuCommand _showInternalErrorsCommand;

        private INanoDeviceCommService _nanoDeviceCommService;
        public static DeviceExplorerToolbar Instance { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DeviceExplorerToolbar"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        private DeviceExplorerToolbar(Package package)
        {
           _package = package ?? throw new ArgumentNullException("Package can't be null.");
        }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private System.IServiceProvider ServiceProvider => _package;

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static async Task InitializeAsync(AsyncPackage package, DeviceExplorerControlViewModel deviceExplorerControlViewModel, INanoDeviceCommService nanoDeviceCommService)
        {
            Instance = new DeviceExplorerToolbar(package);

            Instance._deviceExplorerControlViewModel = deviceExplorerControlViewModel;
            Instance._nanoDeviceCommService = nanoDeviceCommService;

            // need to switch to the main thread to initialize the command handlers
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            Instance.CreateToolbarHandlers();
            Instance._deviceExplorerControlViewModel.ViewLoaded += Instance.DeviceExplorerControlViewModel_ViewLoaded;

            // setup message listeners to be notified of events occurring in the View Model
            Messenger.Default.Register<NotificationMessage>(Instance, DeviceExplorerControlViewModel.MessagingTokens.SelectedNanoDeviceHasChanged, (message) => Instance.SelectedNanoDeviceHasChangedHandler());
            Messenger.Default.Register<NotificationMessage>(Instance, DeviceExplorerControlViewModel.MessagingTokens.NanoDevicesCollectionHasChanged, (message) => Instance.NanoDevicesCollectionChangedHandler());
        }

        private void CreateToolbarHandlers()
        {
            // Create the handles for the toolbar commands
            var menuCommandService = ServiceProvider.GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            Microsoft.Assumes.Present(menuCommandService);

            _pingMenuCommand = GenerateToolbarMenuCommand(PingDeviceCommandHandler, PingDeviceCommandID, false, true);
            menuCommandService.AddCommand(_pingMenuCommand);

            _capabilitiesMenuCommand = GenerateToolbarMenuCommand(DeviceCapabilitiesCommandHandler, DeviceCapabilitiesID, false, true);
            menuCommandService.AddCommand(_capabilitiesMenuCommand);

            _eraseMenuCommand = GenerateToolbarMenuCommand(DeviceEraseCommandHandler, DeviceEraseID, false, true);
            menuCommandService.AddCommand(_eraseMenuCommand);

            _rebootMenuCommand = GenerateToolbarMenuCommand(RebootCommandHandler, RebootID, false, true);
            menuCommandService.AddCommand(_rebootMenuCommand);

            _networkConfigMenuCommand = GenerateToolbarMenuCommand(NetworkConfigCommandHandler, NetworkConfigID, false, true);
            menuCommandService.AddCommand(_networkConfigMenuCommand);

            _showInternalErrorsCommand = GenerateToolbarMenuCommand(ShowInternalErrorsCommandHandler, ShowInternalErrorsCommandID, false, true);
            // can't set the checked status here because the service provider of the preferences persistence is not available at this time
            // deferring to when the Device Explorer control is loaded
            //_showInternalErrorsCommand.Checked = NanoFrameworkPackage.OptionShowInternalErrors;
            menuCommandService.AddCommand(_showInternalErrorsCommand);
        }

        private void DeviceExplorerControlViewModel_ViewLoaded()
        {
            _showInternalErrorsCommand.Checked = NanoFrameworkPackage.OptionShowInternalErrors;
        }

        #region Command button handlers

        /// <summary>
        /// Handler for PingDeviceCommand
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="arguments"></param>
        /// <remarks>OK to use async void because this is a top-level event-handler 
        /// https://channel9.msdn.com/Series/Three-Essential-Tips-for-Async/Tip-1-Async-void-is-for-top-level-event-handlers-only
        /// </remarks>
        private async void PingDeviceCommandHandler(object sender, EventArgs arguments)
        {
            // yield to give the UI thread a chance to respond to user input
            await Task.Yield();

            MessageCentre.StartProgressMessage($"Pinging {_deviceExplorerControlViewModel.SelectedDevice.Description}...");
            try
            {
                // disable the button
                (sender as MenuCommand).Enabled = false;

                // make sure this device is showing as selected in Device Explorer tree view
                _deviceExplorerControlViewModel.ForceNanoDeviceSelection();

                // check if debugger engine exists
                if (_nanoDeviceCommService.Device.DebugEngine == null)
                {
                    _nanoDeviceCommService.Device.CreateDebugEngine();
                }

                // connect to the device
                if (await _nanoDeviceCommService.Device.DebugEngine.ConnectAsync(5000))
                {
                    // ping device
                    var connection = _nanoDeviceCommService.Device.Ping();

                    switch (_deviceExplorerControlViewModel.SelectedDevice.DebugEngine.ConnectionSource)
                    {
                        case Tools.Debugger.WireProtocol.ConnectionSource.Unknown:
                            MessageCentre.OutputMessage($"No reply from {_deviceExplorerControlViewModel.SelectedDevice.Description}");
                            break;

                        case Tools.Debugger.WireProtocol.ConnectionSource.nanoBooter:
                        case Tools.Debugger.WireProtocol.ConnectionSource.nanoCLR:
                            MessageCentre.OutputMessage($"{_deviceExplorerControlViewModel.SelectedDevice.Description} is active running {_deviceExplorerControlViewModel.SelectedDevice.DebugEngine.ConnectionSource.ToString()}");
                            break;
                    }
                }
                else
                {
                    MessageCentre.OutputMessage($"{_deviceExplorerControlViewModel.SelectedDevice.Description} is not responding, please reboot the device.");
                }
            }
            catch (Exception)
            {

            }
            finally
            {
                // enable the button
                (sender as MenuCommand).Enabled = true;

                MessageCentre.StopProgressMessage();
            }
        }

        /// <summary>
        /// Handler for DeviceCapabilitiesCommand
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="arguments"></param>
        /// <remarks>OK to use async void because this is a top-level event-handler 
        /// https://channel9.msdn.com/Series/Three-Essential-Tips-for-Async/Tip-1-Async-void-is-for-top-level-event-handlers-only
        /// </remarks>
        private async void DeviceCapabilitiesCommandHandler(object sender, EventArgs arguments)
        {
            // yield to give the UI thread a chance to respond to user input
            await Task.Yield();

            MessageCentre.StartProgressMessage($"Querying {_deviceExplorerControlViewModel.SelectedDevice.Description} capabilities...");

            try
            {
                // disable the button
                (sender as MenuCommand).Enabled = false;

                // make sure this device is showing as selected in Device Explorer tree view
                _deviceExplorerControlViewModel.ForceNanoDeviceSelection();

                // only query device if it's different 
                if (_deviceExplorerControlViewModel.SelectedDevice.Description.GetHashCode() != _deviceExplorerControlViewModel.LastDeviceConnectedHash)
                {
                    // keep device description hash code to avoid get info twice
                    _deviceExplorerControlViewModel.LastDeviceConnectedHash = _deviceExplorerControlViewModel.SelectedDevice.Description.GetHashCode();

                    // check if debugger engine exists
                    if (_nanoDeviceCommService.Device.DebugEngine == null)
                    {
                        _nanoDeviceCommService.Device.CreateDebugEngine();
                    }

                    // connect to the device
                    if (await _nanoDeviceCommService.Device.DebugEngine.ConnectAsync(5000))
                    {
                        try
                        {
                            // get device info
                            var deviceInfo = _nanoDeviceCommService.Device.GetDeviceInfo(true);
                            var memoryMap = _nanoDeviceCommService.Device.DebugEngine.GetMemoryMap();
                            var flashMap = _nanoDeviceCommService.Device.DebugEngine.GetFlashSectorMap();
                            var deploymentMap = _nanoDeviceCommService.Device.DebugEngine.GetDeploymentMap();

                            // we have to have a valid device info
                            if (deviceInfo.Valid)
                            {

                                // load view model properties for maps
                                _deviceExplorerControlViewModel.DeviceMemoryMap = new StringBuilder(memoryMap?.ToStringForOutput() ?? "Empty");
                                _deviceExplorerControlViewModel.DeviceFlashSectorMap = new StringBuilder(flashMap?.ToStringForOutput() ?? "Empty");
                                _deviceExplorerControlViewModel.DeviceDeploymentMap = new StringBuilder(deploymentMap?.ToStringForOutput() ?? "Empty");

                                // load view model property for system
                                _deviceExplorerControlViewModel.DeviceSystemInfo = new StringBuilder(deviceInfo?.ToString() ?? "Empty");
                            }
                            else
                            {
                                // reset property to force that device capabilities are retrieved on next connection
                                _deviceExplorerControlViewModel.LastDeviceConnectedHash = 0;

                                // report issue to user
                                MessageCentre.OutputMessage($"Error retrieving device information from { _deviceExplorerControlViewModel.SelectedDevice.Description}. Please reconnect device.");

                                return;
                            }
                        }
                        catch
                        {
                            // reset property to force that device capabilities are retrieved on next connection
                            _deviceExplorerControlViewModel.LastDeviceConnectedHash = 0;

                            // report issue to user
                            MessageCentre.OutputMessage($"Error retrieving device information from { _deviceExplorerControlViewModel.SelectedDevice.Description}. Please reconnect device.");

                            return;
                        }
                    }
                    else
                    {
                        // reset property to force that device capabilities are retrieved on next connection
                        _deviceExplorerControlViewModel.LastDeviceConnectedHash = 0;

                        MessageCentre.OutputMessage($"{_deviceExplorerControlViewModel.SelectedDevice.Description} is not responding, please reboot the device.");

                        return;
                    }
                }

                MessageCentre.OutputMessage(string.Empty);
                MessageCentre.OutputMessage(string.Empty);
                MessageCentre.OutputMessage("System Information");
                MessageCentre.OutputMessage(_deviceExplorerControlViewModel.DeviceSystemInfo.ToString());

                MessageCentre.OutputMessage(string.Empty);
                MessageCentre.OutputMessage(string.Empty);
                MessageCentre.OutputMessage("--------------------------------");
                MessageCentre.OutputMessage("::        Memory Map          ::");
                MessageCentre.OutputMessage("--------------------------------");
                MessageCentre.OutputMessage(_deviceExplorerControlViewModel.DeviceMemoryMap.ToString());

                MessageCentre.OutputMessage(string.Empty);
                MessageCentre.OutputMessage(string.Empty);
                MessageCentre.OutputMessage("-----------------------------------------------------------");
                MessageCentre.OutputMessage("::                   Flash Sector Map                    ::");
                MessageCentre.OutputMessage("-----------------------------------------------------------");
                MessageCentre.OutputMessage(_deviceExplorerControlViewModel.DeviceFlashSectorMap.ToString());

                MessageCentre.OutputMessage(string.Empty);
                MessageCentre.OutputMessage(string.Empty);
                MessageCentre.OutputMessage("Deployment Map");
                MessageCentre.OutputMessage(_deviceExplorerControlViewModel.DeviceDeploymentMap.ToString());
                MessageCentre.OutputMessage(string.Empty);

            }
            catch (Exception)
            {

            }
            finally
            {
                // enable the button
                (sender as MenuCommand).Enabled = true;

                // clear status bar
                MessageCentre.StopProgressMessage();
            }
        }

        /// <summary>
        /// Handler for DeviceEraseCommand
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="arguments"></param>
        /// <remarks>OK to use async void because this is a top-level event-handler 
        /// https://channel9.msdn.com/Series/Three-Essential-Tips-for-Async/Tip-1-Async-void-is-for-top-level-event-handlers-only
        /// </remarks>
        private async void DeviceEraseCommandHandler(object sender, EventArgs arguments)
        {
            // yield to give the UI thread a chance to respond to user input
            await Task.Yield();

            MessageCentre.StartProgressMessage($"Erasing {_deviceExplorerControlViewModel.SelectedDevice.Description} deployment area...");

            try
            {
                // disable the button
                (sender as MenuCommand).Enabled = false;

                // make sure this device is showing as selected in Device Explorer tree view
                _deviceExplorerControlViewModel.ForceNanoDeviceSelection();

                // check if debugger engine exists
                if (_nanoDeviceCommService.Device.DebugEngine == null)
                {
                    _nanoDeviceCommService.Device.CreateDebugEngine();
                }

                // connect to the device
                if (await _nanoDeviceCommService.Device.DebugEngine.ConnectAsync(5000))
                {
                    try
                    {
                        if (await _nanoDeviceCommService.Device.EraseAsync(Debugger.EraseOptions.Deployment, CancellationToken.None))
                        {
                            MessageCentre.OutputMessage($"{_deviceExplorerControlViewModel.SelectedDevice.Description} deployment area erased.");

                            // reset the hash for the connected device so the deployment information can be refreshed, if and when requested
                            _deviceExplorerControlViewModel.LastDeviceConnectedHash = 0;

                            // reboot device
                            _nanoDeviceCommService.Device.DebugEngine.RebootDevice(Debugger.RebootOptions.ClrOnly | Debugger.RebootOptions.NoShutdown);

                            // yield to give the UI thread a chance to respond to user input
                            await Task.Yield();
                        }
                        else
                        {
                            // report issue to user
                            MessageCentre.OutputMessage($"Error erasing {_deviceExplorerControlViewModel.SelectedDevice.Description} deployment area.");
                        }
                    }
                    catch
                    {
                        // report issue to user
                        MessageCentre.OutputMessage($"Error erasing {_deviceExplorerControlViewModel.SelectedDevice.Description} deployment area.");

                        return;
                    }
                }
                else
                {
                    // reset property to force that device capabilities are retrieved on next connection
                    _deviceExplorerControlViewModel.LastDeviceConnectedHash = 0;

                    MessageCentre.OutputMessage($"{_deviceExplorerControlViewModel.SelectedDevice.Description} is not responding, please reboot the device.");

                    return;
                }
            }
            catch (Exception)
            {

            }
            finally
            {
                // enable the button
                (sender as MenuCommand).Enabled = true;

                // clear status bar
                MessageCentre.StopProgressMessage();
            }
        }

        /// <summary>
        /// Handler for NetworkConfigCommand
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="arguments"></param>
        /// <remarks>OK to use async void because this is a top-level event-handler 
        /// https://channel9.msdn.com/Series/Three-Essential-Tips-for-Async/Tip-1-Async-void-is-for-top-level-event-handlers-only
        /// </remarks>
        private async void NetworkConfigCommandHandler(object sender, EventArgs arguments)
        {
            // yield to give the UI thread a chance to respond to user input
            await Task.Yield();

            try
            {
                // disable the button
                (sender as MenuCommand).Enabled = false;

                // make sure this device is showing as selected in Device Explorer tree view
                _deviceExplorerControlViewModel.ForceNanoDeviceSelection();

                // check if debugger engine exists
                if (_nanoDeviceCommService.Device.DebugEngine == null)
                {
                    _nanoDeviceCommService.Device.CreateDebugEngine();
                }

                // connect to the device
                if (await _nanoDeviceCommService.Device.DebugEngine.ConnectAsync(5000))
                {
                    try
                    {
                        // for now, just get the 1st network configuration, if exists
                        var networkConfigurations = _nanoDeviceCommService.Device.DebugEngine.GetAllNetworkConfigurations();

                        if (networkConfigurations.Count > 0)
                        {
                            _deviceExplorerControlViewModel.DeviceNetworkConfiguration = networkConfigurations[0];
                        }
                        else
                        {
                            _deviceExplorerControlViewModel.DeviceNetworkConfiguration = new Debugger.DeviceConfiguration.NetworkConfigurationProperties();
                        }

                        // for now, just get the 1st Wi-Fi configuration, if exists
                        var wirellesConfigurations = _nanoDeviceCommService.Device.DebugEngine.GetAllWireless80211Configurations();

                        if (wirellesConfigurations.Count > 0)
                        {
                            _deviceExplorerControlViewModel.DeviceWireless80211Configuration = wirellesConfigurations[0];
                        }
                        else
                        {
                            _deviceExplorerControlViewModel.DeviceWireless80211Configuration = new Debugger.DeviceConfiguration.Wireless80211ConfigurationProperties();
                        }

                        // yield to give the UI thread a chance to respond to user input
                        await Task.Yield();

                        // show network configuration dialogue
                        var networkConfigDialog = new NetworkConfigurationDialog();
                        networkConfigDialog.HasMinimizeButton = false;
                        networkConfigDialog.HasMaximizeButton = false;
                        networkConfigDialog.ShowModal();
                    }
                    catch
                    {
                        // report issue to user
                        MessageCentre.OutputMessage($"Error reading {_deviceExplorerControlViewModel.SelectedDevice.Description} configurations.");

                        return;
                    }
                }
                else
                {
                    MessageCentre.OutputMessage($"{_deviceExplorerControlViewModel.SelectedDevice.Description} is not responding, please reboot the device.");

                    return;
                }
            }
            catch (Exception)
            {

            }
            finally
            {
                // enable the button
                (sender as MenuCommand).Enabled = true;

                // clear status bar
                MessageCentre.StopProgressMessage();
            }
        }

        /// <summary>
        /// Handler for RebootCommand
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="arguments"></param>
        /// <remarks>OK to use async void because this is a top-level event-handler 
        /// https://channel9.msdn.com/Series/Three-Essential-Tips-for-Async/Tip-1-Async-void-is-for-top-level-event-handlers-only
        /// </remarks>
        private async void RebootCommandHandler(object sender, EventArgs arguments)
        {
            // yield to give the UI thread a chance to respond to user input
            await Task.Yield();

            try
            {
                //TODO Not the UI Thread...

                // disable the button
                (sender as MenuCommand).Enabled = false;

                // make sure this device is showing as selected in Device Explorer tree view
                _deviceExplorerControlViewModel.ForceNanoDeviceSelection();

                // check if debugger engine exists
                if (_nanoDeviceCommService.Device.DebugEngine == null)
                {
                    _nanoDeviceCommService.Device.CreateDebugEngine();
                }

                // connect to the device
                if (await _nanoDeviceCommService.Device.DebugEngine.ConnectAsync(5000))
                {
                    try
                    {
                        _nanoDeviceCommService.Device.DebugEngine.RebootDevice(Debugger.RebootOptions.NormalReboot);

                        MessageCentre.OutputMessage($"Sent reboot command to {_deviceExplorerControlViewModel.SelectedDevice.Description}.");

                        // reset the hash for the connected device so the deployment information can be refreshed, if and when requested
                        _deviceExplorerControlViewModel.LastDeviceConnectedHash = 0;

                        // yield to give the UI thread a chance to respond to user input
                        await Task.Yield();
                    }
                    catch
                    {
                        // report issue to user
                        MessageCentre.OutputMessage($"Error sending reboot command to {_deviceExplorerControlViewModel.SelectedDevice.Description}.");

                        return;
                    }
                }
                else
                {
                    // reset property to force that device capabilities are retrieved on next connection
                    _deviceExplorerControlViewModel.LastDeviceConnectedHash = 0;

                    MessageCentre.OutputMessage($"{_deviceExplorerControlViewModel.SelectedDevice.Description} is not responding, please reboot the device.");

                    return;
                }
            }
            catch (Exception)
            {

            }
            finally
            {
                // enable the button
                (sender as MenuCommand).Enabled = true;
            }
        }

        private void ShowInternalErrorsCommandHandler(object sender, EventArgs e)
        {
            // save new status
            // the "Checked" property reflects the current state, the final value is the current one negated 
            // this is more a "changing" event rather then a "changed" one
            NanoFrameworkPackage.OptionShowInternalErrors = !(sender as MenuCommand).Checked;

            // toggle button checked state
            var currentCheckState = (sender as MenuCommand).Checked;
            (sender as MenuCommand).Checked = !currentCheckState;
        }

        #endregion

        #region MVVM messaging handlers

        private void SelectedNanoDeviceHasChangedHandler()
        {
            if (_deviceExplorerControlViewModel.SelectedDevice != null)
            {
                _nanoDeviceCommService.SelectDevice(_deviceExplorerControlViewModel.SelectedDevice.Description);
            }
            else
            {
                _nanoDeviceCommService.SelectDevice(null);
            }

            // update toolbar 
            UpdateToolbarButtonsAsync().FireAndForget();
        }

        private void NanoDevicesCollectionChangedHandler()
        {
            // update toolbar 
            UpdateToolbarButtonsAsync().FireAndForget();
        }

        #endregion


        #region Toolbar Updates

        private async Task UpdateToolbarButtonsAsync()
        {
            // switch to UI main thread
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (_deviceExplorerControlViewModel.SelectedDevice != null)
                EnableToolbarMenuCommands();
            else
                DisableToolbarMenuCommands();
        }

        private void EnableToolbarMenuCommands()
        {
            _pingMenuCommand.Enabled = true;
            _capabilitiesMenuCommand.Enabled = true;
            _eraseMenuCommand.Enabled = true;
            _rebootMenuCommand.Enabled = true;
            _networkConfigMenuCommand.Enabled = true;
        }

        private void DisableToolbarMenuCommands()
        {
            _pingMenuCommand.Enabled = false;
            _capabilitiesMenuCommand.Enabled = false;
            _eraseMenuCommand.Enabled = false;
            _rebootMenuCommand.Enabled = false;
            _networkConfigMenuCommand.Enabled = false;
        }

        #endregion


        //TODO move to separate class
        #region helper methods and utilities

        /// <summary>
        /// Generates a <see cref="System.ComponentModel.Design.CommandID"/> specific for the Device Explorer menugroup.
        /// </summary>
        /// <param name="commandID">The ID for the command.</param>
        /// <returns></returns>
        private static CommandID GenerateToolbarCommandID(int commandID)
        {
            return new CommandID(menuGroupID, commandID);
        }

        /// <summary>
        /// Generates a <see cref="MenuCommand"/> to allow setting menu/toolbar item state and event handling.
        /// </summary>
        /// <param name="eventHandler">The event handling callback to be executed.</param>
        /// <param name="commandID">The ID for the command.</param>
        /// <param name="enabled">Whether the command will be enabled (clickable).</param>
        /// <param name="visible">Whether UI element of the command will be visible</param>
        /// <returns></returns>
        private static MenuCommand GenerateToolbarMenuCommand(EventHandler eventHandler, int commandID, bool enabled, bool visible)
        {
            var toolbarButtonCommandId = GenerateToolbarCommandID(commandID);
            return new MenuCommand(eventHandler, toolbarButtonCommandId) { Enabled = enabled, Visible = visible };
        }

        #endregion
    }
}