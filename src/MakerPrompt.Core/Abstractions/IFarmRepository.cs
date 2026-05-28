using MakerPrompt.Core.Models;

namespace MakerPrompt.Core.Abstractions;

/// <summary>
/// Manages farm configurations — named groups of printer connections that
/// can be saved, switched, imported, and exported.
/// </summary>
public interface IFarmRepository
{
    Task<IReadOnlyList<FarmConfiguration>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<FarmConfiguration?> GetByIdAsync(Guid farmId, CancellationToken cancellationToken = default);

    Task SaveAsync(FarmConfiguration farm, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid farmId, CancellationToken cancellationToken = default);
}
