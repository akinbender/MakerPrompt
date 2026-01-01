using MakerPrompt.Shared.Infrastructure;
using MakerPrompt.Shared.Models;
using System.Text;
using System.Threading.Tasks.Dataflow;
using UsbSerialForMacOS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace MakerPrompt.MAUI.Services
{
    public class SerialService : BaseSerialService, ISerialService
    {
        private UsbSerialManager? _manager = new();
        private readonly BufferBlock<string> _commandQueue = new();
        private CancellationTokenSource _cts = new();
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
            var baudRate = connectionSettings.Serial.BaudRate;

            try
            {
                if (_manager == null)
                    throw new InvalidOperationException("Manager is not initialized");
                    
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
            _cts.Cancel();

            try
            {
                if (_sendTask != null)
                    await _sendTask.ConfigureAwait(false);
                if (_receiveTask != null)
                    await _receiveTask.ConfigureAwait(false);
            }
            finally
            {
                _manager?.Close();
                _manager = new UsbSerialManager();
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
            // Use the UsbSerialManager instance to list available ports
            return _manager?.AvailablePorts().OrderBy(p => p).ToList() ?? new List<string>();
        }

        private async Task SendLoopAsync(CancellationToken ct)
        {
            try
            {
                while (IsConnected && !ct.IsCancellationRequested)
                {
                    var command = await _commandQueue.ReceiveAsync(ct);
                    var manager = _manager;
                    if (!IsConnected || manager == null || ct.IsCancellationRequested)
                    {
                        // Connection was closed or manager disposed; exit send loop.
                        break;
                    }
                    manager.Write(command);
                    await Task.Delay(10, ct); // Flow control delay
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
                    if (!IsConnected || manager == null || ct.IsCancellationRequested)
                    {
                        // Connection was closed or manager disposed; exit receive loop.
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
                    await DisposeAsync();
                }
            }
        }

        public override async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;

            await DisconnectAsync();
            
            try
            {
                _cts.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // Ignore if already disposed to ensure idempotent disposal
            }
        }

        public Task<bool> CheckSupportedAsync() => Task.FromResult(true);

        public Task RequestPortAsync() => Task.CompletedTask;
    }

    public class SerialException : Exception
    {
        public SerialException(string message, Exception inner)
            : base(message, inner) { }
    }
}