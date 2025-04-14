using MakerPrompt.Shared.Infrastructure;
using Microsoft.Extensions.Logging;
using MakerPrompt.Shared.Utils;
using Microsoft.AspNetCore.Builder;
using MakerPrompt.MAUI.Services;

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


            builder.Services.AddLocalization(options =>
            {
                options.ResourcesPath = "Resources";
            });

            //TODO fix
            var supportedCultures = new[] { "en-US", "de-DE" };
            var localizationOptions = new RequestLocalizationOptions()
                .SetDefaultCulture(supportedCultures[0])
                .AddSupportedCultures(supportedCultures)
                .AddSupportedUICultures(supportedCultures);


#endif
#if WINDOWS
            builder.Services.RegisterMakerPromptSharedServices<AppConfigurationService, WindowsSerialService>();
#endif
            return builder.Build();
        }
    }
}
