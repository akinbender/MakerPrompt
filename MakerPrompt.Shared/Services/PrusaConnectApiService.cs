namespace MakerPrompt.Shared.Services;

/// <summary>
/// PrusaConnect cloud API backend.
///
/// Communicates with the Prusa Connect REST API hosted at connect.prusa3d.com.
/// Documented at: https://connect.prusa3d.com/docs/
///
/// Authentication: API key sent in the <c>x-api-key</c> header.
/// Obtain it from Prusa Connect → Account → API Keys.
///
/// Connection settings mapping:
///   <c>Api.UserName</c> — Printer UUID (visible in Prusa Connect printer detail)
///   <c>Api.Password</c> — API key
///   <c>Api.Url</c>      — ignored; base URL is always <see cref="BaseUrl"/>
///
/// Supports:
///   - Printer state + temperature telemetry polling
///   - Job progress and elapsed time
///   - Camera snapshot retrieval via webcam.connect.prusa3d.com
///
/// Direct G-code, motion, and temperature control commands are not available
/// through the PrusaConnect cloud API. Use PrusaLink for local direct control.
/// </summary>
public sealed class PrusaConnectApiService : BasePrinterConnectionService, IPrinterCommunicationService
{
    private const string BaseUrl = "https://connect.prusa3d.com";
    private const string WebcamBaseUrl = "https://webcam.connect.prusa3d.com";

    private readonly CancellationTokenSource _cts = new();
    private HttpClient? _httpClient;
    private bool _telemetryTimerInitialized;
    private string? _printerUuid;

    public override PrinterConnectionType ConnectionType => PrinterConnectionType.PrusaConnect;
    public bool SupportsDirectControl => false;

    public PrusaConnectApiService() { }

    public PrusaConnectApiService(HttpMessageHandler handler)
    {
        _httpClient = new HttpClient(handler, false) { BaseAddress = new Uri(BaseUrl) };
    }

    private HttpClient Client => _httpClient ??= new HttpClient { BaseAddress = new Uri(BaseUrl) };

    public async Task<bool> ConnectAsync(PrinterConnectionSettings connectionSettings)
    {
        if (connectionSettings.Api is null)
            throw new ArgumentException("PrusaConnect connection requires API settings.", nameof(connectionSettings));

        _printerUuid = connectionSettings.Api.UserName;
        ConfigureClient(connectionSettings.Api.Password);

        try
        {
            var printer = await GetPrinterAsync(_cts.Token);
            if (printer == null)
            {
                IsConnected = false;
                RaiseConnectionChanged();
                return false;
            }

            ConnectionName = printer.Name ?? $"PrusaConnect ({_printerUuid})";
            LastTelemetry.PrinterName = printer.Name ?? LastTelemetry.PrinterName;
            LastTelemetry.ConnectionTime = DateTime.UtcNow;

            if (!_telemetryTimerInitialized)
            {
                updateTimer.Elapsed += async (_, _) => await SafeTelemetryAsync();
                _telemetryTimerInitialized = true;
            }

            updateTimer.Start();
            IsConnected = true;
        }
        catch
        {
            IsConnected = false;
        }

        RaiseConnectionChanged();
        return IsConnected;
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

    public Task WriteDataAsync(string command) =>
        Task.FromException(new NotSupportedException("PrusaConnect cloud API does not support direct G-code commands."));

    public async Task<PrinterTelemetry> GetPrinterTelemetryAsync()
    {
        if (string.IsNullOrEmpty(_printerUuid)) return LastTelemetry;

        var printer = await GetPrinterAsync(_cts.Token);
        if (printer == null) return LastTelemetry;

        if (printer.Telemetry != null)
        {
            LastTelemetry.HotendTemp = printer.Telemetry.TempNozzle ?? LastTelemetry.HotendTemp;
            LastTelemetry.HotendTarget = printer.Telemetry.TargetNozzle ?? LastTelemetry.HotendTarget;
            LastTelemetry.BedTemp = printer.Telemetry.TempBed ?? LastTelemetry.BedTemp;
            LastTelemetry.BedTarget = printer.Telemetry.TargetBed ?? LastTelemetry.BedTarget;
            LastTelemetry.FeedRate = printer.Telemetry.PrintSpeed ?? LastTelemetry.FeedRate;

            if (printer.Telemetry.ZHeight.HasValue)
                LastTelemetry.Position = LastTelemetry.Position with { Z = printer.Telemetry.ZHeight.Value };
        }

        LastTelemetry.Status = MapState(printer.State);

        if (printer.JobInfo != null)
        {
            LastTelemetry.SDCard.Progress = printer.JobInfo.Progress ?? LastTelemetry.SDCard.Progress;
            LastTelemetry.SDCard.Printing = LastTelemetry.Status == PrinterStatus.Printing;
            IsPrinting = LastTelemetry.SDCard.Printing;

            if (printer.JobInfo.TimePrinting.HasValue)
                LastTelemetry.PrintDuration = TimeSpan.FromSeconds(printer.JobInfo.TimePrinting.Value);
        }

        LastTelemetry.LastResponse = "PrusaConnect telemetry update";
        RaiseTelemetryUpdated();
        return LastTelemetry;
    }

    public Task<List<FileEntry>> GetFilesAsync() =>
        Task.FromResult(new List<FileEntry>());

    /// <summary>
    /// Retrieves cameras registered for this printer in Prusa Connect.
    /// Snapshot URLs are served by webcam.connect.prusa3d.com and are
    /// authenticated via the per-camera token embedded in the query string.
    /// Ref: https://connect.prusa3d.com/docs/camera/
    /// </summary>
    public async Task<IReadOnlyList<PrinterCamera>> GetCamerasAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConnected || string.IsNullOrEmpty(_printerUuid))
            return Array.Empty<PrinterCamera>();

