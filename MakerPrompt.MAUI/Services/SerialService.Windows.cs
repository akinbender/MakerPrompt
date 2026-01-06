using System.Text;
using System.IO.Ports;
using System.Threading.Tasks.Dataflow;
using MakerPrompt.Shared.Infrastructure;
using MakerPrompt.Shared.Models;
using MakerPrompt.Shared.Utils;
using MakerPrompt.Shared.Services;

namespace MakerPrompt.MAUI.Services
{
    public class SerialService : BaseSerialService, ISerialService
    {
        private readonly SerialPort _serialPort;
        private readonly BufferBlock<string> _commandQueue = new();
        private readonly CancellationTokenSource _cts = new();
        private Task? _sendTask;
        private Task? _receiveTask;
        public bool IsSupported => true;

        public SerialService()
        {
            _serialPort = new SerialPort
            {
                DataBits = 8,
                Parity = Parity.None,
                StopBits = StopBits.One,
                Handshake = Handshake.None,   // was RequestToSend
                DtrEnable = true,             // assert DTR for many CDC devices
                RtsEnable = true,             // assert RTS manually (optional)
                ReadTimeout = 2000,
                WriteTimeout = 5000,
                NewLine = "\n",
                Encoding = Encoding.ASCII
            };
        }

        public async Task<bool> ConnectAsync(PrinterConnectionSettings connectionSettings)
        {
            if (IsConnected) return IsConnected;

            if (connectionSettings.ConnectionType != ConnectionType || string.IsNullOrWhiteSpace(connectionSettings.Serial.PortName)) return false;

            _serialPort.PortName = connectionSettings.Serial.PortName;
            _serialPort.BaudRate = connectionSettings.Serial.BaudRate;

            try
            {
                await Task.Run(() => _serialPort.Open());
                IsConnected = true;
                ConnectionName = connectionSettings.Serial.PortName;
                _sendTask = Task.Run(() => SendLoopAsync(_cts.Token));
                _receiveTask = Task.Run(() => ReceiveLoopAsync(_cts.Token));
                updateTimer.Elapsed += async (s, e) => await GetPrinterTelemetryAsync();
                updateTimer.Start();
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
            updateTimer.Stop();
            _serialPort.Close();
            IsConnected = false;
            RaiseConnectionChanged();
        }

        public override async Task WriteDataAsync(string data)
        {
            if (!IsConnected) return;
            await _commandQueue.SendAsync(data);
        }

        public async Task<IEnumerable<string>> GetAvailablePortsAsync()
        {
            return await Task.Run(() => SerialPort.GetPortNames()
                .OrderBy(p => p)
                .ToList());
        }

        private async Task SendLoopAsync(CancellationToken ct)
        {
            try
            {
                while (IsConnected && !ct.IsCancellationRequested)
                {
                    var command = await _commandQueue.ReceiveAsync(ct);
                    if (!_serialPort.IsOpen) break;

                    var payload = Encoding.ASCII.GetBytes(command + _serialPort.NewLine);
                    await _serialPort.BaseStream.WriteAsync(payload, 0, payload.Length, ct);
                    await _serialPort.BaseStream.FlushAsync(ct);
                    await Task.Delay(10, ct);
                }
            }
            catch (OperationCanceledException) { }
        }

        private async Task ReceiveLoopAsync(CancellationToken ct)
        {
            var buffer = new byte[4096];

            while (!ct.IsCancellationRequested && IsConnected)
            {
                try
                {
                    var bytesRead = await _serialPort.BaseStream
                        .ReadAsync(buffer, 0, buffer.Length, ct);

                    if (bytesRead > 0)
                    {
                        var received = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                        ProcessReceivedData(received);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Receive error: {ex.Message}");
                    await DisposeAsync();
                }
            }
        }

        public override async ValueTask DisposeAsync()
        {
            if (!IsConnected) return;

            IsConnected = false;

            try
            {
                // First cancel operations
                _cts.Cancel();

                // Then await tasks completion
                if (_sendTask != null)
                    await _sendTask.ContinueWith(_ => { }); // Suppress exceptions
                if (_receiveTask != null)
                    await _receiveTask.ContinueWith(_ => { });
            }
            finally
            {
                // Then close the port
                if (_serialPort.IsOpen)
                {
                    await Task.Run(() =>
                    {
                        try
                        {
                            _serialPort.DiscardInBuffer();
                            _serialPort.DiscardOutBuffer();
                            _serialPort.Close();
                        }
                        catch { /* Ignore close errors */ }
                    });
                }

                // Dispose resources in reverse order
                _serialPort.Dispose();
                _cts.Dispose();
                updateTimer.Dispose();
                RaiseConnectionChanged();
            }
        }

        public Task<bool> CheckSupportedAsync() => Task.FromResult(true);

        public Task RequestPortAsync() => Task.CompletedTask;

        public Task StartPrint(GCodeDoc gcodeDoc)
        {
            // Stream G-code through the existing send queue without blocking.
            if (!IsConnected || string.IsNullOrEmpty(gcodeDoc.Content))
            {
                return Task.CompletedTask;
            }

            return Task.Run(async () =>
            {
                await foreach (var command in gcodeDoc.EnumerateCommandsAsync(_cts.Token))
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