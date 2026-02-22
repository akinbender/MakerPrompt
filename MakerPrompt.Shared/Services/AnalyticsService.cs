using Microsoft.Extensions.Logging;

namespace MakerPrompt.Shared.Services
{
    public class AnalyticsService
    {
        private const string StorageKey = "MakerPrompt.PrintJobUsageRecords.json";
        private readonly IAppLocalStorageProvider _storage;
        private readonly ILogger<AnalyticsService> _logger;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private List<PrintJobUsageRecord> _records = [];

        public event EventHandler? AnalyticsUpdated;

        public AnalyticsService(IAppLocalStorageProvider storage, ILogger<AnalyticsService> logger)
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
                        var stored = JsonSerializer.Deserialize<List<PrintJobUsageRecord>>(json);
                        if (stored != null)
                        {
                            _records = stored;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load print job usage records");
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task RecordUsageAsync(PrintJobUsageRecord record)
        {
            await _lock.WaitAsync();
            try
            {
                _records.Add(record);
                var json = JsonSerializer.Serialize(_records);
                using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
                await _storage.SaveFileAsync(StorageKey, stream);
            }
            finally
            {
                _lock.Release();
            }
            AnalyticsUpdated?.Invoke(this, EventArgs.Empty);
        }

        public IReadOnlyList<PrintJobUsageRecord> GetRecords() => _records.AsReadOnly();

        public TimeSpan GetTotalPrintHours() => TimeSpan.FromTicks(_records.Sum(r => r.Duration.Ticks));

        public double GetTotalFilamentConsumed() => _records.Sum(r => r.ActualFilamentUsedGrams > 0 ? r.ActualFilamentUsedGrams : r.EstimatedFilamentUsedGrams);

        public double GetFilamentConsumedByPrinter(Guid printerId) => _records.Where(r => r.PrinterId == printerId).Sum(r => r.ActualFilamentUsedGrams > 0 ? r.ActualFilamentUsedGrams : r.EstimatedFilamentUsedGrams);

        public double GetFilamentConsumedBySpool(Guid spoolId) => _records.Where(r => r.FilamentSpoolId == spoolId).Sum(r => r.ActualFilamentUsedGrams > 0 ? r.ActualFilamentUsedGrams : r.EstimatedFilamentUsedGrams);
    }
}
