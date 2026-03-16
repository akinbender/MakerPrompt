namespace MakerPrompt.Shared.Services;

/// <summary>
/// PrusaConnect printer backend using the mobile API.
///
/// Base URL: https://connect-mobile-api.prusa3d.com
/// Auth: Authorization: Bearer {token}
///
/// Connection settings mapping:
///   Api.UserName — Printer UUID (from PrusaConnectProvider or printer detail page)
///   Api.Password — Bearer token (from account login)
///   Api.Url      — ignored; base URL is always <see cref="BaseUrl"/>
///
/// Supports: state + temperature telemetry polling, job progress, G-code commands.
/// Direct motion/temperature/tuning commands are not available via the cloud API.
///
/// Endpoints used:
///   GET  /api/v1/printers/{uuid}
///   GET  /api/v1/printers/{uuid}/telemetry
///   GET  /api/v1/printers/{uuid}/files
///   GET  /api/v1/printers/{uuid}/cameras
///   POST /api/v1/printers/{uuid}/command
/// </summary>
public sealed class PrusaConnectPrinterService : BasePrinterConnectionService, IPrinterCommunicationService
{
    private const string BaseUrl = "https://connect-mobile-api.prusa3d.com";

    private readonly CancellationTokenSource _cts = new();
    private HttpClient? _httpClient;
    private bool _timerInitialized;
    private string? _printerUuid;

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public override PrinterConnectionType ConnectionType => PrinterConnectionType.PrusaConnect;

    public PrusaConnectPrinterService() { }

    public PrusaConnectPrinterService(HttpMessageHandler handler)
    {
        _httpClient = new HttpClient(handler, false) { BaseAddress = new Uri(BaseUrl) };
    }

    private HttpClient Client => _httpClient ??= new HttpClient { BaseAddress = new Uri(BaseUrl) };

