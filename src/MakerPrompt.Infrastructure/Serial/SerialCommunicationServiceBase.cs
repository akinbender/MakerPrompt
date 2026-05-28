using System.Text;
using System.Text.RegularExpressions;
using System.Timers;
using MakerPrompt.Core.Abstractions;
using MakerPrompt.Core.Models;

namespace MakerPrompt.Infrastructure;

/// <summary>
/// Base class for serial/USB printer communication services (Marlin / RepRap firmware).
///
/// Architecture
/// ------------
/// This class implements all <see cref="IPrinterCommunicationService"/> methods that
/// are protocol-level (G-code command building, Marlin response parsing, telemetry
/// polling timer).  Platform-specific transport (opening the port, writing/reading
/// bytes) is left to the concrete subclass via <see cref="WriteTransportAsync"/> and
/// the lifecycle hooks <see cref="OpenTransportAsync"/> / <see cref="CloseTransportAsync"/>.
///
/// Dependency
/// ----------
/// Only references <c>MakerPrompt.Core</c> — no Blazor, no MAUI, no platform APIs.
/// Platform subclasses live in the host projects (MakerPrompt.UI.MAUI).
/// </summary>
public abstract class SerialCommunicationServiceBase : IPrinterCommunicationService
{
    // ── Regex patterns for Marlin response parsing ───────────────────────────
    private static readonly Regex TempRegex =
        new(@"T:([\d.]+)\s*/\s*([\d.]+)\s+B:([\d.]+)\s*/\s*([\d.]+)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex PrintProgressRegex =
        new(@"SD printing byte (\d+)/(\d+)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // ── Events ───────────────────────────────────────────────────────────────
    public event EventHandler<bool>? ConnectionStateChanged;
    public event EventHandler<PrinterTelemetry>? TelemetryUpdated;

    // ── State ────────────────────────────────────────────────────────────────
    public PrinterConnectionType ConnectionType => PrinterConnectionType.Serial;
    public PrinterTelemetry LastTelemetry { get; private set; } = new();
    public string ConnectionName { get; protected set; } = string.Empty;
    public bool IsConnected { get; protected set; }
    public bool IsPrinting { get; protected set; }

    // ── Private fields ───────────────────────────────────────────────────────
    /// <summary>Default baud rate for Marlin/RepRap firmware. 250 000 bps is standard.</summary>
    protected const int DefaultBaudRate = 250_000;
    private static readonly TimeSpan TelemetryPollInterval = TimeSpan.FromSeconds(3);

    private readonly System.Timers.Timer _telemetryTimer = new(TelemetryPollInterval);
    private readonly StringBuilder _receiveBuffer = new();

    protected SerialCommunicationServiceBase()
    {
        _telemetryTimer.Elapsed += (_, _) => SafePollTelemetry();
        _telemetryTimer.AutoReset = true;
    }

    // Non-async timer callback that fires-and-forgets with exception guarding.
    private void SafePollTelemetry()
    {
        _ = PollTelemetryAsync().ContinueWith(
            t => Console.WriteLine($"[SerialCommunicationServiceBase] Telemetry poll error: {t.Exception?.GetBaseException().Message}"),
            System.Threading.Tasks.TaskContinuationOptions.OnlyOnFaulted);
    }

    // ── Abstract transport hooks ─────────────────────────────────────────────

    /// <summary>
    /// Opens the underlying transport (serial port, USB driver, etc.) using the
    /// supplied connection settings.  Called by <see cref="ConnectAsync"/>.
    /// </summary>
    protected abstract Task OpenTransportAsync(PrinterConnectionSettings settings,
        CancellationToken cancellationToken);

    /// <summary>
    /// Closes the underlying transport.  Called by <see cref="DisconnectAsync"/>
    /// and <see cref="DisposeAsync"/>.
    /// </summary>
    protected abstract Task CloseTransportAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Writes <paramref name="data"/> (a single G-code command line) to the transport.
    /// The base class appends a newline; implementations should send the bytes as-is
    /// or append their own framing.
    /// </summary>
    protected abstract Task WriteTransportAsync(string data, CancellationToken cancellationToken);

    // ── IPrinterCommunicationService: Lifecycle ──────────────────────────────

    public async Task<bool> ConnectAsync(PrinterConnectionSettings settings,
        CancellationToken cancellationToken = default)
    {
        if (IsConnected) return true;

        try
        {
            await OpenTransportAsync(settings, cancellationToken);
            IsConnected = true;
            ConnectionName = settings.PortName ?? settings.ConnectionType.ToString();
            LastTelemetry = new PrinterTelemetry { Status = PrinterStatus.Connected };
            _telemetryTimer.Start();
            RaiseConnectionChanged();
            return true;
        }
        catch
        {
            IsConnected = false;
            return false;
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConnected) return;

        _telemetryTimer.Stop();
        IsConnected = false;
        IsPrinting = false;

        try
        {
            await CloseTransportAsync(cancellationToken);
        }
        catch
        {
            // Swallow close errors — we are already marking as disconnected.
        }

        RaiseConnectionChanged();
    }

    // ── IPrinterCommunicationService: Data transfer ──────────────────────────

    public Task WriteDataAsync(string command, CancellationToken cancellationToken = default)
        => IsConnected ? WriteTransportAsync(command, cancellationToken) : Task.CompletedTask;

    public async Task<PrinterTelemetry> GetTelemetryAsync(
        CancellationToken cancellationToken = default)
    {
        await WriteTransportAsync("M105", cancellationToken); // temperatures
        await WriteTransportAsync("M27", cancellationToken);  // SD print progress
        await Task.Delay(200, cancellationToken);
        return LastTelemetry;
    }

    public Task<IReadOnlyList<string>> GetFilesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<string>>([]);

    // ── IPrinterCommunicationService: Print control ──────────────────────────

    public Task SetHotendTempAsync(int targetCelsius, CancellationToken cancellationToken = default)
    {
        if (!IsConnected) return Task.CompletedTask;
        return WriteTransportAsync($"M104 S{targetCelsius}", cancellationToken);
    }

    public Task SetBedTempAsync(int targetCelsius, CancellationToken cancellationToken = default)
    {
        if (!IsConnected) return Task.CompletedTask;
        return WriteTransportAsync($"M140 S{targetCelsius}", cancellationToken);
    }

    public async Task HomeAsync(bool x = true, bool y = true, bool z = true,
        CancellationToken cancellationToken = default)
    {
        if (!IsConnected) return;

        var axes = string.Concat(
            x ? "X" : "",
            y ? "Y" : "",
            z ? "Z" : "");

        await WriteTransportAsync(axes.Length > 0 ? $"G28 {axes}" : "G28", cancellationToken);
    }

    public async Task RelativeMoveAsync(int feedRate,
        float x = 0f, float y = 0f, float z = 0f, float e = 0f,
        CancellationToken cancellationToken = default)
    {
        if (!IsConnected) return;

        var sb = new StringBuilder("G1");
        if (x != 0f) sb.Append($" X{x:0.0}");
        if (y != 0f) sb.Append($" Y{y:0.0}");
        if (z != 0f) sb.Append($" Z{z:0.0}");
        if (e != 0f) sb.Append($" E{e:0.0}");
        sb.Append($" F{feedRate}");

        await WriteTransportAsync("G91", cancellationToken);
        await WriteTransportAsync(sb.ToString(), cancellationToken);
        await WriteTransportAsync("G90", cancellationToken);
    }

    public Task SetFanSpeedAsync(int speedPercent, CancellationToken cancellationToken = default)
    {
        if (!IsConnected) return Task.CompletedTask;
        if (speedPercent <= 0)
            return WriteTransportAsync("M107", cancellationToken);

        var value = (int)Math.Clamp(speedPercent * 2.55, 0, 255);
        return WriteTransportAsync($"M106 S{value}", cancellationToken);
    }

    public Task SetPrintSpeedAsync(int speedPercent, CancellationToken cancellationToken = default)
    {
        if (!IsConnected) return Task.CompletedTask;
        return WriteTransportAsync($"M220 S{speedPercent}", cancellationToken);
    }

    public Task SetPrintFlowAsync(int flowPercent, CancellationToken cancellationToken = default)
    {
        if (!IsConnected) return Task.CompletedTask;
        return WriteTransportAsync($"M221 S{flowPercent}", cancellationToken);
    }

    public Task StartPrintAsync(string fileName, CancellationToken cancellationToken = default)
    {
        if (!IsConnected) return Task.CompletedTask;
        IsPrinting = true;
        return WriteTransportAsync($"M23 {fileName}", cancellationToken);
    }

    public async Task StartPrintAsync(GCodeDoc gcode, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || string.IsNullOrWhiteSpace(gcode.Content)) return;
        IsPrinting = true;
        await foreach (var command in gcode.EnumerateCommandsAsync(cancellationToken))
            await WriteTransportAsync(command, cancellationToken);
    }

    // ── Response parsing (called by platform subclasses) ─────────────────────

    /// <summary>
    /// Appends <paramref name="data"/> to the receive buffer and processes any
    /// complete lines.  Platform subclasses call this from their read loops.
    /// </summary>
    protected void ProcessReceivedData(string data)
    {
        _receiveBuffer.Append(data);

        while (true)
        {
            var bufferStr = _receiveBuffer.ToString();
            var newlineIndex = bufferStr.IndexOf('\n');
            if (newlineIndex < 0) break;

            var line = bufferStr[..(newlineIndex + 1)].Trim('\r', '\n', ' ');
            if (!string.IsNullOrEmpty(line))
                ParseLine(line);

            _receiveBuffer.Remove(0, newlineIndex + 1);
        }
    }

    private void ParseLine(string line)
    {
        try
        {
            // Temperature response: ok T:200.00 /200.00 B:60.00 /60.00
            if (line.StartsWith("ok T:", StringComparison.Ordinal) ||
                line.StartsWith("T:", StringComparison.Ordinal))
            {
                var m = TempRegex.Match(line);
                if (m.Success)
                {
                    LastTelemetry.HotendTemp = double.Parse(m.Groups[1].Value,
                        System.Globalization.CultureInfo.InvariantCulture);
                    LastTelemetry.HotendTarget = double.Parse(m.Groups[2].Value,
                        System.Globalization.CultureInfo.InvariantCulture);
                    LastTelemetry.BedTemp = double.Parse(m.Groups[3].Value,
                        System.Globalization.CultureInfo.InvariantCulture);
                    LastTelemetry.BedTarget = double.Parse(m.Groups[4].Value,
                        System.Globalization.CultureInfo.InvariantCulture);
                    LastTelemetry.CapturedAt = DateTimeOffset.UtcNow;
                    LastTelemetry.Status = IsConnected ? PrinterStatus.Connected : PrinterStatus.Disconnected;
                }
            }
            // SD progress: SD printing byte 12345/67890
            else if (line.Contains("SD printing byte", StringComparison.Ordinal))
            {
                var m = PrintProgressRegex.Match(line);
                if (m.Success)
                {
                    var done = double.Parse(m.Groups[1].Value,
                        System.Globalization.CultureInfo.InvariantCulture);
                    var total = double.Parse(m.Groups[2].Value,
                        System.Globalization.CultureInfo.InvariantCulture);
                    if (total > 0)
                    {
                        LastTelemetry.PrintProgress = done / total * 100.0;
                        LastTelemetry.Status = PrinterStatus.Printing;
                        IsPrinting = true;
                    }
                }
            }
            // Print complete
            else if (line.Equals("Done printing file", StringComparison.OrdinalIgnoreCase))
            {
                LastTelemetry.PrintProgress = 100;
                LastTelemetry.Status = PrinterStatus.Connected;
                IsPrinting = false;
            }

            RaiseTelemetryUpdated();
        }
        catch
        {
            // Swallow parse errors — never crash the receive loop.
        }
    }

    // ── Telemetry polling ────────────────────────────────────────────────────

    private async Task PollTelemetryAsync()
    {
        if (!IsConnected) return;
        try
        {
            await WriteTransportAsync("M105", CancellationToken.None);
            await WriteTransportAsync("M27", CancellationToken.None);
        }
        catch
        {
            // Telemetry polling errors are swallowed silently — no log spam.
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    protected void RaiseConnectionChanged() =>
        ConnectionStateChanged?.Invoke(this, IsConnected);

    protected void RaiseTelemetryUpdated() =>
        TelemetryUpdated?.Invoke(this, LastTelemetry);

    // ── IAsyncDisposable ─────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        _telemetryTimer.Stop();
        _telemetryTimer.Dispose();

        if (IsConnected)
        {
            await DisconnectAsync();
        }

        GC.SuppressFinalize(this);
    }
}
