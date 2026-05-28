using MakerPrompt.Core.Models;
using MakerPrompt.UI.Components.Infrastructure;

namespace MakerPrompt.Tests.Unit.Helpers;

/// <summary>
/// In-memory implementation of <see cref="IAppLocalStorageProvider"/> for unit tests.
/// Stores files as byte arrays keyed by path.
/// </summary>
public sealed class InMemoryAppLocalStorageProvider : IAppLocalStorageProvider
{
    private readonly Dictionary<string, byte[]> _files = new();

    public string DisplayName => "InMemory";
    public string Key => "inmemory";
    public string RootPath => "/";

    public Task<List<FileEntry>> ListFilesAsync(CancellationToken cancellationToken = default)
    {
        var entries = _files.Keys.Select(k => new FileEntry
        {
            FullPath = k,
            Size = _files[k].Length,
            IsAvailable = true
        }).ToList();
        return Task.FromResult(entries);
    }

    public Task<Stream?> OpenReadAsync(string fullPath, CancellationToken cancellationToken = default)
    {
        if (_files.TryGetValue(fullPath, out var data))
            return Task.FromResult<Stream?>(new MemoryStream(data, writable: false));
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
}
