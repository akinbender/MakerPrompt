﻿namespace MakerPrompt.Shared.Infrastructure
{
    public abstract class BasePrinterConnectionService : IAsyncDisposable
    {
        public event EventHandler<bool>? ConnectionStateChanged;
        public event EventHandler<PrinterTelemetry>? TelemetryUpdated;
        internal PrinterTelemetry LastTelemetry { get; set; } = new();

        public abstract Enums.PrinterConnectionType ConnectionType { get; }

        public string ConnectionName { get; set; } = string.Empty;

        public bool IsConnected { get; set; } = false;

        public readonly System.Timers.Timer updateTimer = new(TimeSpan.FromMilliseconds(3000));

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
