using MakerPrompt.Core.Models;

namespace MakerPrompt.Core.Abstractions;

/// <summary>
/// Contract for the local EdgeAgent component that runs in the hackerspace / farm
/// and bridges printers to the cloud backend.
///
/// The EdgeAgent is responsible for:
///   • Connecting to configured printers via <see cref="IPrinterCommunicationService"/>.
///   • Polling telemetry on a regular interval.
///   • Forwarding telemetry and camera snapshots to the cloud.
///   • Optionally capturing webcam snapshots.
/// </summary>
public interface IEdgeAgentClient
{
    /// <summary>
    /// Submits a telemetry snapshot to the cloud backend.
    /// The return value reports whether the cloud accepted the snapshot. Periodic
    /// callers can retry naturally on their next polling cycle.
    /// </summary>
    Task<bool> SendTelemetryAsync(string printerId, PrinterTelemetry telemetry,
        CancellationToken cancellationToken = default);

    /// <summary>Submits a camera snapshot to the cloud backend.</summary>
    Task<bool> SendCameraSnapshotAsync(
        CameraSnapshot snapshot,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks connectivity to the cloud backend.
    /// </summary>
    Task<bool> IsReachableAsync(CancellationToken cancellationToken = default);
}
