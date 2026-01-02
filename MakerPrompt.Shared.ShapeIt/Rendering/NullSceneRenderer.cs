namespace MakerPrompt.Shared.ShapeIt.Rendering;

/// <summary>
/// A no-op scene renderer used as a default implementation.
/// </summary>
public class NullSceneRenderer : ISceneRenderer
{
    /// <inheritdoc />
    public Task InitializeAsync(CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RenderAsync(SceneSnapshot snapshot, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SetCameraAsync(CameraState camera, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
