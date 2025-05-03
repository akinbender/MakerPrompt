using System.Net;
using System.Net.Http.Headers;

namespace MakerPrompt.Shared.Services
{
    public class MoonrakerApiService : BasePrinterConnectionService, IDisposable
    {
        private HttpClient _httpClient;
        private Uri _baseUri;
        private string _jwtToken = string.Empty;
        private string _refreshToken = string.Empty;
        public override PrinterConnectionType ConnectionType { get; } = PrinterConnectionType.Moonraker;

        public override async Task<bool> ConnectAsync(PrinterConnectionSettings connectionSettings)
        {
            if (IsConnected) return IsConnected;

            if (connectionSettings.ConnectionType != ConnectionType || connectionSettings.Api == null) throw new ArgumentException();

            _baseUri = new Uri(connectionSettings.Api.Url);
            _httpClient = new HttpClient
            {
                BaseAddress = _baseUri,
                Timeout = TimeSpan.FromSeconds(30)
            };

            if (!string.IsNullOrEmpty(connectionSettings.Api.UserName) && !string.IsNullOrEmpty(connectionSettings.Api.Password))
            {
                IsConnected = await AuthenticateAsync(connectionSettings.Api.UserName, connectionSettings.Api.Password);
                if (!IsConnected) return IsConnected;
            }

            try
            {
                var response = await _httpClient.GetAsync("/printer/info");
                IsConnected = response.IsSuccessStatusCode;
                updateTimer.Elapsed += async (s, e) => await GetPrinterTelemetryAsync();
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

        public override async Task DisconnectAsync()
        {
            updateTimer.Stop();
            _httpClient.CancelPendingRequests();
            IsConnected = false;
            RaiseConnectionChanged();
            await Task.CompletedTask;
        }

        public override async Task WriteDataAsync(string command)
        {
            if (!IsConnected) return;

            var response = await _httpClient.PostAsync(
                $"/printer/gcode/script?script={WebUtility.UrlEncode(command)}",
                null);

            var content = await response.Content.ReadAsStringAsync();
            LastTelemetry.LastResponse = content;
            RaiseTelemetryUpdated();
        }

        public override async Task<PrinterTelemetry> GetPrinterTelemetryAsync()
        {
            if (!IsConnected) return LastTelemetry;

            // Get temperature data
            var tempResponse = await _httpClient.GetAsync("/printer/objects/query?heater_bed&extruder");
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

            var motionResponse = await _httpClient.GetAsync("/printer/objects/query?gcode_move,fan");
            if (motionResponse.IsSuccessStatusCode)
            {
                var json = await motionResponse.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var status = doc.RootElement.GetProperty("result").GetProperty("status");

                // Position data
                var position = status.GetProperty("gcode_move").GetProperty("position");
                LastTelemetry.Position = new Vector3(
                    (double)position[0].GetDecimal(),
                    (double)position[1].GetDecimal(),
                    (double)position[2].GetDecimal()
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
            var statusResponse = await _httpClient.GetAsync("/printer/objects/query?print_stats");
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

        public override async Task<List<FileEntry>> GetFilesAsync()
        {
            if (!IsConnected) return new List<FileEntry>();

            var response = await _httpClient.GetAsync(
                $"/server/files/list?root=gcodes");
            response.EnsureSuccessStatusCode();
            var content = JsonSerializer.Deserialize<FileListResponse>(await response.Content.ReadAsStringAsync());
            return response?.Files.Select(f => new FileEntry
                {
                    FullPath = f.Path,
                    Size = f.Size,
                    ModifiedDate = DateTimeOffset.FromUnixTimeSeconds((long)f.Modified).DateTime,
                    Available = s.Permissions.Contains("rw"),
                }).ToList() ?? new List<FileEntry>();
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

                var response = await _httpClient.PostAsync("/access/login", content);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync();
                var authResponse = JsonSerializer.Deserialize<AuthResponse>(responseJson);

                _jwtToken = authResponse?.Token ?? string.Empty;
                _refreshToken = authResponse?.RefreshToken ?? string.Empty;

                if (!string.IsNullOrEmpty(_jwtToken))
                {
                    _httpClient.DefaultRequestHeaders.Authorization =
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
            _httpClient.Dispose();
            GC.SuppressFinalize(this);
        }

        public override ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }

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
            public List<FileItem> Files { get; set; } = new List<FileItem>();
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