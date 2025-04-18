#if WINDOWS
using System.IO.Ports;
using System.Threading.Tasks.Dataflow;
using MakerPrompt.Shared.Infrastructure;
using System.Text;
using MakerPrompt.Shared.Models;

namespace MakerPrompt.MAUI.Services
{
    public class WindowsSerialService : BaseSerialService, ISerialService
    {
        private readonly SerialPort _serialPort;
        private Task? _sendTask;
        private Task? _receiveTask;
        public bool IsSupported => true;

        public WindowsSerialService()
        {
            _serialPort = new SerialPort
            {
                DataBits = 8,
                Parity = Parity.None,
                StopBits = StopBits.One,
                Handshake = Handshake.RequestToSend,
                ReadTimeout = 500,
                WriteTimeout = 500,
                NewLine = "\n",
                Encoding = Encoding.ASCII
            };
        }

        public override async Task<bool> ConnectAsync(PrinterConnectionSettings connectionSettings)
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

        public override async Task DisconnectAsync()
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

        private async Task SendLoop()
        {
            while (IsConnected)
            {
                var pending = await _commandQueue.ReceiveAsync(_cts.Token);
                try
                {
                    _serialPort.WriteLine(pending.Command);
                    await pending.ResponseSource.Task
                        .WaitAsync(TimeSpan.FromSeconds(2), _cts.Token);
                }
                catch (TimeoutException)
                {
                    if (!pending.IsAutomatic)
                        RaiseDataRecieved($"CMD: {pending.Command} → Timeout");
                }
            }
        }

        private async Task ReceiveLoop()
        {
            var buffer = new byte[4096];
            while (IsConnected)
            {
                var bytesRead = await _serialPort.BaseStream.ReadAsync(buffer, 0, buffer.Length);
                var data = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                _receiveBuffer.Append(data);

                while (_receiveBuffer.ToString().Contains('\n'))
                {
                    var line = _receiveBuffer.ToString().Split('\n')[0];
                    _receiveBuffer = _receiveBuffer.Remove(0, line.Length + 1);
                    ProcessReceivedData(line.Trim());
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
    }

    public class SerialException : Exception
    {
        public SerialException(string message, Exception inner)
            : base(message, inner) { }
    }
}
#endif