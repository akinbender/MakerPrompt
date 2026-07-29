namespace MakerPrompt.Core.Abstractions;

/// <summary>
/// Abstraction for a camera feed associated with a printer or a hackerspace bay.
///
/// Implementations may read from:
/// - an MJPEG HTTP stream (most webcams, OctoPrint, Mainsail)
/// - an RTSP stream (IP cameras)
/// - a local V4L2 device (Linux EdgeAgent)
///
/// The camera is identified by its <see cref="CameraId"/> which correlates snapshots
/// with the printer they are mounted next to (same ID as the printer it monitors).
/// </summary>
public interface ICameraProvider : IAsyncDisposable
{
    /// <summary>Unique identifier for this camera, typically matching a printer ID.</summary>
    string CameraId { get; }

    /// <summary>Human-readable label (e.g. "Ender-3 Webcam").</summary>
    string Label { get; }

    /// <summary>Whether the camera stream is currently reachable.</summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Captures a single JPEG snapshot from the camera stream.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Raw JPEG bytes, or an empty array if the camera is unavailable.</returns>
    Task<byte[]> CaptureSnapshotAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies the camera is reachable and updates <see cref="IsAvailable"/>.
    /// Called during EdgeAgent startup and periodically during health checks.
    /// </summary>
    Task<bool> CheckAvailabilityAsync(CancellationToken cancellationToken = default);
}
