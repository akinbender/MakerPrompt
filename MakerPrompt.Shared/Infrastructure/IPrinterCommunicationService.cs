namespace MakerPrompt.Shared.Infrastructure
{
    public interface IPrinterCommunicationService : IAsyncDisposable
    {
        event EventHandler<bool> ConnectionStateChanged;
        event EventHandler<PrinterTelemetry> TelemetryUpdated;

        PrinterConnectionType ConnectionType { get; }
        string ConnectionName { get; }
        bool IsConnected { get; }

        Task<bool> ConnectAsync(PrinterConnectionSettings connectionSettings);
        Task DisconnectAsync();
        Task WriteDataAsync(string command);
        Task<PrinterTelemetry> GetPrinterTelemetryAsync();
    }

}
