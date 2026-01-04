using Microsoft.Extensions.Logging;
using MakerPrompt.Shared.Utils;
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
            builder.Services.RegisterMakerPromptSharedServices<AppConfigurationService, SerialService>();
            builder.Services.AddScoped<IAppLocalStorageProvider, MauiAppLocalStorageProvider>();
            return builder.Build();
        }
    }
}
