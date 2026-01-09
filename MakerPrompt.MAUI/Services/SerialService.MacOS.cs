using MakerPrompt.Shared.Infrastructure;
using MakerPrompt.Shared.Models;
using MakerPrompt.Shared.Services;
using MakerPrompt.Shared.Utils;
using System.Text;
using System.Threading.Tasks.Dataflow;
using UsbSerialForMacOS;
namespace MakerPrompt.MAUI.Services
{
    public class SerialService : BaseSerialService, ISerialService
    {
        private UsbSerialManager? _manager = new();
        private readonly BufferBlock<string> _commandQueue = new();
        private CancellationTokenSource? _cts;
        private Task? _sendTask;
        private Task? _receiveTask;
        private bool _disposed = false;
        public bool IsSupported => true;

        public SerialService() { }

        public async Task<bool> ConnectAsync(PrinterConnectionSettings connectionSettings)
        {
            if (IsConnected) return IsConnected;
            if (connectionSettings.ConnectionType != ConnectionType || connectionSettings.Serial == null)
                throw new ArgumentException("Invalid connection settings");

            var portName = connectionSettings.Serial.PortName;
            var baudRate = connectionSettings.Serial.BaudRate == 0
                ? 250000
                : connectionSettings.Serial.BaudRate;

            try
            {
                _manager ??= new UsbSerialManager();
                _cts?.Dispose();
                _cts = new CancellationTokenSource();

                IsConnected = _manager.Open(portName, baudRate);
                ConnectionName = portName;

                _sendTask = Task.Run(() => SendLoopAsync(_cts.Token));
                _receiveTask = Task.Run(() => ReceiveLoopAsync(_cts.Token));
                RaiseConnectionChanged();
            }
            catch (Exception ex)
            {
                IsConnected = false;
                throw new SerialException("Connection failed", ex);
            }

            return IsConnected;
        }

        public async Task DisconnectAsync()
        {
            if (!IsConnected) return;
            _cts?.Cancel();

            try
            {
                if (_sendTask != null)
                    await _sendTask.ConfigureAwait(false);
                if (_receiveTask != null)
                    await _receiveTask.ConfigureAwait(false);
            }
            finally
            {
                try
                {
                    _manager?.Close();
                }
                catch
                {
                    // swallow close exceptions on shutdown
                }

                _manager = null;
                IsConnected = false;
                RaiseConnectionChanged();
            }
        }

        public override async Task WriteDataAsync(string data)
        {
            if (!IsConnected) return;
            await _commandQueue.SendAsync(data);
        }

        public async Task<IEnumerable<string>> GetAvailablePortsAsync()
        {
            return _manager?.AvailablePorts().OrderBy(p => p).ToList() ?? [];
        }

        private async Task SendLoopAsync(CancellationToken ct)
        {
            try
            {
                while (IsConnected && !ct.IsCancellationRequested)
                {
                    var command = await _commandQueue.ReceiveAsync(ct);
                    var manager = _manager;
                    if (manager == null || ct.IsCancellationRequested)
                    {
                        break;
                    }
                    manager.Write(command);
                    await Task.Delay(10, ct);
                }
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown
            }
        }

        private async Task ReceiveLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && IsConnected)
            {
                try
                {
                    var manager = _manager;
                    if (manager == null || ct.IsCancellationRequested)
                    {
                        break;
                    }
                    var bytesRead = manager.Read(4096);
                    if (bytesRead.Length > 0)
                    {
                        var received = Encoding.UTF8.GetString(bytesRead.ToArray());
                        ProcessReceivedData(received);
                    }
                    await Task.Delay(10, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Receive error: {ex.Message}");
                    IsConnected = false;
                    await DisposeAsync();
                }
            }
        }

        public override async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                await DisconnectAsync();
                _cts?.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // Ignore if already disposed to ensure idempotent disposal
            }
        }

        public Task<bool> CheckSupportedAsync() => Task.FromResult(true);

        public Task RequestPortAsync() => Task.CompletedTask;

        public Task StartPrint(GCodeDoc gcodeDoc)
        {
            if (!IsConnected || string.IsNullOrEmpty(gcodeDoc.Content))
            {
                return Task.CompletedTask;
            }

            return Task.Run(async () =>
            {
                await foreach (var command in gcodeDoc.EnumerateCommandsAsync(_cts?.Token ?? CancellationToken.None))
                {
                    if (!IsConnected)
                    {
                        break;
                    }

                    await WriteDataAsync(command);
                }
            });
        }
    }
}