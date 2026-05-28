using System.Security.Cryptography;
using System.Text;
using MakerPrompt.Core.Abstractions;
using MakerPrompt.Core.Models;
using MakerPrompt.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ── Constants ─────────────────────────────────────────────────────────────────

const int JwtClockSkewMinutes = 5;

// ── Authentication — JWT Bearer / OIDC ───────────────────────────────────────
//
// The Cloud API validates JWTs issued by any OIDC-compliant provider
// (Azure AD, Auth0, Keycloak, etc.).  Configure the authority and audience
// via environment variables or appsettings.json:
//
//   MakerPrompt:Auth:Authority   – OIDC issuer URL  (e.g. https://tenant.auth0.com/)
//   MakerPrompt:Auth:Audience    – API identifier   (e.g. https://api.makerprompt.io)
//   MakerPrompt:Auth:RequireHttpsMetadata – true in production, false in local dev
//
// Edge Agents send a machine-to-machine token; member clients send user tokens.

var authSection = builder.Configuration.GetSection("MakerPrompt:Auth");
var authority = authSection["Authority"];
var audience = authSection["Audience"];
var requireHttps = authSection.GetValue("RequireHttpsMetadata", true);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // If no Authority is configured, we run in open/dev mode.
        if (!string.IsNullOrWhiteSpace(authority))
        {
            options.Authority = authority;
            options.Audience = audience;
            options.RequireHttpsMetadata = requireHttps;

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = !string.IsNullOrWhiteSpace(audience),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(JwtClockSkewMinutes),
            };
        }
        else
        {
            // Development fallback: accept any well-formed token but skip signature.
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = false,
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = false,
                SignatureValidator = (token, _) =>
                {
                    // Only allow the bypass in Development.
                    if (!builder.Environment.IsDevelopment())
                        throw new SecurityTokenValidationException(
                            "Auth:Authority must be configured in non-Development environments.");

                    return new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(token);
                }
            };
        }

        // Swallow token validation exceptions — return 401 instead of 500.
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = ctx =>
            {
                ctx.Response.Headers.Append("WWW-Authenticate",
                    $"Bearer error=\"invalid_token\", " +
                    $"error_description=\"{Uri.EscapeDataString(ctx.Exception.Message)}\"");
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    // Default policy: require any authenticated user / machine.
    options.DefaultPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    // "EdgeAgent" policy: must have the edge-agent scope claim.
    options.AddPolicy("EdgeAgent", policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim("scope", "makerprompt:ingest"));

    // "Member" read policy: authenticated users may read telemetry.
    options.AddPolicy("MemberRead", policy =>
        policy.RequireAuthenticatedUser());
});

// ── Services ─────────────────────────────────────────────────────────────────

// Local in-memory telemetry store (swap for SqliteTelemetryStore / InfluxDbTelemetryStore in production).
builder.Services.AddSingleton<ITelemetryStore, InMemoryTelemetryStore>();

// In-memory camera snapshot store (swap for SqliteCameraSnapshotStore in production).
builder.Services.AddSingleton<ICameraSnapshotStore, InMemoryCameraSnapshotStore>();

// Health checks — available at /health (no auth required).
builder.Services.AddHealthChecks();

// API Explorer for potential future Swagger integration.
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// ── Middleware ────────────────────────────────────────────────────────────────

app.UseHttpsRedirection();

// ── Blazor WASM static files ──────────────────────────────────────────────────
// Serve the Blazor WASM app from wwwroot/ (published by UI.Blazor project reference).
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

// ── Endpoints ─────────────────────────────────────────────────────────────────

// Health check — public, no auth required.
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (ctx, report) =>
    {
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString().ToLowerInvariant(),
            utc = DateTimeOffset.UtcNow,
        });
    }
}).AllowAnonymous();

