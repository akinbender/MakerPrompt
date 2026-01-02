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

    /// <summary>
    /// Creates a box (rectangular prism) solid and adds it to the document.
    /// </summary>
    /// <param name="centerX">Center X coordinate.</param>
    /// <param name="centerY">Center Y coordinate.</param>
    /// <param name="centerZ">Center Z coordinate.</param>
    /// <param name="width">Width (X direction).</param>
    /// <param name="depth">Depth (Y direction).</param>
    /// <param name="height">Height (Z direction).</param>
    /// <param name="ct">Cancellation token.</param>
    Task CreateBoxAsync(double centerX, double centerY, double centerZ, 
                       double width, double depth, double height, 
                       CancellationToken ct = default);

    /// <summary>
    /// Creates a sphere solid and adds it to the document.
    /// </summary>
    /// <param name="centerX">Center X coordinate.</param>
    /// <param name="centerY">Center Y coordinate.</param>
    /// <param name="centerZ">Center Z coordinate.</param>
    /// <param name="radius">Sphere radius.</param>
    /// <param name="ct">Cancellation token.</param>
    Task CreateSphereAsync(double centerX, double centerY, double centerZ, 
                          double radius, 
                          CancellationToken ct = default);

    /// <summary>
    /// Creates a cylinder solid and adds it to the document.
    /// </summary>
    /// <param name="baseX">Base center X coordinate.</param>
    /// <param name="baseY">Base center Y coordinate.</param>
    /// <param name="baseZ">Base center Z coordinate.</param>
    /// <param name="radius">Cylinder radius.</param>
    /// <param name="height">Cylinder height (Z direction).</param>
    /// <param name="ct">Cancellation token.</param>
    Task CreateCylinderAsync(double baseX, double baseY, double baseZ, 
                            double radius, double height, 
                            CancellationToken ct = default);

    /// <summary>
    /// Creates a cone solid and adds it to the document.
    /// </summary>
    /// <param name="baseX">Base center X coordinate.</param>
    /// <param name="baseY">Base center Y coordinate.</param>
    /// <param name="baseZ">Base center Z coordinate.</param>
    /// <param name="baseRadius">Base radius.</param>
    /// <param name="height">Cone height (Z direction).</param>
    /// <param name="topRadius">Top radius (0 for sharp cone).</param>
    /// <param name="ct">Cancellation token.</param>
    Task CreateConeAsync(double baseX, double baseY, double baseZ, 
                        double baseRadius, double height, double topRadius = 0, 
                        CancellationToken ct = default);
}
