using MakerPrompt.Cloud.Authentication;
using MakerPrompt.Core.Abstractions;
using MakerPrompt.Core.Models;
using MakerPrompt.Infrastructure.InMemoryStores;
using MakerPrompt.Infrastructure.Sqlite;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

const int MaxResourceIdLength = 128;
const int MaxCameraBytes = 5 * 1024 * 1024;
const int StaleTelemetrySeconds = 30;

var builder = WebApplication.CreateBuilder(args);
var authSection = builder.Configuration.GetSection("MakerPrompt:Auth");
var authority = authSection["Authority"];
var audience = authSection["Audience"];
var requireHttpsMetadata = authSection.GetValue("RequireHttpsMetadata", true);

if (!builder.Environment.IsDevelopment() &&
    (string.IsNullOrWhiteSpace(authority) || string.IsNullOrWhiteSpace(audience)))
{
    throw new InvalidOperationException(
        "MakerPrompt:Auth:Authority and MakerPrompt:Auth:Audience are required outside Development.");
}

var configuredApiKeyHash = authSection["AgentApiKeyHash"];
if (string.IsNullOrWhiteSpace(configuredApiKeyHash))
{
    configuredApiKeyHash = builder.Configuration["CloudApi:AgentApiKeyHash"];
}
if (!string.IsNullOrWhiteSpace(configuredApiKeyHash) &&
    !AgentApiKeyAuthenticationHandler.TryDecodeSha256(configuredApiKeyHash, out _))
{
    throw new InvalidOperationException(
        "MakerPrompt:Auth:AgentApiKeyHash must be a 64-character SHA-256 hex digest.");
}

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 8 * 1024 * 1024;
});

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        if (!string.IsNullOrWhiteSpace(authority))
        {
            options.Authority = authority;
            options.Audience = audience;
            options.RequireHttpsMetadata = requireHttpsMetadata;
        }

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromMinutes(2),
        };
    })
    .AddScheme<AuthenticationSchemeOptions, AgentApiKeyAuthenticationHandler>(
        AgentApiKeyAuthenticationHandler.SchemeName,
        _ => { });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("EdgeAgent", policy =>
    {
        policy.AddAuthenticationSchemes(
            JwtBearerDefaults.AuthenticationScheme,
            AgentApiKeyAuthenticationHandler.SchemeName);
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(context =>
            context.User.Claims
                .Where(claim => claim.Type is "scope" or "scp")
                .SelectMany(claim => claim.Value.Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries))
                .Contains(
                    AgentApiKeyAuthenticationHandler.IngestScope,
                    StringComparer.Ordinal));
    });

    options.AddPolicy("MemberRead", policy =>
    {
        policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme);
        policy.RequireAuthenticatedUser();
    });
});

RegisterStores(builder);

builder.Services.AddHealthChecks();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();
if (builder.Configuration.GetValue(
        "MakerPrompt:UseHttpsRedirection",
        !app.Environment.IsDevelopment()))
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString().ToLowerInvariant(),
            utc = DateTimeOffset.UtcNow,
        });
    },
}).AllowAnonymous();

app.MapPost("/api/telemetry/{printerId}", async (
    string printerId,
    [FromBody] PrinterTelemetry telemetry,
    ITelemetryStore store,
    CancellationToken cancellationToken) =>
{
    if (!IsValidResourceId(printerId))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [nameof(printerId)] = ["Use 1-128 letters, digits, '.', '_', ':', or '-'."],
        });
    }

    var capturedAt = telemetry.CapturedAt.ToUniversalTime();
    if (capturedAt > DateTimeOffset.UtcNow.AddMinutes(5))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [nameof(telemetry.CapturedAt)] = ["The timestamp is too far in the future."],
        });
    }

    if ((DateTimeOffset.UtcNow - capturedAt).TotalSeconds > StaleTelemetrySeconds)
    {
        telemetry.Status = PrinterStatus.Disconnected;
    }

    telemetry.CapturedAt = capturedAt;
    await store.SaveAsync(printerId, telemetry, cancellationToken);
    return Results.Accepted();
})
.WithName("IngestTelemetry")
.WithTags("Telemetry")
.RequireAuthorization("EdgeAgent");

app.MapGet("/api/telemetry/{printerId}/latest", async (
    string printerId,
    ITelemetryStore store,
    CancellationToken cancellationToken) =>
{
    if (!IsValidResourceId(printerId))
    {
        return Results.BadRequest();
    }

    var latest = await store.GetLatestAsync(printerId, cancellationToken);
    return latest is null ? Results.NotFound() : Results.Ok(latest);
})
.WithName("GetLatestTelemetry")
.WithTags("Telemetry")
.RequireAuthorization("MemberRead");

