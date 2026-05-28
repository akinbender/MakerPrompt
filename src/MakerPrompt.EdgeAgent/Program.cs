using MakerPrompt.Core.Abstractions;
using MakerPrompt.Core.Models;
using MakerPrompt.EdgeAgent.Models;
using MakerPrompt.EdgeAgent.Workers;
using MakerPrompt.Infrastructure.InMemoryStores;
using MakerPrompt.Infrastructure.Services;
using MakerPrompt.Infrastructure.Services.Printers;
using MakerPrompt.Infrastructure.Utils;

// Disambiguate: both Core.Abstractions and Infrastructure.Printers define IPrinterCommunicationService
using IPrinterService = MakerPrompt.Core.Abstractions.IPrinterCommunicationService;

var builder = Host.CreateApplicationBuilder(args);
var configuration = builder.Configuration;

// ── Telemetry store ───────────────────────────────────────────────────────────
// Default: in-memory ring buffer.
// Swap to SqliteTelemetryStore or InfluxDbTelemetryStore via DI registration below.
builder.Services.AddSingleton<ITelemetryStore, InMemoryTelemetryStore>();

// ── Camera snapshot store ─────────────────────────────────────────────────────
builder.Services.AddSingleton<ICameraSnapshotStore, InMemoryCameraSnapshotStore>();

// ── Cloud client (optional — only registered when CloudApi:BaseUrl is set) ────
var cloudBaseUrl = configuration["CloudApi:BaseUrl"];
var cloudApiToken = configuration["CloudApi:ApiToken"];
if (!string.IsNullOrWhiteSpace(cloudBaseUrl))
{
    builder.Services.AddHttpClient<HttpEdgeAgentClient>(client =>
    {
        client.BaseAddress = new Uri(cloudBaseUrl.TrimEnd('/') + "/");
        if (!string.IsNullOrWhiteSpace(cloudApiToken))
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", cloudApiToken);
    });
    builder.Services.AddSingleton<IEdgeAgentClient>(sp =>
        sp.GetRequiredService<HttpEdgeAgentClient>());
}

// ── Application-layer fleet manager ──────────────────────────────────────────
builder.Services.AddSingleton<PrinterFleetService>();

// ── Background workers ────────────────────────────────────────────────────────
// Telemetry polling — polls each connected printer, saves to ITelemetryStore.
builder.Services.AddHostedService<PrinterPollingWorker>();

// Camera polling — captures MJPEG snapshots, saves to ICameraSnapshotStore.
builder.Services.AddHostedService<CameraPollingWorker>();

// ── Build ─────────────────────────────────────────────────────────────────────
var host = builder.Build();

// ── Bootstrap printers from EdgeAgent:Printers config ────────────────────────
var fleet = host.Services.GetRequiredService<PrinterFleetService>();
var startupLogger = host.Services.GetRequiredService<ILogger<Program>>();
var printerConfigs = configuration.GetSection("EdgeAgent:Printers")
    .Get<List<PrinterConfig>>() ?? [];

foreach (var cfg in printerConfigs)
{
    if (string.IsNullOrWhiteSpace(cfg.PrinterId))
    {
        startupLogger.LogWarning("Printer entry missing PrinterId — skipping");
        continue;
    }

    if (!Enum.TryParse<PrinterConnectionType>(cfg.Protocol, ignoreCase: true, out var connectionType))
    {
        startupLogger.LogWarning(
            "Unknown protocol '{Protocol}' for printer {PrinterId} — skipping",
            cfg.Protocol, cfg.PrinterId);
        continue;
    }

    IPrinterService service = connectionType switch
    {
        PrinterConnectionType.Moonraker    => new MoonrakerApiService(),
        PrinterConnectionType.PrusaLink    => new PrusaLinkApiService(),
        PrinterConnectionType.PrusaConnect => new PrusaConnectPrinterService(),
        PrinterConnectionType.BambuLab     => new BambuLabApiService(),
        PrinterConnectionType.OctoPrint    => new OctoPrintApiService(),
        _                                  => new DemoPrinterService(),
    };

    var settings = new PrinterConnectionSettings
    {
        ConnectionType = connectionType,
        ApiUrl         = cfg.ApiUrl,
        UserName       = cfg.UserName,
        Password       = cfg.Password,
    };

    startupLogger.LogInformation(
        "Connecting printer {PrinterId} ({Protocol}) at {Url}",
        cfg.PrinterId, cfg.Protocol, cfg.ApiUrl);

    var connected = await fleet.AddAndConnectAsync(cfg.PrinterId, service, settings);
    if (!connected)
        startupLogger.LogWarning(
            "Initial connection failed for printer {PrinterId} — will retry on next poll",
            cfg.PrinterId);
}

// ── Run ───────────────────────────────────────────────────────────────────────
await host.RunAsync();
