using MakerPrompt.Shared.Models;
using MakerPrompt.Shared.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace MakerPrompt.Tests;

public class AnalyticsServiceTests
{
    private static AnalyticsService Create() =>
        new(new InMemoryStorageProvider(), NullLogger<AnalyticsService>.Instance);

    private static PrintJobUsageRecord BuildRecord(
        Guid? printerId = null,
        Guid? spoolId = null,
        double estimated = 0,
        double actual = 0,
        TimeSpan? duration = null) =>
        new()
        {
            PrinterId = printerId ?? Guid.NewGuid(),
            FilamentSpoolId = spoolId ?? Guid.NewGuid(),
            JobName = "test-job",
            EstimatedFilamentUsedGrams = estimated,
            ActualFilamentUsedGrams = actual,
            Duration = duration ?? TimeSpan.FromMinutes(30)
        };

    // ── Record / Read ──

    [Fact]
    public async Task RecordUsageAsync_AddsToInMemoryList()
    {
        var svc = Create();
        await svc.RecordUsageAsync(BuildRecord());
        Assert.Single(svc.GetRecords());
    }

    [Fact]
    public async Task RecordUsageAsync_MultipleRecords_AllAppear()
    {
        var svc = Create();
        await svc.RecordUsageAsync(BuildRecord());
        await svc.RecordUsageAsync(BuildRecord());
        await svc.RecordUsageAsync(BuildRecord());
        Assert.Equal(3, svc.GetRecords().Count);
    }

    [Fact]
    public async Task RecordUsageAsync_FiresAnalyticsUpdatedEvent()
    {
        var svc = Create();
        bool fired = false;
        svc.AnalyticsUpdated += (_, _) => fired = true;
        await svc.RecordUsageAsync(BuildRecord());
        Assert.True(fired);
    }

    // ── Aggregates ──

    [Fact]
    public async Task GetTotalPrintHours_SumsAllDurations()
    {
        var svc = Create();
        await svc.RecordUsageAsync(BuildRecord(duration: TimeSpan.FromHours(1)));
        await svc.RecordUsageAsync(BuildRecord(duration: TimeSpan.FromHours(2)));
        Assert.Equal(TimeSpan.FromHours(3), svc.GetTotalPrintHours());
    }

    [Fact]
    public async Task GetTotalFilamentConsumed_PrefersActualOverEstimated()
    {
        var svc = Create();
        // actual > 0 → use actual (20g), not estimated (50g)
        await svc.RecordUsageAsync(BuildRecord(estimated: 50, actual: 20));
        // actual == 0 → fall back to estimated (30g)
        await svc.RecordUsageAsync(BuildRecord(estimated: 30, actual: 0));
        Assert.Equal(50.0, svc.GetTotalFilamentConsumed(), 2);
    }

    [Fact]
    public async Task GetFilamentConsumedByPrinter_FiltersCorrectly()
    {
        var svc = Create();
        var printerId = Guid.NewGuid();
        await svc.RecordUsageAsync(BuildRecord(printerId: printerId, actual: 15));
        await svc.RecordUsageAsync(BuildRecord(actual: 25));  // different printer
        Assert.Equal(15.0, svc.GetFilamentConsumedByPrinter(printerId), 2);
    }

    [Fact]
    public async Task GetFilamentConsumedBySpool_FiltersCorrectly()
    {
        var svc = Create();
        var spoolId = Guid.NewGuid();
        await svc.RecordUsageAsync(BuildRecord(spoolId: spoolId, actual: 10));
        await svc.RecordUsageAsync(BuildRecord(actual: 40));  // different spool
        Assert.Equal(10.0, svc.GetFilamentConsumedBySpool(spoolId), 2);
    }

    [Fact]
    public void GetRecords_EmptyService_ReturnsEmpty()
    {
        var svc = Create();
        Assert.Empty(svc.GetRecords());
    }

    [Fact]
    public void GetTotalPrintHours_EmptyService_ReturnsZero()
    {
        var svc = Create();
        Assert.Equal(TimeSpan.Zero, svc.GetTotalPrintHours());
    }
}
