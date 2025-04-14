using System.Net;
using System.Text.Json;
using MakerPrompt.Shared.Infrastructure;
using MakerPrompt.Shared.Models;
using MakerPrompt.Shared.Utils;
using static MakerPrompt.Shared.Utils.Enums;

namespace MakerPrompt.Shared.Services
{
    public class MoonrakerApiService : BasePrinterConnectionService, IPrinterCommunicationService, IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly Uri _baseUri;

        public override PrinterConnectionType ConnectionType { get; } = PrinterConnectionType.Moonraker;

        public MoonrakerApiService(ApiConnectionSettings connectionSettings)
        {
            
            _baseUri = new Uri(connectionSettings.Url);
            _httpClient = new HttpClient
            {
                BaseAddress = _baseUri,
                Timeout = TimeSpan.FromSeconds(30)
            };
        }

        public override async Task<bool> ConnectAsync()
        {
            //TODO implement auth
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
    }

}