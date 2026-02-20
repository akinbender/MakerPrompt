using BlazorBootstrap;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;

namespace MakerPrompt.Shared.Components;

/// <summary>
/// Global error boundary that catches unhandled UI/component exceptions,
/// logs them via ILogger and surfaces a toast notification via ToastService.
/// Overrides BuildRenderTree to always render child content so the default
/// Blazor error UI (red banner) is never shown.
/// </summary>
public class GlobalErrorBoundary : ErrorBoundary
{
    [Inject]
    private ILogger<GlobalErrorBoundary> Logger { get; set; } = null!;

    [Inject]
    private ToastService ToastService { get; set; } = null!;

    protected override async Task OnErrorAsync(Exception ex)
    {
        Logger.LogError(ex, "Unhandled UI exception");
        // Reset error state first so the boundary re-renders child content.
        Recover();
        // Yield to let the recovery render batch complete before notifying,
        // otherwise the Toasts StateHasChanged gets swallowed in the same batch.
        await Task.Yield();
        ToastService.Notify(new ToastMessage(
            ToastType.Danger,
            "An unexpected error occurred",
            ex.Message));
    }

    // Always render child content — never let the base class swap in the red
    // error banner, regardless of whether CurrentException is set.
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.AddContent(0, ChildContent);
    }
}
