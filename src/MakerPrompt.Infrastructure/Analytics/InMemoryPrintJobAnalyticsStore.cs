using MakerPrompt.Core.Abstractions;
using MakerPrompt.Core.Models;

namespace MakerPrompt.Infrastructure.Analytics;

/// <summary>
/// Thread-safe, in-memory implementation of <see cref="IPrintJobAnalyticsStore"/>.
/// </summary>
public sealed class InMemoryPrintJobAnalyticsStore : IPrintJobAnalyticsStore
{
    private readonly List<PrintJobUsageRecord> _records = [];
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<IReadOnlyList<PrintJobUsageRecord>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try { return _records.AsReadOnly(); }
        finally { _lock.Release(); }
    }

    public async Task SaveAsync(PrintJobUsageRecord record, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            // Replace if already present, otherwise append.
            var idx = _records.FindIndex(r => r.Id == record.Id);
            if (idx >= 0) _records[idx] = record;
            else _records.Add(record);
        }
        finally { _lock.Release(); }
    }

    public async Task<IReadOnlyList<PrintJobUsageRecord>> GetByPrinterAsync(
        Guid printerId, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            return _records
                .Where(r => r.PrinterId == printerId)
                .OrderByDescending(r => r.Timestamp)
                .ToList()
                .AsReadOnly();
        }
        finally { _lock.Release(); }
    }

    public async Task<IReadOnlyList<PrintJobUsageRecord>> GetBySpoolAsync(
        Guid spoolId, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            return _records
                .Where(r => r.FilamentSpoolId == spoolId)
                .OrderByDescending(r => r.Timestamp)
                .ToList()
                .AsReadOnly();
        }
        finally { _lock.Release(); }
    }
}
