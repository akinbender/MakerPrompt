using MakerPrompt.Shared.Infrastructure;
using Microsoft.JSInterop;

namespace MakerPrompt.Blazor.Services
{
    public sealed class BlazorWebCryptoDataProtectionService : IAppDataProtectionService
    {
        private readonly IJSRuntime _jsRuntime;
        private readonly IAppEncryptionKeyStore _keyStore;

        public BlazorWebCryptoDataProtectionService(IJSRuntime jsRuntime, IAppEncryptionKeyStore keyStore)
        {
            _jsRuntime = jsRuntime;
            _keyStore = keyStore;
        }

        public async Task<string> EncryptAsync(string plaintext, CancellationToken cancellationToken = default)
        {
            var key = await _keyStore.GetOrCreateKeyAsync(cancellationToken);
            var keyBase64 = Convert.ToBase64String(key);
            return await _jsRuntime.InvokeAsync<string>("makerPromptCrypto.encrypt", cancellationToken, plaintext, keyBase64);
        }

        public async Task<string?> DecryptAsync(string ciphertext, CancellationToken cancellationToken = default)
        {
            var key = await _keyStore.GetOrCreateKeyAsync(cancellationToken);
            var keyBase64 = Convert.ToBase64String(key);

            try
            {
                return await _jsRuntime.InvokeAsync<string?>("makerPromptCrypto.decrypt", cancellationToken, ciphertext, keyBase64);
            }
            catch
            {
                return null;
            }
        }
    }
}
