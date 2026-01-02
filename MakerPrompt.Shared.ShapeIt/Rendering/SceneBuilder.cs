using CADability;
using CADability.GeoObject;

namespace MakerPrompt.Shared.ShapeIt.Rendering;

/// <summary>
/// Builds scene snapshots from CADability Model objects.
/// </summary>
public static class SceneBuilder
{
    /// <summary>
    /// Builds a scene snapshot from a CADability Model.
    /// </summary>
    public static SceneSnapshot BuildSceneFromModel(Model model, SceneDetailLevel detail)
    {
        var nodes = new List<SceneNode>();

        foreach (var obj in model.AllObjects)
        {
            if (obj is Solid solid)
            {
                var node = BuildNodeFromSolid(solid, detail);
                if (node != null)
                    nodes.Add(node);
            }
        }

        return new SceneSnapshot(nodes);
    }

    private static SceneNode? BuildNodeFromSolid(Solid solid, SceneDetailLevel detail)
    {
        if (detail == SceneDetailLevel.BoundingBoxesOnly)
        {
            // For bounding boxes, we could create a simple box mesh, but for now return null
            return null;
        }

        // Get the primary shell from the solid
        var shell = solid.Shells.Length > 0 ? solid.Shells[0] : null;
        if (shell == null)
            return null;

        var positions = new List<float>();
        var normals = new List<float>();
        var indices = new List<int>();

        foreach (var face in shell.Faces)
        {
            try
            {
                // Get triangulation from the face
                GeoPoint[] vertices;
                GeoPoint2D[] uvVertices;
                int[] triangleIndices;
                BoundingCube extent;
                
                face.GetTriangulation(0.01, out vertices, out uvVertices, out triangleIndices, out extent);
                if (vertices == null || triangleIndices == null)
                    continue;

                int baseIndex = positions.Count / 3;

                // Add vertices
                foreach (var point in vertices)
                {
                    positions.Add((float)point.x);
                    positions.Add((float)point.y);
                    positions.Add((float)point.z);

                    // For now, use face normal for all vertices (could be improved)
                    try
                    {
                        var normal = face.Surface.GetNormal(new CADability.GeoPoint2D(0.5, 0.5));
                        normals.Add((float)normal.x);
                        normals.Add((float)normal.y);
                        normals.Add((float)normal.z);
                    }
                    catch
                    {
                        // Fallback to a default normal
                        normals.Add(0);
                        normals.Add(0);
                        normals.Add(1);
                    }
                }

                // Add indices (triangleIndices is a flat array: i0, i1, i2, i3, i4, i5, ...)
                for (int i = 0; i < triangleIndices.Length; i += 3)
                {
                    if (i + 2 < triangleIndices.Length)
                    {
                        indices.Add(baseIndex + triangleIndices[i]);
                        indices.Add(baseIndex + triangleIndices[i + 1]);
                        indices.Add(baseIndex + triangleIndices[i + 2]);
                    }
                }
            }
            catch
            {
                // Skip faces that fail to triangulate
                continue;
            }
        }

        if (positions.Count == 0)
            return null;

        var meshData = new MeshData(
            positions.ToArray(),
            normals.ToArray(),
            null, // No colors for now
            indices.ToArray()
        );

        // Identity transform for now
        var transform = new TransformData(new float[] {
            1, 0, 0, 0,
            0, 1, 0, 0,
            0, 0, 1, 0,
            0, 0, 0, 1
        });

        IReadOnlyList<EdgeData>? edges = null;
        if (detail == SceneDetailLevel.ShadedWithEdges)
        {
            // Could extract edges here, but leaving as null for now
            edges = null;
        }

        return new SceneNode(
            Guid.NewGuid(),
            "Solid",
            meshData,
            edges,
            transform
        );
    }
}
