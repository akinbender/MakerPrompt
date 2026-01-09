using Microsoft.AspNetCore.Components;

namespace MakerPrompt.Shared.Services
{
    public class MakerPromptJsInterop : IAsyncDisposable
    {
        private readonly IJSRuntime jsRuntime;
        private readonly Lazy<Task<IJSObjectReference>> moduleTask;

        public MakerPromptJsInterop(IJSRuntime jsRuntime)
        {
            this.jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
            moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./_content/MakerPrompt.Shared/js/makerpromptJsInterop.js").AsTask());
        }

        public async ValueTask<string> Prompt(string message)
        {
            var module = await moduleTask.Value;
            return await module.InvokeAsync<string>("showPrompt", message);
        }

        public async ValueTask ScrollToBottom(ElementReference container)
        {
            var module = await moduleTask.Value;
            await module.InvokeVoidAsync("scrollToBottom", container);
        }

        public async ValueTask CopyToClipboard(string text)
        {
            await jsRuntime.InvokeVoidAsync("navigator.clipboard.writeText", text);
        }

        public async ValueTask<string> ReadFromClipboard()
        {
            return await jsRuntime.InvokeAsync<string>("navigator.clipboard.readText");
        }

        // https://github.com/aligator/gcode-viewer implementation
        public async ValueTask InitializeViewerAsync(ElementReference container, string gcodeContent)
        {
            var module = await moduleTask.Value;
            await module.InvokeVoidAsync("initializeViewer", container, gcodeContent);
        }

        public async ValueTask DisposeViewerAsync(ElementReference container)
        {
            var module = await moduleTask.Value;
            await module.InvokeVoidAsync("disposeViewer", container);
        }

        public async ValueTask DisposeAsync()
        {
            if (moduleTask.IsValueCreated)
            {
                try
                {
                    var module = await moduleTask.Value;
                    await module.DisposeAsync();
                }
                catch
                {
                    // ignore JS module dispose errors
                }
            }
        }
    }
}
