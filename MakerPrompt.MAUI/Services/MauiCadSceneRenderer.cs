using MakerPrompt.Shared.ShapeIt.Rendering;

namespace MakerPrompt.MAUI.Services;

/// <summary>
/// MAUI stub renderer for CAD scenes.
/// This is a minimal implementation placeholder for future MAUI-specific rendering.
/// </summary>
public class MauiCadSceneRenderer : ISceneRenderer
{
    public Task InitializeAsync(CancellationToken ct = default)
    {
        // TODO: Implement MAUI-specific initialization
        return Task.CompletedTask;
    }

    public Task RenderAsync(SceneSnapshot snapshot, CancellationToken ct = default)
    {
        // TODO: Implement MAUI-specific rendering
        return Task.CompletedTask;
    }

    public Task SetCameraAsync(CameraState camera, CancellationToken ct = default)
    {
        // TODO: Implement MAUI-specific camera handling
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        // TODO: Implement MAUI-specific cleanup
        return ValueTask.CompletedTask;
    }
}
