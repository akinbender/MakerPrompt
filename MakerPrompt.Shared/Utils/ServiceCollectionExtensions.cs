using System.Globalization;
using Blazored.Modal;
using Microsoft.Extensions.DependencyInjection;

namespace MakerPrompt.Shared.Utils
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection RegisterMakerPromptSharedServices<P, L>(this IServiceCollection services) 
            where P : class, IAppConfigurationService
            where L : class, ISerialService
        {
            CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("en-US");
            CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("en-US");

            services.AddScoped<IAppConfigurationService, P>();
            services.AddSingleton<ISerialService, L>();
            services.AddSingleton<PrinterCommunicationServiceFactory>();
            services.AddScoped<MakerPromptJsInterop>();
            services.AddScoped<LocalizedTitleService>();
            services.AddScoped<ThemeService>();
            services.AddLocalization(options =>
            {
                options.ResourcesPath = "Resources";
            });
            services.AddBlazoredModal();
            services.AddHttpClient();
            return services;
        }
    }
}
