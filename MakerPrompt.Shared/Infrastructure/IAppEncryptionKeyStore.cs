namespace MakerPrompt.Shared.Infrastructure
{
    public interface IAppEncryptionKeyStore
    {
        Task<byte[]> GetOrCreateKeyAsync(CancellationToken cancellationToken = default);
    }
}
