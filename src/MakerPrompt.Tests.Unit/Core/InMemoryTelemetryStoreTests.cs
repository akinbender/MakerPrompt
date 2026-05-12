using MakerPrompt.Core.Models;
using MakerPrompt.Infrastructure.Telemetry;

namespace MakerPrompt.Tests.Unit.Core;

/// <summary>
/// Unit tests for <see cref="InMemoryTelemetryStore"/>.
/// </summary>
public sealed class InMemoryTelemetryStoreTests
{
    private readonly InMemoryTelemetryStore _sut = new();

    [Fact]
    public async Task GetLatest_WhenNoDataSaved_ReturnsNull()
    {
        var result = await _sut.GetLatestAsync("unknown");
        Assert.Null(result);
    }

    [Fact]
    public async Task SaveAndGetLatest_ReturnsMostRecentSnapshot()
    {
        var first = new PrinterTelemetry { HotendTemp = 200 };
        var second = new PrinterTelemetry { HotendTemp = 210 };

        await _sut.SaveAsync("p1", first);
        await _sut.SaveAsync("p1", second);

        var latest = await _sut.GetLatestAsync("p1");

        Assert.NotNull(latest);
        Assert.Equal(210, latest.HotendTemp);
    }

    [Fact]
    public async Task GetHistory_ReturnsSnapshotsInDescendingOrder()
    {
        for (var i = 1; i <= 5; i++)
            await _sut.SaveAsync("p1", new PrinterTelemetry { HotendTemp = i * 10 });

        var history = await _sut.GetHistoryAsync("p1", count: 5);

        Assert.Equal(5, history.Count);
        // Most-recent first (50 °C was saved last)
        Assert.Equal(50, history[0].HotendTemp);
    }

    [Fact]
    public async Task GetHistory_CountParameter_LimitsResults()
    {
        for (var i = 0; i < 20; i++)
            await _sut.SaveAsync("p1", new PrinterTelemetry());

        var history = await _sut.GetHistoryAsync("p1", count: 5);

        Assert.Equal(5, history.Count);
    }

    [Fact]
    public async Task GetHistory_UnknownPrinterId_ReturnsEmpty()
    {
        var result = await _sut.GetHistoryAsync("nonexistent");
        Assert.Empty(result);
    }

    [Fact]
    public async Task BoundedBuffer_OldestSnapshotEvicted()
    {
        var bounded = new InMemoryTelemetryStore(maxPerPrinter: 3);

        for (var i = 1; i <= 5; i++)
            await bounded.SaveAsync("p1", new PrinterTelemetry { HotendTemp = i * 10 });

        var history = await bounded.GetHistoryAsync("p1", count: 10);

        Assert.Equal(3, history.Count);
        // Should retain the 3 newest: 50, 40, 30
        Assert.DoesNotContain(history, t => t.HotendTemp == 10);
        Assert.DoesNotContain(history, t => t.HotendTemp == 20);
    }

    [Fact]
    public async Task MultiPrinter_DataIsIsolated()
    {
        await _sut.SaveAsync("printer-a", new PrinterTelemetry { HotendTemp = 190 });
        await _sut.SaveAsync("printer-b", new PrinterTelemetry { HotendTemp = 240 });

        var a = await _sut.GetLatestAsync("printer-a");
        var b = await _sut.GetLatestAsync("printer-b");

        Assert.Equal(190, a?.HotendTemp);
        Assert.Equal(240, b?.HotendTemp);
    }
}
