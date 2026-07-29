using PrinterStatus = MakerPrompt.Core.Models.PrinterStatus;

namespace MakerPrompt.Tests.Unit.Services.Printers;

public class OctoPrintApiServiceTests
{
    [Fact]
    public async Task ConnectAsync_SucceedsAndSetsConnectionName()
    {
        var handler = new FakeOctoPrintHandler(BuildDefaultResponses());
        var service = new OctoPrintApiService(handler);

        var connected = await service.ConnectAsync(BuildSettings());
        service.updateTimer.Stop();

        Assert.True(connected);
        Assert.True(service.IsConnected);
        Assert.Equal("OctoPrint 1.9.0", service.ConnectionName);
    }

    [Fact]
    public async Task GetPrinterTelemetryAsync_MapsTemperaturesAndStatus()
    {
        var handler = new FakeOctoPrintHandler(BuildDefaultResponses());
        var service = new OctoPrintApiService(handler);
        await service.ConnectAsync(BuildSettings());
        service.updateTimer.Stop();

        var telemetry = await service.GetTelemetryAsync();

        Assert.Equal(200.0, telemetry.HotendTemp, 1);
        Assert.Equal(210.0, telemetry.HotendTarget, 1);
        Assert.Equal(55.0, telemetry.BedTemp, 1);
        Assert.Equal(60.0, telemetry.BedTarget, 1);
        Assert.Equal(PrinterStatus.Printing, telemetry.Status);
    }

    [Fact]
    public async Task GetFilesAsync_ReturnsAllRecursiveMachineCodeFiles()
    {
        var handler = new FakeOctoPrintHandler(BuildDefaultResponses());
        var service = new OctoPrintApiService(handler);
        await service.ConnectAsync(BuildSettings());
        service.updateTimer.Stop();

        var files = await service.GetFilesAsync();

        Assert.Equal(2, files.Count);
        Assert.Contains(files, file => file.FullPath == "root.gcode");
        Assert.Contains(files, file => file.FullPath == "folder/nested.gcode");
        Assert.All(files, file => Assert.True(file.Size > 0));
    }

    [Fact]
    public async Task WriteDataAsync_PostsToCommandEndpoint()
    {
        var handler = new FakeOctoPrintHandler(BuildDefaultResponses());
        var service = new OctoPrintApiService(handler);
        await service.ConnectAsync(BuildSettings());
        service.updateTimer.Stop();

        await service.WriteDataAsync("M105");

        Assert.Contains(handler.RequestPaths, p => p.Contains("/api/printer/command", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ConnectAsync_ServerReturnsError_ReturnsFalse()
    {
        var handler = new FakeOctoPrintHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var service = new OctoPrintApiService(handler);

        var connected = await service.ConnectAsync(BuildSettings());

        Assert.False(connected);
        Assert.False(service.IsConnected);
    }

    [Fact]
    public async Task ConnectAsync_AfterDisconnect_ReusesAdapterSafely()
    {
        var versionRequests = 0;
        var defaultResponder = BuildDefaultResponses();
        var service = new OctoPrintApiService(new FakeOctoPrintHandler(request =>
        {
            if (request.RequestUri?.AbsolutePath == "/api/version")
                versionRequests++;
            return defaultResponder(request);
        }));

        Assert.True(await service.ConnectAsync(BuildSettings()));
        await service.DisconnectAsync();
        Assert.True(await service.ConnectAsync(BuildSettings()));

        Assert.Equal(2, versionRequests);
    }

    private static Func<HttpRequestMessage, HttpResponseMessage> BuildDefaultResponses()
    {
        const string printerJson = """
            {
              "temperature": {
                "tool0": {"actual": 200.0, "target": 210.0},
                "bed":   {"actual": 55.0,  "target": 60.0}
              },
              "state": {
                "flags": {"printing": true, "pausing": false, "error": false}
              }
            }
            """;

        const string jobJson = """
            {"progress": {"completion": 42.0, "printTime": 300}, "state": "Printing"}
            """;

        const string filesJson = """
            {
              "files": [
                {"type": "machinecode", "path": "root.gcode", "size": 1234, "date": 1700000000},
                {"type": "folder", "children": [
                  {"type": "machinecode", "path": "folder/nested.gcode", "size": 5678}
                ]}
              ]
            }
            """;

        return request =>
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            return path switch
            {
                "/api/version"          => JsonResponse("""{"text":"OctoPrint 1.9.0"}"""),
                "/api/connection"       => JsonResponse("""{"current":{"state":"Operational"}}"""),
                "/api/printer"          => JsonResponse(printerJson),
                "/api/job"              => JsonResponse(jobJson),
                "/api/files"            => JsonResponse(filesJson),
                "/api/printer/command"  => new HttpResponseMessage(HttpStatusCode.NoContent),
                _                       => new HttpResponseMessage(HttpStatusCode.NotFound)
            };
        };
    }

    private static PrinterConnectionSettings BuildSettings() =>
        new() { ConnectionType = PrinterConnectionType.OctoPrint, ApiUrl = "http://octoprint.local", Password = "api-key" };

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private sealed class FakeOctoPrintHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
        public List<string> RequestPaths { get; } = [];

        public FakeOctoPrintHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
            => _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestPaths.Add(request.RequestUri?.AbsoluteUri ?? string.Empty);
            return Task.FromResult(_responder(request));
        }
    }
}
