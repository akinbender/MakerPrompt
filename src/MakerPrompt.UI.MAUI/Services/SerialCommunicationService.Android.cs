using System.Text;
using MakerPrompt.Core.Models;
using UsbSerialForAndroid.Net;
using UsbSerialForAndroid.Net.Drivers;
using UsbSerialForAndroid.Net.Helper;

namespace MakerPrompt.UI.MAUI.Services;

public partial class SerialCommunicationService
{
    // ── Android state ────────────────────────────────────────────────────────
    private UsbDriverBase? _usbDriver;
    private CancellationTokenSource? _androidCts;
    private Task? _androidReceiveTask;

    // ── Transport hooks ──────────────────────────────────────────────────────

    protected override Task OpenTransportAsync(PrinterConnectionSettings settings,
        CancellationToken cancellationToken)
    {
        var deviceName = settings.PortName
            ?? throw new ArgumentException("PortName (device name) is required for Android serial connections");
        var baudRate = settings.BaudRate == 0 ? DefaultBaudRate : settings.BaudRate;

        // Locate the USB device by name.
        var usbDevice = UsbManagerHelper.GetAllUsbDevices()
            .FirstOrDefault(d => d.DeviceName == deviceName)
            ?? throw new InvalidOperationException($"USB device '{deviceName}' not found.");

        // Request permission if not yet granted.
        if (!UsbManagerHelper.HasPermission(usbDevice))
            UsbManagerHelper.RequestPermission(usbDevice);

        _usbDriver = UsbDriverFactory.CreateUsbDriver(usbDevice.DeviceId);
        _usbDriver.Open(baudRate,
            dataBits: 8,
            stopBits: UsbSerialForAndroid.Net.Enums.StopBits.One,
            parity: UsbSerialForAndroid.Net.Enums.Parity.None);

        _androidCts?.Dispose();
        _androidCts = new CancellationTokenSource();
        _androidReceiveTask = Task.Run(() => ReceiveLoopAsync(_androidCts.Token));

        return Task.CompletedTask;
    }

    protected override async Task CloseTransportAsync(CancellationToken cancellationToken)
    {
        _androidCts?.Cancel();

        if (_androidReceiveTask is not null)
            await _androidReceiveTask.ContinueWith(_ => { }, TaskContinuationOptions.None);

        try { _usbDriver?.Close(); }
        catch { /* Ignore close errors */ }

        _usbDriver = null;
        _androidCts?.Dispose();
        _androidCts = null;
    }

    protected override Task WriteTransportAsync(string data, CancellationToken cancellationToken)
    {
        if (_usbDriver is null) return Task.CompletedTask;

        // Android USB driver uses synchronous write — run on thread pool.
        return Task.Run(() =>
        {
            var bytes = Encoding.ASCII.GetBytes(data + "\n");
            _usbDriver.Write(bytes);
        }, cancellationToken);
    }

    // ── Receive loop (Android) ───────────────────────────────────────────────

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && IsConnected)
        {
            try
            {
                if (_usbDriver is null) break;

                // Android driver read is synchronous; offload to thread pool.
                var bytes = await Task.Run(() => _usbDriver.Read(4096), ct);
                if (bytes.Length > 0)
                    ProcessReceivedData(Encoding.ASCII.GetString(bytes));

                await Task.Delay(10, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SerialService.Android] Receive error: {ex.Message}");
                await DisconnectAsync();
                break;
            }
        }
    }

    // ── Available ports (Android — USB device names) ─────────────────────────

    public static partial Task<IReadOnlyList<string>> GetAvailablePortsAsync()
        => Task.FromResult<IReadOnlyList<string>>(
            UsbManagerHelper.GetAllUsbDevices()
                .Select(d => d.DeviceName)
                .ToArray());
}
