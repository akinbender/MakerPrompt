using System.Net.Http.Headers;
using MakerPrompt.Shared.Infrastructure;
using MakerPrompt.Shared.Models;
using MakerPrompt.Shared.Utils;

namespace MakerPrompt.Shared.Services;

/// <summary>
/// OctoPrint REST API backend.
/// 
/// Implements printer communication via OctoPrint's documented REST API:
/// https://docs.octoprint.org/en/master/api/
/// 
/// Follows the same HTTP/JSON polling pattern as PrusaLinkApiService.
/// Supports:
///   - Connection management (connect/disconnect)
///   - Printer status & telemetry polling
///   - Job status (progress, filename, time remaining)
///   - G-code command execution
///   - File listing and print start
///   - Webcam streams (via OctoPrint settings)
///   - Temperature control, fan, movement commands via G-code
/// 
/// Authentication: X-Api-Key header (standard OctoPrint API key auth).
/// </summary>
public sealed class OctoPrintApiService : BasePrinterConnectionService, IPrinterCommunicationService
{
    private readonly CancellationTokenSource _cts = new();
    private readonly HttpMessageHandler? _customHandler;
    private HttpClient? _httpClient;
    private Uri? _baseUri;
    private bool _telemetryTimerInitialized;
    private bool _disposed;

    public override PrinterConnectionType ConnectionType => PrinterConnectionType.OctoPrint;

    public OctoPrintApiService() { }

    public OctoPrintApiService(HttpMessageHandler handler)
    {
        _customHandler = handler;
        _httpClient = new HttpClient(handler, false);
    }

    private HttpClient Client
    {
        get
        {
            _httpClient ??= _customHandler is not null
                ? new HttpClient(_customHandler, false)
                : new HttpClient();
            return _httpClient;
        }
    }

    public async Task<bool> ConnectAsync(PrinterConnectionSettings connectionSettings)
    {
        if (IsConnected) return true;

        if (connectionSettings.Api is null)
            throw new ArgumentException("OctoPrint connection requires API settings.", nameof(connectionSettings));

        _baseUri = new Uri(connectionSettings.Api.Url);
        ConfigureClient(connectionSettings.Api);

        try
        {
            // Verify connectivity using the version endpoint (no auth for basic check, but
            // API key is required for full access)
            using var response = await Client.GetAsync("/api/version", _cts.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                IsConnected = false;
                RaiseConnectionChanged();
                return false;
            }

            var versionJson = await response.Content.ReadAsStringAsync(_cts.Token).ConfigureAwait(false);
            using var versionDoc = JsonDocument.Parse(versionJson);
            if (versionDoc.RootElement.TryGetProperty("text", out var textEl))
            {
                ConnectionName = textEl.GetString() ?? _baseUri.AbsoluteUri;
            }
            else
            {
                ConnectionName = _baseUri.AbsoluteUri;
            }

            // Also get current printer state
            using var stateResponse = await Client.GetAsync("/api/connection", _cts.Token).ConfigureAwait(false);
            if (stateResponse.IsSuccessStatusCode)
            {
                var stateJson = await stateResponse.Content.ReadAsStringAsync(_cts.Token).ConfigureAwait(false);
                using var stateDoc = JsonDocument.Parse(stateJson);
                if (stateDoc.RootElement.TryGetProperty("current", out var current) &&
                    current.TryGetProperty("state", out var stateStr))
                {
                    var state = stateStr.GetString();
                    // If OctoPrint is not connected to the printer, try connecting
                    if (state == "Closed" || state == "Offline")
                    {
                        await SendConnectCommandAsync().ConfigureAwait(false);
                    }
                }
            }

            IsConnected = true;
            LastTelemetry.ConnectionTime = DateTime.UtcNow;

            if (!_telemetryTimerInitialized)
            {
                updateTimer.Elapsed += async (_, _) => await SafeTelemetryAsync().ConfigureAwait(false);
                _telemetryTimerInitialized = true;
            }
            updateTimer.Start();
        }
        catch
        {
            IsConnected = false;
        }

        RaiseConnectionChanged();
        return IsConnected;
    }

