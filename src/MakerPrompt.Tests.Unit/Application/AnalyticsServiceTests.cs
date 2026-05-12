using MakerPrompt.Application.Services;
using MakerPrompt.Core.Models;
using MakerPrompt.Infrastructure.Analytics;
using Microsoft.Extensions.Logging.Abstractions;

namespace MakerPrompt.Tests.Unit.Application;

/// <summary>
/// Unit tests for <see cref="AnalyticsService"/>.
/// </summary>
public sealed class AnalyticsServiceTests
{
    private static AnalyticsService CreateSut() =>
        new(new InMemoryPrintJobAnalyticsStore(), NullLogger<AnalyticsService>.Instance);

    private static readonly Guid PrinterA = Guid.NewGuid();
    private static readonly Guid PrinterB = Guid.NewGuid();
    private static readonly Guid SpoolX = Guid.NewGuid();

    [Fact]
    public async Task GetRecords_Initially_ReturnsEmpty()
    {
        var sut = CreateSut();
        var records = await sut.GetRecordsAsync();
        Assert.Empty(records);
    }

    [Fact]
    public async Task RecordUsage_AppearsInGetRecords()
    {
        var sut = CreateSut();
        var record = new PrintJobUsageRecord
        {
            PrinterId = PrinterA,
            JobName = "Benchy",
            Duration = TimeSpan.FromHours(2),
            EstimatedFilamentUsedGrams = 30,
        };

        await sut.RecordUsageAsync(record);

        var records = await sut.GetRecordsAsync();
        Assert.Single(records);
        Assert.Equal("Benchy", records[0].JobName);
    }

    [Fact]
    public async Task GetTotalPrintTime_SumsAllDurations()
    {
        var sut = CreateSut();
        await sut.RecordUsageAsync(new PrintJobUsageRecord { Duration = TimeSpan.FromHours(1) });
        await sut.RecordUsageAsync(new PrintJobUsageRecord { Duration = TimeSpan.FromHours(2.5) });

        var total = await sut.GetTotalPrintTimeAsync();

        Assert.Equal(TimeSpan.FromHours(3.5), total);
    }

    [Fact]
    public async Task GetTotalFilamentConsumed_UsesActualWhenPresent()
    {
        var sut = CreateSut();
        await sut.RecordUsageAsync(new PrintJobUsageRecord
        {
            EstimatedFilamentUsedGrams = 50,
            ActualFilamentUsedGrams = 48, // actual overrides estimated
        });
        await sut.RecordUsageAsync(new PrintJobUsageRecord
        {
            EstimatedFilamentUsedGrams = 30,
            ActualFilamentUsedGrams = 0, // use estimated when actual is 0
        });

        var total = await sut.GetTotalFilamentConsumedGramsAsync();

        Assert.Equal(78, total); // 48 + 30
    }

    [Fact]
    public async Task GetFilamentConsumedByPrinter_FiltersCorrectly()
    {
        var sut = CreateSut();
        await sut.RecordUsageAsync(new PrintJobUsageRecord { PrinterId = PrinterA, EstimatedFilamentUsedGrams = 40 });
        await sut.RecordUsageAsync(new PrintJobUsageRecord { PrinterId = PrinterB, EstimatedFilamentUsedGrams = 60 });
        await sut.RecordUsageAsync(new PrintJobUsageRecord { PrinterId = PrinterA, EstimatedFilamentUsedGrams = 20 });

        var consumed = await sut.GetFilamentConsumedByPrinterAsync(PrinterA);

        Assert.Equal(60, consumed); // 40 + 20
    }

    [Fact]
    public async Task GetFilamentConsumedBySpool_FiltersCorrectly()
    {
        var sut = CreateSut();
        await sut.RecordUsageAsync(new PrintJobUsageRecord { FilamentSpoolId = SpoolX, EstimatedFilamentUsedGrams = 35 });
        await sut.RecordUsageAsync(new PrintJobUsageRecord { FilamentSpoolId = Guid.NewGuid(), EstimatedFilamentUsedGrams = 100 });

        var consumed = await sut.GetFilamentConsumedBySpoolAsync(SpoolX);

        Assert.Equal(35, consumed);
    }

    [Fact]
    public async Task RecordUsage_RaisesAnalyticsUpdatedEvent()
    {
        var sut = CreateSut();
        var raised = false;
        sut.AnalyticsUpdated += (_, _) => raised = true;

        await sut.RecordUsageAsync(new PrintJobUsageRecord());

        Assert.True(raised);
    }

    [Fact]
    public async Task GetTotalPrintTime_WhenNoRecords_ReturnsZero()
    {
        var sut = CreateSut();
        var total = await sut.GetTotalPrintTimeAsync();
        Assert.Equal(TimeSpan.Zero, total);
    }
}
