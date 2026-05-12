using MakerPrompt.Application.Services;
using MakerPrompt.Core.Abstractions;
using MakerPrompt.Infrastructure.Analytics;
using MakerPrompt.Infrastructure.Farm;
using MakerPrompt.Infrastructure.Inventory;
using MakerPrompt.Infrastructure.Projects;
using MakerPrompt.Infrastructure.Telemetry;

namespace MakerPrompt.UI.MAUI;

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

        // Enable GPU rasterisation in the embedded WebView2 on Windows.
        var webViewArgs = "--ignore-gpu-blocklist --enable-gpu-rasterization";
#if DEBUG
        webViewArgs += " --remote-debugging-port=9223";
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif
        Environment.SetEnvironmentVariable("WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS", webViewArgs);

        // ── Infrastructure stores (in-memory) ────────────────────────────────
        builder.Services.AddSingleton<ITelemetryStore, InMemoryTelemetryStore>();
        builder.Services.AddSingleton<IFilamentInventoryStore, InMemoryFilamentInventoryStore>();
        builder.Services.AddSingleton<IPrintJobAnalyticsStore, InMemoryPrintJobAnalyticsStore>();
        builder.Services.AddSingleton<IPrintProjectRepository, InMemoryPrintProjectRepository>();
        builder.Services.AddSingleton<IFarmRepository, InMemoryFarmRepository>();

        // ── Application services ─────────────────────────────────────────────
        builder.Services.AddSingleton<PrinterFleetService>();
        builder.Services.AddSingleton<TelemetryAggregationService>();
        builder.Services.AddSingleton<FilamentInventoryService>();
        builder.Services.AddSingleton<AnalyticsService>();
        builder.Services.AddSingleton<PrintProjectService>();
        builder.Services.AddSingleton<FarmService>();

        // ── Platform-specific serial service ─────────────────────────────────
        // SerialCommunicationService is a partial class with platform-specific
        // transport implementations compiled conditionally per-platform.
        // It implements IPrinterCommunicationService via SerialCommunicationServiceBase.
        builder.Services.AddTransient<Services.SerialCommunicationService>();

        return builder.Build();
    }
}
