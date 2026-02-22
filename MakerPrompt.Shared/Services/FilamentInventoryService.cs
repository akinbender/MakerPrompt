using Microsoft.Extensions.Logging;

namespace MakerPrompt.Shared.Services
{
    public class FilamentInventoryService
    {
        private const string StorageKey = "MakerPrompt.FilamentInventory.json";
        private readonly IAppLocalStorageProvider _storage;
        private readonly ILogger<FilamentInventoryService> _logger;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private List<FilamentSpool> _spools = [];

        public event EventHandler? InventoryChanged;

        public FilamentInventoryService(IAppLocalStorageProvider storage, ILogger<FilamentInventoryService> logger)
        {
            _storage = storage;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            await _lock.WaitAsync();
            try
            {
                var files = await _storage.ListFilesAsync();
                var file = files.FirstOrDefault(f => f.FullPath.Contains(StorageKey));
                if (file != null)
                {
                    using var stream = await _storage.OpenReadAsync(file.FullPath);
                    if (stream != null)
                    {
                        using var reader = new StreamReader(stream);
                        var json = await reader.ReadToEndAsync();
                        var stored = JsonSerializer.Deserialize<List<FilamentSpool>>(json);
                        if (stored != null)
                        {
                            _spools = stored;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load filament inventory");
            }
            finally
            {
                _lock.Release();
            }
        }

        public IReadOnlyList<FilamentSpool> GetSpools() => _spools.AsReadOnly();

        public FilamentSpool? GetSpool(Guid id) => _spools.FirstOrDefault(s => s.Id == id);

        public async Task AddSpoolAsync(FilamentSpool spool)
        {
            await _lock.WaitAsync();
            try
            {
                _spools.Add(spool);
                await SaveAsync();
            }
            finally
            {
                _lock.Release();
            }
            InventoryChanged?.Invoke(this, EventArgs.Empty);
        }

        public async Task UpdateSpoolAsync(FilamentSpool spool)
        {
            await _lock.WaitAsync();
            try
            {
                var index = _spools.FindIndex(s => s.Id == spool.Id);
                if (index >= 0)
                {
                    _spools[index] = spool;
                    await SaveAsync();
                }
            }
            finally
            {
                _lock.Release();
            }
            InventoryChanged?.Invoke(this, EventArgs.Empty);
        }

        public async Task DeleteSpoolAsync(Guid id)
        {
            await _lock.WaitAsync();
            try
            {
                var index = _spools.FindIndex(s => s.Id == id);
                if (index >= 0)
                {
                    _spools.RemoveAt(index);
                    await SaveAsync();
                }
            }
            finally
            {
                _lock.Release();
            }
            InventoryChanged?.Invoke(this, EventArgs.Empty);
        }

        public async Task DeductFilamentAsync(Guid spoolId, double grams)
        {
            await _lock.WaitAsync();
            try
            {
                var spool = _spools.FirstOrDefault(s => s.Id == spoolId);
                if (spool != null)
                {
                    spool.RemainingWeightGrams = Math.Max(0, spool.RemainingWeightGrams - grams);
                    await SaveAsync();
                }
            }
            finally
            {
                _lock.Release();
            }
            InventoryChanged?.Invoke(this, EventArgs.Empty);
        }

        private async Task SaveAsync()
        {
            var json = JsonSerializer.Serialize(_spools);
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
            await _storage.SaveFileAsync(StorageKey, stream);
        }
    }
}
