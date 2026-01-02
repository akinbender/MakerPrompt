using Microsoft.Extensions.Logging;
using MakerPrompt.Shared.Utils;
using MakerPrompt.Shared.ShapeIt;
using MakerPrompt.Shared.ShapeIt.Rendering;
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
#endif

            builder.Services.AddLocalization(options =>
            {
                options.ResourcesPath = "Resources";
            });

            //TODO fix
            var supportedCultures = new AppConfiguration().SupportedCultures;
            var localizationOptions = new RequestLocalizationOptions()
                .SetDefaultCulture(supportedCultures[0])
                .AddSupportedCultures(supportedCultures)
                .AddSupportedUICultures(supportedCultures);
            builder.Services.RegisterMakerPromptSharedServices<AppConfigurationService, SerialService>();
            
            // Register ShapeIt CAD services
            builder.Services.AddShapeItForMakerPrompt();
            // Override with MAUI-specific renderer
            builder.Services.AddScoped<ISceneRenderer, MauiCadSceneRenderer>();
            
            return builder.Build();
        }
    }
}
