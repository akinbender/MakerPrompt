using MakerPrompt.Shared.ShapeIt.Parameters;
using MakerPrompt.Shared.ShapeIt.Rendering;

namespace MakerPrompt.Shared.ShapeIt.Documents;

/// <summary>
/// Abstraction over a CAD document host that manages a CAD kernel and provides
/// a UI-agnostic interface for document operations.
/// </summary>
public interface ICadDocumentHost
{
    /// <summary>
    /// Unique identifier for this document instance.
    /// </summary>
    Guid Id { get; }

    /// <summary>
    /// Optional document name.
    /// </summary>
    string? Name { get; }

    /// <summary>
    /// Event raised when the document has changed (geometry added/removed/modified).
    /// </summary>
    event EventHandler? Changed;

    /// <summary>
    /// Event raised when a parameter has been changed.
    /// </summary>
    event EventHandler? ParameterChanged;

    /// <summary>
    /// Initializes a new CAD document, optionally from a template.
    /// </summary>
    /// <param name="template">Optional template identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    Task InitializeNewAsync(string? template = null, CancellationToken ct = default);

    /// <summary>
    /// Loads a CAD document from a stream.
    /// </summary>
    /// <param name="fileStream">Stream containing the document data.</param>
    /// <param name="fileName">Optional filename hint.</param>
    /// <param name="ct">Cancellation token.</param>
    Task LoadAsync(Stream fileStream, string? fileName = null, CancellationToken ct = default);

    /// <summary>
    /// Saves the current document to a stream.
    /// </summary>
    /// <param name="target">Target stream to write the document.</param>
    /// <param name="fileName">Optional filename hint.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SaveAsync(Stream target, string? fileName = null, CancellationToken ct = default);

    /// <summary>
    /// Gets the list of available parameters for this document.
    /// </summary>
    Task<IReadOnlyList<CadParameterDescriptor>> GetParametersAsync(CancellationToken ct = default);

    /// <summary>
    /// Updates one or more parameter values.
    /// </summary>
    Task UpdateParametersAsync(IEnumerable<CadParameterValue> values, CancellationToken ct = default);

    /// <summary>
    /// Forces a regeneration of the document geometry.
    /// </summary>
    Task RegenerateAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets a snapshot of the current scene for rendering.
    /// </summary>
    Task<SceneSnapshot> GetSceneAsync(SceneDetailLevel detail, CancellationToken ct = default);

    /// <summary>
    /// Exports the document mesh to a specific format (e.g., STL).
    /// </summary>
    Task<MeshExportResult> ExportMeshAsync(MeshExportOptions options, CancellationToken ct = default);
}
