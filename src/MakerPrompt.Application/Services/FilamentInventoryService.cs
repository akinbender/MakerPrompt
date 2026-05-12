using MakerPrompt.Core.Abstractions;
using MakerPrompt.Core.Models;
using Microsoft.Extensions.Logging;

namespace MakerPrompt.Application.Services;

/// <summary>
/// Application service for filament spool inventory management.
///
/// Wraps <see cref="IFilamentInventoryStore"/> with business rules:
/// - Deduction clamping (never negative)
/// - Event publication on any change
/// - Aggregation helpers (total filament consumed per spool)
/// </summary>
public sealed class FilamentInventoryService
{
    private readonly IFilamentInventoryStore _store;
    private readonly ILogger<FilamentInventoryService> _logger;

    /// <summary>Raised whenever the inventory is modified.</summary>
    public event EventHandler? InventoryChanged;

    public FilamentInventoryService(IFilamentInventoryStore store, ILogger<FilamentInventoryService> logger)
    {
        _store = store;
        _logger = logger;
    }

    public Task<IReadOnlyList<FilamentSpool>> GetSpoolsAsync(CancellationToken cancellationToken = default)
        => _store.GetAllAsync(cancellationToken);

    public Task<FilamentSpool?> GetSpoolAsync(Guid id, CancellationToken cancellationToken = default)
        => _store.GetByIdAsync(id, cancellationToken);

    public async Task AddSpoolAsync(FilamentSpool spool, CancellationToken cancellationToken = default)
    {
        await _store.SaveAsync(spool, cancellationToken);
        OnInventoryChanged();
    }

    public async Task UpdateSpoolAsync(FilamentSpool spool, CancellationToken cancellationToken = default)
    {
        await _store.SaveAsync(spool, cancellationToken);
        OnInventoryChanged();
    }

    public async Task DeleteSpoolAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _store.DeleteAsync(id, cancellationToken);
        OnInventoryChanged();
    }

    /// <summary>
    /// Deducts consumed filament from the spool.
    /// Logs a warning if the spool is not found.
    /// </summary>
    public async Task DeductFilamentAsync(Guid spoolId, double grams, CancellationToken cancellationToken = default)
    {
        var spool = await _store.GetByIdAsync(spoolId, cancellationToken);
        if (spool is null)
        {
            _logger.LogWarning("DeductFilament: spool {SpoolId} not found", spoolId);
            return;
        }

        await _store.DeductFilamentAsync(spoolId, grams, cancellationToken);
        OnInventoryChanged();
    }

    private void OnInventoryChanged() => InventoryChanged?.Invoke(this, EventArgs.Empty);
}
