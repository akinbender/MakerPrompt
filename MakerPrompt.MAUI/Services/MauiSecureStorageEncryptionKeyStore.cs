using System.Security.Cryptography;
using MakerPrompt.Shared.Infrastructure;

namespace MakerPrompt.MAUI.Services
{
    public sealed class MauiSecureStorageEncryptionKeyStore : IAppEncryptionKeyStore
    {
        private const string SecureStorageKey = "makerprompt-appstorage-encryption-key";

        private readonly SemaphoreSlim _syncLock = new(1, 1);
        private byte[]? _cachedKey;

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

                string? serialized = null;

                try
                {
                    serialized = await SecureStorage.Default.GetAsync(SecureStorageKey);
                }
                catch
                {
                    serialized = null;
                }

                if (string.IsNullOrWhiteSpace(serialized))
                {
                    serialized = Preferences.Default.Get<string?>(SecureStorageKey, null);
                }

                if (!string.IsNullOrWhiteSpace(serialized))
                {
                    try
                    {
                        var existing = Convert.FromBase64String(serialized);
                        if (existing.Length == 32)
                        {
                            _cachedKey = existing;
                            return _cachedKey;
                        }
                    }
                    catch
                    {
                        // Ignore and regenerate below.
                    }
                }

                _cachedKey = RandomNumberGenerator.GetBytes(32);
                serialized = Convert.ToBase64String(_cachedKey);

                try
                {
                    await SecureStorage.Default.SetAsync(SecureStorageKey, serialized);
                }
                catch
                {
                    Preferences.Default.Set(SecureStorageKey, serialized);
                }

                return _cachedKey;
            }
            finally
            {
                _syncLock.Release();
            }
        }
    }
}
