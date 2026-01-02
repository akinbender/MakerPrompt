using CADability;
using CADability.GeoObject;
using System.Text;

namespace MakerPrompt.Shared.ShapeIt.Rendering;

/// <summary>
/// Exports CADability models to various mesh formats.
/// </summary>
public static class MeshExporter
{
    /// <summary>
    /// Exports a CADability Model to STL format.
    /// </summary>
    public static MeshExportResult ExportModelToStl(Model model, MeshExportOptions options)
    {
        if (options.Format != "stl-binary")
            throw new NotSupportedException($"Format '{options.Format}' is not supported. Only 'stl-binary' is currently supported.");

        var triangles = new List<Triangle>();

        // Collect all triangles from all solids
        foreach (var obj in model.AllObjects)
        {
            if (obj is Solid solid)
            {
                CollectTrianglesFromSolid(solid, triangles, options.Tolerance);
            }
        }

        // Write binary STL
        var content = WriteBinaryStl(triangles);

        return new MeshExportResult(
            "model/stl",
            "export.stl",
            content
        );
    }

    private static void CollectTrianglesFromSolid(Solid solid, List<Triangle> triangles, double tolerance)
    {
        foreach (var shell in solid.Shells)
        {
            foreach (var face in shell.Faces)
            {
                try
                {
                    GeoPoint[] vertices;
                    GeoPoint2D[] uvVertices;
                    int[] triangleIndices;
                    BoundingCube extent;
                    
                    face.GetTriangulation(tolerance, out vertices, out uvVertices, out triangleIndices, out extent);
                    if (vertices == null || triangleIndices == null)
                        continue;

                    // Convert to triangles (indices are flat: i0, i1, i2, i3, i4, i5, ...)
                    for (int i = 0; i < triangleIndices.Length; i += 3)
                    {
                        if (i + 2 < triangleIndices.Length)
                        {
                            var v1 = vertices[triangleIndices[i]];
                            var v2 = vertices[triangleIndices[i + 1]];
                            var v3 = vertices[triangleIndices[i + 2]];

                            // Calculate normal
                            var edge1 = new CADability.GeoVector(v2.x - v1.x, v2.y - v1.y, v2.z - v1.z);
                            var edge2 = new CADability.GeoVector(v3.x - v1.x, v3.y - v1.y, v3.z - v1.z);
                            var normal = edge1 ^ edge2; // Cross product
                            normal.Norm();

                            triangles.Add(new Triangle
                            {
                                Normal = normal,
                                V1 = v1,
                                V2 = v2,
                                V3 = v3
                            });
                        }
                    }
                }
                catch
                {
                    // Skip faces that fail to triangulate
                    continue;
                }
            }
        }
    }

    private static byte[] WriteBinaryStl(List<Triangle> triangles)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // Write 80-byte header
        var header = new byte[80];
        var headerText = Encoding.ASCII.GetBytes("Binary STL from MakerPrompt");
        Array.Copy(headerText, header, Math.Min(headerText.Length, 80));
        writer.Write(header);

        // Write triangle count
        writer.Write((uint)triangles.Count);

        // Write triangles
        foreach (var triangle in triangles)
        {
            // Normal
            writer.Write((float)triangle.Normal.x);
            writer.Write((float)triangle.Normal.y);
            writer.Write((float)triangle.Normal.z);

            // Vertex 1
            writer.Write((float)triangle.V1.x);
            writer.Write((float)triangle.V1.y);
            writer.Write((float)triangle.V1.z);

            // Vertex 2
            writer.Write((float)triangle.V2.x);
            writer.Write((float)triangle.V2.y);
            writer.Write((float)triangle.V2.z);

            // Vertex 3
            writer.Write((float)triangle.V3.x);
            writer.Write((float)triangle.V3.y);
            writer.Write((float)triangle.V3.z);

            // Attribute byte count (unused)
            writer.Write((ushort)0);
        }

        return ms.ToArray();
    }

    private struct Triangle
    {
        public CADability.GeoVector Normal;
        public CADability.GeoPoint V1;
        public CADability.GeoPoint V2;
        public CADability.GeoPoint V3;
    }
}
