using MakerPrompt.Core.Models;

namespace MakerPrompt.Core.Abstractions;

/// <summary>
/// Persists and retrieves telemetry snapshots for reporting, analytics, and the
/// cloud backend. Implementations may write to a local SQLite database (EdgeAgent),
/// an in-memory store (tests), or a remote REST API (Cloud-side projections).
/// </summary>
public interface ITelemetryStore
{
    /// <summary>
    /// Persists a telemetry snapshot under <paramref name="printerId"/>. The
    /// snapshot's capture timestamp is supplied by the producer.
    /// </summary>
    Task SaveAsync(string printerId, PrinterTelemetry telemetry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the most recent telemetry snapshot for the given printer,
    /// or <c>null</c> if no snapshot has been stored yet.
    /// </summary>
    Task<PrinterTelemetry?> GetLatestAsync(string printerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns up to <paramref name="count"/> telemetry snapshots for the given printer,
    /// ordered from most-recent to oldest.
    /// </summary>
    Task<IReadOnlyList<PrinterTelemetry>> GetHistoryAsync(
        string printerId, int count = 100, CancellationToken cancellationToken = default);
}
