using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;
using MakerPrompt.Core.Abstractions;
using MakerPrompt.Core.Models;
using Microsoft.Extensions.Logging;

namespace MakerPrompt.Infrastructure.InfluxDb;

/// <summary>
/// InfluxDB 2.x / 3.x (compatibility API) implementation of <see cref="ITelemetryStore"/>.
///
/// Data model — Line Protocol
/// --------------------------
/// Each telemetry snapshot is stored as a single data point in the
/// <c>printer_telemetry</c> measurement with these fields and tags:
///
/// Tags (indexed, low-cardinality):
///   printer_id  — unique printer identifier
///   printer_name — display name
///   status       — PrinterStatus enum value
///
/// Fields (numeric/string values):
///   hotend_temp, hotend_target, bed_temp, bed_target, chamber_temp, chamber_target
///   feed_rate, flow_rate, fan_speed
///   print_progress, filament_used, print_duration_secs
///   print_job_name
///
/// Configuration
/// -------------
/// Configure via DI:
/// <code>
/// builder.Services.AddSingleton&lt;ITelemetryStore&gt;(sp =>
///     new InfluxDbTelemetryStore(
///         url:    "http://influxdb:8086",
///         token:  "my-api-token",
///         org:    "makerprompt",
///         bucket: "telemetry",
///         sp.GetRequiredService&lt;ILogger&lt;InfluxDbTelemetryStore&gt;&gt;()));
/// </code>
///
/// Or use environment variables:
///   INFLUXDB_URL, INFLUXDB_TOKEN, INFLUXDB_ORG, INFLUXDB_BUCKET
/// </summary>
public sealed class InfluxDbTelemetryStore : ITelemetryStore, IAsyncDisposable
{
    private const string Measurement = "printer_telemetry";

    private readonly InfluxDBClient _client;
    private readonly string _org;
    private readonly string _bucket;
    private readonly ILogger<InfluxDbTelemetryStore> _logger;

    /// <param name="url">InfluxDB base URL (e.g. "http://influxdb:8086").</param>
    /// <param name="token">InfluxDB API token.</param>
    /// <param name="org">InfluxDB organisation name.</param>
    /// <param name="bucket">InfluxDB bucket name.</param>
    /// <param name="logger">Logger.</param>
    public InfluxDbTelemetryStore(
        string url,
        string token,
        string org,
        string bucket,
        ILogger<InfluxDbTelemetryStore> logger)
    {
        _org = org;
        _bucket = bucket;
        _logger = logger;
        _client = new InfluxDBClient(url, token);
    }

    // ── ITelemetryStore ───────────────────────────────────────────────────────

