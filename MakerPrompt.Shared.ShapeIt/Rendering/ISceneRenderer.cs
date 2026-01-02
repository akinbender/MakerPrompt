namespace MakerPrompt.Shared.ShapeIt.Rendering;

/// <summary>
/// Interface for rendering 3D scenes.
/// </summary>
public interface ISceneRenderer : IAsyncDisposable
{
    /// <summary>
    /// Initializes the renderer.
    /// </summary>
    Task InitializeAsync(CancellationToken ct = default);

    /// <summary>
    /// Renders the given scene snapshot.
    /// </summary>
    Task RenderAsync(SceneSnapshot snapshot, CancellationToken ct = default);

    /// <summary>
    /// Sets the camera state for rendering.
    /// </summary>
    Task SetCameraAsync(CameraState camera, CancellationToken ct = default);
}
