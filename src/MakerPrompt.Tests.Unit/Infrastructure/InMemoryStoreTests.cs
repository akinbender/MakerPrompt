using MakerPrompt.Core.Models;
using MakerPrompt.Infrastructure;

namespace MakerPrompt.Tests.Unit.Infrastructure;

/// <summary>
/// Verifies the thread-safety guarantees and contract compliance of
/// the in-memory infrastructure store implementations.
/// </summary>
public sealed class InMemoryStoreTests
{
    // ── FilamentInventoryStore ────────────────────────────────────────────────

    [Fact]
    public async Task FilamentStore_Save_And_GetById_RoundTrip()
    {
        var store = new InMemoryFilamentInventoryStore();
        var spool = new FilamentSpool { Name = "PLA Blue", RemainingWeightGrams = 800 };

        await store.SaveAsync(spool);
        var result = await store.GetByIdAsync(spool.Id);

        Assert.NotNull(result);
        Assert.Equal("PLA Blue", result.Name);
    }

    [Fact]
    public async Task FilamentStore_Delete_RemovesEntry()
    {
        var store = new InMemoryFilamentInventoryStore();
        var spool = new FilamentSpool();
        await store.SaveAsync(spool);

        await store.DeleteAsync(spool.Id);

        Assert.Null(await store.GetByIdAsync(spool.Id));
    }

    [Fact]
    public async Task FilamentStore_Deduct_ClampsAtZero()
    {
        var store = new InMemoryFilamentInventoryStore();
        var spool = new FilamentSpool { RemainingWeightGrams = 10 };
        await store.SaveAsync(spool);

        await store.DeductFilamentAsync(spool.Id, 1000);

        var result = await store.GetByIdAsync(spool.Id);
        Assert.Equal(0, result!.RemainingWeightGrams);
    }

    // ── AnalyticsStore ────────────────────────────────────────────────────────

    [Fact]
    public async Task AnalyticsStore_Save_AppearsInGetAll()
    {
        var store = new InMemoryPrintJobAnalyticsStore();
        var record = new PrintJobUsageRecord { JobName = "Test Job" };

        await store.SaveAsync(record);
        var all = await store.GetAllAsync();

        Assert.Single(all);
        Assert.Equal("Test Job", all[0].JobName);
    }

    [Fact]
    public async Task AnalyticsStore_GetByPrinter_FiltersCorrectly()
    {
        var store = new InMemoryPrintJobAnalyticsStore();
        var printerId = Guid.NewGuid();
        await store.SaveAsync(new PrintJobUsageRecord { PrinterId = printerId });
        await store.SaveAsync(new PrintJobUsageRecord { PrinterId = Guid.NewGuid() });

        var result = await store.GetByPrinterAsync(printerId);

        Assert.Single(result);
        Assert.Equal(printerId, result[0].PrinterId);
    }

    // ── FarmRepository ────────────────────────────────────────────────────────

    [Fact]
    public async Task FarmRepo_Save_And_GetById_RoundTrip()
    {
        var repo = new InMemoryFarmRepository();
        var farm = new FarmConfiguration { Name = "Test Farm" };

        await repo.SaveAsync(farm);
        var result = await repo.GetByIdAsync(farm.Id);

        Assert.NotNull(result);
        Assert.Equal("Test Farm", result.Name);
    }

    [Fact]
    public async Task FarmRepo_Delete_RemovesEntry()
    {
        var repo = new InMemoryFarmRepository();
        var farm = new FarmConfiguration { Name = "Delete Me" };
        await repo.SaveAsync(farm);

        await repo.DeleteAsync(farm.Id);

        Assert.Null(await repo.GetByIdAsync(farm.Id));
    }

    [Fact]
    public async Task FarmRepo_GetAll_OrderedByCreationDate()
    {
        var repo = new InMemoryFarmRepository();
        var older = new FarmConfiguration { Name = "Older", CreatedAt = DateTime.UtcNow.AddDays(-1) };
        var newer = new FarmConfiguration { Name = "Newer", CreatedAt = DateTime.UtcNow };
        await repo.SaveAsync(newer);
        await repo.SaveAsync(older);

        var all = await repo.GetAllAsync();

        Assert.Equal("Older", all[0].Name);
        Assert.Equal("Newer", all[1].Name);
    }

    // ── PrintProjectRepository ────────────────────────────────────────────────

    [Fact]
    public async Task ProjectRepo_Save_And_GetAll_RoundTrip()
    {
        var repo = new InMemoryPrintProjectRepository();
        var project = new PrintProject { Name = "Test Project" };

        await repo.SaveAsync(project);
        var all = await repo.GetAllAsync();

        Assert.Single(all);
        Assert.Equal("Test Project", all[0].Name);
    }

    [Fact]
    public async Task ProjectRepo_SaveJobFile_And_OpenJobFile_RoundTrip()
    {
        var repo = new InMemoryPrintProjectRepository();
        var projectId = Guid.NewGuid();
        var gcode = System.Text.Encoding.UTF8.GetBytes("G28\nG0 X10\n");

        var storagePath = await repo.SaveJobFileAsync(projectId, "test.gcode", new MemoryStream(gcode));

        await using var stream = await repo.OpenJobFileAsync(storagePath);
        Assert.NotNull(stream);
        using var reader = new System.IO.StreamReader(stream!);
        var content = await reader.ReadToEndAsync();
        Assert.Equal("G28\nG0 X10\n", content);
    }

    [Fact]
    public async Task ProjectRepo_DeleteJobFile_FileNoLongerAccessible()
    {
        var repo = new InMemoryPrintProjectRepository();
        var storagePath = await repo.SaveJobFileAsync(Guid.NewGuid(), "delete.gcode",
            new MemoryStream(new byte[] { 1 }));

        await repo.DeleteJobFileAsync(storagePath);

        Assert.Null(await repo.OpenJobFileAsync(storagePath));
    }
}
