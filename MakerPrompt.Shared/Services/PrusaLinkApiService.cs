using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MakerPrompt.Shared.Infrastructure;
using MakerPrompt.Shared.Models;
using MakerPrompt.Shared.Utils;
using static MakerPrompt.Shared.Utils.Enums;

namespace MakerPrompt.Shared.Services;

public class PrusaLinkApiService : BasePrinterConnectionService, IPrinterCommunicationService
{
    private readonly CancellationTokenSource _cts = new();
    private readonly HttpMessageHandler? _customHandler;
    private HttpClient? _httpClient;
    private bool _ownsClient;
    private ApiConnectionSettings? _connectionSettings;
    private Uri? _baseUri;

    public override PrinterConnectionType ConnectionType => PrinterConnectionType.PrusaLink;

    public PrusaLinkApiService()
    {
    }

    public PrusaLinkApiService(HttpMessageHandler handler)
    {
        _customHandler = handler;
        _httpClient = new HttpClient(handler, false);
        _ownsClient = true;
    }

    private HttpClient Client
    {
        get
        {
            _httpClient ??= new HttpClient();
            return _httpClient;
        }
    }

    public async Task<bool> ConnectAsync(PrinterConnectionSettings connectionSettings)
    {
        if (connectionSettings.Api is null)
        {
            throw new ArgumentException("PrusaLink connection requires API settings.", nameof(connectionSettings));
        }

        _connectionSettings = connectionSettings.Api;
        ConfigureClient(_connectionSettings);

        try
        {
            var version = await GetVersionAsync(_cts.Token);
            if (version == null)
            {
                IsConnected = false;
                RaiseConnectionChanged();
                return IsConnected;
            }

            var info = await GetInfoAsync(_cts.Token);
            ConnectionName = _baseUri?.AbsoluteUri ?? _connectionSettings.Url;
            LastTelemetry.PrinterName = info?.Name ?? LastTelemetry.PrinterName;
            LastTelemetry.ConnectionTime ??= DateTime.UtcNow;

            updateTimer.Elapsed += async (_, _) => await SafeTelemetryAsync();
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

    private async Task SafeTelemetryAsync()
    {
        try
        {
            await GetPrinterTelemetryAsync();
        }
        catch
        {
            // swallow background polling errors to avoid breaking UI
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

    public Task WriteDataAsync(string command)
    {
        throw new NotSupportedException("Direct G-code injection is not supported by the PrusaLink API.");
    }

    public async Task<PrinterTelemetry> GetPrinterTelemetryAsync()
    {
        var status = await GetStatusAsync(_cts.Token);
        if (status?.Printer is null)
        {
            return LastTelemetry;
        }

        LastTelemetry.HotendTemp = status.Printer.TempNozzle ?? LastTelemetry.HotendTemp;
        LastTelemetry.HotendTarget = status.Printer.TargetNozzle ?? LastTelemetry.HotendTarget;
        LastTelemetry.BedTemp = status.Printer.TempBed ?? LastTelemetry.BedTemp;
        LastTelemetry.BedTarget = status.Printer.TargetBed ?? LastTelemetry.BedTarget;
        LastTelemetry.Position = new Vector3(
            status.Printer.AxisX ?? LastTelemetry.Position.X,
            status.Printer.AxisY ?? LastTelemetry.Position.Y,
            status.Printer.AxisZ ?? LastTelemetry.Position.Z);
        LastTelemetry.Status = MapPrusaStatus(status.Printer.State);
        LastTelemetry.FanSpeed = status.Printer.FanPrint ?? LastTelemetry.FanSpeed;
        LastTelemetry.FeedRate = status.Printer.Speed ?? LastTelemetry.FeedRate;
        LastTelemetry.FlowRate = status.Printer.Flow ?? LastTelemetry.FlowRate;

        if (status.Job != null)
        {
            LastTelemetry.SDCard.Printing = status.Job.State?.Equals("PRINTING", StringComparison.OrdinalIgnoreCase) == true;
        }

        if (status.Storage != null)
        {
            LastTelemetry.SDCard.Present = !status.Storage.ReadOnly;
        }

        LastTelemetry.LastResponse = "PrusaLink status update";
        RaiseTelemetryUpdated();
        return LastTelemetry;
    }

    public async Task<List<FileEntry>> GetFilesAsync()
    {
        var storages = await GetStorageAsync(_cts.Token);
        var firstStorage = storages?.StorageList?.FirstOrDefault();
        if (firstStorage == null)
        {
            return [];
        }

        var storageKey = firstStorage.Path.Trim('/');
        var folder = await GetFolderAsync(storageKey, "/", _cts.Token);
        if (folder?.Children == null)
        {
            return [];
        }

        return folder.Children.Select(child => new FileEntry
        {
            FullPath = $"{firstStorage.Path}{child.Path ?? string.Empty}",
            Size = child.Size ?? 0,
            ModifiedDate = child.Timestamp.HasValue
                ? DateTimeOffset.FromUnixTimeSeconds(child.Timestamp.Value).DateTime
                : null,
            IsAvailable = !child.ReadOnly
        }).ToList();
    }

    public Task SetHotendTemp(int targetTemp = 0) =>
        Task.FromException(new NotSupportedException("PrusaLink API does not expose hotend temperature control."));

    public Task SetBedTemp(int targetTemp = 0) =>
        Task.FromException(new NotSupportedException("PrusaLink API does not expose bed temperature control."));

    public Task Home(bool x = true, bool y = true, bool z = true) =>
        Task.FromException(new NotSupportedException("PrusaLink API does not expose homing commands."));

    public Task RelativeMove(int feedRate, float x = 0.0f, float y = 0.0f, float z = 0.0f, float e = 0.0f) =>
        Task.FromException(new NotSupportedException("PrusaLink API does not expose move commands."));

    public Task SetFanSpeed(int fanSpeedPercentage = 0) =>
        Task.FromException(new NotSupportedException("PrusaLink API does not expose fan control."));

    public Task SetPrintSpeed(int speed) =>
        Task.FromException(new NotSupportedException("PrusaLink API does not expose print speed control."));

    public Task SetPrintFlow(int flow) =>
        Task.FromException(new NotSupportedException("PrusaLink API does not expose flow control."));

    public Task SetAxisPerUnit(float x = 0.0f, float y = 0.0f, float z = 0.0f, float e = 0.0f) =>
        Task.FromException(new NotSupportedException("PrusaLink API does not expose steps-per-unit control."));

    public Task RunPidTuning(int cycles, int targetTemp, int extruderIndex) =>
        Task.FromException(new NotSupportedException("PrusaLink API does not expose PID tuning commands."));

    public Task RunThermalModelCalibration(int cycles, int targetTemp) =>
        Task.FromException(new NotSupportedException("PrusaLink API does not expose thermal model calibration."));

    public Task StartPrint(FileEntry file) =>
        Task.FromException(new NotSupportedException("Starting prints requires uploading with Print-After-Upload per PrusaLink spec."));

    public Task StartPrint(GCodeDoc gcodeDoc) =>
        Task.FromException(new NotSupportedException("Direct G-code printing is not supported by the PrusaLink API."));

    public Task SaveEEPROM() =>
        Task.FromException(new NotSupportedException("PrusaLink API does not expose EEPROM save commands."));

    public override ValueTask DisposeAsync()
    {
        _cts.Cancel();
        if (_ownsClient)
        {
            _httpClient?.Dispose();
        }

        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// PrusaLink / Prusa Connect cameras require additional registration and authentication
    /// not currently configured in MakerPrompt. For now this returns an empty set so that
    /// webcam UI remains disabled for this backend until full camera support is wired up.
    /// </summary>
    public Task<IReadOnlyList<PrinterCamera>> GetCamerasAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult((IReadOnlyList<PrinterCamera>)Array.Empty<PrinterCamera>());
    }

    private void ConfigureClient(ApiConnectionSettings settings)
    {
        _baseUri = new Uri(settings.Url);
        // In Blazor WebAssembly (browser) we cannot use HttpClientHandler.Credentials or
        // most handler-specific features. Instead, rely on the platform HttpClient and
        // send Basic auth via headers when credentials are supplied.
        if (OperatingSystem.IsBrowser())
        {
            if (_customHandler != null)
            {
                _httpClient ??= new HttpClient(_customHandler, false);
                _ownsClient = false;
            }
            else
            {
                _httpClient ??= new HttpClient();
                _ownsClient = true;
            }
        }
        else
        {
            if (_customHandler != null)
            {
                _httpClient?.Dispose();
                _httpClient = new HttpClient(_customHandler, false);
                _ownsClient = false;
            }
            else
            {
                _httpClient?.Dispose();
                var handler = new HttpClientHandler();
                if (!string.IsNullOrEmpty(settings.UserName) || !string.IsNullOrEmpty(settings.Password))
                {
#pragma warning disable CA1416
                    handler.Credentials = new NetworkCredential(settings.UserName, settings.Password);
                    handler.PreAuthenticate = true;
#pragma warning restore CA1416
                }

                _httpClient = new HttpClient(handler);
                _ownsClient = true;
            }
        }

        Client.BaseAddress = _baseUri;
        Client.Timeout = TimeSpan.FromSeconds(30);
        Client.DefaultRequestHeaders.Accept.Clear();
        Client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        if (!string.IsNullOrEmpty(settings.UserName) || !string.IsNullOrEmpty(settings.Password))
        {
            var credentialBytes = Encoding.ASCII.GetBytes($"{settings.UserName}:{settings.Password}");
            Client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", Convert.ToBase64String(credentialBytes));
        }
    }

    private async Task<PrusaVersionResponse?> GetVersionAsync(CancellationToken cancellationToken) =>
        await GetAsync<PrusaVersionResponse>("/api/version", cancellationToken);

    private async Task<PrusaInfoResponse?> GetInfoAsync(CancellationToken cancellationToken) =>
        await GetAsync<PrusaInfoResponse>("/api/v1/info", cancellationToken);

    private async Task<PrusaStatusResponse?> GetStatusAsync(CancellationToken cancellationToken) =>
        await GetAsync<PrusaStatusResponse>("/api/v1/status", cancellationToken);

    private async Task<PrusaStorageListResponse?> GetStorageAsync(CancellationToken cancellationToken) =>
        await GetAsync<PrusaStorageListResponse>("/api/v1/storage", cancellationToken);

    private async Task<PrusaFileSystemEntry?> GetFolderAsync(string storage, string path, CancellationToken cancellationToken)
    {
        var sanitizedStorage = storage.Trim('/');
        var encodedPath = Uri.EscapeDataString(path.TrimStart('/'));
        var requestPath = $"/api/v1/files/{sanitizedStorage}/{encodedPath}";
        return await GetAsync<PrusaFileSystemEntry>(requestPath, cancellationToken);
    }

    private async Task<T?> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        var response = await SendWithRetryAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return default;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<T>(stream, cancellationToken: cancellationToken);
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        const int maxAttempts = 3;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var response = await Client.SendAsync(request, cancellationToken);
                if (response.IsSuccessStatusCode || attempt == maxAttempts || (int)response.StatusCode < 500)
                {
                    return response;
                }
            }
            catch when (attempt < maxAttempts)
            {
                // transient failure, retry below
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt), cancellationToken);
        }

        return await Client.SendAsync(request, cancellationToken);
    }

