using System.Net;
using System.Text;
using MakerPrompt.Shared.Models;
using MakerPrompt.Shared.Services;
using MakerPrompt.Shared.Utils;
using static MakerPrompt.Shared.Utils.Enums;

namespace MakerPrompt.Tests;

public class MoonrakerApiServiceTests
{
    [Fact]
    public async Task ConnectAsync_SucceedsWithInfo()
    {
        var handler = new FakeMoonrakerHandler(BuildDefaultResponses());
        var service = new MoonrakerApiService(handler);

        var connected = await service.ConnectAsync(BuildSettings());

        Assert.True(connected);
        Assert.True(service.IsConnected);
        Assert.Equal("http://moonraker.local/", service.ConnectionName);
    }

    [Fact]
    public async Task GetPrinterTelemetryAsync_MapsStatus()
    {
        var handler = new FakeMoonrakerHandler(BuildDefaultResponses());
        var service = new MoonrakerApiService(handler);
        await service.ConnectAsync(BuildSettings());

        var telemetry = await service.GetPrinterTelemetryAsync();

        Assert.Equal(215, telemetry.HotendTarget);
        Assert.Equal(60, telemetry.BedTarget);
        Assert.Equal(PrinterStatus.Printing, telemetry.Status);
        Assert.Equal(100, telemetry.FeedRate);
    }

    [Fact]
    public async Task SetHotendTemp_SendsGcodeScript()
    {
        var handler = new FakeMoonrakerHandler(BuildDefaultResponses());
        var service = new MoonrakerApiService(handler);
        await service.ConnectAsync(BuildSettings());

        await service.SetHotendTemp(200);

        Assert.Contains(handler.RequestPaths, p => p.Contains("/printer/gcode/script", StringComparison.Ordinal));
        Assert.Contains("M104+S200", handler.RequestPaths.Last());
    }

    [Fact]
    public async Task StartPrint_InvokesPrintStartEndpoint()
    {
        var handler = new FakeMoonrakerHandler(BuildDefaultResponses());
        var service = new MoonrakerApiService(handler);
        await service.ConnectAsync(BuildSettings());

        await service.StartPrint(new FileEntry { FullPath = "gcodes/test.gcode" });

        Assert.Contains(handler.RequestPaths, p => p.Contains("/printer/print/start", StringComparison.Ordinal));
    }

        [Fact]
    public async Task GetGcodeHelpAsync_ReturnsParsedCommands()
    {
        var handler = new FakeMoonrakerHandler(BuildDefaultResponses());
        var service = new MoonrakerApiService(handler);

        await service.ConnectAsync(new MakerPrompt.Shared.Models.PrinterConnectionSettings(
            new MakerPrompt.Shared.Models.ApiConnectionSettings("http://moonraker.local", string.Empty, string.Empty),
            PrinterConnectionType.Moonraker));

        var help = await service.GetGcodeHelpAsync();

        Assert.NotNull(help);
        Assert.True(help.Count > 0);
        Assert.Equal("Reload config file and restart host software", help["RESTART"]);
        Assert.Equal("Restart firmware, host, and reload config", help["FIRMWARE_RESTART"]);
    }

    private static Func<HttpRequestMessage, HttpResponseMessage> BuildDefaultResponses()
    {
        return request =>
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            var query = request.RequestUri?.Query ?? string.Empty;
            return path switch
            {
                "/printer/info" => JsonResponse("""{"result":{"state":"ready"}}"""),
                "/printer/gcode/help" => JsonResponse("{\"result\":{\"RESTART\":\"Reload config file and restart host software\",\"FIRMWARE_RESTART\":\"Restart firmware, host, and reload config\"}}"),
                "/printer/objects/query" when query.Contains("heater_bed", StringComparison.OrdinalIgnoreCase) =>
                    JsonResponse("""{"result":{"status":{"heater_bed":{"temperature":59.5,"target":60},"extruder":{"temperature":214.9,"target":215}}}}"""),
                "/printer/objects/query" when query.Contains("gcode_move", StringComparison.OrdinalIgnoreCase) =>
                    JsonResponse("""{"result":{"status":{"gcode_move":{"position":[1,2,3],"speed":100,"extrude_factor":1.0},"fan":{"speed":1}}}}"""),
                "/printer/objects/query" when query.Contains("print_stats", StringComparison.OrdinalIgnoreCase) =>
                    JsonResponse("""{"result":{"status":{"print_stats":{"state":"printing"}}}}"""),
                "/server/files/list" => JsonResponse("""{"result":[{"path":"gcodes/test.gcode","modified":1700000000,"size":1234,"permissions":"rw"}]}"""),
                "/printer/gcode/script" => new HttpResponseMessage(HttpStatusCode.OK),
                "/printer/print/start" => new HttpResponseMessage(HttpStatusCode.OK),
                _ => new HttpResponseMessage(HttpStatusCode.NotFound)
            };
        };
    }

    private static PrinterConnectionSettings BuildSettings() =>
        new(new ApiConnectionSettings("http://moonraker.local", string.Empty, string.Empty), PrinterConnectionType.Moonraker);

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private sealed class FakeMoonrakerHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> responder;
        public List<string> RequestPaths { get; } = [];

        public FakeMoonrakerHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            this.responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestPaths.Add(request.RequestUri?.AbsoluteUri ?? string.Empty);
            return Task.FromResult(responder(request));
        }
    }
}
