namespace MakerPrompt.Shared.Infrastructure
{
    public abstract class BasePrinterConnectionService : IPrinterCommunicationService
    {
        public event EventHandler<bool>? ConnectionStateChanged;
        public event EventHandler<string>? DataRecieved;
        public event EventHandler<PrinterTelemetry>? TelemetryUpdated;

        internal PrinterTelemetry LastTelemetry { get; set; } = new();

        public abstract PrinterConnectionType ConnectionType { get; }

        public string ConnectionName { get; set; } = string.Empty;

        public bool IsConnected { get; set; } = false;

        public CancellationTokenSource _cts = new();

        public readonly System.Timers.Timer updateTimer = new(TimeSpan.FromMilliseconds(3000));

        public void RaiseConnectionChanged()
        {
            ConnectionStateChanged?.Invoke(this, IsConnected);
        }

        public void RaiseDataRecieved(string data)
        {
            if (DataRecieved == null) return;
            DataRecieved?.Invoke(this, data);
        }

        public void RaiseTelemetryUpdated()
        {
            TelemetryUpdated?.Invoke(this, LastTelemetry);
        }

        public abstract Task<bool> ConnectAsync(PrinterConnectionSettings connectionSettings);

        public abstract Task DisconnectAsync();

        public abstract Task WriteDataAsync(string command);

        public abstract Task WriteDataAsync(GCodeCommand command);

        public abstract Task<PrinterTelemetry> GetPrinterTelemetryAsync();

        public abstract ValueTask DisposeAsync();
    }
}