        try
        {
            using var response = await Client.GetAsync(
                $"/app/printers/{_printerUuid}/cameras", cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode) return Array.Empty<PrinterCamera>();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            // Response is either a root array or { "cameras": [...] }
            var root = doc.RootElement;
            JsonElement cameraArray;
            if (root.ValueKind == JsonValueKind.Array)
                cameraArray = root;
            else if (root.TryGetProperty("cameras", out var nested))
                cameraArray = nested;
            else
                return Array.Empty<PrinterCamera>();

            var cameras = new List<PrinterCamera>();
            foreach (var cam in cameraArray.EnumerateArray())
            {
                var fingerprint = cam.TryGetProperty("camera_fingerprint", out var fp) ? fp.GetString() : null;
                var token = cam.TryGetProperty("token", out var tk) ? tk.GetString() : null;
                var name = cam.TryGetProperty("name", out var nm) ? nm.GetString() : null;
                var registered = !cam.TryGetProperty("registered", out var reg) || reg.GetBoolean();

                if (!registered || string.IsNullOrEmpty(token) || string.IsNullOrEmpty(fingerprint))
                    continue;

                cameras.Add(new PrinterCamera
                {
                    Id = fingerprint,
                    DisplayName = name ?? "Camera",
                    SnapshotUrl = $"{WebcamBaseUrl}/c/snapshot?fingerprint={fingerprint}&token={token}",
                    IsEnabled = true
                });
            }

            return cameras;
        }
        catch
        {
            return Array.Empty<PrinterCamera>();
        }
    }

    // ── Unsupported operations (cloud API — no direct printer control) ────

    public Task SetHotendTemp(int targetTemp = 0) =>
        Task.FromException(new NotSupportedException("PrusaConnect cloud API does not support temperature control."));

    public Task SetBedTemp(int targetTemp = 0) =>
        Task.FromException(new NotSupportedException("PrusaConnect cloud API does not support temperature control."));

    public Task Home(bool x = true, bool y = true, bool z = true) =>
        Task.FromException(new NotSupportedException("PrusaConnect cloud API does not support homing commands."));

    public Task RelativeMove(int feedRate, float x = 0.0f, float y = 0.0f, float z = 0.0f, float e = 0.0f) =>
        Task.FromException(new NotSupportedException("PrusaConnect cloud API does not support move commands."));

    public Task SetFanSpeed(int fanSpeedPercentage = 0) =>
        Task.FromException(new NotSupportedException("PrusaConnect cloud API does not support fan control."));

    public Task SetPrintSpeed(int speed) =>
        Task.FromException(new NotSupportedException("PrusaConnect cloud API does not support print speed control."));

    public Task SetPrintFlow(int flow) =>
        Task.FromException(new NotSupportedException("PrusaConnect cloud API does not support flow control."));

    public Task SetAxisPerUnit(float x = 0.0f, float y = 0.0f, float z = 0.0f, float e = 0.0f) =>
        Task.FromException(new NotSupportedException("PrusaConnect cloud API does not support steps-per-unit control."));

    public Task RunPidTuning(int cycles, int targetTemp, int extruderIndex) =>
        Task.FromException(new NotSupportedException("PrusaConnect cloud API does not support PID tuning."));

    public Task RunThermalModelCalibration(int cycles, int targetTemp) =>
        Task.FromException(new NotSupportedException("PrusaConnect cloud API does not support thermal model calibration."));

    public Task StartPrint(FileEntry file) =>
        Task.FromException(new NotSupportedException("PrusaConnect cloud API does not support starting prints remotely."));

    public Task StartPrint(GCodeDoc gcodeDoc) =>
        Task.FromException(new NotSupportedException("PrusaConnect cloud API does not support direct G-code printing."));

    public Task SaveEEPROM() =>
        Task.FromException(new NotSupportedException("PrusaConnect cloud API does not support EEPROM commands."));

    public override ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _httpClient?.Dispose();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    // ── Private helpers ───────────────────────────────────────────────────

    private void ConfigureClient(string? apiKey)
    {
        var client = Client;
        client.BaseAddress ??= new Uri(BaseUrl);
        client.Timeout = TimeSpan.FromSeconds(30);
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        if (!string.IsNullOrEmpty(apiKey))
        {
            client.DefaultRequestHeaders.Remove("x-api-key");
            client.DefaultRequestHeaders.Add("x-api-key", apiKey);
        }
    }

    private async Task<PrusaConnectPrinterResponse?> GetPrinterAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_printerUuid)) return null;

        using var response = await Client.GetAsync($"/app/printers/{_printerUuid}", cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode) return null;

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<PrusaConnectPrinterResponse>(
            stream, s_jsonOptions, cancellationToken);
    }

    private async Task SafeTelemetryAsync()
    {
        try
        {
            await GetPrinterTelemetryAsync();
        }
        catch
        {
            // swallow background polling errors
        }
    }

    private static PrinterStatus MapState(string? state) => state?.ToUpperInvariant() switch
    {
        "PRINTING" => PrinterStatus.Printing,
        "PAUSED" => PrinterStatus.Paused,
        "ERROR" or "ATTENTION" => PrinterStatus.Error,
        "IDLE" or "READY" or "FINISHED" or "STOPPED" => PrinterStatus.Connected,
        _ => PrinterStatus.Disconnected
    };

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };
}

