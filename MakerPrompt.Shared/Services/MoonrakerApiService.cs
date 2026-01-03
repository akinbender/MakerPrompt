using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MakerPrompt.Shared.Infrastructure;
using MakerPrompt.Shared.Models;
using MakerPrompt.Shared.Utils;
using static MakerPrompt.Shared.Utils.Enums;

namespace MakerPrompt.Shared.Services
{
    public class MoonrakerApiService : BasePrinterConnectionService, IPrinterCommunicationService
    {
        private readonly CancellationTokenSource _cts = new();
        private readonly HttpMessageHandler? _customHandler;
        private HttpClient? _httpClient;
        private Uri _baseUri = null!;
        private string _jwtToken = string.Empty;
        private string _refreshToken = string.Empty;
        public override PrinterConnectionType ConnectionType { get; } = PrinterConnectionType.Moonraker;

        public MoonrakerApiService()
        {
        }

        public MoonrakerApiService(HttpMessageHandler handler)
        {
            _customHandler = handler;
            _httpClient = new HttpClient(handler, false);
        }

        private HttpClient Client => _httpClient ??= _customHandler != null
            ? new HttpClient(_customHandler, false)
            : new HttpClient();

        public async Task<bool> ConnectAsync(PrinterConnectionSettings connectionSettings)
        {
            if (IsConnected) return IsConnected;

            if (connectionSettings.ConnectionType != ConnectionType || connectionSettings.Api == null) throw new ArgumentException();

            _baseUri = new Uri(connectionSettings.Api.Url);
            ConfigureClient(connectionSettings.Api);

            if (!string.IsNullOrEmpty(connectionSettings.Api.UserName) && !string.IsNullOrEmpty(connectionSettings.Api.Password))
            {
                IsConnected = await AuthenticateAsync(connectionSettings.Api.UserName, connectionSettings.Api.Password);
                if (!IsConnected) return IsConnected;
            }

            try
            {
                var response = await Client.GetAsync("/printer/info", _cts.Token);
                IsConnected = response.IsSuccessStatusCode;
                updateTimer.Elapsed += async (s, e) => await SafeTelemetryAsync();
                updateTimer.Start();
                ConnectionName = _baseUri.AbsoluteUri;
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

        public async Task WriteDataAsync(string command)
        {
            if (!IsConnected) return;

            var response = await Client.PostAsync(
                $"/printer/gcode/script?script={WebUtility.UrlEncode(command)}",
                null,
                _cts.Token);

            var content = await response.Content.ReadAsStringAsync();
            LastTelemetry.LastResponse = content;
            RaiseTelemetryUpdated();
        }

        public async Task<PrinterTelemetry> GetPrinterTelemetryAsync()
        {
            if (!IsConnected) return LastTelemetry;

            // Get temperature data
            var tempResponse = await Client.GetAsync("/printer/objects/query?heater_bed&extruder", _cts.Token);
            if (tempResponse.IsSuccessStatusCode)
            {
                var json = await tempResponse.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement.GetProperty("result").GetProperty("status");

                LastTelemetry.BedTemp = root.GetProperty("heater_bed").GetProperty("temperature").GetDouble();
                LastTelemetry.BedTarget = root.GetProperty("heater_bed").GetProperty("target").GetDouble();
                LastTelemetry.HotendTemp = root.GetProperty("extruder").GetProperty("temperature").GetDouble();
                LastTelemetry.HotendTarget = root.GetProperty("extruder").GetProperty("target").GetDouble();
            }

            var motionResponse = await Client.GetAsync("/printer/objects/query?gcode_move,fan", _cts.Token);
            if (motionResponse.IsSuccessStatusCode)
            {
                var json = await motionResponse.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var status = doc.RootElement.GetProperty("result").GetProperty("status");

                // Position data
                var position = status.GetProperty("gcode_move").GetProperty("position");
                LastTelemetry.Position = new Vector3(
                    (float)position[0].GetDecimal(),
                    (float)position[1].GetDecimal(),
                    (float)position[2].GetDecimal()
                );
                // Speed and flow data
                LastTelemetry.FeedRate = (int)status.GetProperty("gcode_move")
                    .GetProperty("speed").GetDecimal();
                LastTelemetry.FlowRate = (int)(status.GetProperty("gcode_move")
                    .GetProperty("extrude_factor").GetDecimal() * 100);

                // Fan speed
                LastTelemetry.FanSpeed = (int)(status.GetProperty("fan")
                    .GetProperty("speed").GetDecimal() * 100);
            }

            // Get printer status
            var statusResponse = await Client.GetAsync("/printer/objects/query?print_stats", _cts.Token);
            if (statusResponse.IsSuccessStatusCode)
            {
                var json = await statusResponse.Content.ReadAsStringAsync();
                LastTelemetry.Status = json.Contains("printing") ?
                    PrinterStatus.Printing :
                    PrinterStatus.Connected;
            }

            RaiseTelemetryUpdated();
            return LastTelemetry;
        }

        public async Task<List<FileEntry>> GetFilesAsync()
        {
            if (!IsConnected) return [];

            var response = await Client.GetAsync(
                $"/server/files/list?root=gcodes", _cts.Token);
            response.EnsureSuccessStatusCode();
            var content = JsonSerializer.Deserialize<FileListResponse>(await response.Content.ReadAsStringAsync());
            var files = content?.Files ?? [];
            return files.Select(f => new FileEntry
                {
                    FullPath = f.Path,
                    Size = f.Size,
                    ModifiedDate = f.ModifiedDate,
                    IsAvailable = f.Permissions.Contains("rw"),
                }).ToList();
        }

        public async Task<bool> AuthenticateAsync(string username, string password)
        {
            try
            {
                var request = new
                {
                    username,
                    password,
                    source = "moonraker"
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await Client.PostAsync("/access/login", content, _cts.Token);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync();
                var authResponse = JsonSerializer.Deserialize<AuthResponse>(responseJson);

                _jwtToken = authResponse?.Token ?? string.Empty;
                _refreshToken = authResponse?.RefreshToken ?? string.Empty;

                if (!string.IsNullOrEmpty(_jwtToken))
                {
                    Client.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", _jwtToken);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            updateTimer.Dispose();
            _httpClient?.Dispose();
            _cts.Dispose();
            GC.SuppressFinalize(this);
        }

        public override ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }

        private void ConfigureClient(ApiConnectionSettings settings)
        {
            var client = Client;
            client.BaseAddress = _baseUri;
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            if (!string.IsNullOrEmpty(settings.UserName) && !string.IsNullOrEmpty(settings.Password))
            {
                var credentialBytes = Encoding.ASCII.GetBytes($"{settings.UserName}:{settings.Password}");
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Basic", Convert.ToBase64String(credentialBytes));
            }
        }

        private async Task SafeTelemetryAsync()
        {
            try
            {
                await GetPrinterTelemetryAsync();
            }
            catch
            {
                // swallow background telemetry errors
            }
        }

        private Task SendGcodeAsync(string gcode) => WriteDataAsync(gcode);

        public Task SetHotendTemp(int targetTemp = 0) =>
            SendGcodeAsync($"M104 S{targetTemp}");

        public Task SetBedTemp(int targetTemp = 0) =>
            SendGcodeAsync($"M140 S{targetTemp}");

        public Task Home(bool x = true, bool y = true, bool z = true)
        {
            var axes = new StringBuilder();
            if (x) axes.Append(" X");
            if (y) axes.Append(" Y");
            if (z) axes.Append(" Z");
            var command = axes.Length == 0 ? "G28" : $"G28{axes}";
            return SendGcodeAsync(command);
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
            return SendGcodeAsync(sb.ToString());
        }

        public Task SetFanSpeed(int speed)
        {
            var clamped = Math.Clamp(speed, 0, 100);
            var duty = (int)Math.Round(clamped * 255.0 / 100.0, MidpointRounding.AwayFromZero);
            return SendGcodeAsync($"M106 S{duty}");
        }

        public Task SetPrintSpeed(int speed)
        {
            var clamped = Math.Clamp(speed, 1, 200);
            return SendGcodeAsync($"M220 S{clamped}");
        }

        public Task SetPrintFlow(int flow)
        {
            var clamped = Math.Clamp(flow, 1, 200);
            return SendGcodeAsync($"M221 S{clamped}");
        }

        public Task SetAxisPerUnit(float x = 0.0f, float y = 0.0f, float z = 0.0f, float e = 0.0f)
        {
            var sb = new StringBuilder("M92");
            if (x > 0) sb.Append($" X{x}");
            if (y > 0) sb.Append($" Y{y}");
            if (z > 0) sb.Append($" Z{z}");
            if (e > 0) sb.Append($" E{e}");
            return SendGcodeAsync(sb.ToString());
        }

        public Task RunPidTuning(int cycles, int targetTemp, int extruderIndex)
        {
            var heater = extruderIndex == 0 ? "extruder" : $"extruder{extruderIndex}";
            return SendGcodeAsync($"PID_CALIBRATE HEATER={heater} TARGET={targetTemp}");
        }

        public Task RunThermalModelCalibration(int cycles, int targetTemp)
        {
            // No direct Moonraker endpoint; fall back to PID tuning for the bed as closest available.
            return SendGcodeAsync($"PID_CALIBRATE HEATER=heater_bed TARGET={targetTemp}");
        }

        public async Task StartPrint(FileEntry file)
        {
            var filename = WebUtility.UrlEncode(file.FullPath);
            await Client.PostAsync($"/printer/print/start?filename={filename}", null, _cts.Token);
        }

        public Task SaveEEPROM() => SendGcodeAsync("SAVE_CONFIG");

        private record AuthResponse
        {
            [JsonPropertyName("username")]
            public string Username { get; set; } = string.Empty;

            [JsonPropertyName("token")]
            public string Token { get; set; } = string.Empty;

            [JsonPropertyName("refresh_token")]
            public string RefreshToken { get; set; } = string.Empty;

            [JsonPropertyName("action")]
            public string Action { get; set; } = string.Empty;

            [JsonPropertyName("source")]
            public string Source { get; set; } = string.Empty;
        }

        private record FileListResponse
        {
            [JsonPropertyName("result")]
            public List<FileItem> Files { get; set; } = [];
        }

        private record FileItem
        {
            [JsonPropertyName("path")]
            public string Path { get; set; } = string.Empty;

            [JsonPropertyName("modified")]
            public double ModifiedSeconds { get; set; }

            [JsonIgnore]
            public DateTime ModifiedDate => 
                DateTimeOffset.FromUnixTimeSeconds((long)ModifiedSeconds).DateTime;

            [JsonPropertyName("size")]
            public long Size { get; set; }

            [JsonPropertyName("permissions")]
            public string Permissions { get; set; } = string.Empty;

            [JsonIgnore]
            public bool IsDirectory => Size == 0;
        }

    }

}
