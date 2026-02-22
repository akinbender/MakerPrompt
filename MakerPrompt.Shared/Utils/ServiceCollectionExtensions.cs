using System.Globalization;
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
			services.AddSingleton<PrusaLinkApiService>();
			services.AddSingleton<MoonrakerApiService>();
			services.AddSingleton<BambuLabApiService>();
			services.AddSingleton<OctoPrintApiService>();
			services.AddSingleton<PrinterCommunicationServiceFactory>();
			services.AddScoped<PrinterConnectionManager>();
			services.AddScoped<PrintProjectService>();
			services.AddScoped<FilamentInventoryService>();
			services.AddScoped<AnalyticsService>();
			services.AddScoped<NotificationService>();
			services.AddScoped<IPrinterCameraProvider, PrinterCameraProvider>();
            services.AddScoped<PrinterStorageProvider>();
            services.AddSingleton<GCodeDocumentService>();
            services.AddSingleton<MakerPromptJsInterop>();
            services.AddScoped<LocalizedTitleService>();
            services.AddScoped<ThemeService>();
            services.AddLocalization(options =>
            {
                options.ResourcesPath = "Resources";
            });
            services.AddHttpClient();
            services.AddBlazorBootstrap();  

            return services;
        }
    }
}
