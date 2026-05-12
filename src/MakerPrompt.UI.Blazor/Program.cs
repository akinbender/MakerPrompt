using MakerPrompt.Application.Services;
using MakerPrompt.Core.Abstractions;
using MakerPrompt.Infrastructure.Analytics;
using MakerPrompt.Infrastructure.Farm;
using MakerPrompt.Infrastructure.Inventory;
using MakerPrompt.Infrastructure.Projects;
using MakerPrompt.Infrastructure.Telemetry;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<MakerPrompt.UI.Components.App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// ── Infrastructure stores (in-memory; swap for SQLite/cloud in production) ──
builder.Services.AddSingleton<ITelemetryStore, InMemoryTelemetryStore>();
builder.Services.AddSingleton<IFilamentInventoryStore, InMemoryFilamentInventoryStore>();
builder.Services.AddSingleton<IPrintJobAnalyticsStore, InMemoryPrintJobAnalyticsStore>();
builder.Services.AddSingleton<IPrintProjectRepository, InMemoryPrintProjectRepository>();
builder.Services.AddSingleton<IFarmRepository, InMemoryFarmRepository>();

// ── Application services ─────────────────────────────────────────────────────
builder.Services.AddSingleton<PrinterFleetService>();
builder.Services.AddSingleton<TelemetryAggregationService>();
builder.Services.AddSingleton<FilamentInventoryService>();
builder.Services.AddSingleton<AnalyticsService>();
builder.Services.AddSingleton<PrintProjectService>();
builder.Services.AddSingleton<FarmService>();

// ── Logging ──────────────────────────────────────────────────────────────────
builder.Logging.SetMinimumLevel(LogLevel.Warning);

await builder.Build().RunAsync();
