using Microsoft.Extensions.Logging;

namespace MakerPrompt.UI.Components.Services;

/// <summary>
/// Manages named farm snapshots and coordinates switching the connection store
/// consumed by <see cref="PrinterConnectionManager"/>.
/// </summary>
public sealed class FarmConfigurationService(
    IAppLocalStorageProvider storage,
    IAppConfigurationService configService,
    PrinterConnectionManager connectionManager,
    PrinterConfigurationPersistence persistence,
    ILogger<FarmConfigurationService> logger)
{
    private const string StorageKey = "MakerPrompt.FarmConfigurations";
    private const string PrinterStorageKey = "MakerPrompt.PrinterConnections";

    private readonly SemaphoreSlim _lock = new(1, 1);
    private List<FarmConfiguration> _farms = [];

    public IReadOnlyList<FarmConfiguration> Farms => _farms.AsReadOnly();

    public FarmConfiguration? ActiveFarm =>
        _farms.FirstOrDefault(
            farm => farm.Id == configService.Configuration.ActiveFarmId);

    public event EventHandler? FarmsChanged;

    public async Task InitializeAsync()
    {
        await _lock.WaitAsync();
        try
        {
            var result = await LoadFarmsAsync();
            _farms = result.Items.ToList();
            if (result.RequiresMigration)
            {
                try
                {
                    await SaveFarmsUnsafeAsync();
                }
                catch (Exception ex)
                {
                    logger.LogWarning(
                        ex,
                        "Loaded legacy farm configurations, but could not migrate the store");
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load farm configurations");
            _farms = [];
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<FarmConfiguration> CreateFarmAsync(string name)
    {
        var farm = new FarmConfiguration { Name = name };

        await _lock.WaitAsync();
        try
        {
            _farms.Add(farm);
            try
            {
                await SaveFarmsUnsafeAsync();
            }
            catch
            {
                _farms.Remove(farm);
                throw;
            }
        }
        finally
        {
            _lock.Release();
        }

        RaiseFarmsChanged();
        return farm;
    }

    public async Task UpdateFarmNameAsync(Guid farmId, string name)
    {
        var changed = false;
        await _lock.WaitAsync();
        try
        {
            var farm = _farms.FirstOrDefault(item => item.Id == farmId);
            if (farm == null)
                return;

            var previousName = farm.Name;
            farm.Name = name;
            try
            {
                await SaveFarmsUnsafeAsync();
                if (configService.Configuration.ActiveFarmId == farmId)
                {
                    configService.Configuration.FarmName = name;
                    await configService.SaveConfigurationAsync();
                }
                changed = true;
            }
            catch
            {
                farm.Name = previousName;
                throw;
            }
        }
        finally
        {
            _lock.Release();
        }

        if (changed)
            RaiseFarmsChanged();
    }

    public async Task DeleteFarmAsync(Guid farmId)
    {
        var changed = false;
        await _lock.WaitAsync();
        try
        {
            var farm = _farms.FirstOrDefault(item => item.Id == farmId);
            if (farm == null)
                return;

            var wasActive = configService.Configuration.ActiveFarmId == farmId;
            _farms.Remove(farm);

            if (!wasActive)
            {
                await SaveFarmsUnsafeAsync();
            }
            else if (_farms.FirstOrDefault() is { } nextFarm)
            {
                await SwitchFarmUnsafeAsync(nextFarm, snapshotOutgoingFarm: false);
            }
            else
            {
                configService.Configuration.ActiveFarmId = null;
                configService.Configuration.FarmName = string.Empty;
                await configService.SaveConfigurationAsync();
                await WritePrinterDefinitionsAsync([]);
                await connectionManager.ReloadAsync();
                await SaveFarmsUnsafeAsync();
            }

            changed = true;
        }
        finally
        {
            _lock.Release();
        }

        if (changed)
            RaiseFarmsChanged();
    }

    /// <summary>
    /// Saves the outgoing printer snapshot and activates the requested farm.
    /// </summary>
    public async Task SwitchFarmAsync(Guid farmId)
    {
        var changed = false;
        await _lock.WaitAsync();
        try
        {
            var newFarm = _farms.FirstOrDefault(farm => farm.Id == farmId);
            if (newFarm == null)
                return;

            await SwitchFarmUnsafeAsync(newFarm, snapshotOutgoingFarm: true);
            changed = true;
        }
        finally
        {
            _lock.Release();
        }

        if (changed)
            RaiseFarmsChanged();
    }

    /// <summary>
    /// Clears persisted and live printer connections.
    /// </summary>
    public async Task ClearPrinterConnectionsAsync()
    {
        await _lock.WaitAsync();
        try
        {
            await WritePrinterDefinitionsAsync([]);
            await connectionManager.ReloadAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Exports a farm with credentials removed.
    /// </summary>
    public string ExportFarm(Guid farmId)
    {
        var farm = _farms.FirstOrDefault(item => item.Id == farmId);
        if (farm == null)
            return "{}";

        var export = persistence.CloneFarm(farm);
        if (farm.Id == configService.Configuration.ActiveFarmId)
        {
            export.Printers = connectionManager.Printers
                .Select(state => persistence.CloneDefinition(state.Definition))
                .ToList();
        }

        return persistence.SerializeFarmExport(export);
    }

    /// <summary>
    /// Imports current or legacy farm JSON and assigns it a new identity.
    /// </summary>
    public async Task<FarmConfiguration> ImportFarmAsync(string json)
    {
        var farm = persistence.DeserializeFarmExport(json);
        farm.Id = Guid.NewGuid();
        farm.CreatedAt = DateTime.UtcNow;

        await _lock.WaitAsync();
        try
        {
            _farms.Add(farm);
            try
            {
                await SaveFarmsUnsafeAsync();
            }
            catch
            {
                _farms.Remove(farm);
                throw;
            }
        }
        finally
        {
            _lock.Release();
        }

        RaiseFarmsChanged();
        return farm;
    }

    private async Task SwitchFarmUnsafeAsync(
        FarmConfiguration newFarm,
        bool snapshotOutgoingFarm)
    {
        if (snapshotOutgoingFarm && ActiveFarm is { } currentFarm)
        {
            currentFarm.Printers = connectionManager.Printers
                .Select(state => persistence.CloneDefinition(state.Definition))
                .ToList();
        }

        configService.Configuration.ActiveFarmId = newFarm.Id;
        configService.Configuration.FarmName = newFarm.Name;
        await configService.SaveConfigurationAsync();

        await WritePrinterDefinitionsAsync(newFarm.Printers);
        await connectionManager.ReloadAsync();
        await SaveFarmsUnsafeAsync();
    }

    private async Task<PersistenceReadResult<FarmConfiguration>> LoadFarmsAsync()
    {
        var files = await storage.ListFilesAsync();
        var file = files.FirstOrDefault(item =>
            IsStorageFile(item.FullPath, StorageKey));
        if (file == null)
            return new([], false);

        using var stream = await storage.OpenReadAsync(file.FullPath);
        if (stream == null)
            return new([], false);

        using var reader = new StreamReader(stream);
        return persistence.DeserializeFarms(await reader.ReadToEndAsync());
    }

    private Task SaveFarmsUnsafeAsync() =>
        WriteStorageFileAsync(StorageKey, persistence.SerializeFarms(_farms));

    private Task WritePrinterDefinitionsAsync(
        IEnumerable<PrinterConnectionDefinition> definitions) =>
        WriteStorageFileAsync(
            PrinterStorageKey,
            persistence.SerializeConnections(definitions));

    private async Task WriteStorageFileAsync(string storageKey, string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        using var stream = new MemoryStream(bytes, writable: false);
        await storage.SaveFileAsync(storageKey, stream);
    }

    private static bool IsStorageFile(string fullPath, string storageKey) =>
        string.Equals(fullPath, storageKey, StringComparison.Ordinal)
        || string.Equals(
            Path.GetFileName(fullPath),
            storageKey,
            StringComparison.Ordinal);

    private void RaiseFarmsChanged() =>
        FarmsChanged?.Invoke(this, EventArgs.Empty);
}
