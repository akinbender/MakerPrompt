using MakerPrompt.Core.Models;

namespace MakerPrompt.Core.Abstractions;

/// <summary>
/// Persists and retrieves filament spools for inventory tracking.
/// Implementations may use local JSON storage, a database, or a cloud API.
/// </summary>
public interface IFilamentInventoryStore
{
    Task<IReadOnlyList<FilamentSpool>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<FilamentSpool?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task SaveAsync(FilamentSpool spool, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deducts <paramref name="grams"/> from the spool's remaining weight.
    /// Clamps to zero — never goes negative.
    /// </summary>
    Task DeductFilamentAsync(Guid spoolId, double grams, CancellationToken cancellationToken = default);
}
