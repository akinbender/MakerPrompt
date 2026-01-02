namespace MakerPrompt.Shared.ShapeIt.Rendering;

/// <summary>
/// Options for exporting mesh data.
/// </summary>
/// <param name="Format">Export format (e.g., "stl-binary").</param>
/// <param name="Tolerance">Tessellation tolerance for mesh generation.</param>
public record MeshExportOptions(
    string Format,
    double Tolerance
);
