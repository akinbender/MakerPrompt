namespace MakerPrompt.Infrastructure.InMemoryStores;

/// <summary>
/// Thread-safe, in-memory implementation of <see cref="IPrintProjectRepository"/>.
/// G-code file "storage" is kept in-memory as byte arrays.
/// </summary>
public sealed class InMemoryPrintProjectRepository : IPrintProjectRepository
{
    private readonly Dictionary<Guid, PrintProject> _projects = [];
    private readonly Dictionary<string, byte[]> _files = [];
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<IReadOnlyList<PrintProject>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try { return _projects.Values.ToList().AsReadOnly(); }
        finally { _lock.Release(); }
    }

    public async Task<PrintProject?> GetByIdAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try { return _projects.GetValueOrDefault(projectId); }
        finally { _lock.Release(); }
    }

    public async Task SaveAsync(PrintProject project, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try { _projects[project.Id] = project; }
        finally { _lock.Release(); }
    }

    public async Task DeleteAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try { _projects.Remove(projectId); }
        finally { _lock.Release(); }
    }

    public async Task<Stream?> OpenJobFileAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            return _files.TryGetValue(storagePath, out var data)
                ? new MemoryStream(data, writable: false)
                : null;
        }
        finally { _lock.Release(); }
    }

    public async Task<string> SaveJobFileAsync(Guid projectId, string fileName, Stream content,
        CancellationToken cancellationToken = default)
    {
        var storagePath = $"PrintProjects/{projectId}/{fileName}";
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, cancellationToken);

        await _lock.WaitAsync(cancellationToken);
        try { _files[storagePath] = ms.ToArray(); }
        finally { _lock.Release(); }

        return storagePath;
    }

    public async Task DeleteJobFileAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try { _files.Remove(storagePath); }
        finally { _lock.Release(); }
    }
}