    /// <summary>
    /// Sends a connect command to OctoPrint to connect to the physical printer.
    /// </summary>
    private async Task SendConnectCommandAsync()
    {
        try
        {
            var payload = JsonSerializer.Serialize(new { command = "connect" });
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            await Client.PostAsync("/api/connection", content, _cts.Token).ConfigureAwait(false);
        }
        catch
        {
            // Non-critical — telemetry polling will catch errors
        }
    }

    public async Task DisconnectAsync()
    {
        updateTimer.Stop();
        _cts.Cancel();
        _httpClient?.CancelPendingRequests();
        IsConnected = false;
        RaiseConnectionChanged();
        await Task.CompletedTask;
    }

    public async Task WriteDataAsync(string command)
    {
        if (!IsConnected) return;

        var payload = JsonSerializer.Serialize(new { command });
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await Client.PostAsync("/api/printer/command", content, _cts.Token).ConfigureAwait(false);
        var result = await response.Content.ReadAsStringAsync(_cts.Token).ConfigureAwait(false);
        LastTelemetry.LastResponse = result;
        RaiseTelemetryUpdated();
    }

    /// <summary>
    /// Sends one or more G-code commands via the OctoPrint command API.
    /// </summary>
    private async Task SendGcodeAsync(params string[] commands)
    {
        if (!IsConnected) return;

        var payload = JsonSerializer.Serialize(new { commands });
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        await Client.PostAsync("/api/printer/command", content, _cts.Token).ConfigureAwait(false);
    }

    public async Task<PrinterTelemetry> GetPrinterTelemetryAsync()
    {
        if (!IsConnected) return LastTelemetry;

        try
        {
            // Get printer state (temperatures, flags)
            using var printerResponse = await Client.GetAsync("/api/printer", _cts.Token).ConfigureAwait(false);
            if (printerResponse.IsSuccessStatusCode)
            {
                var json = await printerResponse.Content.ReadAsStringAsync(_cts.Token).ConfigureAwait(false);
                ParsePrinterState(json);
            }

            // Get job status (progress, file, time remaining)
            using var jobResponse = await Client.GetAsync("/api/job", _cts.Token).ConfigureAwait(false);
            if (jobResponse.IsSuccessStatusCode)
            {
                var json = await jobResponse.Content.ReadAsStringAsync(_cts.Token).ConfigureAwait(false);
                ParseJobState(json);
            }
        }
        catch
        {
            // Swallow telemetry errors
        }

        LastTelemetry.LastResponse = "OctoPrint telemetry update";
        RaiseTelemetryUpdated();
        return LastTelemetry;
    }

    private void ParsePrinterState(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Temperature
        if (root.TryGetProperty("temperature", out var temp))
        {
            if (temp.TryGetProperty("tool0", out var tool0))
            {
                if (tool0.TryGetProperty("actual", out var actual))
                    LastTelemetry.HotendTemp = actual.GetDouble();
                if (tool0.TryGetProperty("target", out var target))
                    LastTelemetry.HotendTarget = target.GetDouble();
            }

            if (temp.TryGetProperty("bed", out var bed))
            {
                if (bed.TryGetProperty("actual", out var actual))
                    LastTelemetry.BedTemp = actual.GetDouble();
                if (bed.TryGetProperty("target", out var target))
                    LastTelemetry.BedTarget = target.GetDouble();
            }
        }

        // State flags
        if (root.TryGetProperty("state", out var state))
        {
            if (state.TryGetProperty("flags", out var flags))
            {
                var isPrinting = flags.TryGetProperty("printing", out var p) && p.GetBoolean();
                var isPaused = flags.TryGetProperty("pausing", out var pa) && pa.GetBoolean();
                var isError = flags.TryGetProperty("error", out var e) && e.GetBoolean();

                if (isError)
                    LastTelemetry.Status = PrinterStatus.Error;
                else if (isPaused)
                    LastTelemetry.Status = PrinterStatus.Paused;
                else if (isPrinting)
                    LastTelemetry.Status = PrinterStatus.Printing;
                else
                    LastTelemetry.Status = PrinterStatus.Connected;
            }
        }
    }

    private void ParseJobState(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("progress", out var progress))
        {
            if (progress.TryGetProperty("completion", out var completion) &&
                completion.ValueKind == JsonValueKind.Number)
            {
                LastTelemetry.SDCard.Progress = completion.GetDouble();
            }

            if (progress.TryGetProperty("printTime", out var printTime) &&
                printTime.ValueKind == JsonValueKind.Number)
            {
                // printTime is in seconds
            }
        }

