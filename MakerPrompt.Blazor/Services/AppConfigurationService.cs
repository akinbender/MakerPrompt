using MakerPrompt.Shared.Infrastructure;
using MakerPrompt.Shared.Utils;
using Microsoft.JSInterop;
using System.Text.Json;

namespace MakerPrompt.Blazor.Services
{
    public class AppConfigurationService : IAppConfigurationService, IAsyncDisposable
    {
        private const string StorageKey = "AppConfig";
        private readonly IJSRuntime _jsRuntime;
        private AppConfiguration _config = new();

        public AppConfiguration Configuration => _config;

        public AppConfigurationService(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        public async Task InitializeAsync()
        {
            var json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", StorageKey);
            _config = json != null
                ? JsonSerializer.Deserialize<AppConfiguration>(json) ?? new AppConfiguration()
                : new AppConfiguration();
        }

        public async Task SaveConfigurationAsync()
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKey,
                JsonSerializer.Serialize(_config));
        }

        public async Task ResetToDefaultsAsync()
        {
            _config = new AppConfiguration();
            await SaveConfigurationAsync();
        }

        public async ValueTask DisposeAsync() => await SaveConfigurationAsync();
    }
}
