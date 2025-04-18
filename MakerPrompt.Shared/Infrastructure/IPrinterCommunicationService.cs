namespace MakerPrompt.Shared.Infrastructure
{
    public interface IPrinterCommunicationService : IAsyncDisposable
    {
        event EventHandler<bool> ConnectionStateChanged;
        event EventHandler<string> DataRecieved;
        event EventHandler<PrinterTelemetry> TelemetryUpdated;
        PrinterConnectionType ConnectionType { get; }
        string ConnectionName { get; }
        bool IsConnected { get; }

        Task<bool> ConnectAsync(PrinterConnectionSettings connectionSettings);
        Task DisconnectAsync();
        Task WriteDataAsync(string command);

        Task WriteDataAsync(GCodeCommand command);
        Task<PrinterTelemetry> GetPrinterTelemetryAsync();
    }

}
