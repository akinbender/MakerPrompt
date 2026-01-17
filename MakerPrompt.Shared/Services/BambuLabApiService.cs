using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using MakerPrompt.Shared.Infrastructure;
using MakerPrompt.Shared.Models;
using MakerPrompt.Shared.Utils;

namespace MakerPrompt.Shared.Services;

public sealed class BambuLabApiService : BasePrinterConnectionService, IPrinterCommunicationService
{
    private readonly CancellationTokenSource _cts = new();
    private readonly HttpMessageHandler? _customHandler;
    private HttpClient? _httpClient;
    private Uri? _httpBaseUri;

    private ClientWebSocket? _ws;
    private Task? _receiveLoopTask;
    private readonly object _syncRoot = new();
    private bool _disposed;
    private bool _telemetryTimerInitialized;

    public override PrinterConnectionType ConnectionType { get; } = PrinterConnectionType.BambuLab;

    public BambuLabApiService()
    {
    }

    public BambuLabApiService(HttpMessageHandler handler)
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
        if (connectionSettings.ConnectionType != ConnectionType)
        {
            throw new ArgumentException("BambuLab connection type mismatch.", nameof(connectionSettings));
        }

        if (connectionSettings.Api is null)
        {
            return false;
        }

        _httpBaseUri = new Uri(connectionSettings.Api.Url);
        ConfigureClient(connectionSettings.Api);

        var accessCode = connectionSettings.Api.Password;
        var serial = connectionSettings.Api.UserName;

        if (string.IsNullOrWhiteSpace(accessCode) || string.IsNullOrWhiteSpace(serial))
        {
            IsConnected = false;
            RaiseConnectionChanged();
            return false;
        }

        ConnectionName = _httpBaseUri.AbsoluteUri;

        var mqttOk = await ConnectMqttAsync(_httpBaseUri, accessCode, serial, _cts.Token).ConfigureAwait(false);

        try
        {
            using var response = await Client.GetAsync("/api/v1/system/status", _cts.Token).ConfigureAwait(false);
            IsConnected = response.IsSuccessStatusCode || mqttOk;
        }
        catch
        {
            IsConnected = mqttOk;
        }

        if (IsConnected && !_telemetryTimerInitialized)
        {
            updateTimer.Elapsed += async (_, _) => await SafeTelemetryAsync().ConfigureAwait(false);
            _telemetryTimerInitialized = true;
            updateTimer.Start();
        }

