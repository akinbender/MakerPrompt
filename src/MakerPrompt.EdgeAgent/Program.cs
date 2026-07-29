using System.Net.Http.Headers;
using MakerPrompt.Application.Services;
using MakerPrompt.Core.Abstractions;
using MakerPrompt.EdgeAgent.Workers;
using MakerPrompt.Infrastructure.InMemoryStores;
using MakerPrompt.Infrastructure.Sqlite;
using MakerPrompt.Infrastructure.Utils;

var builder = Host.CreateApplicationBuilder(args);

RegisterStores(builder);
RegisterCloudClient(builder);

builder.Services.AddSingleton<PrinterFleetService>();
builder.Services.AddHostedService<PrinterConnectionWorker>();
builder.Services.AddHostedService<PrinterPollingWorker>();
builder.Services.AddHostedService<CameraPollingWorker>();

await builder.Build().RunAsync();

static void RegisterCloudClient(HostApplicationBuilder builder)
{
    var baseUrl = builder.Configuration["CloudApi:BaseUrl"];
    if (string.IsNullOrWhiteSpace(baseUrl))
    {
        return;
    }

    if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var cloudUri) ||
        cloudUri.Scheme is not ("http" or "https"))
    {
        throw new InvalidOperationException(
            "CloudApi:BaseUrl must be an absolute HTTP or HTTPS URL.");
    }

    var token = builder.Configuration["CloudApi:ApiToken"];
    if (string.IsNullOrWhiteSpace(token))
    {
        throw new InvalidOperationException(
            "CloudApi:ApiToken is required when CloudApi:BaseUrl is configured.");
    }

    builder.Services.AddHttpClient<IEdgeAgentClient, HttpEdgeAgentClient>(client =>
    {
        client.BaseAddress = new Uri(cloudUri.AbsoluteUri.TrimEnd('/') + "/");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("MakerPrompt-EdgeAgent/1.0");
        client.Timeout = TimeSpan.FromSeconds(15);
    });
}

static void RegisterStores(HostApplicationBuilder builder)
{
    var provider = builder.Configuration["EdgeAgent:Storage:Provider"] ?? "Sqlite";
    if (provider.Equals("InMemory", StringComparison.OrdinalIgnoreCase))
    {
        builder.Services.AddSingleton<ITelemetryStore, InMemoryTelemetryStore>();
        builder.Services.AddSingleton<ICameraSnapshotStore, InMemoryCameraSnapshotStore>();
        return;
    }

    if (!provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException(
            $"Unsupported EdgeAgent storage provider '{provider}'.");
    }

    var dataDirectory = Path.Combine(builder.Environment.ContentRootPath, "data");
    Directory.CreateDirectory(dataDirectory);
    var connectionString =
        builder.Configuration.GetConnectionString("MakerPrompt") ??
        $"Data Source={Path.Combine(dataDirectory, "makerprompt-edge.db")};Cache=Shared";
    var telemetryRetention = Math.Max(
        1,
        builder.Configuration.GetValue("EdgeAgent:Storage:TelemetryRetention", 10_000));
    var cameraRetention = Math.Max(
        1,
        builder.Configuration.GetValue("EdgeAgent:Storage:CameraRetention", 200));

    builder.Services.AddSingleton<ITelemetryStore>(services =>
        new SqliteTelemetryStore(
            connectionString,
            services.GetRequiredService<ILogger<SqliteTelemetryStore>>(),
            telemetryRetention));
    builder.Services.AddSingleton<ICameraSnapshotStore>(services =>
        new SqliteCameraSnapshotStore(
            connectionString,
            services.GetRequiredService<ILogger<SqliteCameraSnapshotStore>>(),
            cameraRetention));
}
