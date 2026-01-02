using MakerPrompt.Shared.ShapeIt.Rendering;
using Microsoft.JSInterop;

namespace MakerPrompt.Blazor.Services;

/// <summary>
/// Web-based CAD scene renderer using a dedicated Web Worker and OffscreenCanvas.
/// </summary>
public class WebCadSceneRenderer : ISceneRenderer
{
    private readonly IJSRuntime _jsRuntime;
    private bool _initialized;

    public WebCadSceneRenderer(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_initialized)
            return;

        // Initialize the CAD renderer with a canvas element
        // The canvas ID would need to be provided by the component using this renderer
        _initialized = true;
        await Task.CompletedTask;
    }

    public async Task RenderAsync(SceneSnapshot snapshot, CancellationToken ct = default)
    {
        if (!_initialized)
            throw new InvalidOperationException("Renderer not initialized. Call InitializeAsync first.");

        // Send scene snapshot to the web worker via JS interop
        await _jsRuntime.InvokeVoidAsync("cadRenderer.render", snapshot);
    }

    public async Task SetCameraAsync(CameraState camera, CancellationToken ct = default)
    {
        if (!_initialized)
            throw new InvalidOperationException("Renderer not initialized. Call InitializeAsync first.");

        // Send camera state to the web worker via JS interop
        await _jsRuntime.InvokeVoidAsync("cadRenderer.setCamera", camera);
    }

    public async ValueTask DisposeAsync()
    {
        if (_initialized)
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("cadRenderer.dispose");
            }
            catch
            {
                // Ignore errors during disposal
            }
            _initialized = false;
        }
    }
}
