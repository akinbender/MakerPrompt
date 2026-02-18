using System.Security.Cryptography;

namespace MakerPrompt.Shared.Services
{
    public sealed class LocalEncryptedAppStorageService : IAppStorage
    {
        private readonly IAppLocalStorageProvider _localStorageProvider;
        private readonly IAppDataProtectionService _dataProtectionService;
        private readonly IAppConfigurationService _appConfigurationService;
        private readonly SemaphoreSlim _initializeLock = new(1, 1);

        private bool _initialized;

        public LocalEncryptedAppStorageService(
            IAppLocalStorageProvider localStorageProvider,
            IAppDataProtectionService dataProtectionService,
            IAppConfigurationService appConfigurationService)
        {
            _localStorageProvider = localStorageProvider;
            _dataProtectionService = dataProtectionService;
            _appConfigurationService = appConfigurationService;
        }

        public async Task<T?> GetItemAsync<T>(string key)
        {
            await EnsureInitializedAsync();

            var payload = await ReadPayloadAsync(key);
            if (string.IsNullOrWhiteSpace(payload))
            {
                return default;
            }

            var json = await _dataProtectionService.DecryptAsync(payload) ?? payload;

            try
            {
                return JsonSerializer.Deserialize<T>(json);
            }
            catch
            {
                return default;
            }
        }

        public async Task SetItemAsync<T>(string key, T value)
        {
            await EnsureInitializedAsync();

            var json = JsonSerializer.Serialize(value);
            var encrypted = await _dataProtectionService.EncryptAsync(json);
            await SavePayloadAsync(key, encrypted);
        }

        public async Task RemoveItemAsync(string key)
        {
            await EnsureInitializedAsync();
            await _localStorageProvider.DeleteFileAsync(ToStorageFileName(key));
        }

        private async Task EnsureInitializedAsync()
        {
            if (_initialized)
            {
                return;
            }

            await _initializeLock.WaitAsync();
            try
            {
                if (_initialized)
                {
                    return;
                }

                await _appConfigurationService.InitializeAsync();
                await MigrateLegacyStorageAsync();

                _initialized = true;
            }
            finally
            {
                _initializeLock.Release();
            }
        }

        private async Task MigrateLegacyStorageAsync()
        {
            var config = _appConfigurationService.Configuration;
            var changed = false;

            if (config.LegacyPrinterConnectionSettings is not null
                && !config.StorageItems.ContainsKey("printer-connection-settings"))
            {
                config.StorageItems["printer-connection-settings"] = JsonSerializer.Serialize(config.LegacyPrinterConnectionSettings);
                config.LegacyPrinterConnectionSettings = null;
                changed = true;
            }

            if (config.StorageItems.Count > 0)
            {
                foreach (var item in config.StorageItems)
                {
                    if (string.IsNullOrWhiteSpace(item.Key) || string.IsNullOrWhiteSpace(item.Value))
                    {
                        continue;
                    }

                    if (await ExistsAsync(item.Key))
                    {
                        continue;
                    }

                    var encrypted = await _dataProtectionService.EncryptAsync(item.Value);
                    await SavePayloadAsync(item.Key, encrypted);
                    changed = true;
                }

                config.StorageItems.Clear();
                changed = true;
            }

            if (changed)
            {
                await _appConfigurationService.SaveConfigurationAsync();
            }
        }

        private async Task<bool> ExistsAsync(string key)
        {
            await using var stream = await _localStorageProvider.OpenReadAsync(ToStorageFileName(key));
            return stream is not null;
        }

        private async Task<string?> ReadPayloadAsync(string key)
        {
            await using var stream = await _localStorageProvider.OpenReadAsync(ToStorageFileName(key));
            if (stream is null)
            {
                return null;
            }

            using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: false);
            return await reader.ReadToEndAsync();
        }

        private async Task SavePayloadAsync(string key, string payload)
        {
            var bytes = Encoding.UTF8.GetBytes(payload);
            await using var stream = new MemoryStream(bytes, writable: false);
            await _localStorageProvider.SaveFileAsync(ToStorageFileName(key), stream);
        }

        private static string ToStorageFileName(string key)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
            return $"app-storage-{Convert.ToHexString(hash).ToLowerInvariant()}.json";
        }
    }
}
