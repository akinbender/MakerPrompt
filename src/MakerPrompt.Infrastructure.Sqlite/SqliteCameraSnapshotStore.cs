using MakerPrompt.Core.Abstractions;
using MakerPrompt.Core.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace MakerPrompt.Infrastructure.Sqlite;

/// <summary>
/// SQLite-backed implementation of <see cref="ICameraSnapshotStore"/>.
///
/// Schema
/// ------
/// <code>
/// CREATE TABLE camera_snapshots (
///     id          INTEGER PRIMARY KEY AUTOINCREMENT,
///     camera_id   TEXT    NOT NULL,
///     label       TEXT    NOT NULL,
///     captured_at TEXT    NOT NULL,   -- ISO-8601 UTC
///     width       INTEGER NOT NULL DEFAULT 0,
///     height      INTEGER NOT NULL DEFAULT 0,
///     jpeg_data   BLOB    NOT NULL
/// );
/// CREATE INDEX ix_camera_captured ON camera_snapshots (camera_id, captured_at DESC);
/// </code>
///
/// Usage
/// -----
/// <code>
/// builder.Services.AddSingleton&lt;ICameraSnapshotStore&gt;(sp =>
///     new SqliteCameraSnapshotStore("Data Source=cameras.db", sp.GetRequiredService&lt;ILogger&lt;SqliteCameraSnapshotStore&gt;&gt;()));
/// </code>
/// </summary>
public sealed class SqliteCameraSnapshotStore : ICameraSnapshotStore, IAsyncDisposable
{
    private readonly string _connectionString;
    private readonly ILogger<SqliteCameraSnapshotStore> _logger;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    /// <param name="connectionString">SQLite connection string, e.g. "Data Source=cameras.db".</param>
    /// <param name="logger">Logger.</param>
    public SqliteCameraSnapshotStore(string connectionString, ILogger<SqliteCameraSnapshotStore> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
        InitialiseSchema();
    }

    // ── ICameraSnapshotStore ──────────────────────────────────────────────────

    public async Task SaveAsync(CameraSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await using var conn = await OpenConnectionAsync(cancellationToken);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO camera_snapshots (camera_id, label, captured_at, width, height, jpeg_data)
                VALUES ($cameraId, $label, $capturedAt, $width, $height, $jpegData);
                """;
            cmd.Parameters.AddWithValue("$cameraId", snapshot.CameraId);
            cmd.Parameters.AddWithValue("$label", snapshot.Label);
            cmd.Parameters.AddWithValue("$capturedAt", snapshot.CapturedAt.ToString("O"));
            cmd.Parameters.AddWithValue("$width", snapshot.Width);
            cmd.Parameters.AddWithValue("$height", snapshot.Height);
            cmd.Parameters.AddWithValue("$jpegData", snapshot.JpegData);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<CameraSnapshot?> GetLatestAsync(string cameraId,
        CancellationToken cancellationToken = default)
    {
        await using var conn = await OpenConnectionAsync(cancellationToken);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT camera_id, label, captured_at, width, height, jpeg_data
            FROM camera_snapshots
            WHERE camera_id = $cameraId
            ORDER BY captured_at DESC
            LIMIT 1;
            """;
        cmd.Parameters.AddWithValue("$cameraId", cameraId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;

        return ReadSnapshot(reader, includeBlob: true);
    }

    public async Task<IReadOnlyList<CameraSnapshot>> GetHistoryAsync(
        string cameraId, int count = 20, CancellationToken cancellationToken = default)
    {
        await using var conn = await OpenConnectionAsync(cancellationToken);
        await using var cmd = conn.CreateCommand();
        // History returns metadata only — JPEG blob is excluded to save memory.
        cmd.CommandText = """
            SELECT camera_id, label, captured_at, width, height, NULL as jpeg_data
            FROM camera_snapshots
            WHERE camera_id = $cameraId
            ORDER BY captured_at DESC
            LIMIT $count;
            """;
        cmd.Parameters.AddWithValue("$cameraId", cameraId);
        cmd.Parameters.AddWithValue("$count", count);

        var results = new List<CameraSnapshot>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            results.Add(ReadSnapshot(reader, includeBlob: false));

        return results.AsReadOnly();
    }

    // ── Schema initialisation ─────────────────────────────────────────────────

    private void InitialiseSchema()
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS camera_snapshots (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                camera_id   TEXT    NOT NULL,
                label       TEXT    NOT NULL,
                captured_at TEXT    NOT NULL,
                width       INTEGER NOT NULL DEFAULT 0,
                height      INTEGER NOT NULL DEFAULT 0,
                jpeg_data   BLOB    NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_camera_captured
                ON camera_snapshots (camera_id, captured_at DESC);
            """;
        cmd.ExecuteNonQuery();
        _logger.LogDebug("[SqliteCameraSnapshotStore] Schema initialised ({Connection})", _connectionString);
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

    private static CameraSnapshot ReadSnapshot(SqliteDataReader reader, bool includeBlob)
    {
        return new CameraSnapshot
        {
            CameraId = reader.GetString(0),
            Label = reader.GetString(1),
            CapturedAt = DateTimeOffset.Parse(reader.GetString(2)),
            Width = reader.GetInt32(3),
            Height = reader.GetInt32(4),
            JpegData = includeBlob && !reader.IsDBNull(5)
                ? (byte[])reader.GetValue(5)
                : []
        };
    }

    public ValueTask DisposeAsync()
    {
        _writeLock.Dispose();
        return ValueTask.CompletedTask;
    }
}
