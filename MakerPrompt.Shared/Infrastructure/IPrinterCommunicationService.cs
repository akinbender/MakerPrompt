using MakerPrompt.Shared.Models;
using static MakerPrompt.Shared.Utils.Enums;

namespace MakerPrompt.Shared.Infrastructure
{
    public interface IPrinterCommunicationService : IAsyncDisposable
    {
        event EventHandler<bool> ConnectionStateChanged;
        event EventHandler<PrinterTelemetry> TelemetryUpdated;

        PrinterConnectionType ConnectionType { get; }
        string ConnectionName { get; }
        bool IsConnected { get; }

        Task<bool> ConnectAsync();
        Task DisconnectAsync();
        Task WriteDataAsync(string command);
        Task<PrinterTelemetry> GetPrinterTelemetryAsync();
    }

}
