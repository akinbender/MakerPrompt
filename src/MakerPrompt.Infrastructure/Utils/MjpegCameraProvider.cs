namespace MakerPrompt.Infrastructure.Utils;

/// <summary>
/// Captures one JPEG frame from either a snapshot endpoint or an MJPEG stream.
/// </summary>
public sealed class MjpegCameraProvider : ICameraProvider
{
    private const int MaxFrameBytes = 5 * 1024 * 1024;
    private static readonly HttpClient SharedClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10),
    };

    private readonly string _streamUrl;
    private readonly ILogger<MjpegCameraProvider> _logger;
    private readonly HttpClient _httpClient;

    public MjpegCameraProvider(
        string cameraId,
        string label,
        string streamUrl,
        ILogger<MjpegCameraProvider> logger)
        : this(cameraId, label, streamUrl, logger, SharedClient)
    {
    }

    internal MjpegCameraProvider(
        string cameraId,
        string label,
        string streamUrl,
        ILogger<MjpegCameraProvider> logger,
        HttpClient httpClient)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cameraId);
        ArgumentException.ThrowIfNullOrWhiteSpace(streamUrl);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(httpClient);

        if (!Uri.TryCreate(streamUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https"))
        {
            throw new ArgumentException(
                "Camera URL must be an absolute HTTP or HTTPS URL.",
                nameof(streamUrl));
        }

        CameraId = cameraId;
        Label = string.IsNullOrWhiteSpace(label) ? cameraId : label;
        _streamUrl = uri.AbsoluteUri;
        _logger = logger;
        _httpClient = httpClient;
    }

    /// <inheritdoc />
    public string CameraId { get; }

    /// <inheritdoc />
    public string Label { get; }

    /// <inheritdoc />
    public bool IsAvailable { get; private set; }

    /// <inheritdoc />
    public async Task<byte[]> CaptureSnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync(
                _streamUrl,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();

            var mediaType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            byte[] frame;
            if (mediaType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase) ||
                mediaType.Equals("image/jpg", StringComparison.OrdinalIgnoreCase))
            {
                frame = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                if (frame.Length > MaxFrameBytes || !IsJpeg(frame))
                {
                    _logger.LogWarning(
                        "Camera {CameraId} returned invalid JPEG data or exceeded {MaxFrameBytes} bytes",
                        CameraId,
                        MaxFrameBytes);
                    frame = [];
                }
            }
            else if (mediaType.StartsWith(
                         "multipart/x-mixed-replace",
                         StringComparison.OrdinalIgnoreCase))
            {
                frame = await ExtractFirstMjpegFrameAsync(response, cancellationToken);
            }
            else
            {
                _logger.LogWarning(
                    "Camera {CameraId} returned unsupported content type {ContentType}",
                    CameraId,
                    mediaType);
                frame = [];
            }

            IsAvailable = frame.Length > 0;
            return frame;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            IsAvailable = false;
            _logger.LogDebug("Camera {CameraId} capture timed out", CameraId);
            return [];
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            IsAvailable = false;
            _logger.LogDebug(ex, "Camera {CameraId} capture failed", CameraId);
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<bool> CheckAvailabilityAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var head = new HttpRequestMessage(HttpMethod.Head, _streamUrl);
            using var headResponse = await _httpClient.SendAsync(
                head,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (headResponse.StatusCode is
                System.Net.HttpStatusCode.MethodNotAllowed or
                System.Net.HttpStatusCode.NotImplemented)
            {
                using var getResponse = await _httpClient.GetAsync(
                    _streamUrl,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);
                IsAvailable = getResponse.IsSuccessStatusCode;
            }
            else
            {
                IsAvailable = headResponse.IsSuccessStatusCode;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            IsAvailable = false;
            _logger.LogDebug(ex, "Camera {CameraId} availability check failed", CameraId);
        }

        return IsAvailable;
    }

    internal static async Task<byte[]> ExtractFirstMjpegFrameAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        await using var stream =
            await response.Content.ReadAsStreamAsync(cancellationToken);
        using var frame = new MemoryStream();
        var buffer = new byte[8192];
        var inJpeg = false;
        byte? previous = null;

        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            for (var index = 0; index < read; index++)
            {
                var current = buffer[index];
                if (!inJpeg)
                {
                    if (previous == 0xFF && current == 0xD8)
                    {
                        frame.WriteByte(0xFF);
                        frame.WriteByte(0xD8);
                        inJpeg = true;
                    }
                }
                else
                {
                    frame.WriteByte(current);
                    if (previous == 0xFF && current == 0xD9)
                    {
                        return frame.ToArray();
                    }

                    if (frame.Length > MaxFrameBytes)
                    {
                        return [];
                    }
                }

                previous = current;
            }
        }

        return [];
    }

    private static bool IsJpeg(byte[] value) =>
        value is { Length: >= 4 } &&
        value[0] == 0xFF &&
        value[1] == 0xD8 &&
        value[^2] == 0xFF &&
        value[^1] == 0xD9;

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
