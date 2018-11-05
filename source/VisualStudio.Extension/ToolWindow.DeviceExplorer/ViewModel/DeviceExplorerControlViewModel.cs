//
// Copyright (c) 2017 The nanoFramework project contributors
// See LICENSE file in the project root for full license information.
//

using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Messaging;
using Microsoft.VisualStudio.Shell;
using nanoFramework.Tools.Debugger;
using nanoFramework.Tools.Debugger.WireProtocol;
using PropertyChanged;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Task = System.Threading.Tasks.Task;

namespace nanoFramework.Tools.VisualStudio.Extension.ToolWindow.ViewModel
{
    [AddINotifyPropertyChangedInterface]
    /// <summary>
    /// This class contains properties that the main View can data bind to.
    /// <para>
    /// Use the <strong>mvvminpc</strong> snippet to add bindable properties to this ViewModel.
    /// </para>
    /// <para>
    /// You can also use Blend to data bind with the tool's support.
    /// </para>
    /// <para>
    /// See http://www.galasoft.ch/mvvm
    /// </para>
    /// </summary>
    public class DeviceExplorerControlViewModel : ViewModelBase, INotifyPropertyChanging, INotifyPropertyChanged
    {
        public const int WRITE_TO_OUTPUT_TOKEN = 1;
        public const int SELECTED_NULL_TOKEN = 2;

        // for serial devices we wait 10 seconds for the device to be available again
        private const int SerialDeviceReconnectMaximumAttempts = 4 * 10;

        // keep this here otherwise Fody won't be able to properly implement INotifyPropertyChanging
#pragma warning disable 67
        public event PropertyChangingEventHandler PropertyChanging;
#pragma warning restore 67
        public event Action ViewLoaded;

        /// <summary>
        /// Sets if Device Explorer should auto-select a device when there is only a single one in the available list.
        /// </summary>
        public bool AutoSelect { get; set; } = true;

        private bool _isViewLoaded;
        public bool IsViewLoaded
        {
            get { return _isViewLoaded; }
            set
            {
                _isViewLoaded = value;
                ViewLoaded?.Invoke();
            }
        }

        private INanoDeviceCommService nanoDeviceCommService;
        public INanoDeviceCommService NanoDeviceCommService
        {
            private get { return nanoDeviceCommService; }
            set
            {
                nanoDeviceCommService = value;
                nanoDeviceCommService.DebugClient.NanoFrameworkDevices.CollectionChanged += NanoFrameworkDevices_CollectionChanged;
            }
        }

        /// <summary>
        /// Initializes a new instance of the MainViewModel class.
        /// </summary>
        public DeviceExplorerControlViewModel()
        {
            if (IsInDesignMode)
            {
                // Code runs in Blend --> create design time data.
                AvailableDevices = new ObservableCollection<NanoDeviceBase>();

                AvailableDevices.Add(new NanoDevice<NanoSerialDevice>() { Description = "Awesome nanodevice1" });
                AvailableDevices.Add(new NanoDevice<NanoSerialDevice>() { Description = "Awesome nanodevice2" });
            }
            else
            {
                // Code runs "for real"
                AvailableDevices = new ObservableCollection<NanoDeviceBase>();
            }

            SelectedDevice = null;
        }

        public ObservableCollection<NanoDeviceBase> AvailableDevices { set; get; }

        public NanoDeviceBase SelectedDevice { get; set; }

        public string DeviceToReSelect { get; set; } = null;

        public string PreviousSelectedDeviceDescription { get; internal set; }

        public void OnNanoDeviceCommServiceChanged()
        {
            if (NanoDeviceCommService != null)
            {
                NanoDeviceCommService.DebugClient.DeviceEnumerationCompleted += SerialDebugClient_DeviceEnumerationCompleted;

                NanoDeviceCommService.DebugClient.LogMessageAvailable += DebugClient_LogMessageAvailable;
            }
        }

        private void DebugClient_LogMessageAvailable(object sender, StringEventArgs e)
        {
            MessageCentre.InternalErrorMessage(e.EventText);
        }

