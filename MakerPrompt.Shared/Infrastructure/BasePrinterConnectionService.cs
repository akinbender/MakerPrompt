using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MakerPrompt.Shared.Models;
using MakerPrompt.Shared.Utils;

namespace MakerPrompt.Shared.Infrastructure
{
    public abstract class BasePrinterConnectionService : IPrinterCommunicationService
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

        public abstract Task<bool> ConnectAsync();

        public abstract Task DisconnectAsync();

        public abstract Task WriteDataAsync(string command);

        public abstract Task<PrinterTelemetry> GetPrinterTelemetryAsync();

        public abstract ValueTask DisposeAsync();
    }
}
