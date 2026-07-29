namespace MakerPrompt.Infrastructure.Services;

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
    private bool _transportOpened;
    private bool _disposed;
    private int _pollInProgress;

    protected SerialCommunicationServiceBase()
    {
        _telemetryTimer.Elapsed += (_, _) => SafePollTelemetry();
        _telemetryTimer.AutoReset = true;
    }

    private void SafePollTelemetry()
    {
        if (Interlocked.Exchange(ref _pollInProgress, 1) != 0)
        {
            return;
        }

        _ = PollTelemetryAsync().ContinueWith(
            _ => Interlocked.Exchange(ref _pollInProgress, 0),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
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
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsConnected) return true;

        if (_transportOpened)
        {
            await CloseTransportSafelyAsync(CancellationToken.None).ConfigureAwait(false);
        }

        try
        {
            await OpenTransportAsync(settings, cancellationToken).ConfigureAwait(false);
            _transportOpened = true;
            IsConnected = true;
            ConnectionName = settings.PortName ?? settings.ConnectionType.ToString();
            LastTelemetry = new PrinterTelemetry { Status = PrinterStatus.Connected };
            _telemetryTimer.Start();
            RaiseConnectionChanged();
            return true;
        }
        catch (OperationCanceledException)
        {
            await CloseTransportSafelyAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        catch
        {
            await CloseTransportSafelyAsync(CancellationToken.None).ConfigureAwait(false);
            IsConnected = false;
            RaiseConnectionChanged();
            return false;
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        _telemetryTimer.Stop();
        var stateChanged = IsConnected || _transportOpened;
        IsConnected = false;
        IsPrinting = false;
        await CloseTransportSafelyAsync(cancellationToken).ConfigureAwait(false);

        if (stateChanged)
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

    public Task<IReadOnlyList<FileEntry>> GetFilesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<FileEntry>>([]);

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

    public Task SetAxisPerUnitAsync(
        float x = 0f, float y = 0f, float z = 0f, float e = 0f,
        CancellationToken cancellationToken = default)
    {
        if (!IsConnected) return Task.CompletedTask;

        var command = new StringBuilder("M92");
        if (x > 0) command.Append($" X{x}");
        if (y > 0) command.Append($" Y{y}");
        if (z > 0) command.Append($" Z{z}");
        if (e > 0) command.Append($" E{e}");
        return WriteTransportAsync(command.ToString(), cancellationToken);
    }

    public Task RunPidTuningAsync(
        int cycles, int targetTemp, int extruderIndex,
        CancellationToken cancellationToken = default)
    {
        if (!IsConnected) return Task.CompletedTask;
        return WriteTransportAsync(
            $"M303 E{extruderIndex} S{targetTemp} C{cycles}", cancellationToken);
    }

    public Task RunThermalModelCalibrationAsync(
        int cycles, int targetTemp, CancellationToken cancellationToken = default)
    {
        if (!IsConnected) return Task.CompletedTask;
        return WriteTransportAsync($"M303 E-1 S{targetTemp} C{cycles}", cancellationToken);
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

    public Task SaveEepromAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConnected) return Task.CompletedTask;
        return WriteTransportAsync("M500", cancellationToken);
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

    /// <summary>
    /// Marks the connection unavailable after a platform transport loop fails.
    /// Cleanup is performed by the next connect, disconnect, or disposal call so
    /// a receive loop never waits for itself.
    /// </summary>
    protected void ReportTransportFailure()
    {
        if (!IsConnected)
        {
            return;
        }

        _telemetryTimer.Stop();
        IsConnected = false;
        IsPrinting = false;
        LastTelemetry.Status = PrinterStatus.Disconnected;
        RaiseConnectionChanged();
    }

    private async Task CloseTransportSafelyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await CloseTransportAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // The connection is already considered closed. Platform transports
            // may throw while releasing a device that disappeared.
        }
        finally
        {
            _transportOpened = false;
        }
    }

    // ── IAsyncDisposable ─────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _telemetryTimer.Stop();
        await DisconnectAsync().ConfigureAwait(false);
        _telemetryTimer.Dispose();
        _disposed = true;

        GC.SuppressFinalize(this);
    }
}
