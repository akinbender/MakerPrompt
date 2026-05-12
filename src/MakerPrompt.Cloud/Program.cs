using MakerPrompt.Core.Abstractions;
using MakerPrompt.Core.Models;
using MakerPrompt.Infrastructure.Telemetry;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// ── Services ─────────────────────────────────────────────────────────────────

// Local in-memory telemetry store (swap for a real persistence layer in production).
builder.Services.AddSingleton<ITelemetryStore, InMemoryTelemetryStore>();

var app = builder.Build();

// ── Middleware ────────────────────────────────────────────────────────────────

app.UseHttpsRedirection();

// ── Endpoints ─────────────────────────────────────────────────────────────────

// Health check
app.MapGet("/health", () => Results.Ok(new { status = "healthy", utc = DateTimeOffset.UtcNow }))
   .WithName("HealthCheck")
   .WithTags("System");

// Ingest telemetry from an EdgeAgent
app.MapPost("/api/telemetry/{printerId}", async (
    string printerId,
    [FromBody] PrinterTelemetry telemetry,
    ITelemetryStore store,
    CancellationToken ct) =>
{
    await store.SaveAsync(printerId, telemetry, ct);
    return Results.Accepted();
})
.WithName("IngestTelemetry")
.WithTags("Telemetry");

// Retrieve the latest telemetry for a printer
app.MapGet("/api/telemetry/{printerId}/latest", async (
    string printerId,
    ITelemetryStore store,
    CancellationToken ct) =>
{
    var latest = await store.GetLatestAsync(printerId, ct);
    return latest is null ? Results.NotFound() : Results.Ok(latest);
})
.WithName("GetLatestTelemetry")
.WithTags("Telemetry");

// Retrieve telemetry history for a printer
app.MapGet("/api/telemetry/{printerId}/history", async (
    string printerId,
    ITelemetryStore store,
    [FromQuery] int count = 100,
    CancellationToken ct = default) =>
{
    var history = await store.GetHistoryAsync(printerId, count, ct);
    return Results.Ok(history);
})
.WithName("GetTelemetryHistory")
.WithTags("Telemetry");

// ── Run ───────────────────────────────────────────────────────────────────────

app.Run();

// Make the Program class visible for integration tests
public partial class Program { }
