using CoreService = MakerPrompt.Core.Abstractions.IPrinterCommunicationService;

namespace MakerPrompt.Infrastructure.Printers;

/// <summary>
/// Extended printer communication contract for the Infrastructure layer.
/// Adds backward-compatible (no-CT, old naming) methods on top of the Core interface,
/// with default interface implementations that bridge to the Core methods.
/// </summary>
public interface IPrinterCommunicationService : CoreService
{
    // ── Backward-compat methods (no CancellationToken, original names) ──────────

    new Task<bool> ConnectAsync(PrinterConnectionSettings connectionSettings);
    new Task DisconnectAsync();
    new Task WriteDataAsync(string command);
    Task<PrinterTelemetry> GetPrinterTelemetryAsync();
    Task<List<FileEntry>> GetFilesAsync();
    Task SetHotendTemp(int targetTemp = 0);
    Task SetBedTemp(int targetTemp = 0);
    Task Home(bool x = true, bool y = true, bool z = true);
    Task RelativeMove(int feedRate, float x = 0f, float y = 0f, float z = 0f, float e = 0f);
    Task SetFanSpeed(int fanSpeedPercentage = 0);
    Task SetPrintSpeed(int speed);
    Task SetPrintFlow(int flow);
    Task SetAxisPerUnit(float x = 0f, float y = 0f, float z = 0f, float e = 0f);
    Task RunPidTuning(int cycles, int targetTemp, int extruderIndex);
    Task RunThermalModelCalibration(int cycles, int targetTemp);
    Task StartPrint(FileEntry file);
    Task StartPrint(GCodeDoc document);
    Task SaveEEPROM();

    // ── Default implementations — bridge Core interface to old method names ──────

    Task<bool> CoreService.ConnectAsync(PrinterConnectionSettings s, CancellationToken ct)
        => ConnectAsync(s);

    Task CoreService.DisconnectAsync(CancellationToken ct)
        => DisconnectAsync();

    Task CoreService.WriteDataAsync(string command, CancellationToken ct)
        => WriteDataAsync(command);

    async Task<PrinterTelemetry> CoreService.GetTelemetryAsync(CancellationToken ct)
        => await GetPrinterTelemetryAsync();

    async Task<IReadOnlyList<string>> CoreService.GetFilesAsync(CancellationToken ct)
    {
        var files = await GetFilesAsync();
        return files.Select(f => f.FullPath).ToList();
    }

    Task CoreService.SetHotendTempAsync(int targetCelsius, CancellationToken ct)
        => SetHotendTemp(targetCelsius);

    Task CoreService.SetBedTempAsync(int targetCelsius, CancellationToken ct)
        => SetBedTemp(targetCelsius);

    Task CoreService.HomeAsync(bool x, bool y, bool z, CancellationToken ct)
        => Home(x, y, z);

    Task CoreService.RelativeMoveAsync(int feedRate, float x, float y, float z, float e, CancellationToken ct)
        => RelativeMove(feedRate, x, y, z, e);

    Task CoreService.SetFanSpeedAsync(int speedPercent, CancellationToken ct)
        => SetFanSpeed(speedPercent);

    Task CoreService.SetPrintSpeedAsync(int speedPercent, CancellationToken ct)
        => SetPrintSpeed(speedPercent);

    Task CoreService.SetPrintFlowAsync(int flowPercent, CancellationToken ct)
        => SetPrintFlow(flowPercent);

    Task CoreService.StartPrintAsync(string fileName, CancellationToken ct)
        => StartPrint(new FileEntry { FullPath = fileName });
}