    public async Task<bool> ConnectAsync(PrinterConnectionSettings connectionSettings)
    {
        if (connectionSettings.Api is null)
            throw new ArgumentException("PrusaConnect requires API settings.", nameof(connectionSettings));

        _printerUuid = connectionSettings.Api.UserName;
        ConfigureClient(connectionSettings.Api.Password);

        try
        {
            var printer = await FetchPrinterAsync(_cts.Token);
            if (printer is null)
            {
                IsConnected = false;
                RaiseConnectionChanged();
                return false;
            }

            ConnectionName = printer.Name ?? $"PrusaConnect ({_printerUuid})";
            LastTelemetry.PrinterName = ConnectionName;
            LastTelemetry.ConnectionTime = DateTime.UtcNow;

            if (!_timerInitialized)
            {
                updateTimer.Elapsed += async (_, _) => await SafePollAsync();
                _timerInitialized = true;
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

    /// <summary>
    /// Sends a raw G-code command via POST /app/printers/{uuid}/command.
    /// </summary>
    public async Task WriteDataAsync(string command)
    {
        if (string.IsNullOrEmpty(_printerUuid)) return;

        var body = JsonSerializer.Serialize(new { command }, s_jsonOptions);
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await Client
            .PostAsync($"/api/v1/printers/{_printerUuid}/command", content, _cts.Token);
        response.EnsureSuccessStatusCode();
    }

    public async Task<PrinterTelemetry> GetPrinterTelemetryAsync()
    {
        if (string.IsNullOrEmpty(_printerUuid)) return LastTelemetry;

        try
        {
            var telemetry = await FetchTelemetryAsync(_cts.Token);
            if (telemetry is not null)
                ApplyTelemetry(telemetry);

            var printer = await FetchPrinterAsync(_cts.Token);
            if (printer is not null)
                ApplyPrinterState(printer);
        }
        catch
        {
            // swallow background polling errors
        }

        RaiseTelemetryUpdated();
        return LastTelemetry;
    }

    public async Task<List<FileEntry>> GetFilesAsync()
    {
        if (string.IsNullOrEmpty(_printerUuid)) return [];

        try
        {
            using var response = await Client.GetAsync($"/api/v1/printers/{_printerUuid}/files", _cts.Token);
            if (!response.IsSuccessStatusCode) return [];

            await using var stream = await response.Content.ReadAsStreamAsync(_cts.Token);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: _cts.Token);

            var root = doc.RootElement;
            JsonElement fileArray;
            if (root.ValueKind == JsonValueKind.Array)
                fileArray = root;
            else if (root.TryGetProperty("files", out var nested))
                fileArray = nested;
            else
                return [];

            var result = new List<FileEntry>();
            foreach (var item in fileArray.EnumerateArray())
            {
                var path = item.TryGetProperty("path", out var p) ? p.GetString() : null;
                if (string.IsNullOrEmpty(path)) continue;

                var size = item.TryGetProperty("size", out var s) && s.TryGetInt64(out var sv) ? sv : 0L;
                DateTime? modified = null;
                if (item.TryGetProperty("date", out var d) && d.TryGetInt64(out var dv))
                    modified = DateTimeOffset.FromUnixTimeSeconds(dv).DateTime;

                result.Add(new FileEntry { FullPath = path, Size = size, ModifiedDate = modified });
            }

            return result;
        }
        catch
        {
            return [];
        }
    }

    public async Task<IReadOnlyList<PrinterCamera>> GetCamerasAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConnected || string.IsNullOrEmpty(_printerUuid)) return [];

        try
        {
            using var response = await Client.GetAsync(
                $"/api/v1/printers/{_printerUuid}/cameras", cancellationToken);

            if (!response.IsSuccessStatusCode) return [];

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            var root = doc.RootElement;
            JsonElement cameraArray;
            if (root.ValueKind == JsonValueKind.Array)
                cameraArray = root;
            else if (root.TryGetProperty("cameras", out var nested))
                cameraArray = nested;
            else
                return [];

            var cameras = new List<PrinterCamera>();
            foreach (var cam in cameraArray.EnumerateArray())
            {
                if (cam.TryGetProperty("registered", out var reg) && !reg.GetBoolean()) continue;

                var token = cam.TryGetProperty("token", out var tk) ? tk.GetString() : null;
                if (string.IsNullOrEmpty(token)) continue;

                // fingerprint is in config.camera_id per the OpenAPI spec
                string? fingerprint = null;
                if (cam.TryGetProperty("config", out var cfg) &&
                    cfg.TryGetProperty("camera_id", out var fp))
                    fingerprint = fp.GetString();

                if (string.IsNullOrEmpty(fingerprint)) continue;

                var name = cam.TryGetProperty("name", out var nm) ? nm.GetString() : null;

                cameras.Add(new PrinterCamera
                {
                    Id = fingerprint,
                    DisplayName = name ?? "Camera",
                    SnapshotUrl = $"https://webcam.connect.prusa3d.com/c/snapshot?fingerprint={fingerprint}&token={token}",
                    IsEnabled = true,
                });
            }

            return cameras;
        }
        catch
        {
            return [];
        }
    }

    // ── Unsupported cloud operations ──────────────────────────────────────

    public Task SetHotendTemp(int targetTemp = 0) =>
        Task.FromException(new NotSupportedException("PrusaConnect cloud API does not support temperature control."));

    public Task SetBedTemp(int targetTemp = 0) =>
        Task.FromException(new NotSupportedException("PrusaConnect cloud API does not support temperature control."));

    public Task Home(bool x = true, bool y = true, bool z = true) =>
        Task.FromException(new NotSupportedException("PrusaConnect cloud API does not support homing commands."));

    public Task RelativeMove(int feedRate, float x = 0, float y = 0, float z = 0, float e = 0) =>
        Task.FromException(new NotSupportedException("PrusaConnect cloud API does not support move commands."));

    public Task SetFanSpeed(int fanSpeedPercentage = 0) =>
        Task.FromException(new NotSupportedException("PrusaConnect cloud API does not support fan control."));

    public Task SetPrintSpeed(int speed) =>
        Task.FromException(new NotSupportedException("PrusaConnect cloud API does not support print speed control."));

    public Task SetPrintFlow(int flow) =>
        Task.FromException(new NotSupportedException("PrusaConnect cloud API does not support flow control."));

    public Task SetAxisPerUnit(float x = 0, float y = 0, float z = 0, float e = 0) =>
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

