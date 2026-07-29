using MakerPrompt.Tests.Unit.Helpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace MakerPrompt.Tests.Unit.Services;

/// <summary>
/// Unit tests for <see cref="AnalyticsService"/>.
/// </summary>
public sealed class AnalyticsServiceTests
{
    private static AnalyticsService CreateSut() =>
        new(new InMemoryAppLocalStorageProvider(), NullLogger<AnalyticsService>.Instance);

    private static readonly Guid PrinterA = Guid.NewGuid();
    private static readonly Guid PrinterB = Guid.NewGuid();
    private static readonly Guid SpoolX = Guid.NewGuid();

    [Fact]
    public void GetRecords_Initially_ReturnsEmpty()
    {
        var sut = CreateSut();
        Assert.Empty(sut.GetRecords());
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

        var records = sut.GetRecords();
        Assert.Single(records);
        Assert.Equal("Benchy", records[0].JobName);
    }

    [Fact]
    public async Task GetTotalPrintTime_SumsAllDurations()
    {
        var sut = CreateSut();
        await sut.RecordUsageAsync(new PrintJobUsageRecord { Duration = TimeSpan.FromHours(1) });
        await sut.RecordUsageAsync(new PrintJobUsageRecord { Duration = TimeSpan.FromHours(2.5) });

        Assert.Equal(TimeSpan.FromHours(3.5), sut.GetTotalPrintHours());
    }

    [Fact]
    public async Task GetTotalFilamentConsumed_UsesActualWhenPresent()
    {
        var sut = CreateSut();
        await sut.RecordUsageAsync(new PrintJobUsageRecord
        {
            EstimatedFilamentUsedGrams = 50,
            ActualFilamentUsedGrams = 48,
        });
        await sut.RecordUsageAsync(new PrintJobUsageRecord
        {
            EstimatedFilamentUsedGrams = 30,
            ActualFilamentUsedGrams = 0,
        });

        Assert.Equal(78, sut.GetTotalFilamentConsumed()); // 48 + 30
    }

    [Fact]
    public async Task GetFilamentConsumedByPrinter_FiltersCorrectly()
    {
        var sut = CreateSut();
        await sut.RecordUsageAsync(new PrintJobUsageRecord { PrinterId = PrinterA, EstimatedFilamentUsedGrams = 40 });
        await sut.RecordUsageAsync(new PrintJobUsageRecord { PrinterId = PrinterB, EstimatedFilamentUsedGrams = 60 });
        await sut.RecordUsageAsync(new PrintJobUsageRecord { PrinterId = PrinterA, EstimatedFilamentUsedGrams = 20 });

        Assert.Equal(60, sut.GetFilamentConsumedByPrinter(PrinterA)); // 40 + 20
    }

    [Fact]
    public async Task GetFilamentConsumedBySpool_FiltersCorrectly()
    {
        var sut = CreateSut();
        await sut.RecordUsageAsync(new PrintJobUsageRecord { FilamentSpoolId = SpoolX, EstimatedFilamentUsedGrams = 35 });
        await sut.RecordUsageAsync(new PrintJobUsageRecord { FilamentSpoolId = Guid.NewGuid(), EstimatedFilamentUsedGrams = 100 });

        Assert.Equal(35, sut.GetFilamentConsumedBySpool(SpoolX));
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
    public void GetTotalPrintTime_WhenNoRecords_ReturnsZero()
    {
        var sut = CreateSut();
        Assert.Equal(TimeSpan.Zero, sut.GetTotalPrintHours());
    }
}
