namespace MakerPrompt.Shared.Infrastructure
{
    public abstract class BasePrinterConnectionService : IAsyncDisposable
    {
        public event EventHandler<bool>? ConnectionStateChanged;
        public event EventHandler<PrinterTelemetry>? TelemetryUpdated;
        public PrinterTelemetry LastTelemetry { get; set; } = new();

        public abstract PrinterConnectionType ConnectionType { get; }

        public string ConnectionName { get; set; } = string.Empty;

        public bool IsConnected { get; set; } = false;

        public readonly System.Timers.Timer updateTimer = new(TimeSpan.FromMilliseconds(3000));

        // True while a print job is actively streaming G-code to the printer.
        public bool IsPrinting { get; protected set; }

        public void RaiseConnectionChanged()
        {
            ConnectionStateChanged?.Invoke(this, IsConnected);
        }

        public void RaiseTelemetryUpdated()
        {
            TelemetryUpdated?.Invoke(this, LastTelemetry);
        }
        
        public abstract ValueTask DisposeAsync();
    }
}
