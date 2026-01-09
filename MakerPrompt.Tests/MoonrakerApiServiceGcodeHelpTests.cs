using System.Net;
using System.Text;
using MakerPrompt.Shared.Services;
using MakerPrompt.Shared.Utils;
using static MakerPrompt.Shared.Utils.Enums;

namespace MakerPrompt.Tests;

public class MoonrakerApiServiceGcodeHelpTests
{
    [Fact]
    public async Task GetGcodeHelpAsync_ReturnsParsedCommands()
    {
        var handler = new FakeMoonrakerHelpHandler();
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

    private sealed class FakeMoonrakerHelpHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            return Task.FromResult(path switch
            {
                "/printer/info" => JsonResponse("{\"result\":{\"state\":\"ready\"}}"),
                "/printer/gcode/help" => JsonResponse("{\"RESTART\":\"Reload config file and restart host software\",\"FIRMWARE_RESTART\":\"Restart firmware, host, and reload config\"}"),
                _ => new HttpResponseMessage(HttpStatusCode.NotFound)
            });
        }

        private static HttpResponseMessage JsonResponse(string json) =>
            new(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
    }
}
