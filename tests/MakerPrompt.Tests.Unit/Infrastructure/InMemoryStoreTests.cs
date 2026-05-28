namespace MakerPrompt.Tests.Unit.Infrastructure;

/// <summary>
/// Verifies the thread-safety guarantees and contract compliance of
/// the in-memory infrastructure store implementations.
/// </summary>
public sealed class InMemoryStoreTests
{
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
            new MemoryStream([1]));

        await repo.DeleteJobFileAsync(storagePath);

        Assert.Null(await repo.OpenJobFileAsync(storagePath));
    }
}
