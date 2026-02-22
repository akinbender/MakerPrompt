using System.Net;
using System.Text;
using MakerPrompt.Shared.Models;
using MakerPrompt.Shared.Services;
using static MakerPrompt.Shared.Utils.Enums;

namespace MakerPrompt.Tests;

public class PrusaLinkApiServiceTests
{
    [Fact]
    public async Task ConnectAsync_UsesVersionAndInfoEndpoints()
    {
        var handler = new FakePrusaLinkHandler(BuildDefaultResponses());
        var service = new PrusaLinkApiService(handler);

        var connected = await service.ConnectAsync(BuildSettings());

        Assert.True(connected);
        Assert.True(service.IsConnected);
        Assert.Equal("http://printer.local/", service.ConnectionName);
        Assert.Equal("Test Printer", service.LastTelemetry.PrinterName);
    }

    [Fact]
    public async Task GetPrinterTelemetryAsync_MapsStatusFields()
    {
        var handler = new FakePrusaLinkHandler(BuildDefaultResponses());
        var service = new PrusaLinkApiService(handler);
        await service.ConnectAsync(BuildSettings());

        var telemetry = await service.GetPrinterTelemetryAsync();

        Assert.Equal(215, telemetry.HotendTarget);
        Assert.Equal(60, telemetry.BedTarget);
        Assert.Equal(PrinterStatus.Printing, telemetry.Status);
        Assert.Equal(420, telemetry.FanSpeed);
    }

    [Fact]
    public async Task GetFilesAsync_ReturnsChildrenFromDefaultStorage()
    {
        var handler = new FakePrusaLinkHandler(BuildDefaultResponses());
        var service = new PrusaLinkApiService(handler);
        await service.ConnectAsync(BuildSettings());

        var files = await service.GetFilesAsync();

        Assert.Single(files);
        Assert.Equal("/local/examples/file.gcode", files[0].FullPath);
        Assert.Equal(424242, files[0].Size);
    }

    private static Func<HttpRequestMessage, HttpResponseMessage> BuildDefaultResponses()
    {
        return request =>
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            return path switch
            {
                "/api/version" => JsonResponse("""
                    {"api":"1.0.0","version":"0.7.0","printer":"1.3.1","text":"PrusaLink 0.7.0","firmware":"3.10.1"}
                    """),
                "/api/v1/info" => JsonResponse("""
                    {"name":"Test Printer","hostname":"prusa.local"}
                    """),
                "/api/v1/status" => JsonResponse("""
                    {"printer":{"state":"PRINTING","temp_nozzle":214.9,"target_nozzle":215.0,"temp_bed":59.5,"target_bed":60.0,"axis_x":23.2,"axis_y":24.3,"axis_z":0.5,"flow":100,"speed":100,"fan_print":420},"job":{"id":42,"state":"PRINTING","progress":42.0,"time_remaining":520,"time_printing":526},"storage":{"name":"LOCAL","path":"/local","read_only":false,"free_space":123456}}
                    """),
                "/api/v1/storage" => JsonResponse("""
                    {"storage_list":[{"name":"PrusaLink gcodes","type":"LOCAL","path":"/local"}]}
                    """),
                var p when p.StartsWith("/api/v1/files/local/") => JsonResponse("""
                    {"name":"examples","path":"/examples","read_only":false,"type":"FOLDER","m_timestamp":1648042843,"children":[{"name":"file.gcode","path":"/examples/file.gcode","read_only":false,"size":424242,"type":"PRINT_FILE","m_timestamp":1648042843}]}
                    """),
                _ => new HttpResponseMessage(HttpStatusCode.NotFound)
            };
        };
    }

    private static PrinterConnectionSettings BuildSettings() =>
        new(new ApiConnectionSettings("http://printer.local", "user", "pass"), PrinterConnectionType.PrusaLink);

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private sealed class FakePrusaLinkHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> responder;

        public FakePrusaLinkHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            this.responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }
}