// ── Response models ───────────────────────────────────────────────────────

public sealed class PrusaConnectPrinterResponse
{
    [JsonPropertyName("uuid")]
    public string? Uuid { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("printer_type")]
    public string? PrinterType { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("telemetry")]
    public PrusaConnectTelemetry? Telemetry { get; set; }

    [JsonPropertyName("job_info")]
    public PrusaConnectJobInfo? JobInfo { get; set; }
}

public sealed class PrusaConnectTelemetry
{
    [JsonPropertyName("temp_nozzle")]
    public double? TempNozzle { get; set; }

    [JsonPropertyName("target_nozzle")]
    public double? TargetNozzle { get; set; }

    [JsonPropertyName("temp_bed")]
    public double? TempBed { get; set; }

    [JsonPropertyName("target_bed")]
    public double? TargetBed { get; set; }

    [JsonPropertyName("print_speed")]
    public int? PrintSpeed { get; set; }

    [JsonPropertyName("z_height")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public float? ZHeight { get; set; }
}

public sealed class PrusaConnectJobInfo
{
    [JsonPropertyName("progress")]
    public double? Progress { get; set; }

    [JsonPropertyName("time_remaining")]
    public int? TimeRemaining { get; set; }

    [JsonPropertyName("time_printing")]
    public int? TimePrinting { get; set; }
}
