using MakerPrompt.Shared.Infrastructure;

namespace MakerPrompt.Shared.Services
{
    [Obsolete("Use LocalEncryptedAppStorageService instead.")]
    public sealed class AppConfigurationStorageService : IAppStorage
    {
        private readonly LocalEncryptedAppStorageService _inner;

        public AppConfigurationStorageService(LocalEncryptedAppStorageService inner)
        {
            _inner = inner;
        }

        public Task<T?> GetItemAsync<T>(string key) => _inner.GetItemAsync<T>(key);

        public Task SetItemAsync<T>(string key, T value) => _inner.SetItemAsync(key, value);

        public Task RemoveItemAsync(string key) => _inner.RemoveItemAsync(key);
    }
}
