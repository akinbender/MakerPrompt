namespace MakerPrompt.Shared.ShapeIt.Rendering;

/// <summary>
/// Represents the camera state for 3D scene rendering.
/// </summary>
/// <param name="Position">Camera position (x, y, z).</param>
/// <param name="Target">Camera look-at target (x, y, z).</param>
/// <param name="Up">Camera up vector (x, y, z).</param>
/// <param name="FieldOfViewDegrees">Field of view in degrees.</param>
public record CameraState(
    float[] Position,
    float[] Target,
    float[] Up,
    float FieldOfViewDegrees
);
