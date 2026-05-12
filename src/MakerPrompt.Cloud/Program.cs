using MakerPrompt.Core.Abstractions;
using MakerPrompt.Core.Models;
using MakerPrompt.Infrastructure.Telemetry;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

// Local in-memory telemetry store (swap for a real persistence layer in production).
builder.Services.AddSingleton<ITelemetryStore, InMemoryTelemetryStore>();

// API Explorer for potential future Swagger integration.
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// ── Middleware ────────────────────────────────────────────────────────────────

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

// ── Endpoints ─────────────────────────────────────────────────────────────────

// Health check — public, no auth required.
app.MapGet("/health", () => Results.Ok(new { status = "healthy", utc = DateTimeOffset.UtcNow }))
   .WithName("HealthCheck")
   .WithTags("System")
   .AllowAnonymous();

// Ingest telemetry from an EdgeAgent.
// Requires the "makerprompt:ingest" scope (machine-to-machine token from EdgeAgent).
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

// ── Run ───────────────────────────────────────────────────────────────────────

app.Run();

// Make the Program class visible for integration tests
public partial class Program { }
