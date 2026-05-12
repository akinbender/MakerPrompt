using MakerPrompt.Core.Abstractions;
using MakerPrompt.Core.Models;
using Microsoft.Extensions.Logging;

namespace MakerPrompt.Application.Services;

/// <summary>
/// Aggregates telemetry from multiple printer connections and forwards snapshots
/// to a <see cref="ITelemetryStore"/> for persistence and cloud forwarding.
///
/// This service is the Application-layer glue between the live
/// <see cref="PrinterFleetService"/> and the storage / cloud pipeline:
///
///   [PrinterFleetService] --(telemetry events)--> [TelemetryAggregationService]
///                                                        |
///                                                 [ITelemetryStore]
///                                                        |
///                                               (Cloud / SQLite / etc.)
/// </summary>
public sealed class TelemetryAggregationService : IDisposable
{
    private readonly PrinterFleetService _fleet;
    private readonly ITelemetryStore _store;
    private readonly ILogger<TelemetryAggregationService> _logger;
    private bool _disposed;

    public TelemetryAggregationService(
        PrinterFleetService fleet,
        ITelemetryStore store,
        ILogger<TelemetryAggregationService> logger)
    {
        _fleet = fleet;
        _store = store;
        _logger = logger;

        _fleet.FleetChanged += OnFleetChanged;
    }

    private void OnFleetChanged(object? sender, EventArgs e)
    {
        var snapshot = _fleet.GetFleetTelemetry();
        _ = PersistSnapshotAsync(snapshot);
    }

    private async Task PersistSnapshotAsync(IReadOnlyDictionary<string, PrinterTelemetry> snapshot)
    {
        foreach (var (printerId, telemetry) in snapshot)
        {
            try
            {
                await _store.SaveAsync(printerId, telemetry);
            }
            catch (Exception ex)
            {
                // Telemetry persistence errors are swallowed — never spam logs.
                _logger.LogDebug(ex, "Failed to persist telemetry for {PrinterId}", printerId);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _fleet.FleetChanged -= OnFleetChanged;
        _disposed = true;
    }
}
