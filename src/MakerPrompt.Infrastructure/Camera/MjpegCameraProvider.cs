using MakerPrompt.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace MakerPrompt.Infrastructure.Camera;

/// <summary>
/// Camera provider that reads a single JPEG frame from an MJPEG HTTP stream.
///
/// Compatibility
/// -------------
/// Works with any device that exposes a standard MJPEG endpoint, including:
/// - OctoPrint (e.g. http://printer:8080/?action=snapshot)
/// - Mainsail / Fluidd webcam streams
/// - Generic USB webcams served via mjpg-streamer
/// - Any IP camera that exposes an MJPEG endpoint
///
/// The provider captures a single frame per <see cref="CaptureSnapshotAsync"/> call
/// by reading only the first JPEG segment of the multipart MJPEG stream.
/// </summary>
public sealed class MjpegCameraProvider : ICameraProvider
{
    private static readonly HttpClient SharedClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    private readonly string _streamUrl;
    private readonly ILogger<MjpegCameraProvider> _logger;

    /// <inheritdoc />
    public string CameraId { get; }

    /// <inheritdoc />
    public string Label { get; }

    /// <inheritdoc />
    public bool IsAvailable { get; private set; }

    /// <param name="cameraId">Printer/camera ID this feed belongs to.</param>
    /// <param name="label">Human-readable label for the camera.</param>
    /// <param name="streamUrl">
    ///   MJPEG snapshot URL (e.g. http://printer:8080/?action=snapshot).
    ///   May be a snapshot endpoint (returns a single JPEG) or a live MJPEG
    ///   stream (the provider will extract the first frame automatically).
    /// </param>
    /// <param name="logger">Logger.</param>
    public MjpegCameraProvider(
        string cameraId,
        string label,
        string streamUrl,
        ILogger<MjpegCameraProvider> logger)
    {
        CameraId = cameraId;
        Label = label;
        _streamUrl = streamUrl;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<byte[]> CaptureSnapshotAsync(CancellationToken cancellationToken = default)
    {
        if (!IsAvailable) return [];

        try
        {
            using var response = await SharedClient.GetAsync(
                _streamUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            response.EnsureSuccessStatusCode();

            var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;

            if (contentType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase) ||
                contentType.Equals("image/jpg", StringComparison.OrdinalIgnoreCase))
            {
                // Snapshot endpoint — response is already a single JPEG.
                return await response.Content.ReadAsByteArrayAsync(cancellationToken);
            }
            else if (contentType.StartsWith("multipart/x-mixed-replace", StringComparison.OrdinalIgnoreCase))
            {
                // MJPEG stream — extract the first JPEG frame.
                return await ExtractFirstMjpegFrameAsync(response, cancellationToken);
            }
            else
            {
                _logger.LogWarning(
                    "[CameraProvider:{CameraId}] Unexpected content-type: {ContentType}",
                    CameraId, contentType);
                return [];
            }
        }
        catch (OperationCanceledException)
        {
            return [];
        }
        catch (Exception ex)
        {
            // Swallow capture errors — camera may be temporarily unavailable.
            _logger.LogDebug(ex, "[CameraProvider:{CameraId}] Snapshot capture failed", CameraId);
            IsAvailable = false;
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<bool> CheckAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, _streamUrl);
            using var response = await SharedClient.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            IsAvailable = response.IsSuccessStatusCode;
        }
        catch
        {
            IsAvailable = false;
        }

        return IsAvailable;
    }

    // ── MJPEG frame extraction ────────────────────────────────────────────────

    private static async Task<byte[]> ExtractFirstMjpegFrameAsync(
        HttpResponseMessage response, CancellationToken ct)
    {
        // MJPEG multipart boundary is declared in the Content-Type header.
        // e.g. multipart/x-mixed-replace;boundary=--myboundary
        // We scan for the JPEG SOI marker (0xFF 0xD8) and EOF marker (0xFF 0xD9).
        await using var stream = await response.Content.ReadAsStreamAsync(ct);

        using var ms = new MemoryStream();
        var buf = new byte[8192];
        bool inJpeg = false;
        int soi0 = -1;

        while (true)
        {
            ct.ThrowIfCancellationRequested();
            int read = await stream.ReadAsync(buf, ct);
            if (read == 0) break;

            if (!inJpeg)
            {
                for (int i = 0; i < read - 1; i++)
                {
                    if (buf[i] == 0xFF && buf[i + 1] == 0xD8)
                    {
                        soi0 = i;
                        inJpeg = true;
                        ms.Write(buf, i, read - i);
                        break;
                    }
                }
            }
            else
            {
                ms.Write(buf, 0, read);

                // Check for EOI marker (0xFF 0xD9).
                var data = ms.GetBuffer();
                var len = (int)ms.Length;
                for (int i = len - 2; i >= Math.Max(0, len - read - 2); i--)
                {
                    if (data[i] == 0xFF && data[i + 1] == 0xD9)
                        return ms.ToArray()[..(i + 2)];
                }
            }

            // Safety valve: don't buffer more than 5 MB.
            if (ms.Length > 5 * 1024 * 1024)
                break;
        }

        return ms.Length > 0 ? ms.ToArray() : [];
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
