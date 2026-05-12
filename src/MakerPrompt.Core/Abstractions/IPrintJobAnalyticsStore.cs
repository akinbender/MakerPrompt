using MakerPrompt.Core.Models;

namespace MakerPrompt.Core.Abstractions;

/// <summary>
/// Stores and queries print-job analytics records.
/// </summary>
public interface IPrintJobAnalyticsStore
{
    Task<IReadOnlyList<PrintJobUsageRecord>> GetAllAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(PrintJobUsageRecord record, CancellationToken cancellationToken = default);

    /// <summary>Returns records for a specific printer, ordered newest-first.</summary>
    Task<IReadOnlyList<PrintJobUsageRecord>> GetByPrinterAsync(
        Guid printerId, CancellationToken cancellationToken = default);

    /// <summary>Returns records for a specific spool, ordered newest-first.</summary>
    Task<IReadOnlyList<PrintJobUsageRecord>> GetBySpoolAsync(
        Guid spoolId, CancellationToken cancellationToken = default);
}
