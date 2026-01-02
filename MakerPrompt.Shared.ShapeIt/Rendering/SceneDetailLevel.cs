namespace MakerPrompt.Shared.ShapeIt.Rendering;

/// <summary>
/// Defines the level of detail for scene rendering.
/// </summary>
public enum SceneDetailLevel
{
    /// <summary>
    /// Only render bounding boxes (fastest).
    /// </summary>
    BoundingBoxesOnly,

    /// <summary>
    /// Render shaded meshes.
    /// </summary>
    ShadedMeshes,

    /// <summary>
    /// Render shaded meshes with visible edges.
    /// </summary>
    ShadedWithEdges
}
