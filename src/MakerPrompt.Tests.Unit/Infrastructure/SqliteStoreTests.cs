using MakerPrompt.Core.Models;
using MakerPrompt.Infrastructure.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace MakerPrompt.Tests.Unit.Infrastructure;

/// <summary>
/// Integration-style tests for the SQLite-backed infrastructure stores.
/// Each test creates a unique temp-file SQLite database and cleans it up on dispose.
/// </summary>
public sealed class SqliteStoreTests
{
    // ── Helper: unique temp-file SQLite database per test ────────────────────

    // SQLite in-memory databases disappear when the last connection closes.
    // Using a temp file per test ensures the schema (created in the constructor)
    // persists across multiple connection open/close cycles in the same test.
    // The returned TempDb is IDisposable and deletes the file on dispose.
    private static TempDb CreateTempDb()
    {
        var path = Path.Combine(Path.GetTempPath(), $"makerprompt_test_{Guid.NewGuid():N}.db");
        return new TempDb(path);
    }

    private sealed class TempDb : IDisposable
    {
        public string ConnectionString { get; }
        private readonly string _path;

        public TempDb(string path)
        {
            _path = path;
            ConnectionString = $"Data Source={path}";
        }

        public void Dispose()
        {
            try { if (File.Exists(_path)) File.Delete(_path); }
            catch { /* best-effort cleanup */ }
        }
    }

    // ── SqliteTelemetryStore ──────────────────────────────────────────────────

    [Fact]
    public async Task TelemetryStore_Save_And_GetLatest_RoundTrip()
    {
        using var db = CreateTempDb();
        await using var store = new SqliteTelemetryStore(
            db.ConnectionString,
            NullLogger<SqliteTelemetryStore>.Instance);

        var telemetry = new PrinterTelemetry
        {
            PrinterName = "Ender-3",
            HotendTemp = 215.0,
            HotendTarget = 215.0,
            BedTemp = 60.0,
            Status = PrinterStatus.Printing,
            PrintProgress = 45.0,
            CapturedAt = DateTimeOffset.UtcNow
        };

        await store.SaveAsync("printer-1", telemetry);
        var latest = await store.GetLatestAsync("printer-1");

        Assert.NotNull(latest);
        Assert.Equal("Ender-3", latest.PrinterName);
        Assert.Equal(215.0, latest.HotendTemp);
        Assert.Equal(PrinterStatus.Printing, latest.Status);
        Assert.Equal(45.0, latest.PrintProgress);
    }

    [Fact]
    public async Task TelemetryStore_GetLatest_ReturnsNull_WhenEmpty()
    {
        using var db = CreateTempDb();
        await using var store = new SqliteTelemetryStore(
            db.ConnectionString,
            NullLogger<SqliteTelemetryStore>.Instance);

        var result = await store.GetLatestAsync("printer-x");
        Assert.Null(result);
    }

    [Fact]
    public async Task TelemetryStore_GetHistory_ReturnsMultipleSnapshots_OrderedNewestFirst()
    {
        using var db = CreateTempDb();
        await using var store = new SqliteTelemetryStore(
            db.ConnectionString,
            NullLogger<SqliteTelemetryStore>.Instance);

        var baseTime = DateTimeOffset.UtcNow;
        for (int i = 0; i < 5; i++)
        {
            await store.SaveAsync("p1", new PrinterTelemetry
            {
                PrinterName = $"Snap-{i}",
                HotendTemp = 200 + i,
                CapturedAt = baseTime.AddSeconds(i)
            });
        }

        var history = await store.GetHistoryAsync("p1", count: 5);

        Assert.Equal(5, history.Count);
        // Newest (highest HotendTemp) should be first.
        Assert.Equal(204.0, history[0].HotendTemp);
    }

    [Fact]
    public async Task TelemetryStore_GetHistory_RespectsCountLimit()
    {
        using var db = CreateTempDb();
        await using var store = new SqliteTelemetryStore(
            db.ConnectionString,
            NullLogger<SqliteTelemetryStore>.Instance);

        for (int i = 0; i < 10; i++)
            await store.SaveAsync("p2", new PrinterTelemetry { CapturedAt = DateTimeOffset.UtcNow.AddSeconds(i) });

        var history = await store.GetHistoryAsync("p2", count: 3);

        Assert.Equal(3, history.Count);
    }