    private void ConfigureClient(string? bearerToken)
    {
        var client = Client;
        client.BaseAddress ??= new Uri(BaseUrl);
        client.Timeout = TimeSpan.FromSeconds(30);
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        if (!string.IsNullOrEmpty(bearerToken))
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
    }

    private async Task<PrusaConnectMobilePrinterResponse?> FetchPrinterAsync(CancellationToken ct)
    {
        using var response = await Client.GetAsync($"/api/v1/printers/{_printerUuid}", ct);
        if (!response.IsSuccessStatusCode) return null;

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        return await JsonSerializer.DeserializeAsync<PrusaConnectMobilePrinterResponse>(stream, s_jsonOptions, ct);
    }

    private async Task<PrusaConnectMobileTelemetryResponse?> FetchTelemetryAsync(CancellationToken ct)
    {
        using var response = await Client.GetAsync($"/api/v1/printers/{_printerUuid}/telemetry", ct);
        if (!response.IsSuccessStatusCode) return null;

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        return await JsonSerializer.DeserializeAsync<PrusaConnectMobileTelemetryResponse>(stream, s_jsonOptions, ct);
    }

    private void ApplyTelemetry(PrusaConnectMobileTelemetryResponse t)
    {
        LastTelemetry.HotendTemp   = t.TempNozzle  ?? LastTelemetry.HotendTemp;
        LastTelemetry.HotendTarget = t.TargetNozzle ?? LastTelemetry.HotendTarget;
        LastTelemetry.BedTemp      = t.TempBed      ?? LastTelemetry.BedTemp;
        LastTelemetry.BedTarget    = t.TargetBed    ?? LastTelemetry.BedTarget;
        LastTelemetry.FeedRate     = t.PrintSpeed   ?? LastTelemetry.FeedRate;

        if (t.ZHeight.HasValue)
            LastTelemetry.Position = LastTelemetry.Position with { Z = t.ZHeight.Value };
    }

    private void ApplyPrinterState(PrusaConnectMobilePrinterResponse p)
    {
        LastTelemetry.Status = MapState(p.State);

        if (p.Telemetry is not null)
            ApplyTelemetry(p.Telemetry);

        if (p.JobInfo is not null)
        {
            LastTelemetry.SDCard.Progress = p.JobInfo.Progress ?? LastTelemetry.SDCard.Progress;
            LastTelemetry.SDCard.Printing = LastTelemetry.Status == PrinterStatus.Printing;
            IsPrinting = LastTelemetry.SDCard.Printing;

            if (p.JobInfo.TimePrinting.HasValue)
                LastTelemetry.PrintDuration = TimeSpan.FromSeconds(p.JobInfo.TimePrinting.Value);
        }

        LastTelemetry.LastResponse = "PrusaConnect telemetry update";
    }

    private async Task SafePollAsync()
    {
        try { await GetPrinterTelemetryAsync(); }
        catch { }
    }

    private static PrinterStatus MapState(string? state) => state?.ToUpperInvariant() switch
    {
        "PRINTING"                       => PrinterStatus.Printing,
        "PAUSED"                         => PrinterStatus.Paused,
        "ERROR" or "ATTENTION"           => PrinterStatus.Error,
        "IDLE" or "READY" or "FINISHED"
            or "STOPPED"                 => PrinterStatus.Connected,
        _                                => PrinterStatus.Disconnected,
    };
}

// ── Response models ───────────────────────────────────────────────────────

public sealed class PrusaConnectMobilePrinterResponse
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
    public PrusaConnectMobileTelemetryResponse? Telemetry { get; set; }

    [JsonPropertyName("job_info")]
    public PrusaConnectMobileJobInfo? JobInfo { get; set; }
}

public sealed class PrusaConnectMobileTelemetryResponse
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

public sealed class PrusaConnectMobileJobInfo
{
    [JsonPropertyName("progress")]
    public double? Progress { get; set; }

    [JsonPropertyName("time_remaining")]
    public int? TimeRemaining { get; set; }

    [JsonPropertyName("time_printing")]
    public int? TimePrinting { get; set; }
}
