using System.Text;
using System.Threading.Tasks.Dataflow;
using MakerPrompt.Core.Models;
using UsbSerialForMacOS;

namespace MakerPrompt.UI.MAUI.Services;

public partial class SerialCommunicationService
{
    // ── macOS state ──────────────────────────────────────────────────────────
    private UsbSerialManager? _manager;
    private readonly BufferBlock<string> _macCommandQueue = new();
    private CancellationTokenSource? _macCts;
    private Task? _macSendTask;
    private Task? _macReceiveTask;

    // ── Transport hooks ──────────────────────────────────────────────────────

    protected override Task OpenTransportAsync(PrinterConnectionSettings settings,
        CancellationToken cancellationToken)
    {
        var portName = settings.PortName
            ?? throw new ArgumentException("PortName is required for macOS serial connections");
        var baudRate = settings.BaudRate == 0 ? DefaultBaudRate : settings.BaudRate;

        _manager?.Close();
        _manager = new UsbSerialManager();

        var opened = _manager.Open(portName, baudRate);
        if (!opened)
            throw new InvalidOperationException($"Failed to open serial port '{portName}'");

        _macCts?.Dispose();
        _macCts = new CancellationTokenSource();

        _macSendTask = Task.Run(() => SendLoopAsync(_macCts.Token));
        _macReceiveTask = Task.Run(() => ReceiveLoopAsync(_macCts.Token));

        return Task.CompletedTask;
    }

    protected override async Task CloseTransportAsync(CancellationToken cancellationToken)
    {
        _macCts?.Cancel();

        if (_macSendTask is not null)
            await _macSendTask.ContinueWith(_ => { }, TaskContinuationOptions.None);
        if (_macReceiveTask is not null)
            await _macReceiveTask.ContinueWith(_ => { }, TaskContinuationOptions.None);

        try { _manager?.Close(); }
        catch { /* Swallow close errors */ }

        _manager = null;
        _macCts?.Dispose();
        _macCts = null;
    }

    protected override Task WriteTransportAsync(string data, CancellationToken cancellationToken)
    {
        if (_manager is null) return Task.CompletedTask;
        return _macCommandQueue.SendAsync(data, cancellationToken).AsTask();
    }

    // ── Send loop (macOS) ────────────────────────────────────────────────────

    private async Task SendLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && IsConnected)
            {
                var command = await _macCommandQueue.ReceiveAsync(ct);
                var mgr = _manager;
                if (mgr is null || ct.IsCancellationRequested) break;

                mgr.Write(command + "\n");
                await Task.Delay(10, ct);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Console.WriteLine($"[SerialService.MacOS] Send loop error: {ex.Message}");
        }
    }

    // ── Receive loop (macOS) ─────────────────────────────────────────────────

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && IsConnected)
        {
            try
            {
                var mgr = _manager;
                if (mgr is null) break;

                var bytes = mgr.Read(4096);
                if (bytes.Length > 0)
                    ProcessReceivedData(Encoding.UTF8.GetString(bytes.ToArray()));

                await Task.Delay(10, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SerialService.MacOS] Receive error: {ex.Message}");
                await DisconnectAsync();
                break;
            }
        }
    }

    // ── Available ports (macOS) ──────────────────────────────────────────────

    public static partial Task<IReadOnlyList<string>> GetAvailablePortsAsync()
    {
        var mgr = new UsbSerialManager();
        return Task.FromResult<IReadOnlyList<string>>(
            mgr.AvailablePorts().OrderBy(p => p).ToArray());
    }
}
