using System.Net;
using System.Text;
using MakerPrompt.Shared.Models;
using MakerPrompt.Shared.Services;

namespace MakerPrompt.Tests;

public class PrusaConnectProviderTests
{
    private const string TestToken = "test-bearer-token";

    [Fact]
    public async Task GetPrintersAsync_ReturnsMapping_FromRootArray()
    {
        var provider = new PrusaConnectProvider(new FakeHandler(DefaultResponder()));
        provider.Configure(TestToken);

        var printers = await provider.GetPrintersAsync();

        Assert.Equal(2, printers.Count);
        Assert.Contains(printers, p => p.Id   == "uuid-001" &&
                                       p.Name  == "MK4 Alpha" &&
                                       p.Model == "MK4" &&
                                       p.Status == "IDLE");
        Assert.Contains(printers, p => p.Id   == "uuid-002" &&
                                       p.Name  == "Mini Beta" &&
                                       p.Model == "MINI" &&
                                       p.Status == "PRINTING");
    }

    [Fact]
    public async Task GetPrintersAsync_ReturnsMapping_FromNestedPrintersProperty()
    {
        const string json = """
            {
              "printers": [
                {"uuid":"p1","name":"Wrapped","printer_type":"MK3S","state":"IDLE"}
              ]
            }
            """;
        var provider = new PrusaConnectProvider(new FakeHandler(_ => JsonResponse(json)));
        provider.Configure(TestToken);

        var printers = await provider.GetPrintersAsync();

        Assert.Single(printers);
        Assert.Equal("p1",      printers[0].Id);
        Assert.Equal("Wrapped", printers[0].Name);
        Assert.Equal("MK3S",    printers[0].Model);
    }

    [Fact]
    public async Task GetPrintersAsync_ReturnsEmpty_WhenResponseIsNotSuccess()
    {
        var provider = new PrusaConnectProvider(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)));
        provider.Configure(TestToken);

        var printers = await provider.GetPrintersAsync();

        Assert.Empty(printers);
    }

    [Fact]
    public async Task GetPrintersAsync_ReturnsEmpty_WithoutConfigure()
    {
        // No Configure() call → no auth header → 401 from real server.
        // We simulate a 401 here too since no token was set.
        var provider = new PrusaConnectProvider(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)));

        var printers = await provider.GetPrintersAsync();

        Assert.Empty(printers);
    }

    [Fact]
    public async Task GetPrintersAsync_SkipsEntriesWithoutUuid()
    {
        const string json = """
            [
              {"name":"No UUID","printer_type":"MK4","state":"IDLE"},
              {"uuid":"valid-uuid","name":"Has UUID","printer_type":"MK4","state":"IDLE"}
            ]
            """;
        var provider = new PrusaConnectProvider(new FakeHandler(_ => JsonResponse(json)));
        provider.Configure(TestToken);

        var printers = await provider.GetPrintersAsync();

        Assert.Single(printers);
        Assert.Equal("valid-uuid", printers[0].Id);
    }

    [Fact]
    public async Task GetPrintersAsync_ReturnsEmpty_OnNetworkError()
    {
        var provider = new PrusaConnectProvider(new ThrowingHandler());
        provider.Configure(TestToken);

        var printers = await provider.GetPrintersAsync();

        Assert.Empty(printers);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static Func<HttpRequestMessage, HttpResponseMessage> DefaultResponder() =>
        _ => JsonResponse("""
            [
              {"uuid":"uuid-001","name":"MK4 Alpha","printer_type":"MK4","state":"IDLE"},
              {"uuid":"uuid-002","name":"Mini Beta","printer_type":"MINI","state":"PRINTING"}
            ]
            """);

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken _)
            => Task.FromResult(responder(request));
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken _)
            => Task.FromException<HttpResponseMessage>(new HttpRequestException("Network error"));
    }
}