    private static PrinterStatus MapPrusaStatus(string? status) => status?.ToUpperInvariant() switch
    {
        "PRINTING" => PrinterStatus.Printing,
        "PAUSED" => PrinterStatus.Paused,
        "ERROR" => PrinterStatus.Error,
        "READY" or "IDLE" => PrinterStatus.Connected,
        _ => PrinterStatus.Disconnected
    };
}

public sealed class PrusaVersionResponse
{
    [JsonPropertyName("api")]
    public string Api { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("printer")]
    public string Printer { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("firmware")]
    public string Firmware { get; set; } = string.Empty;
}

public sealed class PrusaInfoResponse
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("hostname")]
    public string? Hostname { get; set; }
}

public sealed class PrusaStatusResponse
{
    [JsonPropertyName("printer")]
    public PrusaStatusPrinter? Printer { get; set; }

    [JsonPropertyName("job")]
    public PrusaStatusJob? Job { get; set; }

    [JsonPropertyName("storage")]
    public PrusaStatusStorage? Storage { get; set; }
}

public sealed class PrusaStatusPrinter
{
    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("temp_nozzle")]
    public double? TempNozzle { get; set; }

    [JsonPropertyName("target_nozzle")]
    public double? TargetNozzle { get; set; }

    [JsonPropertyName("temp_bed")]
    public double? TempBed { get; set; }

    [JsonPropertyName("target_bed")]
    public double? TargetBed { get; set; }

    [JsonPropertyName("axis_x")]
    public float? AxisX { get; set; }

    [JsonPropertyName("axis_y")]
    public float? AxisY { get; set; }

    [JsonPropertyName("axis_z")]
    public float? AxisZ { get; set; }

    [JsonPropertyName("flow")]
    public int? Flow { get; set; }

    [JsonPropertyName("speed")]
    public int? Speed { get; set; }

    [JsonPropertyName("fan_print")]
    public int? FanPrint { get; set; }
}

public sealed class PrusaStatusJob
{
    [JsonPropertyName("id")]
    public int? Id { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("progress")]
    public double? Progress { get; set; }

    [JsonPropertyName("time_remaining")]
    public int? TimeRemaining { get; set; }

    [JsonPropertyName("time_printing")]
    public int? TimePrinting { get; set; }
}

public sealed class PrusaStatusStorage
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("read_only")]
    public bool ReadOnly { get; set; }

    [JsonPropertyName("free_space")]
    public long? FreeSpace { get; set; }
}

public sealed class PrusaStorageListResponse
{
    [JsonPropertyName("storage_list")]
    public List<PrusaStorageItem>? StorageList { get; set; }
}

public sealed class PrusaStorageItem
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;
}

public sealed class PrusaFileSystemEntry
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("path")]
    public string? Path { get; set; }

    [JsonPropertyName("read_only")]
    public bool ReadOnly { get; set; }

    [JsonPropertyName("size")]
    public long? Size { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("m_timestamp")]
    public long? Timestamp { get; set; }

    [JsonPropertyName("children")]
    public List<PrusaFileSystemEntry>? Children { get; set; }
}
