using MakerPrompt.Shared.Infrastructure;
using MakerPrompt.Shared.Models;
using UsbSerialForAndroid.Net;
using UsbSerialForAndroid.Net.Drivers;
using UsbSerialForAndroid.Net.Helper;
using System.Text;

namespace MakerPrompt.MAUI.Services
{
    public class SerialService : BaseSerialService, ISerialService
    {
        private UsbDriverBase? _usbDriver;
        private bool _isConnected;
        public bool IsSupported => true;

        public override async Task<bool> ConnectAsync(PrinterConnectionSettings connectionSettings)
        {
            if (connectionSettings.ConnectionType != ConnectionType || connectionSettings.Serial == null)
                throw new ArgumentException("Invalid connection settings");

            try
            {
                var deviceName = connectionSettings.Serial.PortName; // fix to id
                var baudRate = connectionSettings.Serial.BaudRate;
                var dataBits = (byte)8;
                var stopBits = UsbSerialForAndroid.Net.Enums.StopBits.One;
                var parity = UsbSerialForAndroid.Net.Enums.Parity.None;

                // Get the USB device
                var usbDevice = UsbManagerHelper.GetAllUsbDevices().FirstOrDefault(d => d.DeviceName == deviceName);
                if (usbDevice == null)
                    throw new InvalidOperationException("USB device not found");

                // Request permission if needed
                if (!UsbManagerHelper.HasPermission(usbDevice))
                    UsbManagerHelper.RequestPermission(usbDevice);

                // Create and open the USB driver
                _usbDriver = UsbDriverFactory.CreateUsbDriver(usbDevice.DeviceId);
                _usbDriver.Open(baudRate, dataBits, stopBits, parity);

                _isConnected = true;
                RaiseConnectionChanged();
                return _isConnected;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error connecting to device: {ex.Message}");
                return false;
            }
        }

        public override async Task DisconnectAsync()
        {
            if (_isConnected && _usbDriver != null)
            {
                _usbDriver.Close();
                _usbDriver = null;
                _isConnected = false;
                RaiseConnectionChanged();
            }
        }

        public override async Task WriteDataAsync(string data)
        {
            if (!_isConnected || _usbDriver == null)
                throw new InvalidOperationException("Device is not connected");

            var buffer = Encoding.ASCII.GetBytes(data);
            _usbDriver.Write(buffer);
        }

        public override async ValueTask DisposeAsync()
        {
            await DisconnectAsync();
        }

        public async Task<IEnumerable<string>> GetAvailablePortsAsync()
        {
            var devices = UsbManagerHelper.GetAllUsbDevices();
            return devices.Select(d => d.DeviceId.ToString()).ToList();
        }

        public async Task<bool> CheckSupportedAsync()
        {
            // Assuming USB support is always available on Android
            return true;
        }

        public Task RequestPortAsync() => Task.CompletedTask;
    }
}