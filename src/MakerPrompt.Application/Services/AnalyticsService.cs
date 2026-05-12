using MakerPrompt.Core.Abstractions;
using MakerPrompt.Core.Models;
using Microsoft.Extensions.Logging;

namespace MakerPrompt.Application.Services;

/// <summary>
/// Application service for print-job analytics.
///
/// Provides aggregation helpers on top of <see cref="IPrintJobAnalyticsStore"/>:
/// - Total print hours
/// - Total filament consumed (all printers / by printer / by spool)
/// </summary>
public sealed class AnalyticsService
{
    private readonly IPrintJobAnalyticsStore _store;
    private readonly ILogger<AnalyticsService> _logger;

    /// <summary>Raised whenever a new usage record is added.</summary>
    public event EventHandler? AnalyticsUpdated;

    public AnalyticsService(IPrintJobAnalyticsStore store, ILogger<AnalyticsService> logger)
    {
        _store = store;
        _logger = logger;
    }

    public Task<IReadOnlyList<PrintJobUsageRecord>> GetRecordsAsync(CancellationToken cancellationToken = default)
        => _store.GetAllAsync(cancellationToken);

    public async Task RecordUsageAsync(PrintJobUsageRecord record, CancellationToken cancellationToken = default)
    {
        try
        {
            await _store.SaveAsync(record, cancellationToken);
            AnalyticsUpdated?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record print job usage");
        }
    }

    // ── Aggregations ─────────────────────────────────────────────────────────

    public async Task<TimeSpan> GetTotalPrintTimeAsync(CancellationToken cancellationToken = default)
    {
        var records = await _store.GetAllAsync(cancellationToken);
        return TimeSpan.FromTicks(records.Sum(r => r.Duration.Ticks));
    }

    public async Task<double> GetTotalFilamentConsumedGramsAsync(CancellationToken cancellationToken = default)
    {
        var records = await _store.GetAllAsync(cancellationToken);
        return records.Sum(r => r.EffectiveFilamentGrams);
    }

    public async Task<double> GetFilamentConsumedByPrinterAsync(Guid printerId, CancellationToken cancellationToken = default)
    {
        var records = await _store.GetByPrinterAsync(printerId, cancellationToken);
        return records.Sum(r => r.EffectiveFilamentGrams);
    }

    public async Task<double> GetFilamentConsumedBySpoolAsync(Guid spoolId, CancellationToken cancellationToken = default)
    {
        var records = await _store.GetBySpoolAsync(spoolId, cancellationToken);
        return records.Sum(r => r.EffectiveFilamentGrams);
    }
}
