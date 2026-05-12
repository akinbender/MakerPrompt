namespace MakerPrompt.Core.Models;

/// <summary>
/// A single camera frame snapshot — JPEG bytes plus metadata.
/// Forwarded from an EdgeAgent to the Cloud API alongside telemetry.
/// </summary>
public sealed class CameraSnapshot
{
    /// <summary>
    /// Unique identifier of the camera (typically matches a printer ID so snapshots
    /// can be correlated with telemetry).
    /// </summary>
    public string CameraId { get; set; } = string.Empty;

    /// <summary>Human-readable camera label.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Raw JPEG image data.</summary>
    public byte[] JpegData { get; set; } = [];

    /// <summary>UTC timestamp when the frame was captured.</summary>
    public DateTimeOffset CapturedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Image width in pixels (0 if unknown).</summary>
    public int Width { get; set; }

    /// <summary>Image height in pixels (0 if unknown).</summary>
    public int Height { get; set; }
}
