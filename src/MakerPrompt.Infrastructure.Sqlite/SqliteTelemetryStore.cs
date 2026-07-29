using System.Text.Json;
using MakerPrompt.Core.Abstractions;
using MakerPrompt.Core.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace MakerPrompt.Infrastructure.Sqlite;

/// <summary>
/// SQLite-backed implementation of <see cref="ITelemetryStore"/>.
///
/// Schema
/// ------
/// <code>
/// CREATE TABLE telemetry_snapshots (
///     id          INTEGER PRIMARY KEY AUTOINCREMENT,
///     printer_id  TEXT    NOT NULL,
///     captured_at TEXT    NOT NULL,   -- ISO-8601 UTC
///     payload     TEXT    NOT NULL    -- JSON-serialised PrinterTelemetry
/// );
/// CREATE INDEX ix_telemetry_printer_captured ON telemetry_snapshots (printer_id, captured_at DESC);
/// </code>
///
/// The full <see cref="PrinterTelemetry"/> model is stored as a JSON payload so the
/// schema never needs to be migrated when new fields are added.
///
/// Usage
/// -----
/// Register via DI in the EdgeAgent or Cloud host:
/// <code>
/// builder.Services.AddSingleton&lt;ITelemetryStore&gt;(sp =>
///     new SqliteTelemetryStore("Data Source=telemetry.db", sp.GetRequiredService&lt;ILogger&lt;SqliteTelemetryStore&gt;&gt;()));
/// </code>
/// </summary>
public sealed class SqliteTelemetryStore : ITelemetryStore, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly string _connectionString;
    private readonly ILogger<SqliteTelemetryStore> _logger;
    private readonly int _maxSnapshotsPerPrinter;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    /// <param name="connectionString">SQLite connection string, e.g. "Data Source=telemetry.db".</param>
    /// <param name="logger">Logger.</param>
    public SqliteTelemetryStore(
        string connectionString,
        ILogger<SqliteTelemetryStore> logger,
        int maxSnapshotsPerPrinter = 10_000)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxSnapshotsPerPrinter);

        _connectionString = connectionString;
        _logger = logger;
        _maxSnapshotsPerPrinter = maxSnapshotsPerPrinter;
        InitialiseSchema();
    }

    // ── ITelemetryStore ───────────────────────────────────────────────────────

    public async Task SaveAsync(string printerId, PrinterTelemetry telemetry,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(printerId);
        ArgumentNullException.ThrowIfNull(telemetry);

        var json = JsonSerializer.Serialize(telemetry, JsonOpts);

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await using var conn = await OpenConnectionAsync(cancellationToken);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO telemetry_snapshots (printer_id, captured_at, payload)
                VALUES ($printerId, $capturedAt, $payload);

                DELETE FROM telemetry_snapshots
                WHERE printer_id = $printerId
                  AND id IN (
                      SELECT id FROM telemetry_snapshots
                      WHERE printer_id = $printerId
                      ORDER BY captured_at DESC, id DESC
                      LIMIT -1 OFFSET $retention
                  );
                """;
            cmd.Parameters.AddWithValue("$printerId", printerId);
            cmd.Parameters.AddWithValue("$capturedAt", telemetry.CapturedAt.ToString("O"));
            cmd.Parameters.AddWithValue("$payload", json);
            cmd.Parameters.AddWithValue("$retention", _maxSnapshotsPerPrinter);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<PrinterTelemetry?> GetLatestAsync(string printerId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(printerId);

        await using var conn = await OpenConnectionAsync(cancellationToken);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT payload FROM telemetry_snapshots
            WHERE printer_id = $printerId
            ORDER BY captured_at DESC
            LIMIT 1;
            """;
        cmd.Parameters.AddWithValue("$printerId", printerId);

        var json = (string?)await cmd.ExecuteScalarAsync(cancellationToken);
        return json is null ? null : Deserialise(json);
    }

    public async Task<IReadOnlyList<PrinterTelemetry>> GetHistoryAsync(
        string printerId, int count = 100, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(printerId);
        count = Math.Clamp(count, 1, 10_000);

        await using var conn = await OpenConnectionAsync(cancellationToken);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT payload FROM telemetry_snapshots
            WHERE printer_id = $printerId
            ORDER BY captured_at DESC
            LIMIT $count;
            """;
        cmd.Parameters.AddWithValue("$printerId", printerId);
        cmd.Parameters.AddWithValue("$count", count);

        var results = new List<PrinterTelemetry>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var item = Deserialise(reader.GetString(0));
            if (item is not null) results.Add(item);
        }

        return results.AsReadOnly();
    }

    // ── Schema initialisation ─────────────────────────────────────────────────

    private void InitialiseSchema()
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS telemetry_snapshots (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                printer_id  TEXT    NOT NULL,
                captured_at TEXT    NOT NULL,
                payload     TEXT    NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_telemetry_printer_captured
                ON telemetry_snapshots (printer_id, captured_at DESC);
            """;
        cmd.ExecuteNonQuery();
        _logger.LogDebug("SQLite telemetry schema initialized");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        return conn;
    }

    private SqliteConnection OpenConnection()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }

    private PrinterTelemetry? Deserialise(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<PrinterTelemetry>(json, JsonOpts);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "[SqliteTelemetryStore] Failed to deserialise telemetry row");
            return null;
        }
    }

    public ValueTask DisposeAsync()
    {
        _writeLock.Dispose();
        return ValueTask.CompletedTask;
    }
}
