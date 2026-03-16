using System.Net;
using System.Text;
using MakerPrompt.Shared.Models;
using MakerPrompt.Shared.Services;
using static MakerPrompt.Shared.Utils.Enums;

namespace MakerPrompt.Tests;

public class PrusaConnectPrinterServiceTests
{
    private const string TestUuid  = "11111111-2222-3333-4444-555555555555";
    private const string TestToken = "bearer-token-abc";

    [Fact]
    public async Task ConnectAsync_SucceedsWithValidPrinterResponse()
    {
        var service = new PrusaConnectPrinterService(new FakeHandler(DefaultResponder()));

        var connected = await service.ConnectAsync(BuildSettings());
        service.updateTimer.Stop();

        Assert.True(connected);
        Assert.True(service.IsConnected);
        Assert.Equal("Mobile Test Printer", service.ConnectionName);
        Assert.Equal("Mobile Test Printer", service.LastTelemetry.PrinterName);
        Assert.Equal(PrinterConnectionType.PrusaConnect, service.ConnectionType);
    }

    [Fact]
    public async Task ConnectAsync_ReturnsFalse_WhenPrinterEndpointFails()
    {
        var service = new PrusaConnectPrinterService(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)));

        var connected = await service.ConnectAsync(BuildSettings());

        Assert.False(connected);
        Assert.False(service.IsConnected);
    }

    [Fact]
    public async Task GetPrinterTelemetryAsync_MapsTelemetryFromTelemetryEndpoint()
    {
        var service = new PrusaConnectPrinterService(new FakeHandler(DefaultResponder()));
        await service.ConnectAsync(BuildSettings());
        service.updateTimer.Stop();

        var telemetry = await service.GetPrinterTelemetryAsync();

        Assert.Equal(210.0, telemetry.HotendTarget, precision: 1);
        Assert.Equal(195.0, telemetry.HotendTemp,   precision: 1);
        Assert.Equal(70.0,  telemetry.BedTarget,    precision: 1);
        Assert.Equal(65.0,  telemetry.BedTemp,      precision: 1);
        Assert.Equal(100,   telemetry.FeedRate);
    }

    [Fact]
    public async Task GetPrinterTelemetryAsync_MapsPrintingStateAndJobProgress()
    {
        var service = new PrusaConnectPrinterService(new FakeHandler(DefaultResponder()));
        await service.ConnectAsync(BuildSettings());
        service.updateTimer.Stop();

        var telemetry = await service.GetPrinterTelemetryAsync();

        Assert.Equal(PrinterStatus.Printing, telemetry.Status);
        Assert.Equal(75.0, telemetry.SDCard.Progress);
        Assert.Equal(TimeSpan.FromSeconds(600), telemetry.PrintDuration);
    }

    [Fact]
    public async Task GetPrinterTelemetryAsync_MapsIdleStateToConnected()
    {
        var service = new PrusaConnectPrinterService(new FakeHandler(req =>
        {
            var path = req.RequestUri?.AbsolutePath ?? string.Empty;
            if (path == $"/api/v1/printers/{TestUuid}/telemetry")
                return JsonResponse("""{"temp_nozzle":25.0,"target_nozzle":0,"temp_bed":24.0,"target_bed":0,"print_speed":100}""");
            if (path == $"/api/v1/printers/{TestUuid}")
                return JsonResponse($$"""{"uuid":"{{TestUuid}}","name":"Idle","state":"IDLE","job_info":null}""");
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }));
        await service.ConnectAsync(BuildSettings());
        service.updateTimer.Stop();

        var telemetry = await service.GetPrinterTelemetryAsync();

        Assert.Equal(PrinterStatus.Connected, telemetry.Status);
    }

    [Fact]
    public async Task WriteDataAsync_PostsCommandToCommandEndpoint()
    {
        var capturedPaths = new List<string>();
        string? capturedBody = null;

        var service = new PrusaConnectPrinterService(new FakeHandler(async req =>
        {
            capturedPaths.Add(req.RequestUri?.AbsolutePath ?? string.Empty);
            if (req.Method == HttpMethod.Post)
                capturedBody = await req.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));
        await service.ConnectAsync(BuildSettings());
        service.updateTimer.Stop();

        await service.WriteDataAsync("G28");

        Assert.Contains(capturedPaths, p => p == $"/api/v1/printers/{TestUuid}/command");
        Assert.NotNull(capturedBody);
        Assert.Contains("G28", capturedBody);
    }

    [Fact]
    public async Task DisconnectAsync_ClearsIsConnected()
    {
        var service = new PrusaConnectPrinterService(new FakeHandler(DefaultResponder()));
        await service.ConnectAsync(BuildSettings());
        service.updateTimer.Stop();
        Assert.True(service.IsConnected);

        await service.DisconnectAsync();

        Assert.False(service.IsConnected);
    }

    [Fact]
    public async Task GetFilesAsync_ReturnsEmptyList()
    {
        var service = new PrusaConnectPrinterService(new FakeHandler(DefaultResponder()));
        await service.ConnectAsync(BuildSettings());
        service.updateTimer.Stop();

        var files = await service.GetFilesAsync();

        Assert.Empty(files);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static PrinterConnectionSettings BuildSettings() =>
        new(new ApiConnectionSettings(string.Empty, TestUuid, TestToken), PrinterConnectionType.PrusaConnect);

    private static Func<HttpRequestMessage, HttpResponseMessage> DefaultResponder() =>
        req =>
        {
            var path = req.RequestUri?.AbsolutePath ?? string.Empty;
            if (path == $"/api/v1/printers/{TestUuid}")
                return JsonResponse($$"""
                    {
                      "uuid": "{{TestUuid}}",
                      "name": "Mobile Test Printer",
                      "printer_type": "MK4",
                      "state": "PRINTING",
                      "job_info": {"progress": 75.0, "time_remaining": 300, "time_printing": 600}
                    }
                    """);
            if (path == $"/api/v1/printers/{TestUuid}/telemetry")
                return JsonResponse($$"""
                    {
                      "temp_nozzle": 195.0,
                      "target_nozzle": 210.0,
                      "temp_bed": 65.0,
                      "target_bed": 70.0,
                      "print_speed": 100
                    }
                    """);
            if (path == $"/api/v1/printers/{TestUuid}/command")
                return new HttpResponseMessage(HttpStatusCode.OK);
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        };

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _responder;

        public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
            => _responder = req => Task.FromResult(responder(req));

        public FakeHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> asyncResponder)
            => _responder = asyncResponder;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => _responder(request);
    }
}
