using MakerPrompt.Shared.Infrastructure;
using MakerPrompt.Shared.Models;

namespace MakerPrompt.Tests;

/// <summary>
/// In-memory implementation of IAppLocalStorageProvider used by unit tests.
/// </summary>
internal sealed class InMemoryStorageProvider : IAppLocalStorageProvider
{
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.OrdinalIgnoreCase);

    public string DisplayName => "Test";
    public string Key => "test";
    public string RootPath => "/test/";

    public Task<List<FileEntry>> ListFilesAsync(CancellationToken cancellationToken = default)
    {
        var entries = _files.Select(kv => new FileEntry
        {
            FullPath = kv.Key,
            Size = kv.Value.Length,
            IsAvailable = true
        }).ToList();
        return Task.FromResult(entries);
    }

    public Task<Stream?> OpenReadAsync(string fullPath, CancellationToken cancellationToken = default)
    {
        if (_files.TryGetValue(fullPath, out var bytes))
            return Task.FromResult<Stream?>(new MemoryStream(bytes));
        return Task.FromResult<Stream?>(null);
    }

    public async Task SaveFileAsync(string fullPath, Stream content, CancellationToken cancellationToken = default)
    {
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, cancellationToken);
        _files[fullPath] = ms.ToArray();
    }

    public Task DeleteFileAsync(string fullPath, CancellationToken cancellationToken = default)
    {
        _files.Remove(fullPath);
        return Task.CompletedTask;
    }

    public string GetString(string path)
        => _files.TryGetValue(path, out var b) ? System.Text.Encoding.UTF8.GetString(b) : string.Empty;
}