        RaiseConnectionChanged();
        return IsConnected;
    }

    private async Task<bool> ConnectMqttAsync(Uri baseUri, string accessCode, string serial, CancellationToken cancellationToken)
    {
        try
        {
            lock (_syncRoot)
            {
                _ws?.Dispose();
                _ws = new ClientWebSocket();
            }

            var builder = new UriBuilder(baseUri)
            {
                Scheme = baseUri.Scheme == Uri.UriSchemeHttps ? "wss" : "ws",
                Path = "/ws/mqtt"
            };

            var wsUri = builder.Uri;

            await _ws!.ConnectAsync(wsUri, cancellationToken).ConfigureAwait(false);

            var clientId = $"makerprompt_{serial}";
            var authPayload = JsonSerializer.Serialize(new
            {
                client_id = clientId,
                user = serial,
                password = accessCode
            });

            await SendRawAsync(authPayload, cancellationToken).ConfigureAwait(false);

            var subscribePayload = JsonSerializer.Serialize(new
            {
                type = "subscribe",
                topics = new[]
                {
                    $"device/{serial}/report",
                    $"device/{serial}/history"
                }
            });

            await SendRawAsync(subscribePayload, cancellationToken).ConfigureAwait(false);

            _receiveLoopTask = Task.Run(() => ReceiveLoopAsync(_cts.Token), _cts.Token);

            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task SendRawAsync(string payload, CancellationToken cancellationToken)
    {
        ClientWebSocket? ws;
        lock (_syncRoot)
        {
            ws = _ws;
        }

        if (ws is null || ws.State != WebSocketState.Open) return;

        var bytes = Encoding.UTF8.GetBytes(payload);
        await ws.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[16 * 1024];

        while (!cancellationToken.IsCancellationRequested)
        {
            WebSocketReceiveResult? result = null;
            var ms = new MemoryStream();

            try
            {
                ClientWebSocket? ws;
                lock (_syncRoot)
                {
                    ws = _ws;
                }

                if (ws is null || ws.State != WebSocketState.Open)
                {
                    break;
                }

                do
                {
                    result = await ws.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }

                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                if (result is null || result.MessageType != WebSocketMessageType.Text)
                {
                    continue;
                }

                ms.Position = 0;
                using var reader = new StreamReader(ms, Encoding.UTF8, leaveOpen: false);
                var json = await reader.ReadToEndAsync().ConfigureAwait(false);
                HandleTelemetryMessage(json);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // swallow receive loop errors; connection state will be updated externally
            }
            finally
            {
                ms.Dispose();
            }
        }
    }

    private void HandleTelemetryMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("print", out var print))
            {
                if (print.TryGetProperty("stg", out var stateElement) && stateElement.ValueKind == JsonValueKind.String)
                {
                    var s = stateElement.GetString();
                    LastTelemetry.Status = s?.ToLowerInvariant() switch
                    {
                        "printing" => PrinterStatus.Printing,
                        "paused" => PrinterStatus.Paused,
                        "finish" or "idle" => PrinterStatus.Connected,
                        "error" => PrinterStatus.Error,
                        _ => LastTelemetry.Status
                    };
                }

                if (print.TryGetProperty("bed_temp", out var bedTemp))
                {
                    LastTelemetry.BedTemp = bedTemp.GetDouble();
                }

                if (print.TryGetProperty("bed_target_temp", out var bedTarget))
                {
                    LastTelemetry.BedTarget = bedTarget.GetDouble();
                }

                if (print.TryGetProperty("nozzle_temp", out var nozzleTemp))
                {
                    LastTelemetry.HotendTemp = nozzleTemp.GetDouble();
                }

                if (print.TryGetProperty("nozzle_target_temp", out var nozzleTarget))
                {
                    LastTelemetry.HotendTarget = nozzleTarget.GetDouble();
                }

                if (print.TryGetProperty("fan_speed", out var fanSpeed))
                {
                    LastTelemetry.FanSpeed = fanSpeed.GetInt32();
                }
            }

            LastTelemetry.LastResponse = "BambuLab telemetry update";
            RaiseTelemetryUpdated();
        }
        catch
        {
            // ignore malformed telemetry
        }
    }

    private async Task SafeTelemetryAsync()
    {
        try
        {
            await GetPrinterTelemetryAsync().ConfigureAwait(false);
        }
        catch
        {
        }
    }

    public async Task DisconnectAsync()
    {
        updateTimer.Stop();
        _cts.Cancel();

        lock (_syncRoot)
        {
            _ws?.Abort();
            _ws?.Dispose();
            _ws = null;
        }

        try
        {
            if (_receiveLoopTask is not null)
            {
                await _receiveLoopTask.ConfigureAwait(false);
            }
        }
        catch
        {
        }

        _httpClient?.CancelPendingRequests();
        IsConnected = false;
        RaiseConnectionChanged();
    }

    public Task WriteDataAsync(string command)
    {
        return Task.CompletedTask;
    }

    public async Task<PrinterTelemetry> GetPrinterTelemetryAsync()
    {
        if (!IsConnected || _httpBaseUri is null)
        {
            return LastTelemetry;
        }

        try
        {
            using var response = await Client.GetAsync("/api/v1/printer/print", _cts.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return LastTelemetry;
            }

            var json = await response.Content.ReadAsStringAsync(_cts.Token).ConfigureAwait(false);
            HandleTelemetryMessage(json);
        }
        catch
        {
        }

        return LastTelemetry;
    }

    public Task<List<FileEntry>> GetFilesAsync()
    {
        return Task.FromResult(new List<FileEntry>());
    }

    private Task SendCommandAsync(string name, object payload)
    {
        var envelope = JsonSerializer.Serialize(new
        {
            cmd = name,
            param = payload
        });

        return SendRawAsync(envelope, _cts.Token);
    }

    public Task SetHotendTemp(int targetTemp = 0) =>
        SendCommandAsync("set_nozzle_temp", new { target = targetTemp });

    public Task SetBedTemp(int targetTemp = 0) =>
        SendCommandAsync("set_bed_temp", new { target = targetTemp });

    public Task Home(bool x = true, bool y = true, bool z = true) =>
        SendCommandAsync("home", new { x, y, z });

    public Task RelativeMove(int feedRate, float x = 0.0f, float y = 0.0f, float z = 0.0f, float e = 0.0f) =>
        SendCommandAsync("move_relative", new { feedRate, x, y, z, e });

    public Task SetFanSpeed(int fanSpeedPercentage = 0) =>
        SendCommandAsync("set_fan_speed", new { speed = fanSpeedPercentage });

    public Task SetPrintSpeed(int speed) =>
        SendCommandAsync("set_print_speed", new { speed });

    public Task SetPrintFlow(int flow) =>
        SendCommandAsync("set_print_flow", new { flow });

    public Task SetAxisPerUnit(float x = 0.0f, float y = 0.0f, float z = 0.0f, float e = 0.0f) =>
        SendCommandAsync("set_axis_per_unit", new { x, y, z, e });

    public Task RunPidTuning(int cycles, int targetTemp, int extruderIndex) =>
        SendCommandAsync("run_pid_tuning", new { cycles, targetTemp, extruderIndex });

    public Task RunThermalModelCalibration(int cycles, int targetTemp) =>
        SendCommandAsync("run_thermal_model_calibration", new { cycles, targetTemp });

    public Task StartPrint(FileEntry file) =>
        SendCommandAsync("start_print_file", new { path = file.FullPath });

    public Task StartPrint(GCodeDoc gcodeDoc)
    {
        if (!IsConnected || string.IsNullOrEmpty(gcodeDoc.Content))
        {
            return Task.CompletedTask;
        }

        return Task.Run(async () =>
        {
            await foreach (var command in gcodeDoc.EnumerateCommandsAsync(_cts.Token))
            {
                if (!IsConnected) break;
                await SendCommandAsync("gcode_line", new { command });
            }
        });
    }

    public Task SaveEEPROM() =>
        SendCommandAsync("save_eeprom", new { });

    public override ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        _disposed = true;

        _cts.Cancel();
        updateTimer.Dispose();

        lock (_syncRoot)
        {
            _ws?.Abort();
            _ws?.Dispose();
            _ws = null;
        }

        _httpClient?.Dispose();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    private void ConfigureClient(ApiConnectionSettings settings)
    {
        var client = Client;
        client.BaseAddress = _httpBaseUri;
        client.Timeout = TimeSpan.FromSeconds(30);
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        if (!string.IsNullOrEmpty(settings.Password))
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", settings.Password);
        }
    }
}
