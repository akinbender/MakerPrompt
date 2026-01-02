using CADability;
using CADability.GeoObject;
using MakerPrompt.Shared.ShapeIt.Parameters;
using MakerPrompt.Shared.ShapeIt.Rendering;

namespace MakerPrompt.Shared.ShapeIt.Documents;

/// <summary>
/// CADability-based implementation of ICadDocumentHost.
/// Wraps a CADability Project and Model to provide a UI-agnostic CAD document interface.
/// </summary>
public class CadabilityDocumentHost : ICadDocumentHost, IDisposable
{
    private Project? _project;
    private Model? _activeModel;
    private readonly Guid _id = Guid.NewGuid();
    private string? _name;
    private bool _disposed;

    /// <inheritdoc />
    public Guid Id => _id;

    /// <inheritdoc />
    public string? Name => _name;

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public event EventHandler? ParameterChanged;

    /// <inheritdoc />
    public Task InitializeNewAsync(string? template = null, CancellationToken ct = default)
    {
        _project = Project.Construct();
        _activeModel = _project.GetActiveModel();
        _name = "Untitled";

        WireModelEvents();

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task LoadAsync(Stream fileStream, string? fileName = null, CancellationToken ct = default)
    {
        // CADability's ReadFromFile expects a file path, so we need to write to a temp file
        var tempFile = System.IO.Path.GetTempFileName();
        try
        {
            using (var fs = File.Create(tempFile))
            {
                await fileStream.CopyToAsync(fs, ct);
            }

            _project = Project.ReadFromFile(tempFile);
            _activeModel = _project.GetActiveModel();
            _name = fileName ?? "Document";

            WireModelEvents();
        }
        finally
        {
            // Clean up temp file
            try
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    /// <inheritdoc />
    public async Task SaveAsync(Stream target, string? fileName = null, CancellationToken ct = default)
    {
        if (_project == null)
            throw new InvalidOperationException("No document is loaded.");

        // CADability's WriteToFile expects a file path, so we write to a temp file and then copy to stream
        var tempFile = System.IO.Path.GetTempFileName();
        try
        {
            _project.WriteToFile(tempFile);

            using var fs = File.OpenRead(tempFile);
            await fs.CopyToAsync(target, ct);
        }
        finally
        {
            // Clean up temp file
            try
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<CadParameterDescriptor>> GetParametersAsync(CancellationToken ct = default)
    {
        // Parameters not yet implemented - return empty list
        IReadOnlyList<CadParameterDescriptor> result = Array.Empty<CadParameterDescriptor>();
        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task UpdateParametersAsync(IEnumerable<CadParameterValue> values, CancellationToken ct = default)
    {
        // Parameters not yet implemented - no-op
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RegenerateAsync(CancellationToken ct = default)
    {
        // CADability recomputes triangulation on-demand, so we just notify that something changed
        Changed?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<SceneSnapshot> GetSceneAsync(SceneDetailLevel detail, CancellationToken ct = default)
    {
        if (_activeModel == null)
            return Task.FromResult(new SceneSnapshot(Array.Empty<SceneNode>()));

        var snapshot = SceneBuilder.BuildSceneFromModel(_activeModel, detail);
        return Task.FromResult(snapshot);
    }

    /// <inheritdoc />
    public Task<MeshExportResult> ExportMeshAsync(MeshExportOptions options, CancellationToken ct = default)
    {
        if (_activeModel == null)
            throw new InvalidOperationException("No model is loaded.");

        var result = MeshExporter.ExportModelToStl(_activeModel, options);
        return Task.FromResult(result);
    }

    private void WireModelEvents()
    {
        if (_activeModel == null)
            return;

        // Wire up model events to raise Changed event
        _activeModel.GeoObjectAddedEvent += (go) => Changed?.Invoke(this, EventArgs.Empty);
        _activeModel.GeoObjectRemovedEvent += (go) => Changed?.Invoke(this, EventArgs.Empty);
        _activeModel.GeoObjectDidChangeEvent += (sender, change) => Changed?.Invoke(this, EventArgs.Empty);
    }

    private void UnwireModelEvents()
    {
        if (_activeModel == null)
            return;

        // Remove event handlers to prevent memory leaks
        _activeModel.GeoObjectAddedEvent -= (go) => Changed?.Invoke(this, EventArgs.Empty);
        _activeModel.GeoObjectRemovedEvent -= (go) => Changed?.Invoke(this, EventArgs.Empty);
        _activeModel.GeoObjectDidChangeEvent -= (sender, change) => Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        UnwireModelEvents();
        _activeModel = null;
        _project = null;
        _disposed = true;
    }
}
