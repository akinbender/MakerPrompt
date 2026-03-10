using Microsoft.Extensions.Logging;

namespace MakerPrompt.Shared.Services
{
    /// <summary>
    /// Manages multiple farm configurations. Supports creating, switching, importing,
    /// and exporting farm profiles. Each farm stores a snapshot of printer connection
    /// definitions that are loaded into <see cref="PrinterConnectionManager"/> when active.
    /// </summary>
    public sealed class FarmConfigurationService
    {
        private const string StorageKey = "MakerPrompt.FarmConfigurations";
        private const string PrinterStorageKey = "MakerPrompt.PrinterConnections";

        private readonly IAppLocalStorageProvider _storage;
        private readonly IAppConfigurationService _configService;
        private readonly PrinterConnectionManager _connectionManager;
        private readonly ILogger<FarmConfigurationService> _logger;
        private List<FarmConfiguration> _farms = [];

        public IReadOnlyList<FarmConfiguration> Farms => _farms.AsReadOnly();

        public FarmConfiguration? ActiveFarm =>
            _farms.FirstOrDefault(f => f.Id == _configService.Configuration.ActiveFarmId);

        public event EventHandler? FarmsChanged;

        public FarmConfigurationService(
            IAppLocalStorageProvider storage,
            IAppConfigurationService configService,
            PrinterConnectionManager connectionManager,
            ILogger<FarmConfigurationService> logger)
        {
            _storage = storage;
            _configService = configService;
            _connectionManager = connectionManager;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            try
            {
                _farms = await LoadFarmsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load farm configurations");
                _farms = [];
            }
        }

        public async Task<FarmConfiguration> CreateFarmAsync(string name)
        {
            var farm = new FarmConfiguration { Name = name };
            _farms.Add(farm);
            await SaveFarmsAsync();
            FarmsChanged?.Invoke(this, EventArgs.Empty);
            return farm;
        }

        public async Task UpdateFarmNameAsync(Guid farmId, string name)
        {
            var farm = _farms.FirstOrDefault(f => f.Id == farmId);
            if (farm == null) return;
            farm.Name = name;
            await SaveFarmsAsync();

            if (_configService.Configuration.ActiveFarmId == farmId)
            {
                _configService.Configuration.FarmName = name;
                await _configService.SaveConfigurationAsync();
            }

            FarmsChanged?.Invoke(this, EventArgs.Empty);
        }

        public async Task DeleteFarmAsync(Guid farmId)
        {
            _farms.RemoveAll(f => f.Id == farmId);

            if (_configService.Configuration.ActiveFarmId == farmId)
            {
                _configService.Configuration.ActiveFarmId = _farms.FirstOrDefault()?.Id;
                _configService.Configuration.FarmName = _farms.FirstOrDefault()?.Name ?? string.Empty;
                await _configService.SaveConfigurationAsync();
            }

            await SaveFarmsAsync();
            FarmsChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Switches the active farm. Saves current printers to the outgoing farm,
        /// writes the new farm's printers to storage, and reloads the connection manager.
        /// </summary>
        public async Task SwitchFarmAsync(Guid farmId)
        {
            var newFarm = _farms.FirstOrDefault(f => f.Id == farmId);
            if (newFarm == null) return;

            // Save current printers to current farm
            var currentFarm = ActiveFarm;
            if (currentFarm != null)
            {
                currentFarm.Printers = _connectionManager.Printers
                    .Select(p => CloneDefinition(p.Definition))
                    .ToList();
            }

            // Update config
            _configService.Configuration.ActiveFarmId = farmId;
            _configService.Configuration.FarmName = newFarm.Name;
            await _configService.SaveConfigurationAsync();

            // Write new farm's printers to printer connection storage
            var json = JsonSerializer.Serialize(newFarm.Printers, new JsonSerializerOptions { WriteIndented = true });
            var bytes = Encoding.UTF8.GetBytes(json);
            using var stream = new MemoryStream(bytes);
            await _storage.SaveFileAsync(PrinterStorageKey, stream);

            // Reload connection manager with new printers
            await _connectionManager.ReloadAsync();

            await SaveFarmsAsync();
            FarmsChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Exports a farm configuration as a JSON string suitable for saving to a file.
        /// </summary>
        public string ExportFarm(Guid farmId)
        {
            var farm = _farms.FirstOrDefault(f => f.Id == farmId);
            if (farm == null) return "{}";

            // Snapshot current printers if exporting the active farm
            if (farm.Id == _configService.Configuration.ActiveFarmId)
            {
                farm.Printers = _connectionManager.Printers
                    .Select(p => CloneDefinition(p.Definition))
                    .ToList();
            }

            return JsonSerializer.Serialize(farm, new JsonSerializerOptions { WriteIndented = true });
        }

        /// <summary>
        /// Imports a farm configuration from a JSON string. Assigns a new ID to avoid collisions.
        /// </summary>
        public async Task<FarmConfiguration> ImportFarmAsync(string json)
        {
            var farm = JsonSerializer.Deserialize<FarmConfiguration>(json)
                ?? throw new InvalidOperationException("Invalid farm configuration data.");

            farm.Id = Guid.NewGuid();
            farm.CreatedAt = DateTime.UtcNow;
            _farms.Add(farm);
            await SaveFarmsAsync();
            FarmsChanged?.Invoke(this, EventArgs.Empty);
            return farm;
        }

        private async Task<List<FarmConfiguration>> LoadFarmsAsync()
        {
            try
            {
                var files = await _storage.ListFilesAsync();
                var file = files.FirstOrDefault(f => f.FullPath.Contains(StorageKey));
                if (file == null) return [];

                using var stream = await _storage.OpenReadAsync(file.FullPath);
                if (stream == null) return [];

                using var reader = new StreamReader(stream);
                var json = await reader.ReadToEndAsync();
                return JsonSerializer.Deserialize<List<FarmConfiguration>>(json) ?? [];
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load farm configurations");
                return [];
            }
        }

        private async Task SaveFarmsAsync()
        {
            try
            {
                var json = JsonSerializer.Serialize(_farms, new JsonSerializerOptions { WriteIndented = true });
                var bytes = Encoding.UTF8.GetBytes(json);
                using var stream = new MemoryStream(bytes);
                await _storage.SaveFileAsync(StorageKey, stream);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save farm configurations");
            }
        }

        private static PrinterConnectionDefinition CloneDefinition(PrinterConnectionDefinition original)
        {
            var json = JsonSerializer.Serialize(original);
            return JsonSerializer.Deserialize<PrinterConnectionDefinition>(json)!;
        }
    }
}
