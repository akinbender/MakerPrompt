namespace MakerPrompt.Shared.ShapeIt.Rendering;

/// <summary>
/// Result of a mesh export operation.
/// </summary>
/// <param name="MimeType">MIME type of the exported content (e.g., "model/stl").</param>
/// <param name="SuggestedFileName">Suggested filename for saving.</param>
/// <param name="Content">Binary content of the exported mesh.</param>
public record MeshExportResult(
    string MimeType,
    string SuggestedFileName,
    byte[] Content
);