        private async void SerialDebugClient_DeviceEnumerationCompleted(object sender, EventArgs e)
        {
             await UpdateAvailableDevicesAsync();
        }

        private async Task UpdateAvailableDevicesAsync()
        {
            // handle auto-connect option
            if (NanoDeviceCommService.DebugClient.IsDevicesEnumerationComplete)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                AvailableDevices.Clear();

                foreach (var item in NanoDeviceCommService.DebugClient.NanoFrameworkDevices)
                    AvailableDevices.Add(item);

                // reselect a specific device has higher priority than auto-select
                if (DeviceToReSelect != null)
                {
                    var deviceToReSelect = AvailableDevices.FirstOrDefault(d => d.Description == DeviceToReSelect);
                    if (deviceToReSelect != null)
                    {
                        // device seems to be back online, select it
                        ForceNanoDeviceSelection(deviceToReSelect);

                        // clear device to reselect
                        DeviceToReSelect = null;
                    }
                }
                // this auto-select can only run after the initial device enumeration is completed
                else if (AutoSelect)
                {
                    // is there a single device
                    if (AvailableDevices.Count == 1)
                    {
                          ForceNanoDeviceSelection(AvailableDevices[0]);
                    }
                    else
                    {
                        // we have more than one now, was there any device already selected?
                        if (SelectedDevice != null)
                        {
                            // maintain selection
                            ForceNanoDeviceSelection(AvailableDevices.FirstOrDefault(d => d.Description == SelectedDevice.Description));
                        }
                    }
                }
            }
        }

        private async void NanoFrameworkDevices_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            MessengerInstance.Send(new NotificationMessage(""), MessagingTokens.NanoDevicesCollectionHasChanged);

            await UpdateAvailableDevicesAsync();
        }

        private void ForceNanoDeviceSelection(NanoDeviceBase nanoDevice)
        {
            // select the device
            SelectedDevice = nanoDevice;

            ForceNanoDeviceSelection();
        }

        public void  ForceNanoDeviceSelection()
        {
            // request forced selection of device in UI
            MessengerInstance.Send(new NotificationMessage(""), MessagingTokens.ForceSelectionOfNanoDevice);
        }

        public void OnSelectedDeviceChanging()
        {
            // save previous device
            PreviousSelectedDeviceDescription = SelectedDevice?.Description;
        }

        public void OnSelectedDeviceChanged()
        {
            // clear hash for connected device
            LastDeviceConnectedHash = 0;

            // signal event that the selected device has changed
            MessengerInstance.Send(new NotificationMessage(""), MessagingTokens.SelectedNanoDeviceHasChanged);
        }


        #region Transport

        //Hardcoded until new TransportTypes supported
        public Debugger.WireProtocol.TransportType SelectedTransportType => Debugger.WireProtocol.TransportType.Serial;

        #endregion


        #region Device Capabilities

        public StringBuilder DeviceDeploymentMap { get; set; }

        public StringBuilder DeviceFlashSectorMap { get; set; }

        public StringBuilder DeviceMemoryMap { get; set; }

        public StringBuilder DeviceSystemInfo { get; set; }

        /// <summary>
        /// used to prevent repeated retrieval of device capabilities after connection
        /// </summary>
        public int LastDeviceConnectedHash { get; set; }

        #endregion

        # region Network configuration dialog

        public DeviceConfiguration.NetworkConfigurationProperties DeviceNetworkConfiguration { get; set; }

        public DeviceConfiguration.Wireless80211ConfigurationProperties DeviceWireless80211Configuration { get; set; }

        #endregion

        #region messaging tokens

        public static class MessagingTokens
        {
            public static readonly string SelectedNanoDeviceHasChanged = new Guid("{C3173983-A19A-49DD-A4BD-F25D360F7334}").ToString();
            public static readonly string NanoDevicesCollectionHasChanged = new Guid("{3E8906F9-F68A-45B7-A0CE-6D42BDB22455}").ToString();
            public static readonly string ForceSelectionOfNanoDevice = new Guid("{8F012794-BC66-429D-9F9D-A9B0F546D6B5}").ToString();
        }

        #endregion
    }
}
