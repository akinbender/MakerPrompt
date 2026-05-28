using MakerPrompt.Application.Services;
using MakerPrompt.Core.Models;
using MakerPrompt.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace MakerPrompt.Tests.Unit.Application;

/// <summary>
/// Unit tests for <see cref="FilamentInventoryService"/>.
/// </summary>
public sealed class FilamentInventoryServiceTests
{
    private static FilamentInventoryService CreateSut() =>
        new(new InMemoryFilamentInventoryStore(), NullLogger<FilamentInventoryService>.Instance);

    [Fact]
    public async Task GetSpools_Initially_ReturnsEmpty()
    {
        var sut = CreateSut();
        var spools = await sut.GetSpoolsAsync();
        Assert.Empty(spools);
    }

    [Fact]
    public async Task AddSpool_SpoolAppearsInList()
    {
        var sut = CreateSut();
        var spool = new FilamentSpool { Name = "PETG Black", Material = "PETG" };

        await sut.AddSpoolAsync(spool);

        var spools = await sut.GetSpoolsAsync();
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

        var retrieved = await sut.GetSpoolAsync(spool.Id);
        Assert.Equal("PLA White (renamed)", retrieved?.Name);
    }

    [Fact]
    public async Task DeleteSpool_SpoolNoLongerInList()
    {
        var sut = CreateSut();
        var spool = new FilamentSpool { Name = "ABS Red" };
        await sut.AddSpoolAsync(spool);

        await sut.DeleteSpoolAsync(spool.Id);

        var spools = await sut.GetSpoolsAsync();
        Assert.Empty(spools);
    }

    [Fact]
    public async Task DeductFilament_ReducesRemainingWeight()
    {
        var sut = CreateSut();
        var spool = new FilamentSpool { RemainingWeightGrams = 500 };
        await sut.AddSpoolAsync(spool);

        await sut.DeductFilamentAsync(spool.Id, 100);

        var retrieved = await sut.GetSpoolAsync(spool.Id);
        Assert.Equal(400, retrieved!.RemainingWeightGrams);
    }

    [Fact]
    public async Task DeductFilament_ClampsAtZero_NeverGoesNegative()
    {
        var sut = CreateSut();
        var spool = new FilamentSpool { RemainingWeightGrams = 50 };
        await sut.AddSpoolAsync(spool);

        await sut.DeductFilamentAsync(spool.Id, 999);

        var retrieved = await sut.GetSpoolAsync(spool.Id);
        Assert.Equal(0, retrieved!.RemainingWeightGrams);
    }

    [Fact]
    public async Task DeductFilament_UnknownSpool_DoesNotThrow()
    {
        var sut = CreateSut();
        // Should log a warning and return gracefully
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
