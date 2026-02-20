using BlazorBootstrap;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;

namespace MakerPrompt.Shared.Components;

/// <summary>
/// Global error boundary that catches unhandled UI/component exceptions,
/// logs them via ILogger and surfaces a toast notification via ToastService.
/// Wrap the root &lt;Router&gt; with this component in App.razor / Routes.razor.
/// </summary>
public class GlobalErrorBoundary : ErrorBoundary
{
    [Inject]
    private ILogger<GlobalErrorBoundary> Logger { get; set; } = null!;

    [Inject]
    private ToastService ToastService { get; set; } = null!;

    protected override Task OnErrorAsync(Exception ex)
    {
        Logger.LogError(ex, "Unhandled UI exception");
        ToastService.Notify(new ToastMessage(
            ToastType.Danger,
            "An unexpected error occurred",
            ex.Message));
        return Task.CompletedTask;
    }
}