// Ingest telemetry from an EdgeAgent.
// Accepts either a JWT with the "makerprompt:ingest" scope OR a pre-shared
// SHA-256 API key configured in CloudApi:AgentApiKeyHash.
app.MapPost("/api/telemetry/{printerId}", async (
    string printerId,
    [FromBody] PrinterTelemetry telemetry,
    HttpContext httpContext,
    ITelemetryStore store,
    IConfiguration config,
    CancellationToken ct) =>
{
    // ── SHA-256 API key auth (alternative to JWT) ─────────────────────────
    var keyHash = config["CloudApi:AgentApiKeyHash"];
    if (!string.IsNullOrWhiteSpace(keyHash))
    {
        var authHeader = httpContext.Request.Headers.Authorization.FirstOrDefault();
        var token = authHeader?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) is true
            ? authHeader[7..]
            : null;

        if (token is null)
            return Results.Unauthorized();

        var actualHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

        if (!actualHash.Equals(keyHash.ToLowerInvariant(), StringComparison.Ordinal))
            return Results.Unauthorized();
    }

    // ── Staleness guard ────────────────────────────────────────────────────
    // If the snapshot is older than 30 s, treat the printer as disconnected.
    const int StaleThresholdSeconds = 30;
    if ((DateTime.UtcNow - telemetry.CapturedAt).TotalSeconds > StaleThresholdSeconds)
        telemetry.Status = PrinterStatus.Disconnected;

    await store.SaveAsync(printerId, telemetry, ct);
    return Results.Accepted();
})
.WithName("IngestTelemetry")
.WithTags("Telemetry")
.RequireAuthorization("EdgeAgent");

// Retrieve the latest telemetry for a printer.
// Requires any authenticated user (member read access).
app.MapGet("/api/telemetry/{printerId}/latest", async (
    string printerId,
    ITelemetryStore store,
    CancellationToken ct) =>
{
    var latest = await store.GetLatestAsync(printerId, ct);
    return latest is null ? Results.NotFound() : Results.Ok(latest);
})
.WithName("GetLatestTelemetry")
.WithTags("Telemetry")
.RequireAuthorization("MemberRead");

// Retrieve telemetry history for a printer.
// Requires any authenticated user (member read access).
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
.WithTags("Telemetry")
.RequireAuthorization("MemberRead");

// ── Camera endpoints ──────────────────────────────────────────────────────────

// Ingest a camera snapshot from an EdgeAgent.
// Requires the "makerprompt:ingest" scope (same as telemetry ingest).
app.MapPost("/api/camera/{cameraId}/snapshot", async (
    string cameraId,
    [FromBody] CameraSnapshot snapshot,
    ICameraSnapshotStore cameraStore,
    CancellationToken ct) =>
{
    snapshot.CameraId = cameraId;
    await cameraStore.SaveAsync(snapshot, ct);
    return Results.Accepted();
})
.WithName("IngestCameraSnapshot")
.WithTags("Camera")
.RequireAuthorization("EdgeAgent");

// Retrieve the latest JPEG snapshot for a camera (returns raw JPEG bytes).
app.MapGet("/api/camera/{cameraId}/latest", async (
    string cameraId,
    ICameraSnapshotStore cameraStore,
    CancellationToken ct) =>
{
    var snapshot = await cameraStore.GetLatestAsync(cameraId, ct);
    if (snapshot is null) return Results.NotFound();

    return snapshot.JpegData.Length > 0
        ? Results.File(snapshot.JpegData, "image/jpeg")
        : Results.NotFound();
})
.WithName("GetLatestCameraSnapshot")
.WithTags("Camera")
.RequireAuthorization("MemberRead");

// Retrieve snapshot metadata history for a camera (no image data).
app.MapGet("/api/camera/{cameraId}/history", async (
    string cameraId,
    ICameraSnapshotStore cameraStore,
    [FromQuery] int count = 20,
    CancellationToken ct = default) =>
{
    var history = await cameraStore.GetHistoryAsync(cameraId, count, ct);
    return Results.Ok(history);
})
.WithName("GetCameraSnapshotHistory")
.WithTags("Camera")
.RequireAuthorization("MemberRead");

// ── Run ───────────────────────────────────────────────────────────────────────

// Fallback — serve Blazor WASM for any non-API route (client-side routing).
app.MapFallbackToFile("index.html");

app.Run();

// Make the Program class visible for integration tests
public partial class Program { }
