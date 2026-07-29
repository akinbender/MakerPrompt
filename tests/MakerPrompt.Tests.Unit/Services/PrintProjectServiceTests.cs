using MakerPrompt.Tests.Unit.Helpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace MakerPrompt.Tests.Unit.Services;

/// <summary>
/// Unit tests for <see cref="PrintProjectService"/>.
/// </summary>
public sealed class PrintProjectServiceTests
{
    private static PrintProjectService CreateSut() =>
        new(new InMemoryAppLocalStorageProvider(), NullLogger<PrintProjectService>.Instance);

    [Fact]
    public void GetProjects_Initially_ReturnsEmpty()
    {
        var sut = CreateSut();
        Assert.Empty(sut.Projects);
    }

    [Fact]
    public async Task AddProject_AppearsInList()
    {
        var sut = CreateSut();

        await sut.AddProjectAsync("Test Print", "Some notes");

        Assert.Single(sut.Projects);
        Assert.Equal("Test Print", sut.Projects[0].Name);
        Assert.Equal("Some notes", sut.Projects[0].Notes);
    }

    [Fact]
    public async Task AddProject_TrimsName()
    {
        var sut = CreateSut();

        await sut.AddProjectAsync("  Trimmed  ");

        Assert.Equal("Trimmed", sut.Projects[0].Name);
    }

    [Fact]
    public async Task RenameProject_UpdatesName()
    {
        var sut = CreateSut();
        await sut.AddProjectAsync("Old Name");
        var projectId = sut.Projects[0].Id;

        await sut.RenameProjectAsync(projectId, "New Name");

        Assert.Equal("New Name", sut.Projects[0].Name);
    }

    [Fact]
    public async Task DeleteProject_RemovesFromList()
    {
        var sut = CreateSut();
        await sut.AddProjectAsync("To Delete");
        var projectId = sut.Projects[0].Id;

        await sut.DeleteProjectAsync(projectId);

        Assert.Empty(sut.Projects);
    }

    [Fact]
    public async Task AddJob_JobAppearsInProject()
    {
        var sut = CreateSut();
        await sut.AddProjectAsync("Batch");
        var projectId = sut.Projects[0].Id;
        var content = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("G28\nG0 X10\n"));

        await sut.AddJobAsync(projectId, "test.gcode", content);

        Assert.Single(sut.Projects[0].Jobs);
        Assert.Equal("test.gcode", sut.Projects[0].Jobs[0].FileName);
    }

    [Fact]
    public async Task RemoveJob_JobRemovedFromProject()
    {
        var sut = CreateSut();
        await sut.AddProjectAsync("P");
        var projectId = sut.Projects[0].Id;
        await sut.AddJobAsync(projectId, "file.gcode", new MemoryStream([1, 2, 3]));

        var jobId = sut.Projects[0].Jobs[0].Id;
        await sut.RemoveJobAsync(projectId, jobId);

        Assert.Empty(sut.Projects[0].Jobs);
    }

    [Fact]
    public async Task AssignJob_SetsPrinterAndStatusToPrinting()
    {
        var sut = CreateSut();
        await sut.AddProjectAsync("P");
        var projectId = sut.Projects[0].Id;
        await sut.AddJobAsync(projectId, "job.gcode", new MemoryStream([1]));

        var jobId = sut.Projects[0].Jobs[0].Id;
        var printerId = Guid.NewGuid();

        await sut.AssignJobAsync(projectId, jobId, printerId, "MK4 Alpha");

        var job = sut.Projects[0].Jobs[0];
        Assert.Equal(PrintJobStatus.Printing, job.Status);
        Assert.Equal(printerId, job.AssignedPrinterId);
        Assert.Equal("MK4 Alpha", job.AssignedPrinterName);
    }

    [Fact]
    public async Task UpdateJobStatus_ChangesStatus()
    {
        var sut = CreateSut();
        await sut.AddProjectAsync("P");
        var projectId = sut.Projects[0].Id;
        await sut.AddJobAsync(projectId, "j.gcode", new MemoryStream([1]));

        var jobId = sut.Projects[0].Jobs[0].Id;

        await sut.UpdateJobStatusAsync(projectId, jobId, PrintJobStatus.Completed);

        Assert.Equal(PrintJobStatus.Completed, sut.Projects[0].Jobs[0].Status);
    }

    [Fact]
    public async Task OpenJobFile_ReturnsFileContent()
    {
        var sut = CreateSut();
        await sut.AddProjectAsync("P");
        var projectId = sut.Projects[0].Id;
        var originalBytes = System.Text.Encoding.UTF8.GetBytes("G28\nG1 X100\n");
        await sut.AddJobAsync(projectId, "file.gcode", new MemoryStream(originalBytes));

        var jobId = sut.Projects[0].Jobs[0].Id;
        await using var stream = await sut.OpenJobFileAsync(projectId, jobId);

        Assert.NotNull(stream);
        using var reader = new StreamReader(stream!);
        var content = await reader.ReadToEndAsync();
        Assert.Equal("G28\nG1 X100\n", content);
    }

    [Fact]
    public async Task ProjectsChanged_RaisedOnAdd()
    {
        var sut = CreateSut();
        var raised = false;
        sut.ProjectsChanged += (_, _) => raised = true;

        await sut.AddProjectAsync("Event Test");

        Assert.True(raised);
    }
}
