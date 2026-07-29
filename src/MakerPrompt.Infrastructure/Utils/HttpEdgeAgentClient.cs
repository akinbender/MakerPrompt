using System.Net.Http.Json;

namespace MakerPrompt.Infrastructure.Utils;

/// <summary>
/// Pushes telemetry snapshots to the MakerPrompt Cloud backend over HTTP/JSON.
///
/// The <see cref="HttpClient"/> passed in must be pre-configured with:
///   • <c>BaseAddress</c> — the cloud base URL (e.g. http://localhost:8080).
///   • <c>DefaultRequestHeaders.Authorization</c> — Bearer token.
///
/// Transient network failures are reported to the caller without escaping the
/// EdgeAgent polling loop.
/// </summary>
public sealed class HttpEdgeAgentClient(HttpClient http, ILogger<HttpEdgeAgentClient> logger) : IEdgeAgentClient
{
    private readonly HttpClient _http = http;
    private readonly ILogger<HttpEdgeAgentClient> _logger = logger;

    /// <inheritdoc />
    public async Task<bool> SendTelemetryAsync(
        string printerId,
        PrinterTelemetry telemetry,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(printerId);
        ArgumentNullException.ThrowIfNull(telemetry);

        try
        {
            using var response = await _http.PostAsJsonAsync(
                $"api/telemetry/{Uri.EscapeDataString(printerId)}",
                telemetry,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug(
                    "Cloud rejected telemetry for {PrinterId}: HTTP {StatusCode}",
                    printerId, (int)response.StatusCode);
            }

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "Cloud telemetry push failed for printer {PrinterId}", printerId);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> SendCameraSnapshotAsync(
        CameraSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshot.CameraId);

        try
        {
            using var response = await _http.PostAsJsonAsync(
                $"api/camera/{Uri.EscapeDataString(snapshot.CameraId)}/snapshot",
                snapshot,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug(
                    "Cloud rejected camera snapshot for {CameraId}: HTTP {StatusCode}",
                    snapshot.CameraId,
                    (int)response.StatusCode);
            }

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(
                ex,
                "Cloud camera push failed for camera {CameraId}",
                snapshot.CameraId);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> IsReachableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _http.GetAsync("health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "Cloud health check failed");
            return false;
        }
    }
}
