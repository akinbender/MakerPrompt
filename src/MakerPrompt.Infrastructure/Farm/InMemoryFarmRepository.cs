using MakerPrompt.Core.Abstractions;
using MakerPrompt.Core.Models;

namespace MakerPrompt.Infrastructure.Farm;

/// <summary>
/// Thread-safe, in-memory implementation of <see cref="IFarmRepository"/>.
/// </summary>
public sealed class InMemoryFarmRepository : IFarmRepository
{
    private readonly Dictionary<Guid, FarmConfiguration> _farms = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<IReadOnlyList<FarmConfiguration>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try { return _farms.Values.OrderBy(f => f.CreatedAt).ToList().AsReadOnly(); }
        finally { _lock.Release(); }
    }

    public async Task<FarmConfiguration?> GetByIdAsync(Guid farmId, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try { return _farms.GetValueOrDefault(farmId); }
        finally { _lock.Release(); }
    }

    public async Task SaveAsync(FarmConfiguration farm, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try { _farms[farm.Id] = farm; }
        finally { _lock.Release(); }
    }

    public async Task DeleteAsync(Guid farmId, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try { _farms.Remove(farmId); }
        finally { _lock.Release(); }
    }
}
