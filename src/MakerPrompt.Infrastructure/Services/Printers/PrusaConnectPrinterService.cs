namespace MakerPrompt.Infrastructure.Services.Printers;

/// <summary>
/// PrusaConnect printer backend using the mobile API.
///
/// Base URL: https://connect-mobile-api.prusa3d.com
/// Auth: Authorization: Bearer {token}
///
/// Connection settings mapping:
///   ProviderId — Printer UUID (from PrusaConnectProvider or printer detail page)
///   Password   — Bearer token (from account login)
///   ApiUrl     — ignored; base URL is always <see cref="BaseUrl"/>
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

    private CancellationTokenSource _cts = new();
    private HttpClient? _httpClient;
    private bool _timerInitialized;
    private string? _printerUuid;
    private readonly ILogger<PrusaConnectPrinterService>? _logger;

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public override PrinterConnectionType ConnectionType => PrinterConnectionType.PrusaConnect;
    public bool SupportsDirectControl => false;
    public bool SupportsPrintStart => false;

    public PrusaConnectPrinterService(ILogger<PrusaConnectPrinterService>? logger = null)
    {
        _logger = logger;
    }

    public PrusaConnectPrinterService(
        HttpMessageHandler handler, ILogger<PrusaConnectPrinterService>? logger = null)
    {
        _logger = logger;
        _httpClient = new HttpClient(handler, false)
        {
            BaseAddress = new Uri(BaseUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    private HttpClient Client => _httpClient ??= new HttpClient
    {
        BaseAddress = new Uri(BaseUrl),
        Timeout = TimeSpan.FromSeconds(30)
    };

    public async Task<bool> ConnectAsync(PrinterConnectionSettings connectionSettings, CancellationToken cancellationToken = default)
    {
        var printerUuid = connectionSettings.ProviderId ?? connectionSettings.UserName;
        if (string.IsNullOrWhiteSpace(printerUuid))
            throw new ArgumentException("PrusaConnect requires a provider printer ID.", nameof(connectionSettings));

        if (IsConnected && string.Equals(_printerUuid, printerUuid, StringComparison.Ordinal))
            return true;

        if (IsConnected)
            await DisconnectAsync(cancellationToken).ConfigureAwait(false);

        ResetConnectionCancellation();
        using var linkedCts =
            CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);
        _printerUuid = printerUuid;
        ConfigureClient(connectionSettings.Password);

        try
        {
            var printer = await FetchPrinterAsync(linkedCts.Token).ConfigureAwait(false);
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

            IsConnected = true;
            ApplyPrinterState(printer);
            updateTimer.Start();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            updateTimer.Stop();
            IsConnected = false;
            _logger?.LogWarning(ex, "Could not connect to PrusaConnect printer {PrinterId}", _printerUuid);
        }

        RaiseConnectionChanged();
        return IsConnected;
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        updateTimer.Stop();
        _cts.Cancel();
        _httpClient?.CancelPendingRequests();
        IsConnected = false;
        IsPrinting = false;
        LastTelemetry.Status = PrinterStatus.Disconnected;
        RaiseConnectionChanged();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Sends a raw G-code command via POST /app/printers/{uuid}/command.
    /// </summary>
    public async Task WriteDataAsync(string command, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || string.IsNullOrEmpty(_printerUuid)) return;

        using var linkedCts =
            CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);
        var body = JsonSerializer.Serialize(new { command }, s_jsonOptions);
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await Client
            .PostAsync($"/api/v1/printers/{_printerUuid}/command", content, linkedCts.Token)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task<PrinterTelemetry> GetTelemetryAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConnected || string.IsNullOrEmpty(_printerUuid)) return LastTelemetry;

        using var linkedCts =
            CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);
        try
        {
            var telemetry = await FetchTelemetryAsync(linkedCts.Token).ConfigureAwait(false);
            if (telemetry is not null)
                ApplyTelemetry(telemetry);

            var printer = await FetchPrinterAsync(linkedCts.Token).ConfigureAwait(false);
            if (printer is not null)
                ApplyPrinterState(printer);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested)
        {
            return LastTelemetry;
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "PrusaConnect telemetry poll failed for {PrinterId}", _printerUuid);
        }

        RaiseTelemetryUpdated();
        return LastTelemetry;
    }

    public async Task<IReadOnlyList<FileEntry>> GetFilesAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConnected || string.IsNullOrEmpty(_printerUuid)) return [];

        using var linkedCts =
            CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);
        try
        {
            using var response = await Client
                .GetAsync($"/api/v1/printers/{_printerUuid}/files", linkedCts.Token)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return [];

            await using var stream = await response.Content
                .ReadAsStreamAsync(linkedCts.Token)
                .ConfigureAwait(false);
            using var doc = await JsonDocument
                .ParseAsync(stream, cancellationToken: linkedCts.Token)
                .ConfigureAwait(false);

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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "PrusaConnect file listing failed for {PrinterId}", _printerUuid);
            return [];
        }
    }

    public async Task<IReadOnlyList<PrinterCamera>> GetCamerasAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConnected || string.IsNullOrEmpty(_printerUuid)) return [];

        using var linkedCts =
            CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);
        try
        {
            using var response = await Client.GetAsync(
                $"/api/v1/printers/{_printerUuid}/cameras", linkedCts.Token)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode) return [];

            await using var stream = await response.Content
                .ReadAsStreamAsync(linkedCts.Token)
                .ConfigureAwait(false);
            using var doc = await JsonDocument
                .ParseAsync(stream, cancellationToken: linkedCts.Token)
                .ConfigureAwait(false);

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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "PrusaConnect camera listing failed for {PrinterId}", _printerUuid);
            return [];
        }
    }

    // ── Unsupported cloud operations ──────────────────────────────────────

    public Task SetHotendTempAsync(int targetTemp = 0, CancellationToken cancellationToken = default) =>
        Task.FromException(new NotSupportedException("PrusaConnect cloud API does not support temperature control."));

    public Task SetBedTempAsync(int targetTemp = 0, CancellationToken cancellationToken = default) =>
        Task.FromException(new NotSupportedException("PrusaConnect cloud API does not support temperature control."));

    public Task HomeAsync(bool x = true, bool y = true, bool z = true, CancellationToken cancellationToken = default) =>
        Task.FromException(new NotSupportedException("PrusaConnect cloud API does not support homing commands."));

    public Task RelativeMoveAsync(int feedRate, float x = 0, float y = 0, float z = 0, float e = 0, CancellationToken cancellationToken = default) =>
        Task.FromException(new NotSupportedException("PrusaConnect cloud API does not support move commands."));

    public Task SetFanSpeedAsync(int fanSpeedPercentage = 0, CancellationToken cancellationToken = default) =>
        Task.FromException(new NotSupportedException("PrusaConnect cloud API does not support fan control."));

    public Task SetPrintSpeedAsync(int speed, CancellationToken cancellationToken = default) =>
        Task.FromException(new NotSupportedException("PrusaConnect cloud API does not support print speed control."));

    public Task SetPrintFlowAsync(int flow, CancellationToken cancellationToken = default) =>
        Task.FromException(new NotSupportedException("PrusaConnect cloud API does not support flow control."));

    public Task SetAxisPerUnitAsync(
        float x = 0, float y = 0, float z = 0, float e = 0,
        CancellationToken cancellationToken = default) =>
        Task.FromException(new NotSupportedException("PrusaConnect cloud API does not support steps-per-unit control."));

    public Task RunPidTuningAsync(
        int cycles, int targetTemp, int extruderIndex,
        CancellationToken cancellationToken = default) =>
        Task.FromException(new NotSupportedException("PrusaConnect cloud API does not support PID tuning."));

    public Task RunThermalModelCalibrationAsync(
        int cycles, int targetTemp, CancellationToken cancellationToken = default) =>
        Task.FromException(new NotSupportedException("PrusaConnect cloud API does not support thermal model calibration."));

    public Task StartPrintAsync(string fileName, CancellationToken cancellationToken = default) =>
        Task.FromException(new NotSupportedException("PrusaConnect cloud API does not support starting prints remotely."));

    public Task StartPrintAsync(GCodeDoc gcodeDoc, CancellationToken cancellationToken = default) =>
        Task.FromException(new NotSupportedException("PrusaConnect cloud API does not support direct G-code printing."));

    public Task SaveEepromAsync(CancellationToken cancellationToken = default) =>
        Task.FromException(new NotSupportedException("PrusaConnect cloud API does not support EEPROM commands."));

    public override ValueTask DisposeAsync()
    {
        updateTimer.Stop();
        updateTimer.Dispose();
        _cts.Cancel();
        _cts.Dispose();
        _httpClient?.Dispose();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    // ── Private helpers ───────────────────────────────────────────────────

    private void ConfigureClient(string? bearerToken)
    {
        var client = Client;
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        client.DefaultRequestHeaders.Authorization = string.IsNullOrWhiteSpace(bearerToken)
            ? null
            : new AuthenticationHeaderValue("Bearer", bearerToken);
    }

    private async Task<PrusaConnectMobilePrinterResponse?> FetchPrinterAsync(CancellationToken ct)
    {
        using var response = await Client.GetAsync($"/api/v1/printers/{_printerUuid}", ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger?.LogWarning(
                "PrusaConnect printer endpoint returned {Status} for {PrinterId}",
                response.StatusCode, _printerUuid);
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var result = await JsonSerializer.DeserializeAsync<PrusaConnectMobilePrinterResponse>(
            stream, s_jsonOptions, ct);
        if (result is not null)
        {
            if (result.EffectiveState is null)
                _logger?.LogWarning(
                    "PrusaConnect response for {PrinterId} omitted printer state", _printerUuid);
            if (result.ExtraFields?.Count > 0)
                _logger?.LogDebug(
                    "PrusaConnect printer response has unmapped fields: {Fields}",
                    string.Join(", ", result.ExtraFields.Keys));
        }

        return result;
    }

    private async Task<PrusaConnectMobileTelemetryResponse?> FetchTelemetryAsync(CancellationToken ct)
    {
        using var response = await Client.GetAsync($"/api/v1/printers/{_printerUuid}/telemetry", ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger?.LogDebug(
                "PrusaConnect telemetry endpoint returned {Status} for {PrinterId}",
                response.StatusCode, _printerUuid);
            return null;
        }

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

        if (t.EffectiveZHeight.HasValue)
            LastTelemetry.Position = LastTelemetry.Position with { Z = t.EffectiveZHeight.Value };

        if (t.ExtraFields?.Count > 0)
            _logger?.LogDebug(
                "PrusaConnect telemetry has unmapped fields: {Fields}",
                string.Join(", ", t.ExtraFields.Keys));
    }

    private void ApplyPrinterState(PrusaConnectMobilePrinterResponse p)
    {
        LastTelemetry.Status = MapState(p.EffectiveState);

        if (p.Telemetry is not null)
            ApplyTelemetry(p.Telemetry);

        var job = p.EffectiveJobInfo;
        if (job is not null)
        {
            LastTelemetry.SDCard.Progress = job.Progress ?? LastTelemetry.SDCard.Progress;
            LastTelemetry.SDCard.Printing = LastTelemetry.Status == PrinterStatus.Printing;
            IsPrinting = LastTelemetry.SDCard.Printing;

            if (job.TimePrinting.HasValue)
                LastTelemetry.PrintDuration = TimeSpan.FromSeconds(job.TimePrinting.Value);

            if (!string.IsNullOrWhiteSpace(job.FileName))
                LastTelemetry.PrintJobName = job.FileName;
        }
        else
        {
            LastTelemetry.SDCard.Printing = false;
            IsPrinting = false;
        }

        LastTelemetry.LastResponse = "PrusaConnect telemetry update";
    }

    private async Task SafePollAsync()
    {
        try { await GetTelemetryAsync(); }
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

    private void ResetConnectionCancellation()
    {
        if (!_cts.IsCancellationRequested) return;

        _cts.Dispose();
        _cts = new CancellationTokenSource();
    }
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

    [JsonPropertyName("printer_state")]
    public PrusaConnectMobilePrinterState? PrinterState { get; set; }

    [JsonPropertyName("telemetry")]
    public PrusaConnectMobileTelemetryResponse? Telemetry { get; set; }

    [JsonPropertyName("job_info")]
    public PrusaConnectMobileJobInfo? JobInfo { get; set; }

    [JsonPropertyName("job")]
    public PrusaConnectMobileJobInfo? Job { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtraFields { get; set; }

    [JsonIgnore]
    public string? EffectiveState => State ?? PrinterState?.EffectiveState;

    [JsonIgnore]
    public PrusaConnectMobileJobInfo? EffectiveJobInfo => JobInfo ?? Job;
}

public sealed class PrusaConnectMobilePrinterState
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonIgnore]
    public string? EffectiveState => Text ?? State;
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

    [JsonPropertyName("axis_z")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public float? AxisZ { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtraFields { get; set; }

    [JsonIgnore]
    public float? EffectiveZHeight => ZHeight ?? AxisZ;
}

public sealed class PrusaConnectMobileJobInfo
{
    [JsonPropertyName("progress")]
    public double? Progress { get; set; }

    [JsonPropertyName("time_remaining")]
    public int? TimeRemaining { get; set; }

    [JsonPropertyName("time_printing")]
    public int? TimePrinting { get; set; }

    [JsonPropertyName("file_name")]
    public string? FileName { get; set; }
}
