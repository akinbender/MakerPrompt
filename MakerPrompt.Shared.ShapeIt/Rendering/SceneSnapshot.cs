namespace MakerPrompt.Shared.ShapeIt.Rendering;

/// <summary>
/// Represents a 4x4 transformation matrix for positioning and orienting objects in 3D space.
/// </summary>
/// <param name="Matrix4x4">A 16-element array representing the transformation matrix in row-major order.</param>
public record TransformData(float[] Matrix4x4);

/// <summary>
/// Represents mesh data for rendering.
/// </summary>
/// <param name="Positions">Array of vertex positions (x, y, z, x, y, z, ...).</param>
/// <param name="Normals">Array of vertex normals (x, y, z, x, y, z, ...).</param>
/// <param name="Colors">Optional array of vertex colors (r, g, b, a, ...).</param>
/// <param name="Indices">Array of triangle indices.</param>
public record MeshData(
    float[] Positions,
    float[] Normals,
    float[]? Colors,
    int[] Indices
);

/// <summary>
/// Represents edge data for rendering wireframes or silhouettes.
/// </summary>
/// <param name="Positions">Array of edge vertex positions (x, y, z, x, y, z, ...).</param>
/// <param name="IsSilhouette">Indicates if this edge is a silhouette edge.</param>
public record EdgeData(
    float[] Positions,
    bool IsSilhouette
);

/// <summary>
/// Represents a single node in the scene (typically corresponding to a CAD object).
/// </summary>
/// <param name="Id">Unique identifier for the scene node.</param>
/// <param name="Name">Optional human-readable name.</param>
/// <param name="Mesh">Optional mesh data for rendering surfaces.</param>
/// <param name="Edges">Optional edge data for rendering wireframes.</param>
/// <param name="Transform">Transformation matrix for positioning this node.</param>
public record SceneNode(
    Guid Id,
    string? Name,
    MeshData? Mesh,
    IReadOnlyList<EdgeData>? Edges,
    TransformData Transform
);

/// <summary>
/// Represents a complete snapshot of the scene for rendering.
/// </summary>
/// <param name="Nodes">List of all scene nodes to render.</param>
public record SceneSnapshot(IReadOnlyList<SceneNode> Nodes);
