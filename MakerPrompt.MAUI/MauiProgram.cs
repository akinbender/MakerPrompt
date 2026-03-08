using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MakerPrompt.Shared.Utils;
using MakerPrompt.Shared.Services;
using Microsoft.AspNetCore.Builder;
using MakerPrompt.MAUI.Services;
using MakerPrompt.MAUI.Storage;
using MakerPrompt.Shared.Infrastructure;

namespace MakerPrompt.MAUI
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            builder.Services.AddMauiBlazorWebView();
#if DEBUG
            // Enable WebView2 remote debugging so E2E tests can connect via
            // Chrome DevTools Protocol (CDP) with Playwright.
            Environment.SetEnvironmentVariable(
                "WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS",
                "--remote-debugging-port=9222");
            builder.Services.AddBlazorWebViewDeveloperTools();
            builder.Logging.AddDebug();
#endif

            builder.Services.AddLocalization(options =>
            {
                options.ResourcesPath = "Resources";
            });

            var supportedCultures = new AppConfiguration().SupportedCultures;
            var localizationOptions = new RequestLocalizationOptions()
                .SetDefaultCulture(supportedCultures[0])
                .AddSupportedCultures(supportedCultures)
                .AddSupportedUICultures(supportedCultures);

            // Restore the user's saved language from MAUI Preferences.
            // The Blazor WASM host reads this from JS localStorage in Program.cs,
            // but MAUI can't invoke JS before Blazor starts. Read from the native
            // Preferences store (same data that AppConfigurationService persists).
            RestoreSavedCulture(supportedCultures);

            builder.Services.RegisterMakerPromptSharedServices<AppConfigurationService, SerialService>();
            builder.Services.AddScoped<IAppLocalStorageProvider, MauiAppLocalStorageProvider>();
            // MAUI: Use AES-256-GCM encryption for stored credentials
            var deviceId = DeviceInfo.Current.Idiom.ToString();
            var deviceName = DeviceInfo.Current.Name ?? "default";
            builder.Services.AddSingleton<IConnectionEncryptionService>(
                new AesConnectionEncryptionService($"MakerPrompt-{deviceId}-{deviceName}"));
            return builder.Build();
        }

        /// <summary>
        /// Reads the saved language from MAUI Preferences and applies it to
        /// the current thread before Blazor starts, so the first render uses
        /// the correct culture. Without this, a force-reload after a language
        /// change in CultureSelector would revert to the default culture.
        /// </summary>
        private static void RestoreSavedCulture(string[] supportedCultures)
        {
            try
            {
                var json = Preferences.Get("Mak3rPromptAppConfig", (string?)null);
                if (json == null) return;

                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("Language", out var langProp)) return;

                var lang = langProp.GetString();
                if (string.IsNullOrEmpty(lang)) return;
                if (!supportedCultures.Contains(lang, StringComparer.OrdinalIgnoreCase)) return;

                var culture = new CultureInfo(lang);
                CultureInfo.DefaultThreadCurrentCulture = culture;
                CultureInfo.DefaultThreadCurrentUICulture = culture;
            }
            catch
            {
                // Config not saved yet or corrupt — use default culture
            }
        }
    }
}
