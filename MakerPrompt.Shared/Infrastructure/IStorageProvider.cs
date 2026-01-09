namespace MakerPrompt.Shared.Infrastructure
{
    using MakerPrompt.Shared.Models;

    public interface IStorageProvider
    {
        string DisplayName { get; }
        string Key { get; }
        Task<List<FileEntry>> ListFilesAsync(CancellationToken cancellationToken = default);
        Task<Stream?> OpenReadAsync(string fullPath, CancellationToken cancellationToken = default);
        Task SaveFileAsync(string fullPath, Stream content, CancellationToken cancellationToken = default);
        Task DeleteFileAsync(string fullPath, CancellationToken cancellationToken = default);
    }

    public interface IAppLocalStorageProvider : IStorageProvider
    {
        string RootPath { get; }
    }
}
