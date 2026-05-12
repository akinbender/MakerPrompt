using MakerPrompt.Core.Models;

namespace MakerPrompt.Core.Abstractions;

/// <summary>
/// Contract for the local EdgeAgent component that runs in the hackerspace / farm
/// and bridges printers to the cloud backend.
///
/// The EdgeAgent is responsible for:
///   • Connecting to configured printers via <see cref="IPrinterCommunicationService"/>.
///   • Polling telemetry on a regular interval.
///   • Forwarding telemetry snapshots to the cloud via <see cref="ICloudTelemetryClient"/>.
///   • Optionally capturing webcam snapshots.
/// </summary>
public interface IEdgeAgentClient
{
    /// <summary>
    /// Submits a telemetry snapshot to the cloud backend.
    /// Implementations should retry on transient failures and swallow permanent errors silently.
    /// </summary>
    Task SendTelemetryAsync(string printerId, PrinterTelemetry telemetry,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks connectivity to the cloud backend.
    /// </summary>
    Task<bool> IsReachableAsync(CancellationToken cancellationToken = default);
}
