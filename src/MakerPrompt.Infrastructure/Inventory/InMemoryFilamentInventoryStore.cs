using MakerPrompt.Core.Abstractions;
using MakerPrompt.Core.Models;

namespace MakerPrompt.Infrastructure;

/// <summary>
/// Thread-safe, in-memory implementation of <see cref="IFilamentInventoryStore"/>.
/// Suitable for unit tests and development; does not survive process restart.
/// </summary>
public sealed class InMemoryFilamentInventoryStore : IFilamentInventoryStore
{
    private readonly Dictionary<Guid, FilamentSpool> _spools = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<IReadOnlyList<FilamentSpool>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try { return _spools.Values.ToList().AsReadOnly(); }
        finally { _lock.Release(); }
    }

    public async Task<FilamentSpool?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try { return _spools.GetValueOrDefault(id); }
        finally { _lock.Release(); }
    }

    public async Task SaveAsync(FilamentSpool spool, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try { _spools[spool.Id] = spool; }
        finally { _lock.Release(); }
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try { _spools.Remove(id); }
        finally { _lock.Release(); }
    }

    public async Task DeductFilamentAsync(Guid spoolId, double grams, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_spools.TryGetValue(spoolId, out var spool))
                spool.RemainingWeightGrams = Math.Max(0, spool.RemainingWeightGrams - grams);
        }
        finally { _lock.Release(); }
    }
}
