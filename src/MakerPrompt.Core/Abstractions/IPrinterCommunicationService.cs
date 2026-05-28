using MakerPrompt.Core.Models;

namespace MakerPrompt.Core.Abstractions;

/// <summary>
/// Controls a single printer connection.
///
/// Architecture note
/// -----------------
/// This interface is intentionally scoped to ONE printer. Fleet / multi-printer
/// scenarios are handled at the Application layer via <see cref="IPrinterProvider"/>
/// and the fleet orchestration services that compose multiple
/// <see cref="IPrinterCommunicationService"/> instances.
/// </summary>
public interface IPrinterCommunicationService : IAsyncDisposable
{
    // ── Events ──────────────────────────────────────────────────────────────

    /// <summary>Raised when the connection state changes (true = connected, false = disconnected).</summary>
    event EventHandler<bool> ConnectionStateChanged;

    /// <summary>Raised whenever fresh telemetry is available from the printer.</summary>
    event EventHandler<PrinterTelemetry> TelemetryUpdated;

    // ── State ───────────────────────────────────────────────────────────────

    /// <summary>Backend protocol in use.</summary>
    PrinterConnectionType ConnectionType { get; }

    /// <summary>Most recently received telemetry snapshot.</summary>
    PrinterTelemetry LastTelemetry { get; }

    /// <summary>Human-readable name for this connection (e.g. "Workshop Prusa MK4").</summary>
    string ConnectionName { get; }

    /// <summary><c>true</c> when a live connection is established.</summary>
    bool IsConnected { get; }

    /// <summary><c>true</c> when a print job is actively running on this printer.</summary>
    bool IsPrinting { get; }

    /// <summary>
    /// <c>true</c> when this backend supports sending arbitrary G-code commands
    /// via <see cref="WriteDataAsync"/> and displaying the response in the command prompt.
    /// Backends that communicate over read-only APIs (e.g. PrusaLink) should return <c>false</c>.
    /// </summary>
    bool SupportsCommandPrompt => true;

    // ── Lifecycle ───────────────────────────────────────────────────────────

    /// <summary>Establishes the connection using the supplied settings.</summary>
    /// <returns><c>true</c> on success, <c>false</c> on failure.</returns>
    Task<bool> ConnectAsync(PrinterConnectionSettings settings, CancellationToken cancellationToken = default);

    /// <summary>Gracefully closes the connection and releases backend resources.</summary>
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    // ── Data transfer ───────────────────────────────────────────────────────

    /// <summary>Sends a raw G-code command string to the printer.</summary>
    Task WriteDataAsync(string command, CancellationToken cancellationToken = default);

    /// <summary>Fetches an up-to-date telemetry snapshot from the printer.</summary>
    Task<PrinterTelemetry> GetTelemetryAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns the list of files available on the printer's storage.</summary>
    Task<IReadOnlyList<string>> GetFilesAsync(CancellationToken cancellationToken = default);

    // ── Print control ───────────────────────────────────────────────────────

    /// <summary>Sets the hotend target temperature. Pass 0 to turn off.</summary>
    Task SetHotendTempAsync(int targetCelsius, CancellationToken cancellationToken = default);

    /// <summary>Sets the heated bed target temperature. Pass 0 to turn off.</summary>
    Task SetBedTempAsync(int targetCelsius, CancellationToken cancellationToken = default);

    /// <summary>Homes the specified axes.</summary>
    Task HomeAsync(bool x = true, bool y = true, bool z = true, CancellationToken cancellationToken = default);

    /// <summary>Performs a relative move on the given axes at <paramref name="feedRate"/> mm/min.</summary>
    Task RelativeMoveAsync(int feedRate, float x = 0f, float y = 0f, float z = 0f, float e = 0f,
        CancellationToken cancellationToken = default);

    /// <summary>Sets the part-cooling fan speed (0–100 %).</summary>
    Task SetFanSpeedAsync(int speedPercent, CancellationToken cancellationToken = default);

    /// <summary>Sets the print feed-rate override (typically 10–200 %).</summary>
    Task SetPrintSpeedAsync(int speedPercent, CancellationToken cancellationToken = default);

    /// <summary>Sets the extrusion flow-rate override (typically 10–200 %).</summary>
    Task SetPrintFlowAsync(int flowPercent, CancellationToken cancellationToken = default);

    /// <summary>Starts printing the specified file from printer storage.</summary>
    Task StartPrintAsync(string fileName, CancellationToken cancellationToken = default);

    /// <summary>Sends a G-code document to the printer for immediate streaming and printing.</summary>
    Task StartPrintAsync(GCodeDoc gcode, CancellationToken cancellationToken = default);
}
