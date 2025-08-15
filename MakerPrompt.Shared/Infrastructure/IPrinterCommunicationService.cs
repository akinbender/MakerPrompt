namespace MakerPrompt.Shared.Infrastructure
{
    public interface IPrinterCommunicationService : IAsyncDisposable
    {
        event EventHandler<bool> ConnectionStateChanged;
        event EventHandler<PrinterTelemetry> TelemetryUpdated;

        PrinterConnectionType ConnectionType { get; }
        PrinterTelemetry LastTelemetry { get; }
        string ConnectionName { get; }
        bool IsConnected { get; }

        Task<bool> ConnectAsync(PrinterConnectionSettings connectionSettings);
        Task DisconnectAsync();
        Task WriteDataAsync(string command);
        Task<PrinterTelemetry> GetPrinterTelemetryAsync();
        Task<List<FileEntry>> GetFilesAsync();
        Task SetHotendTemp(int targetTemp = 0);
        Task SetBedTemp(int targetTemp = 0);
        Task Home(bool x = true, bool y = true, bool z = true);
        Task RelativeMove(int feedRate, float x = 0.0f, float y = 0.0f, float z = 0.0f, float e = 0.0f);
        Task SetFanSpeed(int fanSpeedPercentage = 0);
        Task SetPrintSpeed(int speed);
        Task SetPrintFlow(int flow);
        Task SetAxisPerUnit(float x = 0.0f, float y = 0.0f, float z = 0.0f, float e = 0.0f);
        Task RunPidTuning(int cycles, int targetTemp, int extruderIndex);
        Task RunThermalModelCalibration(int cycles, int targetTemp);
        Task StartPrint(FileEntry file);
        Task SaveEEPROM();
    }
}
