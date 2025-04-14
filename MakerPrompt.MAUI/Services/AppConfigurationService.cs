using MakerPrompt.Shared.Infrastructure;
using MakerPrompt.Shared.Utils;
using System.Text.Json;

namespace MakerPrompt.MAUI.Services
{
    public class AppConfigurationService : IAppConfigurationService
    {
        private const string StorageKey = "Mak3rPromptAppConfig";
        private AppConfiguration _config = new();

        public AppConfiguration Configuration => _config;

        public async Task InitializeAsync()
        {
            var json = Preferences.Get(StorageKey, null);
            _config = json != null
                ? JsonSerializer.Deserialize<AppConfiguration>(json)
                : new AppConfiguration();
        }

        public async Task SaveConfigurationAsync()
        {
            Preferences.Set(StorageKey, JsonSerializer.Serialize(_config));
        }

        public async Task ResetToDefaultsAsync()
        {
            _config = new AppConfiguration();
            await SaveConfigurationAsync();
        }
    }
}
