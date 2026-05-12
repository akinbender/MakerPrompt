using MakerPrompt.Application.Services;
using MakerPrompt.Core.Models;
using MakerPrompt.Infrastructure.Farm;
using Microsoft.Extensions.Logging.Abstractions;

namespace MakerPrompt.Tests.Unit.Application;

/// <summary>
/// Unit tests for <see cref="FarmService"/>.
/// </summary>
public sealed class FarmServiceTests
{
    private static FarmService CreateSut() =>
        new(new InMemoryFarmRepository(), NullLogger<FarmService>.Instance);

    [Fact]
    public async Task GetFarms_Initially_ReturnsEmpty()
    {
        var sut = CreateSut();
        var farms = await sut.GetFarmsAsync();
        Assert.Empty(farms);
    }

    [Fact]
    public async Task CreateFarm_AppearsInList()
    {
        var sut = CreateSut();

        var farm = await sut.CreateFarmAsync("Hackerspace A");

        var farms = await sut.GetFarmsAsync();
        Assert.Single(farms);
        Assert.Equal("Hackerspace A", farms[0].Name);
    }

    [Fact]
    public async Task CreateFarm_TrimsName()
    {
        var sut = CreateSut();
        var farm = await sut.CreateFarmAsync("  Workshop  ");
        Assert.Equal("Workshop", farm.Name);
    }

    [Fact]
    public async Task RenameFarm_UpdatesName()
    {
        var sut = CreateSut();
        var farm = await sut.CreateFarmAsync("Old");

        await sut.RenameFarmAsync(farm.Id, "New");

        var retrieved = await sut.GetFarmAsync(farm.Id);
        Assert.Equal("New", retrieved?.Name);
    }

    [Fact]
    public async Task RenameFarm_UnknownId_DoesNotThrow()
    {
        var sut = CreateSut();
        await sut.RenameFarmAsync(Guid.NewGuid(), "Whatever");
        // No exception expected
    }

    [Fact]
    public async Task DeleteFarm_RemovesFromList()
    {
        var sut = CreateSut();
        var farm = await sut.CreateFarmAsync("To Delete");

        await sut.DeleteFarmAsync(farm.Id);

        var farms = await sut.GetFarmsAsync();
        Assert.Empty(farms);
    }

    [Fact]
    public async Task SnapshotPrinters_StoresPrinterListInFarm()
    {
        var sut = CreateSut();
        var farm = await sut.CreateFarmAsync("F");
        var printers = new[]
        {
            new PrinterConnectionDefinition { Name = "MK4 Alpha" },
            new PrinterConnectionDefinition { Name = "MK4 Beta" },
        };

        await sut.SnapshotPrintersAsync(farm.Id, printers);

        var retrieved = await sut.GetFarmAsync(farm.Id);
        Assert.Equal(2, retrieved!.Printers.Count);
        Assert.Contains(retrieved.Printers, p => p.Name == "MK4 Alpha");
    }

    [Fact]
    public async Task ExportFarm_ReturnsValidJson()
    {
        var sut = CreateSut();
        var farm = await sut.CreateFarmAsync("Export Test");

        var json = await sut.ExportFarmAsync(farm.Id);

        Assert.Contains("Export Test", json);
        Assert.Contains("\"Name\"", json);
    }

    [Fact]
    public async Task ImportFarm_AddsNewFarmWithFreshId()
    {
        var sut = CreateSut();
        var original = await sut.CreateFarmAsync("Original Farm");
        var json = await sut.ExportFarmAsync(original.Id);

        var imported = await sut.ImportFarmAsync(json);

        // Imported farm should get a new ID
        Assert.NotEqual(original.Id, imported.Id);
        // But same name
        Assert.Equal("Original Farm", imported.Name);

        var farms = await sut.GetFarmsAsync();
        Assert.Equal(2, farms.Count);
    }

    [Fact]
    public async Task FarmsChanged_RaisedOnCreate()
    {
        var sut = CreateSut();
        var raised = false;
        sut.FarmsChanged += (_, _) => raised = true;

        await sut.CreateFarmAsync("Event Farm");

        Assert.True(raised);
    }

    [Fact]
    public async Task ExportFarm_UnknownId_ReturnsEmptyObject()
    {
        var sut = CreateSut();
        var result = await sut.ExportFarmAsync(Guid.NewGuid());
        Assert.Equal("{}", result);
    }
}