        if (root.TryGetProperty("state", out var state) && state.ValueKind == JsonValueKind.String)
        {
            var stateStr = state.GetString()?.ToLowerInvariant();
            if (stateStr?.Contains("printing") == true)
            {
                LastTelemetry.SDCard.Printing = true;
                IsPrinting = true;
            }
            else
            {
                LastTelemetry.SDCard.Printing = false;
                IsPrinting = false;
            }
        }
    }

    public async Task<List<FileEntry>> GetFilesAsync()
    {
        if (!IsConnected) return [];

        try
        {
            using var response = await Client.GetAsync("/api/files?recursive=true", _cts.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return [];

            var json = await response.Content.ReadAsStringAsync(_cts.Token).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("files", out var filesArray))
                return [];

            var files = new List<FileEntry>();
            ParseFilesRecursive(filesArray, files);
            return files;
        }
        catch
        {
            return [];
        }
    }

    private static void ParseFilesRecursive(JsonElement filesArray, List<FileEntry> result)
    {
        foreach (var file in filesArray.EnumerateArray())
        {
            var type = file.TryGetProperty("type", out var t) ? t.GetString() : null;

            if (type == "folder" && file.TryGetProperty("children", out var children))
            {
                ParseFilesRecursive(children, result);
                continue;
            }

            if (type == "machinecode" || type == "model")
            {
                var path = file.TryGetProperty("path", out var p) ? p.GetString() ?? "" : "";
                var size = file.TryGetProperty("size", out var s) && s.ValueKind == JsonValueKind.Number ? s.GetInt64() : 0;
                var dateUnix = file.TryGetProperty("date", out var d) && d.ValueKind == JsonValueKind.Number ? d.GetInt64() : 0;

                result.Add(new FileEntry
                {
                    FullPath = path,
                    Size = size,
                    ModifiedDate = dateUnix > 0 ? DateTimeOffset.FromUnixTimeSeconds(dateUnix).DateTime : null,
                    IsAvailable = true
                });
            }
        }
    }

    /// <summary>
    /// Retrieves available webcam configurations from OctoPrint settings.
    /// </summary>
    public async Task<IReadOnlyList<PrinterCamera>> GetCamerasAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _baseUri is null) return Array.Empty<PrinterCamera>();

        try
        {
            using var response = await Client.GetAsync("/api/settings", cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return Array.Empty<PrinterCamera>();

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("webcam", out var webcam))
                return Array.Empty<PrinterCamera>();

            var streamUrl = webcam.TryGetProperty("streamUrl", out var su) ? su.GetString() : null;
            var snapshotUrl = webcam.TryGetProperty("snapshotUrl", out var sn) ? sn.GetString() : null;
            var webcamEnabled = !webcam.TryGetProperty("webcamEnabled", out var we) || we.GetBoolean();

            if (!webcamEnabled || (string.IsNullOrWhiteSpace(streamUrl) && string.IsNullOrWhiteSpace(snapshotUrl)))
                return Array.Empty<PrinterCamera>();

            // Resolve relative URLs against base URI
            if (streamUrl != null && !Uri.IsWellFormedUriString(streamUrl, UriKind.Absolute))
                streamUrl = new Uri(_baseUri, streamUrl).AbsoluteUri;
            if (snapshotUrl != null && !Uri.IsWellFormedUriString(snapshotUrl, UriKind.Absolute))
                snapshotUrl = new Uri(_baseUri, snapshotUrl).AbsoluteUri;

            return new[]
            {
                new PrinterCamera
                {
                    Id = "octoprint-webcam",
                    DisplayName = "OctoPrint Webcam",
                    StreamUrl = streamUrl,
                    SnapshotUrl = snapshotUrl,
                    IsEnabled = true
                }
            };
        }
        catch
        {
            return Array.Empty<PrinterCamera>();
        }
    }

    // ── Printer control commands via G-code ────────────────────────────

    public Task SetHotendTemp(int targetTemp = 0) =>
        SendGcodeAsync($"M104 S{targetTemp}");

    public Task SetBedTemp(int targetTemp = 0) =>
        SendGcodeAsync($"M140 S{targetTemp}");

    public Task Home(bool x = true, bool y = true, bool z = true)
    {
        var axes = new StringBuilder("G28");
        if (x) axes.Append(" X");
        if (y) axes.Append(" Y");
        if (z) axes.Append(" Z");
        return SendGcodeAsync(axes.ToString());
    }

    public Task RelativeMove(int feedRate, float x = 0.0f, float y = 0.0f, float z = 0.0f, float e = 0.0f)
    {
        var sb = new StringBuilder();
        sb.Append("G91\nG1");
        if (Math.Abs(x) > 0.0001f) sb.Append($" X{x}");
        if (Math.Abs(y) > 0.0001f) sb.Append($" Y{y}");
        if (Math.Abs(z) > 0.0001f) sb.Append($" Z{z}");
        if (Math.Abs(e) > 0.0001f) sb.Append($" E{e}");
        sb.Append($" F{feedRate}\nG90");
        return SendGcodeAsync(sb.ToString().Split('\n'));
    }

    public Task SetFanSpeed(int fanSpeedPercentage = 0)
    {
        var duty = (int)Math.Round(Math.Clamp(fanSpeedPercentage, 0, 100) * 255.0 / 100.0);
        return SendGcodeAsync($"M106 S{duty}");
    }

    public Task SetPrintSpeed(int speed) =>
        SendGcodeAsync($"M220 S{Math.Clamp(speed, 1, 200)}");

    public Task SetPrintFlow(int flow) =>
        SendGcodeAsync($"M221 S{Math.Clamp(flow, 1, 200)}");

    public Task SetAxisPerUnit(float x = 0.0f, float y = 0.0f, float z = 0.0f, float e = 0.0f)
    {
        var sb = new StringBuilder("M92");
        if (x > 0) sb.Append($" X{x}");
        if (y > 0) sb.Append($" Y{y}");
        if (z > 0) sb.Append($" Z{z}");
        if (e > 0) sb.Append($" E{e}");
        return SendGcodeAsync(sb.ToString());
    }

    public Task RunPidTuning(int cycles, int targetTemp, int extruderIndex) =>
        SendGcodeAsync($"M303 E{extruderIndex} S{targetTemp} C{cycles}");

    public Task RunThermalModelCalibration(int cycles, int targetTemp) =>
        SendGcodeAsync($"M303 E-1 S{targetTemp} C{cycles}");

    public async Task StartPrint(FileEntry file)
    {
        if (!IsConnected || string.IsNullOrWhiteSpace(file.FullPath)) return;

        var payload = JsonSerializer.Serialize(new
        {
            command = "select",
            print = true
        });
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");

        // OctoPrint expects POST to /api/files/{location}/{filename}
        var location = "local";
        var filePath = file.FullPath.TrimStart('/');
        await Client.PostAsync($"/api/files/{location}/{filePath}", content, _cts.Token).ConfigureAwait(false);
    }

    public Task StartPrint(GCodeDoc gcodeDoc)
    {
        if (!IsConnected || string.IsNullOrEmpty(gcodeDoc.Content))
            return Task.CompletedTask;

        return Task.Run(async () =>
        {
            await foreach (var command in gcodeDoc.EnumerateCommandsAsync(_cts.Token))
            {
                if (!IsConnected) break;
                await SendGcodeAsync(command);
            }
        });
    }

    public Task SaveEEPROM() => SendGcodeAsync("M500");

    // ── Helpers ──────────────────────────────────────────────────────────

    private async Task SafeTelemetryAsync()
    {
        try
        {
            await GetPrinterTelemetryAsync().ConfigureAwait(false);
        }
        catch
        {
            // Swallow background polling errors
        }
    }

    private void ConfigureClient(ApiConnectionSettings settings)
    {
        var client = Client;
        _baseUri = new Uri(settings.Url);
        client.BaseAddress = _baseUri;
        client.Timeout = TimeSpan.FromSeconds(30);
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        // OctoPrint uses X-Api-Key header for authentication
        // The API key is stored in Password field
        if (!string.IsNullOrEmpty(settings.Password))
        {
            client.DefaultRequestHeaders.Remove("X-Api-Key");
            client.DefaultRequestHeaders.Add("X-Api-Key", settings.Password);
        }
    }

    public override ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        _disposed = true;

        _cts.Cancel();
        updateTimer.Dispose();
        _httpClient?.Dispose();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
