namespace MakerPrompt.Tests.Unit.Utils;

/// <summary>
/// A minimal, fully-controllable in-memory implementation of
/// <see cref="IPrinterCommunicationService"/> for use in unit tests.
/// </summary>
public sealed class FakePrinterService : IPrinterCommunicationService
{
    public PrinterConnectionType ConnectionType { get; set; } = PrinterConnectionType.Demo;
    public PrinterTelemetry LastTelemetry { get; set; } = new();
    public string ConnectionName { get; set; } = "FakePrinter";
    public bool IsConnected { get; private set; }
    public bool IsPrinting { get; private set; }
    public bool IsDisposed { get; private set; }
    public bool SupportsDirectControl { get; set; } = true;
    public bool SupportsCommandPrompt { get; set; } = true;
    public bool SupportsPrintStart { get; set; } = true;
    public bool SupportsPrinterQueue { get; set; }

    public bool ConnectShouldSucceed { get; set; } = true;
    public PrinterTelemetry? TelemetryToReturn { get; set; }
    public IReadOnlyList<FileEntry> FilesToReturn { get; set; } = [];
    public IReadOnlyList<PrintQueueEntry> QueueToReturn { get; set; } = [];

    public event EventHandler<bool>? ConnectionStateChanged;
    public event EventHandler<PrinterTelemetry>? TelemetryUpdated;

    public Task<bool> ConnectAsync(PrinterConnectionSettings settings,
        CancellationToken cancellationToken = default)
    {
        IsConnected = ConnectShouldSucceed;
        ConnectionStateChanged?.Invoke(this, IsConnected);
        return Task.FromResult(IsConnected);
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        IsConnected = false;
        IsPrinting = false;
        ConnectionStateChanged?.Invoke(this, false);
        return Task.CompletedTask;
    }

    public Task WriteDataAsync(string command, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<PrinterTelemetry> GetTelemetryAsync(CancellationToken cancellationToken = default)
    {
        var t = TelemetryToReturn ?? LastTelemetry;
        TelemetryUpdated?.Invoke(this, t);
        return Task.FromResult(t);
    }

    public Task<IReadOnlyList<FileEntry>> GetFilesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(FilesToReturn);

    public Task<IReadOnlyList<PrintQueueEntry>> GetPrinterQueueAsync(
        CancellationToken cancellationToken = default)
        => Task.FromResult(QueueToReturn);

    public Task SetHotendTempAsync(int targetCelsius, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task SetBedTempAsync(int targetCelsius, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task HomeAsync(bool x = true, bool y = true, bool z = true,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task RelativeMoveAsync(int feedRate, float x = 0, float y = 0, float z = 0, float e = 0,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task SetFanSpeedAsync(int speedPercent, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task SetPrintSpeedAsync(int speedPercent, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task SetPrintFlowAsync(int flowPercent, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task SetAxisPerUnitAsync(float x = 0, float y = 0, float z = 0, float e = 0,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task RunPidTuningAsync(int cycles, int targetTemp, int extruderIndex,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task RunThermalModelCalibrationAsync(int cycles, int targetTemp,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task StartPrintAsync(string fileName, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task StartPrintAsync(GCodeDoc gcode, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task SaveEepromAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }
}
