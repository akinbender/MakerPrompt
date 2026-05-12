using MakerPrompt.Application.Services;
using MakerPrompt.Core.Abstractions;
using MakerPrompt.EdgeAgent.Workers;
using MakerPrompt.Infrastructure.Telemetry;

var builder = Host.CreateApplicationBuilder(args);

// ── Services ──────────────────────────────────────────────────────────────────

// Local in-memory telemetry store (swap for SQLite persistence in production).
builder.Services.AddSingleton<ITelemetryStore, InMemoryTelemetryStore>();

// Application-layer fleet manager.
builder.Services.AddSingleton<PrinterFleetService>();

// Background worker that polls printers and forwards telemetry.
builder.Services.AddHostedService<PrinterPollingWorker>();

// ── Build & Run ───────────────────────────────────────────────────────────────

var host = builder.Build();
await host.RunAsync();
