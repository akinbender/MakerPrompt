namespace MakerPrompt.Shared.Services
{
    public class PrusaLinkApiService : BasePrinterConnectionService, IPrinterCommunicationService
    {
        private readonly HttpClient _httpClient;
        private readonly Uri _baseUri;

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

        public override async Task<bool> ConnectAsync(PrinterConnectionSettings connectionSettings)
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

        public override async Task DisconnectAsync()
        {
            updateTimer?.Stop();
            _httpClient.CancelPendingRequests();
            IsConnected = false;
            RaiseConnectionChanged();
            await Task.CompletedTask;
        }

        public override Task WriteDataAsync(string command)
        {
            // PrusaLink doesn't support direct G-code injection like Moonraker
            // This would need to be implemented differently based on actual capabilities
            throw new NotSupportedException("Direct G-code commands not supported in PrusaLink");
        }

        public override Task WriteDataAsync(GCodeCommand command) => WriteDataAsync(string.Empty);

        public override async Task<PrinterTelemetry> GetPrinterTelemetryAsync()
        {
            var response = await _httpClient.GetStringAsync("/api/v1/status");
            var status = JsonSerializer.Deserialize<PrusaStatusResponse>(response);

            LastTelemetry = new PrinterTelemetry
            {
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
        public double? AxisX { get; set; }
        public double? AxisY { get; set; }
        public double? AxisZ { get; set; }
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
}