using System.Security.Cryptography;
using MakerPrompt.Shared.Infrastructure;
using Microsoft.JSInterop;

namespace MakerPrompt.Blazor.Services
{
    public sealed class BlazorAppEncryptionKeyStore : IAppEncryptionKeyStore
    {
        private const string EncryptionKeyStorageKey = "MakerPrompt.AppStorage.EncryptionKey";

        private readonly IJSRuntime _jsRuntime;
        private readonly SemaphoreSlim _syncLock = new(1, 1);

        private byte[]? _cachedKey;

        public BlazorAppEncryptionKeyStore(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        public async Task<byte[]> GetOrCreateKeyAsync(CancellationToken cancellationToken = default)
        {
            if (_cachedKey is not null)
            {
                return _cachedKey;
            }

            await _syncLock.WaitAsync(cancellationToken);
            try
            {
                if (_cachedKey is not null)
                {
                    return _cachedKey;
                }

                var existing = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", cancellationToken, EncryptionKeyStorageKey);

                if (!string.IsNullOrWhiteSpace(existing))
                {
                    try
                    {
                        _cachedKey = Convert.FromBase64String(existing);
                        if (_cachedKey.Length == 32)
                        {
                            return _cachedKey;
                        }
                    }
                    catch
                    {
                        // Ignore and regenerate below.
                    }
                }

                _cachedKey = RandomNumberGenerator.GetBytes(32);
                var serialized = Convert.ToBase64String(_cachedKey);
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", cancellationToken, EncryptionKeyStorageKey, serialized);

                return _cachedKey;
            }
            finally
            {
                _syncLock.Release();
            }
        }
    }
}
