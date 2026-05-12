using System.IO.Ports;
using System.Text;
using System.Threading.Tasks.Dataflow;
using MakerPrompt.Core.Models;

namespace MakerPrompt.UI.MAUI.Services;

public partial class SerialCommunicationService
{
    // ── Windows state ────────────────────────────────────────────────────────
    private SerialPort? _serialPort;
    private readonly BufferBlock<string> _commandQueue = new();
    private CancellationTokenSource? _cts;
    private Task? _sendTask;
    private Task? _receiveTask;

    // ── Transport hooks ──────────────────────────────────────────────────────

    protected override Task OpenTransportAsync(PrinterConnectionSettings settings,
        CancellationToken cancellationToken)
    {
        _serialPort = new SerialPort
        {
            PortName = settings.PortName
                ?? throw new ArgumentException("PortName is required for Serial connections"),
            BaudRate = settings.BaudRate == 0 ? 250_000 : settings.BaudRate,
            DataBits = 8,
            Parity = Parity.None,
            StopBits = StopBits.One,
            Handshake = Handshake.None,
            DtrEnable = true,
            RtsEnable = true,
            ReadTimeout = 2000,
            WriteTimeout = 5000,
            NewLine = "\n",
            Encoding = Encoding.ASCII
        };

        _serialPort.Open();

        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        _sendTask = Task.Run(() => SendLoopAsync(_cts.Token));
        _receiveTask = Task.Run(() => ReceiveLoopAsync(_cts.Token));

        return Task.CompletedTask;
    }

    protected override async Task CloseTransportAsync(CancellationToken cancellationToken)
    {
        if (_cts is { } cts)
        {
            cts.Cancel();

            if (_sendTask is not null)
                await _sendTask.ContinueWith(_ => { }); // suppress exceptions
            if (_receiveTask is not null)
                await _receiveTask.ContinueWith(_ => { });
        }

        if (_serialPort is { IsOpen: true })
        {
            await Task.Run(() =>
            {
                try
                {
                    _serialPort.DiscardInBuffer();
                    _serialPort.DiscardOutBuffer();
                    _serialPort.Close();
                }
                catch
                {
                    // Ignore close errors during shutdown.
                }
            });
        }

        _serialPort?.Dispose();
        _serialPort = null;
        _cts?.Dispose();
        _cts = null;
    }

    protected override async Task WriteTransportAsync(string data, CancellationToken cancellationToken)
    {
        if (_serialPort is not { IsOpen: true }) return;
        await _commandQueue.SendAsync(data, cancellationToken);
    }

    // ── Send loop (Windows) ──────────────────────────────────────────────────

    private async Task SendLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && IsConnected)
            {
                var command = await _commandQueue.ReceiveAsync(ct);
                if (_serialPort is not { IsOpen: true }) break;

                var payload = Encoding.ASCII.GetBytes(command + "\n");
                await _serialPort.BaseStream.WriteAsync(payload, ct);
                await _serialPort.BaseStream.FlushAsync(ct);
                await Task.Delay(10, ct);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Console.WriteLine($"[SerialService.Windows] Send loop error: {ex.Message}");
        }
    }

    // ── Receive loop (Windows) ───────────────────────────────────────────────

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[4096];
        while (!ct.IsCancellationRequested && IsConnected)
        {
            try
            {
                if (_serialPort is not { IsOpen: true }) break;

                var bytesRead = await _serialPort.BaseStream.ReadAsync(buffer, ct);
                if (bytesRead > 0)
                    ProcessReceivedData(Encoding.ASCII.GetString(buffer, 0, bytesRead));
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                // Swallow receive errors — surface only as disconnection.
                Console.WriteLine($"[SerialService.Windows] Receive loop error: {ex.Message}");
                await DisconnectAsync();
                break;
            }
        }
    }

    // ── Available ports (Windows) ────────────────────────────────────────────

    public static partial Task<IReadOnlyList<string>> GetAvailablePortsAsync()
        => Task.FromResult<IReadOnlyList<string>>(
            SerialPort.GetPortNames().OrderBy(p => p).ToArray());
}
