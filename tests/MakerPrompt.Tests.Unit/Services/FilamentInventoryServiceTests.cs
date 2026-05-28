using MakerPrompt.Tests.Unit.Helpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace MakerPrompt.Tests.Unit.Services;

/// <summary>
/// Unit tests for <see cref="FilamentInventoryService"/>.
/// </summary>
public sealed class FilamentInventoryServiceTests
{
    private static FilamentInventoryService CreateSut() =>
        new(new InMemoryAppLocalStorageProvider(), NullLogger<FilamentInventoryService>.Instance);

    [Fact]
    public void GetSpools_Initially_ReturnsEmpty()
    {
        var sut = CreateSut();
        Assert.Empty(sut.GetSpools());
    }

    [Fact]
    public async Task AddSpool_SpoolAppearsInList()
    {
        var sut = CreateSut();
        var spool = new FilamentSpool { Name = "PETG Black", Material = "PETG" };

        await sut.AddSpoolAsync(spool);

        var spools = sut.GetSpools();
        Assert.Single(spools);
        Assert.Equal("PETG Black", spools[0].Name);
    }

    [Fact]
    public async Task UpdateSpool_ReplacesExistingEntry()
    {
        var sut = CreateSut();
        var spool = new FilamentSpool { Name = "PLA White" };
        await sut.AddSpoolAsync(spool);

        spool.Name = "PLA White (renamed)";
        await sut.UpdateSpoolAsync(spool);

        Assert.Equal("PLA White (renamed)", sut.GetSpool(spool.Id)?.Name);
    }

    [Fact]
    public async Task DeleteSpool_SpoolNoLongerInList()
    {
        var sut = CreateSut();
        var spool = new FilamentSpool { Name = "ABS Red" };
        await sut.AddSpoolAsync(spool);

        await sut.DeleteSpoolAsync(spool.Id);

        Assert.Empty(sut.GetSpools());
    }

    [Fact]
    public async Task DeductFilament_ReducesRemainingWeight()
    {
        var sut = CreateSut();
        var spool = new FilamentSpool { RemainingWeightGrams = 500 };
        await sut.AddSpoolAsync(spool);

        await sut.DeductFilamentAsync(spool.Id, 100);

        Assert.Equal(400, sut.GetSpool(spool.Id)!.RemainingWeightGrams);
    }

    [Fact]
    public async Task DeductFilament_ClampsAtZero_NeverGoesNegative()
    {
        var sut = CreateSut();
        var spool = new FilamentSpool { RemainingWeightGrams = 50 };
        await sut.AddSpoolAsync(spool);

        await sut.DeductFilamentAsync(spool.Id, 999);

        Assert.Equal(0, sut.GetSpool(spool.Id)!.RemainingWeightGrams);
    }

    [Fact]
    public async Task DeductFilament_UnknownSpool_DoesNotThrow()
    {
        var sut = CreateSut();
        await sut.DeductFilamentAsync(Guid.NewGuid(), 100);
    }

    [Fact]
    public async Task AddSpool_RaisesInventoryChangedEvent()
    {
        var sut = CreateSut();
        var raised = false;
        sut.InventoryChanged += (_, _) => raised = true;

        await sut.AddSpoolAsync(new FilamentSpool { Name = "TPU" });

        Assert.True(raised);
    }

    [Fact]
    public async Task DeleteSpool_RaisesInventoryChangedEvent()
    {
        var sut = CreateSut();
        var spool = new FilamentSpool();
        await sut.AddSpoolAsync(spool);

        var raised = false;
        sut.InventoryChanged += (_, _) => raised = true;

        await sut.DeleteSpoolAsync(spool.Id);

        Assert.True(raised);
    }
}