    public async Task SaveAsync(string printerId, PrinterTelemetry telemetry,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var point = BuildPoint(printerId, telemetry);
            var writeApi = _client.GetWriteApiAsync();
            await writeApi.WritePointAsync(point, _bucket, _org, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex,
                "[InfluxDbTelemetryStore] Failed to write point for printer {PrinterId}", printerId);
        }
    }

    public async Task<PrinterTelemetry?> GetLatestAsync(string printerId,
        CancellationToken cancellationToken = default)
    {
        var flux = $"""
            from(bucket: "{EscapeFlux(_bucket)}")
              |> range(start: -30d)
              |> filter(fn: (r) => r["_measurement"] == "{EscapeFlux(Measurement)}")
              |> filter(fn: (r) => r["printer_id"] == "{EscapeFlux(printerId)}")
              |> last()
              |> pivot(rowKey: ["_time"], columnKey: ["_field"], valueColumn: "_value")
            """;

        var tables = await QueryAsync(flux, cancellationToken);
        return tables.SelectMany(t => t.Records)
                     .OrderByDescending(r => r.GetTime())
                     .Select(MapRecord)
                     .FirstOrDefault();
    }

    public async Task<IReadOnlyList<PrinterTelemetry>> GetHistoryAsync(
        string printerId, int count = 100, CancellationToken cancellationToken = default)
    {
        var flux = $"""
            from(bucket: "{EscapeFlux(_bucket)}")
              |> range(start: -30d)
              |> filter(fn: (r) => r["_measurement"] == "{EscapeFlux(Measurement)}")
              |> filter(fn: (r) => r["printer_id"] == "{EscapeFlux(printerId)}")
              |> pivot(rowKey: ["_time"], columnKey: ["_field"], valueColumn: "_value")
              |> sort(columns: ["_time"], desc: true)
              |> limit(n: {count})
            """;

        var tables = await QueryAsync(flux, cancellationToken);
        return tables
            .SelectMany(t => t.Records)
            .Select(MapRecord)
            .Where(t => t is not null)
            .Select(t => t!)
            .ToList()
            .AsReadOnly();
    }

    // ── Line Protocol helpers ─────────────────────────────────────────────────

    private static PointData BuildPoint(string printerId, PrinterTelemetry t)
    {
        return PointData
            .Measurement(Measurement)
            .Tag("printer_id", printerId)
            .Tag("printer_name", t.PrinterName)
            .Tag("status", t.Status.ToString())
            .Field("hotend_temp", t.HotendTemp)
            .Field("hotend_target", t.HotendTarget)
            .Field("bed_temp", t.BedTemp)
            .Field("bed_target", t.BedTarget)
            .Field("chamber_temp", t.ChamberTemp)
            .Field("chamber_target", t.ChamberTarget)
            .Field("feed_rate", (long)t.FeedRate)
            .Field("flow_rate", (long)t.FlowRate)
            .Field("fan_speed", (long)t.FanSpeed)
            .Field("print_progress", t.PrintProgress)
            .Field("filament_used", t.FilamentUsed)
            .Field("print_duration_secs", (long)t.PrintDuration.TotalSeconds)
            .Field("print_job_name", t.PrintJobName)
            .Timestamp(t.CapturedAt.UtcDateTime, WritePrecision.Ns);
    }

    // ── Flux query helpers ────────────────────────────────────────────────────

    private async Task<List<InfluxDB.Client.Core.Flux.Domain.FluxTable>> QueryAsync(
        string flux, CancellationToken cancellationToken)
    {
        try
        {
            var queryApi = _client.GetQueryApi();
            return await queryApi.QueryAsync(flux, _org, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "[InfluxDbTelemetryStore] Flux query failed");
            return [];
        }
    }

    private static PrinterTelemetry? MapRecord(InfluxDB.Client.Core.Flux.Domain.FluxRecord r)
    {
        try
        {
            return new PrinterTelemetry
            {
                PrinterName = GetString(r, "printer_name"),
                Status = Enum.TryParse<PrinterStatus>(GetString(r, "status"), out var s) ? s : PrinterStatus.Disconnected,
                HotendTemp = GetDouble(r, "hotend_temp"),
                HotendTarget = GetDouble(r, "hotend_target"),
                BedTemp = GetDouble(r, "bed_temp"),
                BedTarget = GetDouble(r, "bed_target"),
                ChamberTemp = GetDouble(r, "chamber_temp"),
                ChamberTarget = GetDouble(r, "chamber_target"),
                FeedRate = (int)GetLong(r, "feed_rate", 100),
                FlowRate = (int)GetLong(r, "flow_rate", 100),
                FanSpeed = (int)GetLong(r, "fan_speed"),
                PrintProgress = GetDouble(r, "print_progress"),
                FilamentUsed = GetDouble(r, "filament_used"),
                PrintDuration = TimeSpan.FromSeconds(GetLong(r, "print_duration_secs")),
                PrintJobName = GetString(r, "print_job_name"),
                CapturedAt = r.GetTime() is { } t
                    ? new DateTimeOffset(t.ToDateTimeUtc(), TimeSpan.Zero)
                    : DateTimeOffset.UtcNow
            };
        }
        catch
        {
            return null;
        }
    }

    private static string GetString(InfluxDB.Client.Core.Flux.Domain.FluxRecord r, string key)
        => r.GetValueByKey(key) is string v ? v : string.Empty;

    private static double GetDouble(InfluxDB.Client.Core.Flux.Domain.FluxRecord r, string key)
        => r.GetValueByKey(key) is double v ? v : 0.0;

    private static long GetLong(InfluxDB.Client.Core.Flux.Domain.FluxRecord r, string key, long fallback = 0)
        => r.GetValueByKey(key) is long v ? v : fallback;

    /// <summary>Escapes special characters in Flux string literals.</summary>
    private static string EscapeFlux(string value)
        => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }
}
