using Microsoft.AspNetCore.Components;

namespace MakerPrompt.Shared.Services
{
    public class MakerPromptJsInterop : IAsyncDisposable
    {
        private readonly IJSRuntime jsRuntime;
        private readonly Lazy<Task<IJSObjectReference>> moduleTask;
        private IJSObjectReference? viewerInstance;

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

        public async ValueTask<string> ScrollToBottom(ElementReference container)
        {
            var module = await moduleTask.Value;
            return await module.InvokeAsync<string>("scrollToBottom", container);
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
            viewerInstance = await module.InvokeAsync<IJSObjectReference>(
                "initializeViewer", container, gcodeContent);
        }

        public async ValueTask DisposeAsync()
        {
            if (moduleTask.IsValueCreated)
            {
                var module = await moduleTask.Value;
                await module.DisposeAsync();
            }
        }
    }
}
