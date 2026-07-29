using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using MakerPrompt.Core.Abstractions;
using MakerPrompt.Core.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MakerPrompt.Tests.Integration;

public sealed class CloudApiTests : IClassFixture<CloudApiFactory>
{
    private readonly CloudApiFactory _factory;

    public CloudApiTests(CloudApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Health_DoesNotRequireAuthentication()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TelemetryIngest_RejectsMissingAndInvalidTokens()
    {
        using var client = _factory.CreateClient();
        var telemetry = new PrinterTelemetry();

        using var missing = await client.PostAsJsonAsync(
            "/api/telemetry/printer-1",
            telemetry);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "wrong-token");
        using var invalid = await client.PostAsJsonAsync(
            "/api/telemetry/printer-1",
            telemetry);

        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);
    }

    [Fact]
    public async Task TelemetryIngest_ApiKeyPersistsAndMarksStaleTelemetryOffline()
    {
        using var client = _factory.CreateAuthenticatedClient();
        var telemetry = new PrinterTelemetry
        {
            Status = PrinterStatus.Printing,
            CapturedAt = DateTimeOffset.UtcNow.AddMinutes(-2),
        };

        using var response = await client.PostAsJsonAsync(
            "/api/telemetry/printer-stale",
            telemetry);
        var store = _factory.Services.GetRequiredService<ITelemetryStore>();
        var stored = await store.GetLatestAsync("printer-stale");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.NotNull(stored);
        Assert.Equal(PrinterStatus.Disconnected, stored.Status);
    }

    [Fact]
    public async Task CameraIngest_ValidatesAndPersistsJpeg()
    {
        using var client = _factory.CreateAuthenticatedClient();
        var invalid = new CameraSnapshot
        {
            CameraId = "ignored",
            JpegData = [1, 2, 3],
        };
        var valid = new CameraSnapshot
        {
            CameraId = "ignored",
            Label = "Toolhead",
            JpegData = [0xFF, 0xD8, 0x00, 0xFF, 0xD9],
        };

        using var invalidResponse = await client.PostAsJsonAsync(
            "/api/camera/camera-1/snapshot",
            invalid);
        using var validResponse = await client.PostAsJsonAsync(
            "/api/camera/camera-1/snapshot",
            valid);
        var store = _factory.Services.GetRequiredService<ICameraSnapshotStore>();
        var stored = await store.GetLatestAsync("camera-1");

        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, validResponse.StatusCode);
        Assert.NotNull(stored);
        Assert.Equal("camera-1", stored.CameraId);
        Assert.Equal(valid.JpegData, stored.JpegData);
    }
}

public sealed class CloudApiFactory : WebApplicationFactory<Program>
{
    public const string ApiKey = "integration-test-key";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            var hash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(ApiKey)));
            configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["MakerPrompt:Auth:AgentApiKeyHash"] = hash,
                    ["MakerPrompt:Storage:Provider"] = "InMemory",
                    ["MakerPrompt:UseHttpsRedirection"] = "false",
                });
        });
    }

    public HttpClient CreateAuthenticatedClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", ApiKey);
        return client;
    }
}
