namespace MakerPrompt.Shared.Infrastructure
{
    public interface IAppDataProtectionService
    {
        Task<string> EncryptAsync(string plaintext, CancellationToken cancellationToken = default);
        Task<string?> DecryptAsync(string ciphertext, CancellationToken cancellationToken = default);
    }
}
