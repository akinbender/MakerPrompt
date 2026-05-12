using MakerPrompt.Application.Services;
using MakerPrompt.Core.Models;
using MakerPrompt.Infrastructure.Projects;
using Microsoft.Extensions.Logging.Abstractions;

namespace MakerPrompt.Tests.Unit.Application;

/// <summary>
/// Unit tests for <see cref="PrintProjectService"/>.
/// </summary>
public sealed class PrintProjectServiceTests
{
    private static PrintProjectService CreateSut() =>
        new(new InMemoryPrintProjectRepository(), NullLogger<PrintProjectService>.Instance);

    [Fact]
    public async Task GetProjects_Initially_ReturnsEmpty()
    {
        var sut = CreateSut();
        var projects = await sut.GetProjectsAsync();
        Assert.Empty(projects);
    }

    [Fact]
    public async Task CreateProject_AppearsInList()
    {
        var sut = CreateSut();

        var project = await sut.CreateProjectAsync("Test Print", "Some notes");

        var projects = await sut.GetProjectsAsync();
        Assert.Single(projects);
        Assert.Equal("Test Print", projects[0].Name);
        Assert.Equal("Some notes", projects[0].Notes);
    }

    [Fact]
    public async Task CreateProject_TrimsName()
    {
        var sut = CreateSut();

        var project = await sut.CreateProjectAsync("  Trimmed  ");

        Assert.Equal("Trimmed", project.Name);
    }

    [Fact]
    public async Task RenameProject_UpdatesName()
    {
        var sut = CreateSut();
        var project = await sut.CreateProjectAsync("Old Name");

        await sut.RenameProjectAsync(project.Id, "New Name");

        var retrieved = await sut.GetProjectAsync(project.Id);
        Assert.Equal("New Name", retrieved?.Name);
    }

    [Fact]
    public async Task DeleteProject_RemovesFromList()
    {
        var sut = CreateSut();
        var project = await sut.CreateProjectAsync("To Delete");

        await sut.DeleteProjectAsync(project.Id);

        var projects = await sut.GetProjectsAsync();
        Assert.Empty(projects);
    }

    [Fact]
    public async Task AddJob_JobAppearsInProject()
    {
        var sut = CreateSut();
        var project = await sut.CreateProjectAsync("Batch");
        var content = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("G28\nG0 X10\n"));

        await sut.AddJobAsync(project.Id, "test.gcode", content);

        var retrieved = await sut.GetProjectAsync(project.Id);
        Assert.Single(retrieved!.Jobs);
        Assert.Equal("test.gcode", retrieved.Jobs[0].FileName);
    }

    [Fact]
    public async Task RemoveJob_JobRemovedFromProject()
    {
        var sut = CreateSut();
        var project = await sut.CreateProjectAsync("P");
        var content = new MemoryStream(new byte[] { 1, 2, 3 });
        await sut.AddJobAsync(project.Id, "file.gcode", content);

        var retrieved = await sut.GetProjectAsync(project.Id);
        var jobId = retrieved!.Jobs[0].Id;
        await sut.RemoveJobAsync(project.Id, jobId);

        retrieved = await sut.GetProjectAsync(project.Id);
        Assert.Empty(retrieved!.Jobs);
    }

    [Fact]
    public async Task AssignJob_SetsPrinterAndStatusToPrinting()
    {
        var sut = CreateSut();
        var project = await sut.CreateProjectAsync("P");
        var content = new MemoryStream(new byte[] { 1 });
        await sut.AddJobAsync(project.Id, "job.gcode", content);

        var retrieved = await sut.GetProjectAsync(project.Id);
        var jobId = retrieved!.Jobs[0].Id;
        var printerId = Guid.NewGuid();

        await sut.AssignJobAsync(project.Id, jobId, printerId, "MK4 Alpha");

        retrieved = await sut.GetProjectAsync(project.Id);
        var job = retrieved!.Jobs[0];
        Assert.Equal(PrintJobStatus.Printing, job.Status);
        Assert.Equal(printerId, job.AssignedPrinterId);
        Assert.Equal("MK4 Alpha", job.AssignedPrinterName);
    }

    [Fact]
    public async Task UpdateJobStatus_ChangesStatus()
    {
        var sut = CreateSut();
        var project = await sut.CreateProjectAsync("P");
        await sut.AddJobAsync(project.Id, "j.gcode", new MemoryStream(new byte[] { 1 }));

        var retrieved = await sut.GetProjectAsync(project.Id);
        var jobId = retrieved!.Jobs[0].Id;

        await sut.UpdateJobStatusAsync(project.Id, jobId, PrintJobStatus.Completed);

        retrieved = await sut.GetProjectAsync(project.Id);
        Assert.Equal(PrintJobStatus.Completed, retrieved!.Jobs[0].Status);
    }

    [Fact]
    public async Task OpenJobFile_ReturnsFileContent()
    {
        var sut = CreateSut();
        var project = await sut.CreateProjectAsync("P");
        var originalBytes = System.Text.Encoding.UTF8.GetBytes("G28\nG1 X100\n");
        await sut.AddJobAsync(project.Id, "file.gcode", new MemoryStream(originalBytes));

        var retrieved = await sut.GetProjectAsync(project.Id);
        var jobId = retrieved!.Jobs[0].Id;

        await using var stream = await sut.OpenJobFileAsync(project.Id, jobId);

        Assert.NotNull(stream);
        using var reader = new System.IO.StreamReader(stream!);
        var content = await reader.ReadToEndAsync();
        Assert.Equal("G28\nG1 X100\n", content);
    }

    [Fact]
    public async Task ProjectsChanged_RaisedOnCreate()
    {
        var sut = CreateSut();
        var raised = false;
        sut.ProjectsChanged += (_, _) => raised = true;

        await sut.CreateProjectAsync("Event Test");

        Assert.True(raised);
    }
}
