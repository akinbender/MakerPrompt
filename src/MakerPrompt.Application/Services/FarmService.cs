using MakerPrompt.Core.Abstractions;
using MakerPrompt.Core.Models;
using Microsoft.Extensions.Logging;

namespace MakerPrompt.Application.Services;

/// <summary>
/// Application service for managing farm configurations.
///
/// Responsibilities:
/// - CRUD for farm profiles.
/// - Switching the active farm (saving current printer state, loading the new one).
/// - Import / export as JSON.
/// </summary>
public sealed class FarmService
{
    private readonly IFarmRepository _repo;
    private readonly ILogger<FarmService> _logger;

    /// <summary>Raised whenever the list of farms changes.</summary>
    public event EventHandler? FarmsChanged;

    public FarmService(IFarmRepository repo, ILogger<FarmService> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    public Task<IReadOnlyList<FarmConfiguration>> GetFarmsAsync(CancellationToken cancellationToken = default)
        => _repo.GetAllAsync(cancellationToken);

    public Task<FarmConfiguration?> GetFarmAsync(Guid farmId, CancellationToken cancellationToken = default)
        => _repo.GetByIdAsync(farmId, cancellationToken);

    /// <summary>Creates a new farm profile with the given name.</summary>
    public async Task<FarmConfiguration> CreateFarmAsync(string name, CancellationToken cancellationToken = default)
    {
        var farm = new FarmConfiguration { Name = name.Trim() };
        await _repo.SaveAsync(farm, cancellationToken);
        OnFarmsChanged();
        return farm;
    }

    /// <summary>Updates the display name of a farm.</summary>
    public async Task RenameFarmAsync(Guid farmId, string newName, CancellationToken cancellationToken = default)
    {
        var farm = await _repo.GetByIdAsync(farmId, cancellationToken);
        if (farm is null)
        {
            _logger.LogWarning("RenameFarm: farm {FarmId} not found", farmId);
            return;
        }

        farm.Name = newName.Trim();
        await _repo.SaveAsync(farm, cancellationToken);
        OnFarmsChanged();
    }

    /// <summary>
    /// Saves a snapshot of the given printer definitions into the farm, then persists.
    /// Used when switching away from this farm to preserve its current printer list.
    /// </summary>
    public async Task SnapshotPrintersAsync(Guid farmId,
        IEnumerable<PrinterConnectionDefinition> currentPrinters,
        CancellationToken cancellationToken = default)
    {
        var farm = await _repo.GetByIdAsync(farmId, cancellationToken);
        if (farm is null) return;

        farm.Printers = currentPrinters.ToList();
        await _repo.SaveAsync(farm, cancellationToken);
    }

    /// <summary>Deletes a farm profile.</summary>
    public async Task DeleteFarmAsync(Guid farmId, CancellationToken cancellationToken = default)
    {
        await _repo.DeleteAsync(farmId, cancellationToken);
        OnFarmsChanged();
    }

    /// <summary>
    /// Exports a farm as a JSON string suitable for file download / transfer.
    /// </summary>
    public async Task<string> ExportFarmAsync(Guid farmId, CancellationToken cancellationToken = default)
    {
        var farm = await _repo.GetByIdAsync(farmId, cancellationToken);
        if (farm is null) return "{}";
        return System.Text.Json.JsonSerializer.Serialize(farm, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Imports a farm from a JSON string. Assigns a fresh ID to prevent collisions.
    /// </summary>
    public async Task<FarmConfiguration> ImportFarmAsync(string json, CancellationToken cancellationToken = default)
    {
        var farm = System.Text.Json.JsonSerializer.Deserialize<FarmConfiguration>(json)
            ?? throw new InvalidOperationException("Invalid farm configuration JSON.");

        farm.Id = Guid.NewGuid();
        farm.CreatedAt = DateTime.UtcNow;
        await _repo.SaveAsync(farm, cancellationToken);
        OnFarmsChanged();
        return farm;
    }

    private void OnFarmsChanged() => FarmsChanged?.Invoke(this, EventArgs.Empty);
}