app.MapGet("/api/telemetry/{printerId}/history", async (
    string printerId,
    ITelemetryStore store,
    [FromQuery] int count = 100,
    CancellationToken cancellationToken = default) =>
{
    if (!IsValidResourceId(printerId))
    {
        return Results.BadRequest();
    }

    var history = await store.GetHistoryAsync(
        printerId,
        Math.Clamp(count, 1, 1000),
        cancellationToken);
    return Results.Ok(history);
})
.WithName("GetTelemetryHistory")
.WithTags("Telemetry")
.RequireAuthorization("MemberRead");

app.MapPost("/api/camera/{cameraId}/snapshot", async (
    string cameraId,
    [FromBody] CameraSnapshot snapshot,
    ICameraSnapshotStore store,
    CancellationToken cancellationToken) =>
{
    if (!IsValidResourceId(cameraId))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [nameof(cameraId)] = ["Use 1-128 letters, digits, '.', '_', ':', or '-'."],
        });
    }

    if (!IsJpeg(snapshot.JpegData) ||
        (snapshot.Label?.Length ?? 0) > 256 ||
        snapshot.Width < 0 ||
        snapshot.Height < 0)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [nameof(snapshot)] =
            [
                $"Supply a JPEG no larger than {MaxCameraBytes} bytes, a label up to 256 characters, and non-negative dimensions.",
            ],
        });
    }

    snapshot.CameraId = cameraId;
    snapshot.Label ??= string.Empty;
    snapshot.CapturedAt = snapshot.CapturedAt.ToUniversalTime();
    await store.SaveAsync(snapshot, cancellationToken);
    return Results.Accepted();
})
.WithName("IngestCameraSnapshot")
.WithTags("Camera")
.RequireAuthorization("EdgeAgent");

app.MapGet("/api/camera/{cameraId}/latest", async (
    string cameraId,
    ICameraSnapshotStore store,
    CancellationToken cancellationToken) =>
{
    if (!IsValidResourceId(cameraId))
    {
        return Results.BadRequest();
    }

    var snapshot = await store.GetLatestAsync(cameraId, cancellationToken);
    return snapshot is { JpegData.Length: > 0 }
        ? Results.File(
            snapshot.JpegData,
            "image/jpeg",
            lastModified: snapshot.CapturedAt)
        : Results.NotFound();
})
.WithName("GetLatestCameraSnapshot")
.WithTags("Camera")
.RequireAuthorization("MemberRead");

app.MapGet("/api/camera/{cameraId}/history", async (
    string cameraId,
    ICameraSnapshotStore store,
    [FromQuery] int count = 20,
    CancellationToken cancellationToken = default) =>
{
    if (!IsValidResourceId(cameraId))
    {
        return Results.BadRequest();
    }

    var history = await store.GetHistoryAsync(
        cameraId,
        Math.Clamp(count, 1, 200),
        cancellationToken);
    return Results.Ok(history);
})
.WithName("GetCameraSnapshotHistory")
.WithTags("Camera")
.RequireAuthorization("MemberRead");

app.Run();

static bool IsValidResourceId(string value) =>
    !string.IsNullOrWhiteSpace(value) &&
    value.Length <= MaxResourceIdLength &&
    value.All(character =>
        char.IsAsciiLetterOrDigit(character) ||
        character is '.' or '_' or ':' or '-');

static bool IsJpeg(byte[] value) =>
    value is { Length: >= 4 and <= MaxCameraBytes } &&
    value[0] == 0xFF &&
    value[1] == 0xD8 &&
    value[^2] == 0xFF &&
    value[^1] == 0xD9;

static void RegisterStores(WebApplicationBuilder builder)
{
    var provider = builder.Configuration["MakerPrompt:Storage:Provider"] ?? "Sqlite";
    if (provider.Equals("InMemory", StringComparison.OrdinalIgnoreCase))
    {
        if (!builder.Environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "The in-memory store is allowed only in Development.");
        }

        builder.Services.AddSingleton<ITelemetryStore, InMemoryTelemetryStore>();
        builder.Services.AddSingleton<ICameraSnapshotStore, InMemoryCameraSnapshotStore>();
        return;
    }

    if (!provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException(
            $"Unsupported MakerPrompt storage provider '{provider}'.");
    }

    var dataDirectory = Path.Combine(builder.Environment.ContentRootPath, "data");
    Directory.CreateDirectory(dataDirectory);
    var connectionString =
        builder.Configuration.GetConnectionString("MakerPrompt") ??
        $"Data Source={Path.Combine(dataDirectory, "makerprompt.db")};Cache=Shared";
    var telemetryRetention = Math.Max(
        1,
        builder.Configuration.GetValue("MakerPrompt:Storage:TelemetryRetention", 10_000));
    var cameraRetention = Math.Max(
        1,
        builder.Configuration.GetValue("MakerPrompt:Storage:CameraRetention", 200));

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

/// <summary>Marker type for integration-test hosting.</summary>
public partial class Program { }
