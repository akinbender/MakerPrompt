using MakerPrompt.Shared.Infrastructure;
using MakerPrompt.Shared.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.JSInterop;
using System.Text.Json;

namespace MakerPrompt.Blazor.Services
{
    public class AppConfigurationService : IAppConfigurationService, IAsyncDisposable
    {
        private const string StorageKey = "AppConfig";
        private readonly IJSRuntime _jsRuntime;
        private readonly IConfiguration _configuration;
        private AppConfiguration _config = new();

        public AppConfiguration Configuration => _config;

        public AppConfigurationService(IJSRuntime jsRuntime, IConfiguration configuration)
        {
            _jsRuntime = jsRuntime;
            _configuration = configuration;
        }

        public async Task InitializeAsync()
        {
            var json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", StorageKey);
            _config = json != null
                ? JsonSerializer.Deserialize<AppConfiguration>(json) ?? new AppConfiguration()
                : new AppConfiguration();

            // Overlay deployment-time settings from appsettings.json.
            // These cannot be changed at runtime — they reflect the deployment environment.
            ApplyDeploymentSettings();
        }

        public async Task SaveConfigurationAsync()
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKey,
                JsonSerializer.Serialize(_config));
        }

        public async Task ResetToDefaultsAsync()
        {
            _config = new AppConfiguration();
            ApplyDeploymentSettings();
            await SaveConfigurationAsync();
        }

        public async ValueTask DisposeAsync() => await SaveConfigurationAsync();

        // ── private ──────────────────────────────────────────────────────────

        /// <summary>
        /// Reads deployment-time settings from appsettings.json and overlays them
        /// onto the in-memory configuration. These values are not stored in localStorage
        /// because they are controlled by whoever deploys the application, not the end user.
        /// </summary>
        private void ApplyDeploymentSettings()
        {
            var modeStr = _configuration["MakerPrompt:DeploymentMode"] ?? string.Empty;
            _config.DeploymentMode = Enum.TryParse<AppDeploymentMode>(modeStr, true, out var parsedMode)
                ? parsedMode
                : AppDeploymentMode.Standalone;

            _config.CloudApiBaseUrl = _configuration["MakerPrompt:CloudApiBaseUrl"] ?? string.Empty;

            // In CloudMakerspace mode, farm mode is always enabled and the farm name
            // comes from the deployment configuration.
            if (_config.DeploymentMode == AppDeploymentMode.CloudMakerspace)
            {
                _config.FarmModeEnabled = true;

                var configuredFarmName = _configuration["MakerPrompt:FarmName"];
                if (!string.IsNullOrWhiteSpace(configuredFarmName))
                    _config.FarmName = configuredFarmName;
            }
        }
    }
}
