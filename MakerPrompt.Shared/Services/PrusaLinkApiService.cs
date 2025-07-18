﻿namespace MakerPrompt.Shared.Services
{
    public class PrusaLinkApiService : BasePrinterConnectionService, IPrinterCommunicationService
    {
        private readonly HttpClient _httpClient;
        private readonly Uri _baseUri;
        private readonly CancellationTokenSource _cts = new();

        public override PrinterConnectionType ConnectionType => PrinterConnectionType.PrusaLink;

        public PrusaLinkApiService()
        {
            // TODO check compatibility warning
            //_baseUri = new Uri(connectionSettings.Url);
            //var handler = new HttpClientHandler
            //{
            //    PreAuthenticate = true
            //};

            //if (true)
            //{
            //    var credentials = new NetworkCredential(
            //        connectionSettings.UserName,
            //        connectionSettings.Password);
            //    handler.Credentials = credentials;
            //}

            //_httpClient = new HttpClient(handler) { BaseAddress = _baseUri };
        }

        public async Task<bool> ConnectAsync(PrinterConnectionSettings connectionSettings)
        {
            try
            {
                var version = await GetVersionAsync();
                if (version == null) return IsConnected = false;

                IsConnected = true;
                RaiseConnectionChanged();
            }
            catch
            {
                IsConnected = false;
                RaiseConnectionChanged();
            }

            return IsConnected;
        }

        public async Task DisconnectAsync()
        {
            updateTimer?.Stop();
            _httpClient.CancelPendingRequests();
            IsConnected = false;
            RaiseConnectionChanged();
            await Task.CompletedTask;
        }

        public async Task WriteDataAsync(string command)
        {
            // PrusaLink doesn't support direct G-code injection like Moonraker
            // This would need to be implemented differently based on actual capabilities
            throw new NotSupportedException("Direct G-code commands not supported in PrusaLink");
        }

        public async Task<PrinterTelemetry> GetPrinterTelemetryAsync()
        {
            var response = await _httpClient.GetStringAsync("/api/v1/status");
            var status = JsonSerializer.Deserialize<PrusaStatusResponse>(response);

            LastTelemetry = new PrinterTelemetry
            {
                LastResponse = "PrusaLink status update",
                ConnectionTime = IsConnected ? DateTime.Now : null,
                HotendTemp = status?.Printer.TempNozzle ?? 0,
                HotendTarget = status?.Printer.TargetNozzle ?? 0,
                BedTemp = status?.Printer.TempBed ?? 0,
                BedTarget = status?.Printer.TargetBed ?? 0,
                Position = new Vector3(
                    status?.Printer.AxisX ?? 0,
                    status?.Printer.AxisY ?? 0,
                    status?.Printer.AxisZ ?? 0
                ),
                Status = MapPrusaStatus(status?.Printer.State),
                FanSpeed = status?.Printer.FanPrint ?? 0 ,
                SDCard = {
                    Present = status?.Storage?.Any(s => s.Type == "SDCARD" && s.Available) ?? false,
                    Printing = status?.Printer.State == "PRINTING"
                }
            };

            RaiseTelemetryUpdated();
            return LastTelemetry;
        }

        public async Task<List<FileEntry>> GetFilesAsync()
        {
            return new List<FileEntry>();
            // var response = await _httpClient.GetAsync("/api/v1/storage");
            // response.EnsureSuccessStatusCode();
            // var content = JsonSerializer.Deserialize<PrusaFile‚Item>(await response.Content.ReadAsStringAsync());
            // return response?.Storage.Select(s => new FileEntry
            // {
            //     FullPath = s.Path,
            //     Size = s.Size,
            //     Available = !s.Readonly,
            // }).ToList() ?? new List<FileEntry>();
        }

        private async Task<PrusaVersionResponse?> GetVersionAsync()
        {
            var response = await _httpClient.GetStringAsync("/api/version");
            return JsonSerializer.Deserialize<PrusaVersionResponse>(response);
        }

        private static PrinterStatus MapPrusaStatus(string? status) => status?.ToUpper() switch
        {
            "PRINTING" => PrinterStatus.Printing,
            "PAUSED" => PrinterStatus.Paused,
            "ERROR" => PrinterStatus.Error,
            "READY" => PrinterStatus.Connected,
            _ => PrinterStatus.Disconnected
        };

        public void Dispose()
        {
            _cts?.Cancel();
            _httpClient.Dispose();
            GC.SuppressFinalize(this);
        }

        public override ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }

        public Task SetHotendTemp(int targetTemp = 0)
        {
            throw new NotImplementedException();
        }

        public Task SetBedTemp(int targetTemp = 0)
        {
            throw new NotImplementedException();
        }

        public Task Home(bool x = true, bool y = true, bool z = true)
        {
            throw new NotImplementedException();
        }

        public Task RelativeMove(int feedRate, float x = 0.0f, float y = 0.0f, float z = 0.0f, float e = 0.0f)
        {
            throw new NotImplementedException();
        }

        public Task SetFanSpeed(int speed)
        {
            throw new NotImplementedException();
        }

        public Task SetPrintSpeed(int speed)
        {
            throw new NotImplementedException();
        }

        public Task SetPrintFlow(int flow)
        {
            throw new NotImplementedException();
        }

        public Task SetAxisPerUnit(float x = 0.0f, float y = 0.0f, float z = 0.0f, float e = 0.0f)
        {
            throw new NotImplementedException();
        }

        public Task RunPidTuning(int cycles, int targetTemp, int extruderIndex)
        {
            throw new NotImplementedException();
        }

        public Task RunThermalModelCalibration(int cycles, int targetTemp)
        {
            throw new NotImplementedException();
        }

        public Task StartPrint(FileEntry file)
        {
            throw new NotImplementedException();
        }

        public Task SaveEEPROM()
        {
            throw new NotImplementedException();
        }
    }

    // PrusaLink DTOs
    public class PrusaVersionResponse
    {
        public string Api { get; set; }
        public string Version { get; set; }
        public string Printer { get; set; }
        public string Text { get; set; }
        public string Firmware { get; set; }
    }

    public class PrusaStatusResponse
    {
        public PrusaStatusPrinter Printer { get; set; }
        public PrusaStatusJob? Job { get; set; }
        public List<PrusaStorage>? Storage { get; set; }
    }

    public class PrusaStatusPrinter
    {
        public string State { get; set; }
        public double? TempNozzle { get; set; }
        public double? TargetNozzle { get; set; }
        public double? TempBed { get; set; }
        public double? TargetBed { get; set; }
        public float? AxisX { get; set; }
        public float? AxisY { get; set; }
        public float? AxisZ { get; set; }
        public int? FanPrint { get; set; }
    }

    public class PrusaStatusJob
    {
        public int Id { get; set; }
        public double Progress { get; set; }
        public int TimeRemaining { get; set; }
        public int TimePrinting { get; set; }
    }

    public class PrusaStorage
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public bool Available { get; set; }
    }

    public class PrusaFileItem
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }
        [JsonPropertyName("path")]
        public string Path { get; set; }
        [JsonPropertyName("read_only")]
        public bool Readonly { get; set; }
        [JsonPropertyName("free_space")]
        public long Size { get; set; }
    }
}