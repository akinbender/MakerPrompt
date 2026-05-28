namespace MakerPrompt.Tests.Unit.Infrastructure;

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
        Assert.Equal(50, history[0].HotendTemp);
    }

    [Fact]
    public async Task GetHistory_CountParameter_LimitsResults()
    {
        for (var i = 0; i < 20; i++)
            await _sut.SaveAsync("p1", new PrinterTelemetry { HotendTemp = i });

        var history = await _sut.GetHistoryAsync("p1", count: 5);

        Assert.Equal(5, history.Count);
    }

    [Fact]
    public async Task RingBuffer_DropsOldestWhenFull()
    {
        var sut = new InMemoryTelemetryStore(maxPerPrinter: 3);

        for (var i = 1; i <= 5; i++)
            await sut.SaveAsync("p1", new PrinterTelemetry { HotendTemp = i * 10 });

        var history = await sut.GetHistoryAsync("p1", count: 100);

        Assert.Equal(3, history.Count);
        Assert.Equal(50, history[0].HotendTemp);
    }

    [Fact]
    public async Task IsolatesPrinters()
    {
        await _sut.SaveAsync("pA", new PrinterTelemetry { HotendTemp = 200 });
        await _sut.SaveAsync("pB", new PrinterTelemetry { HotendTemp = 999 });

        var latest = await _sut.GetLatestAsync("pA");

        Assert.Equal(200, latest!.HotendTemp);
    }
}
