using MakerPrompt.Shared.Models;
using MakerPrompt.Shared.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace MakerPrompt.Tests;

public class FilamentInventoryServiceTests
{
    private static FilamentInventoryService Create() =>
        new(new InMemoryStorageProvider(), NullLogger<FilamentInventoryService>.Instance);

    private static FilamentSpool BuildSpool(string name = "PLA White", double remaining = 800) =>
        new() { Name = name, Material = "PLA", TotalWeightGrams = 1000, RemainingWeightGrams = remaining };

    // ── Add / Get ──

    [Fact]
    public async Task AddSpoolAsync_AppearsInGetSpools()
    {
        var svc = Create();
        var spool = BuildSpool();
        await svc.AddSpoolAsync(spool);
        Assert.Single(svc.GetSpools());
        Assert.Equal("PLA White", svc.GetSpools()[0].Name);
    }

    [Fact]
    public async Task AddSpoolAsync_MultipleSpools_AllVisible()
    {
        var svc = Create();
        await svc.AddSpoolAsync(BuildSpool("PLA Red"));
        await svc.AddSpoolAsync(BuildSpool("PETG Black"));
        Assert.Equal(2, svc.GetSpools().Count);
    }

    [Fact]
    public async Task GetSpool_ExistingId_ReturnsCorrectSpool()
    {
        var svc = Create();
        var spool = BuildSpool("ABS Blue");
        await svc.AddSpoolAsync(spool);
        var retrieved = svc.GetSpool(spool.Id);
        Assert.NotNull(retrieved);
        Assert.Equal("ABS Blue", retrieved.Name);
    }

    [Fact]
    public async Task GetSpool_UnknownId_ReturnsNull()
    {
        var svc = Create();
        Assert.Null(svc.GetSpool(Guid.NewGuid()));
    }

    // ── Update ──

    [Fact]
    public async Task UpdateSpoolAsync_ChangesFieldsInPlace()
    {
        var svc = Create();
        var spool = BuildSpool("Old Name");
        await svc.AddSpoolAsync(spool);

        spool.Name = "New Name";
        spool.RemainingWeightGrams = 500;
        await svc.UpdateSpoolAsync(spool);

        var updated = svc.GetSpool(spool.Id);
        Assert.NotNull(updated);
        Assert.Equal("New Name", updated.Name);
        Assert.Equal(500, updated.RemainingWeightGrams);
    }

    // ── Delete ──

    [Fact]
    public async Task DeleteSpoolAsync_RemovesFromList()
    {
        var svc = Create();
        var spool = BuildSpool();
        await svc.AddSpoolAsync(spool);
        await svc.DeleteSpoolAsync(spool.Id);
        Assert.Empty(svc.GetSpools());
    }

    [Fact]
    public async Task DeleteSpoolAsync_UnknownId_DoesNotThrow()
    {
        var svc = Create();
        // Should be a no-op
        await svc.DeleteSpoolAsync(Guid.NewGuid());
    }

    // ── Deduct ──

    [Fact]
    public async Task DeductFilamentAsync_ReducesRemainingWeight()
    {
        var svc = Create();
        var spool = BuildSpool(remaining: 500);
        await svc.AddSpoolAsync(spool);

        await svc.DeductFilamentAsync(spool.Id, 100);

        var updated = svc.GetSpool(spool.Id);
        Assert.NotNull(updated);
        Assert.Equal(400, updated.RemainingWeightGrams);
    }

    [Fact]
    public async Task DeductFilamentAsync_MoreThanRemaining_ClampsAtZero()
    {
        var svc = Create();
        var spool = BuildSpool(remaining: 50);
        await svc.AddSpoolAsync(spool);

        await svc.DeductFilamentAsync(spool.Id, 200);

        var updated = svc.GetSpool(spool.Id);
        Assert.NotNull(updated);
        Assert.Equal(0, updated.RemainingWeightGrams);
    }

    // ── Events ──

    [Fact]
    public async Task AddSpoolAsync_FiresInventoryChangedEvent()
    {
        var svc = Create();
        bool fired = false;
        svc.InventoryChanged += (_, _) => fired = true;
        await svc.AddSpoolAsync(BuildSpool());
        Assert.True(fired);
    }

    [Fact]
    public async Task DeleteSpoolAsync_FiresInventoryChangedEvent()
    {
        var svc = Create();
        var spool = BuildSpool();
        await svc.AddSpoolAsync(spool);
        bool fired = false;
        svc.InventoryChanged += (_, _) => fired = true;
        await svc.DeleteSpoolAsync(spool.Id);
        Assert.True(fired);
    }
}
