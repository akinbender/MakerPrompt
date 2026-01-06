using MakerPrompt.Shared.Infrastructure;
using MakerPrompt.Shared.Models;
using MakerPrompt.Shared.Services;
using Microsoft.JSInterop;

namespace MakerPrompt.Blazor.Services
{
    public class WebSerialService : BaseSerialService, ISerialService, IAsyncDisposable
    {
        private readonly Lazy<Task<IJSObjectReference>> _moduleTask;
        private DotNetObjectReference<WebSerialService>? _dotNetRef;
        private IJSObjectReference? _portReference;

        public bool IsSupported { get; private set; } = false;

        public WebSerialService(IJSRuntime jsRuntime)
        {
            _moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./serialJsInterop.js").AsTask());
            _dotNetRef = DotNetObjectReference.Create(this);
        }

        public async Task<bool> CheckSupportedAsync()
        {
            var module = await _moduleTask.Value;
            IsSupported = await module.InvokeAsync<bool>("checkSupported");
            return IsSupported;
        }

        public async Task RequestPortAsync()
        {
            var module = await _moduleTask.Value;
            await module.InvokeVoidAsync("requestPort");
        }

        public async Task<IEnumerable<string>> GetAvailablePortsAsync()
        {
            await RequestPortAsync();
            var module = await _moduleTask.Value;
            var ports = await module.InvokeAsync<IEnumerable<SerialPortInfo>>("getGrantedPorts");
            return ports.Select(p => $"{p.Name} ({p.Manufacturer})");
        }

        public async Task DisconnectAsync()
        {
            if (_portReference != null)
            {
                var module = await _moduleTask.Value;
                await module.InvokeVoidAsync("closePort", _portReference);
                _portReference = null;
                IsConnected = false;
                RaiseConnectionChanged();
            }
        }

        public async Task<bool> ConnectAsync(PrinterConnectionSettings connectionSettings)
        {
            if (connectionSettings.ConnectionType != ConnectionType || connectionSettings.Serial == null) throw new ArgumentException();
            await OpenPortAsync(connectionSettings.Serial.PortName, connectionSettings.Serial.BaudRate); 
            return IsConnected;
        }

        public async Task OpenPortAsync(string port, int baudRate, int dataBits = 8,
            int stopBits = 1, string parity = "none", string flowControl = "none")
        {
            var module = await _moduleTask.Value;
            var options = new { baudRate, dataBits, stopBits, parity, flowControl };
            _portReference = await module.InvokeAsync<IJSObjectReference>("openPort", options, _dotNetRef);
            IsConnected = true;
            ConnectionName = port;
            RaiseConnectionChanged();
        }

        public override async Task WriteDataAsync(string data)
        {
            if (_portReference == null) throw new InvalidOperationException("Port not open");
            var module = await _moduleTask.Value;
            await module.InvokeVoidAsync("writeData", _portReference, data);
        }

        public Task StartPrint(GCodeDoc gcodeDoc)
        {
            if (!IsConnected || string.IsNullOrEmpty(gcodeDoc.Content))
            {
                return Task.CompletedTask;
            }

            return Task.Run(async () =>
            {
                await foreach (var command in gcodeDoc.EnumerateCommandsAsync())
                {
                    if (!IsConnected)
                    {
                        break;
                    }

                    await WriteDataAsync(command);
                }
            });
        }

        [JSInvokable]
        public void OnDataReceived(string data)
        {
            ProcessReceivedData(data);
        }

        [JSInvokable]
        public void OnConnectionChanged(bool isConnected)
        {
            IsConnected = isConnected;
            RaiseConnectionChanged();
        }

        public override async ValueTask DisposeAsync()
        {
            await DisconnectAsync();

            if (_moduleTask.IsValueCreated)
            {
                var module = await _moduleTask.Value;
                await module.DisposeAsync();
            }

            _dotNetRef?.Dispose();
        }
    }

    public class SerialPortInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Manufacturer { get; set; } = string.Empty;
    }
}