    [Fact]
    public async Task TelemetryStore_IsolatePrinters_DifferentPrinterIdsDontMix()
    {
        using var db = CreateTempDb();
        await using var store = new SqliteTelemetryStore(
            db.ConnectionString,
            NullLogger<SqliteTelemetryStore>.Instance);

        await store.SaveAsync("printerA", new PrinterTelemetry { HotendTemp = 190 });
        await store.SaveAsync("printerB", new PrinterTelemetry { HotendTemp = 230 });

        var latestA = await store.GetLatestAsync("printerA");
        var latestB = await store.GetLatestAsync("printerB");

        Assert.Equal(190.0, latestA!.HotendTemp);
        Assert.Equal(230.0, latestB!.HotendTemp);
    }

    [Fact]
    public async Task TelemetryStore_GetLatest_ReturnsNewestSnapshot()
    {
        using var db = CreateTempDb();
        await using var store = new SqliteTelemetryStore(
            db.ConnectionString,
            NullLogger<SqliteTelemetryStore>.Instance);

        var older = new PrinterTelemetry { HotendTemp = 100, CapturedAt = DateTimeOffset.UtcNow.AddSeconds(-10) };
        var newer = new PrinterTelemetry { HotendTemp = 210, CapturedAt = DateTimeOffset.UtcNow };

        await store.SaveAsync("p3", older);
        await store.SaveAsync("p3", newer);

        var latest = await store.GetLatestAsync("p3");
        Assert.Equal(210.0, latest!.HotendTemp);
    }

    // ── SqliteCameraSnapshotStore ─────────────────────────────────────────────

    [Fact]
    public async Task CameraStore_Save_And_GetLatest_RoundTrip()
    {
        using var db = CreateTempDb();
        await using var store = new SqliteCameraSnapshotStore(
            db.ConnectionString,
            NullLogger<SqliteCameraSnapshotStore>.Instance);

        var jpeg = new byte[] { 0xFF, 0xD8, 0x00, 0x01, 0xFF, 0xD9 }; // minimal JPEG
        var snapshot = new CameraSnapshot
        {
            CameraId = "cam-1",
            Label = "Ender-3 Cam",
            JpegData = jpeg,
            Width = 640,
            Height = 480,
            CapturedAt = DateTimeOffset.UtcNow
        };

        await store.SaveAsync(snapshot);
        var latest = await store.GetLatestAsync("cam-1");

        Assert.NotNull(latest);
        Assert.Equal("Ender-3 Cam", latest.Label);
        Assert.Equal(640, latest.Width);
        Assert.Equal(480, latest.Height);
        Assert.Equal(jpeg, latest.JpegData);
    }

    [Fact]
    public async Task CameraStore_GetLatest_ReturnsNull_WhenEmpty()
    {
        using var db = CreateTempDb();
        await using var store = new SqliteCameraSnapshotStore(
            db.ConnectionString,
            NullLogger<SqliteCameraSnapshotStore>.Instance);

        Assert.Null(await store.GetLatestAsync("cam-x"));
    }

    [Fact]
    public async Task CameraStore_GetHistory_ExcludesJpegBlob()
    {
        using var db = CreateTempDb();
        await using var store = new SqliteCameraSnapshotStore(
            db.ConnectionString,
            NullLogger<SqliteCameraSnapshotStore>.Instance);

        for (int i = 0; i < 3; i++)
        {
            await store.SaveAsync(new CameraSnapshot
            {
                CameraId = "cam-2",
                Label = "Test",
                JpegData = new byte[1000],
                CapturedAt = DateTimeOffset.UtcNow.AddSeconds(i)
            });
        }

        var history = await store.GetHistoryAsync("cam-2", count: 10);

        Assert.Equal(3, history.Count);
        // History should strip JPEG data (returns metadata only).
        Assert.All(history, s => Assert.Empty(s.JpegData));
    }

    [Fact]
    public async Task CameraStore_GetHistory_RespectsCountLimit()
    {
        using var db = CreateTempDb();
        await using var store = new SqliteCameraSnapshotStore(
            db.ConnectionString,
            NullLogger<SqliteCameraSnapshotStore>.Instance);

        for (int i = 0; i < 5; i++)
        {
            await store.SaveAsync(new CameraSnapshot
            {
                CameraId = "cam-3",
                CapturedAt = DateTimeOffset.UtcNow.AddSeconds(i)
            });
        }

        var history = await store.GetHistoryAsync("cam-3", count: 2);
        Assert.Equal(2, history.Count);
    }
}
