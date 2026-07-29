using MakerPrompt.Infrastructure.Utils;
using Microsoft.Extensions.Logging.Abstractions;

namespace MakerPrompt.Tests.Unit.Infrastructure;

public sealed class HttpEdgeAgentClientTests
{
    [Fact]
    public async Task SendTelemetry_ReturnsTrueWhenCloudAcceptsSnapshot()
    {
        HttpRequestMessage? captured = null;
        var handler = new DelegateHandler(request =>
        {
            captured = request;
            return new HttpResponseMessage(HttpStatusCode.Accepted);
        });
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://cloud.test/"),
        };
        var client = new HttpEdgeAgentClient(
            httpClient,
            NullLogger<HttpEdgeAgentClient>.Instance);

        var accepted = await client.SendTelemetryAsync(
            "printer one",
            new PrinterTelemetry());

        Assert.True(accepted);
        Assert.Equal(
            "https://cloud.test/api/telemetry/printer%20one",
            captured!.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task SendCamera_ReturnsFalseWhenCloudRejectsSnapshot()
    {
        var handler = new DelegateHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.Unauthorized));
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://cloud.test/"),
        };
        var client = new HttpEdgeAgentClient(
            httpClient,
            NullLogger<HttpEdgeAgentClient>.Instance);

        var accepted = await client.SendCameraSnapshotAsync(new CameraSnapshot
        {
            CameraId = "camera-1",
            JpegData = [0xFF, 0xD8, 0xFF, 0xD9],
        });

        Assert.False(accepted);
    }

    [Fact]
    public async Task SendTelemetry_ReturnsFalseOnNetworkFailure()
    {
        using var httpClient = new HttpClient(new ThrowingHandler())
        {
            BaseAddress = new Uri("https://cloud.test/"),
        };
        var client = new HttpEdgeAgentClient(
            httpClient,
            NullLogger<HttpEdgeAgentClient>.Instance);

        var accepted = await client.SendTelemetryAsync(
            "printer-1",
            new PrinterTelemetry());

        Assert.False(accepted);
    }

    private sealed class DelegateHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(responseFactory(request));
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(
                new HttpRequestException("offline"));
    }
}
