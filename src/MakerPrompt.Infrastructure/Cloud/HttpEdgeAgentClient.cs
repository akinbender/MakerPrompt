using System.Net.Http.Json;
using MakerPrompt.Core.Abstractions;
using MakerPrompt.Core.Models;
using Microsoft.Extensions.Logging;

namespace MakerPrompt.Infrastructure.Cloud;

/// <summary>
/// Pushes telemetry snapshots to the MakerPrompt Cloud backend over HTTP/JSON.
///
/// The <see cref="HttpClient"/> passed in must be pre-configured with:
///   • <c>BaseAddress</c> — the cloud base URL (e.g. http://localhost:8080).
///   • <c>DefaultRequestHeaders.Authorization</c> — Bearer token.
///
/// All network failures are swallowed at Debug level so they never crash the
/// EdgeAgent polling loop.
/// </summary>
public sealed class HttpEdgeAgentClient : IEdgeAgentClient
{
    private readonly HttpClient _http;
    private readonly ILogger<HttpEdgeAgentClient> _logger;

    public HttpEdgeAgentClient(HttpClient http, ILogger<HttpEdgeAgentClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task SendTelemetryAsync(
        string printerId,
        PrinterTelemetry telemetry,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _http.PostAsJsonAsync(
                $"api/telemetry/{Uri.EscapeDataString(printerId)}",
                telemetry,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug(
                    "Cloud rejected telemetry for {PrinterId}: HTTP {StatusCode}",
                    printerId, (int)response.StatusCode);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "Cloud telemetry push failed for printer {PrinterId}", printerId);
        }
    }

    /// <inheritdoc />
    public async Task<bool> IsReachableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _http.GetAsync("health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "Cloud health check failed");
            return false;
        }
    }
}
