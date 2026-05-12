using MakerPrompt.Application.Services;
using MakerPrompt.Core.Abstractions;
using MakerPrompt.EdgeAgent.Workers;
using MakerPrompt.Infrastructure.Camera;
using MakerPrompt.Infrastructure.Telemetry;

var builder = Host.CreateApplicationBuilder(args);
var configuration = builder.Configuration;

// ── Telemetry store ───────────────────────────────────────────────────────────
// Default: in-memory ring buffer.
// Swap to SqliteTelemetryStore or InfluxDbTelemetryStore via DI registration below.
builder.Services.AddSingleton<ITelemetryStore, InMemoryTelemetryStore>();

// ── Camera snapshot store ─────────────────────────────────────────────────────
builder.Services.AddSingleton<ICameraSnapshotStore, InMemoryCameraSnapshotStore>();

// ── Application-layer fleet manager ──────────────────────────────────────────
builder.Services.AddSingleton<PrinterFleetService>();

// ── Background workers ────────────────────────────────────────────────────────
// Telemetry polling — polls each connected printer, saves to ITelemetryStore.
builder.Services.AddHostedService<PrinterPollingWorker>();

// Camera polling — captures MJPEG snapshots, saves to ICameraSnapshotStore.
builder.Services.AddHostedService<CameraPollingWorker>();

// ── Build & Run ───────────────────────────────────────────────────────────────

var host = builder.Build();
await host.RunAsync();
