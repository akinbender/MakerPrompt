using System.Net;
using System.Text;
using MakerPrompt.Shared.Models;
using MakerPrompt.Shared.Services;
using static MakerPrompt.Shared.Utils.Enums;

namespace MakerPrompt.Tests;

public class PrusaConnectApiServiceTests
{
    private const string TestUuid = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";

    [Fact]
    public async Task ConnectAsync_SucceedsWithValidPrinterResponse()
    {
        var service = new PrusaConnectApiService(new FakeHandler(DefaultResponder()));

        var connected = await service.ConnectAsync(BuildSettings());

        Assert.True(connected);
        Assert.True(service.IsConnected);
        Assert.Equal("Test Printer", service.ConnectionName);
        Assert.Equal("Test Printer", service.LastTelemetry.PrinterName);
    }

    [Fact]
    public async Task ConnectAsync_ReturnsFalse_WhenPrinterEndpointFails()
    {
        var service = new PrusaConnectApiService(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)));

        var connected = await service.ConnectAsync(BuildSettings());

        Assert.False(connected);
        Assert.False(service.IsConnected);
    }

    [Fact]
    public async Task GetPrinterTelemetryAsync_MapsTelemetryAndJobFields()
    {
        var service = new PrusaConnectApiService(new FakeHandler(DefaultResponder()));
        await service.ConnectAsync(BuildSettings());

        var telemetry = await service.GetPrinterTelemetryAsync();

        Assert.Equal(215.0, telemetry.HotendTarget);
        Assert.Equal(200.0, telemetry.HotendTemp, precision: 1);
        Assert.Equal(60.0, telemetry.BedTarget);
        Assert.Equal(100, telemetry.FeedRate);
        Assert.Equal(PrinterStatus.Printing, telemetry.Status);
        Assert.Equal(50.0, telemetry.SDCard.Progress);
        Assert.Equal(TimeSpan.FromSeconds(300), telemetry.PrintDuration);
    }

    [Fact]
    public async Task GetPrinterTelemetryAsync_MapsIdleState()
    {
        var service = new PrusaConnectApiService(new FakeHandler(req =>
        {
            if (req.RequestUri?.AbsolutePath == $"/app/printers/{TestUuid}")
                return JsonResponse($$"""{"uuid":"{{TestUuid}}","name":"Idle Printer","state":"IDLE","telemetry":{},"job_info":null}""");
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }));
        await service.ConnectAsync(BuildSettings());

        var telemetry = await service.GetPrinterTelemetryAsync();

        Assert.Equal(PrinterStatus.Connected, telemetry.Status);
    }

    [Fact]
    public async Task GetCamerasAsync_ReturnsCameraWithSnapshotUrl()
    {
        var service = new PrusaConnectApiService(new FakeHandler(DefaultResponder()));
        await service.ConnectAsync(BuildSettings());

        var cameras = await service.GetCamerasAsync();

        Assert.Single(cameras);
        Assert.Equal("cam-fp-abc", cameras[0].Id);
        Assert.Equal("Front Camera", cameras[0].DisplayName);
        Assert.Contains("cam-fp-abc", cameras[0].SnapshotUrl);
        Assert.Contains("cam-tok-xyz", cameras[0].SnapshotUrl);
        Assert.True(cameras[0].IsEnabled);
    }

    [Fact]
    public async Task GetCamerasAsync_ReturnsEmpty_WhenNotConnected()
    {
        var service = new PrusaConnectApiService(new FakeHandler(DefaultResponder()));

        var cameras = await service.GetCamerasAsync();

        Assert.Empty(cameras);
    }

    [Fact]
    public async Task GetCamerasAsync_SkipsUnregisteredCameras()
    {
        var service = new PrusaConnectApiService(new FakeHandler(req =>
        {
            var path = req.RequestUri?.AbsolutePath ?? string.Empty;
            if (path == $"/app/printers/{TestUuid}")
                return JsonResponse($$"""{"uuid":"{{TestUuid}}","name":"Test Printer","state":"IDLE","telemetry":{},"job_info":null}""");
            if (path == $"/app/printers/{TestUuid}/cameras")
                return JsonResponse("""[{"camera_fingerprint":"fp1","token":"tok1","name":"Cam1","registered":false}]""");
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }));
        await service.ConnectAsync(BuildSettings());

        var cameras = await service.GetCamerasAsync();

        Assert.Empty(cameras);
    }

    [Fact]
    public async Task GetCamerasAsync_SkipsCamerasWithoutToken()
    {
        var service = new PrusaConnectApiService(new FakeHandler(req =>
        {
            var path = req.RequestUri?.AbsolutePath ?? string.Empty;
            if (path == $"/app/printers/{TestUuid}")
                return JsonResponse($$"""{"uuid":"{{TestUuid}}","name":"Test Printer","state":"IDLE","telemetry":{},"job_info":null}""");
            if (path == $"/app/printers/{TestUuid}/cameras")
                return JsonResponse("""[{"camera_fingerprint":"fp1","name":"Cam1","registered":true}]""");
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }));
        await service.ConnectAsync(BuildSettings());

        var cameras = await service.GetCamerasAsync();

        Assert.Empty(cameras);
    }

    [Fact]
    public async Task GetFilesAsync_ReturnsEmptyList()
    {
        var service = new PrusaConnectApiService(new FakeHandler(DefaultResponder()));
        await service.ConnectAsync(BuildSettings());

        var files = await service.GetFilesAsync();

        Assert.Empty(files);
    }

    // ── Helpers ──

    private static PrinterConnectionSettings BuildSettings() =>
        new(new ApiConnectionSettings(string.Empty, TestUuid, "test-api-key"), PrinterConnectionType.PrusaConnect);

    private static Func<HttpRequestMessage, HttpResponseMessage> DefaultResponder() =>
        req =>
        {
            var path = req.RequestUri?.AbsolutePath ?? string.Empty;
            if (path == $"/app/printers/{TestUuid}")
                return JsonResponse($$"""
                    {
                      "uuid": "{{TestUuid}}",
                      "name": "Test Printer",
                      "state": "PRINTING",
                      "telemetry": {
                        "temp_nozzle": 200.0,
                        "target_nozzle": 215.0,
                        "temp_bed": 59.5,
                        "target_bed": 60.0,
                        "print_speed": 100
                      },
                      "job_info": {
                        "progress": 50.0,
                        "time_remaining": 600,
                        "time_printing": 300
                      }
                    }
                    """);
            if (path == $"/app/printers/{TestUuid}/cameras")
                return JsonResponse("""
                    [
                      {
                        "camera_fingerprint": "cam-fp-abc",
                        "token": "cam-tok-xyz",
                        "name": "Front Camera",
                        "registered": true
                      }
                    ]
                    """);
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        };

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }
}
