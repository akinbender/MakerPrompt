using MakerPrompt.Shared.Models;
using MakerPrompt.Shared.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace MakerPrompt.Tests;

public class PrintProjectServiceTests
{
    private static PrintProjectService Create() =>
        new(new InMemoryStorageProvider(), NullLogger<PrintProjectService>.Instance);

    private static Stream TextStream(string text) =>
        new MemoryStream(System.Text.Encoding.UTF8.GetBytes(text));

    // ── Project CRUD ──

    [Fact]
    public async Task AddProjectAsync_AppearsInProjects()
    {
        var svc = Create();
        await svc.InitializeAsync();
        await svc.AddProjectAsync("My Project");
        Assert.Single(svc.Projects);
        Assert.Equal("My Project", svc.Projects[0].Name);
    }

    [Fact]
    public async Task AddProjectAsync_TrimsName()
    {
        var svc = Create();
        await svc.InitializeAsync();
        await svc.AddProjectAsync("  Trimmed  ");
        Assert.Equal("Trimmed", svc.Projects[0].Name);
    }

    [Fact]
    public async Task RenameProjectAsync_UpdatesName()
    {
        var svc = Create();
        await svc.InitializeAsync();
        await svc.AddProjectAsync("Original");
        var id = svc.Projects[0].Id;

        await svc.RenameProjectAsync(id, "Renamed");
        Assert.Equal("Renamed", svc.Projects[0].Name);
    }

    [Fact]
    public async Task RenameProjectAsync_UnknownId_IsNoOp()
    {
        var svc = Create();
        await svc.InitializeAsync();
        await svc.RenameProjectAsync(Guid.NewGuid(), "Should not throw");
        Assert.Empty(svc.Projects);
    }

    [Fact]
    public async Task DeleteProjectAsync_RemovesFromList()
    {
        var svc = Create();
        await svc.InitializeAsync();
        await svc.AddProjectAsync("To Delete");
        var id = svc.Projects[0].Id;

        await svc.DeleteProjectAsync(id);
        Assert.Empty(svc.Projects);
    }

    [Fact]
    public async Task ProjectsChanged_FiredOnAdd()
    {
        var svc = Create();
        await svc.InitializeAsync();
        bool fired = false;
        svc.ProjectsChanged += (_, _) => fired = true;
        await svc.AddProjectAsync("Test");
        Assert.True(fired);
    }

    // ── Job management ──

    [Fact]
    public async Task AddJobAsync_AppearsInProjectJobs()
    {
        var svc = Create();
        await svc.InitializeAsync();
        await svc.AddProjectAsync("Project");
        var projectId = svc.Projects[0].Id;

        await svc.AddJobAsync(projectId, "benchy.gcode", TextStream("G28\nM104 S200"));

        Assert.Single(svc.Projects[0].Jobs);
        Assert.Equal("benchy.gcode", svc.Projects[0].Jobs[0].FileName);
    }

    [Fact]
    public async Task AddJobAsync_UnknownProject_ThrowsInvalidOperation()
    {
        var svc = Create();
        await svc.InitializeAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.AddJobAsync(Guid.NewGuid(), "test.gcode", TextStream("G28")));
    }

    [Fact]
    public async Task RemoveJobAsync_RemovesJobFromProject()
    {
        var svc = Create();
        await svc.InitializeAsync();
        await svc.AddProjectAsync("Project");
        var projectId = svc.Projects[0].Id;
        await svc.AddJobAsync(projectId, "test.gcode", TextStream("G28"));
        var jobId = svc.Projects[0].Jobs[0].Id;

        await svc.RemoveJobAsync(projectId, jobId);
        Assert.Empty(svc.Projects[0].Jobs);
    }

    [Fact]
    public async Task AssignJobAsync_SetsPrinterAndPrintingStatus()
    {
        var svc = Create();
        await svc.InitializeAsync();
        await svc.AddProjectAsync("Project");
        var projectId = svc.Projects[0].Id;
        await svc.AddJobAsync(projectId, "test.gcode", TextStream("G28"));
        var job = svc.Projects[0].Jobs[0];
        var printerId = Guid.NewGuid();

        await svc.AssignJobAsync(projectId, job.Id, printerId, "Prusa MK4");

        var assigned = svc.Projects[0].Jobs[0];
        Assert.Equal(printerId, assigned.AssignedPrinterId);
        Assert.Equal("Prusa MK4", assigned.AssignedPrinterName);
        Assert.Equal(PrintJobStatus.Printing, assigned.Status);
    }

    [Fact]
    public async Task UpdateJobStatusAsync_ChangesStatus()
    {
        var svc = Create();
        await svc.InitializeAsync();
        await svc.AddProjectAsync("Project");
        var projectId = svc.Projects[0].Id;
        await svc.AddJobAsync(projectId, "test.gcode", TextStream("G28"));
        var jobId = svc.Projects[0].Jobs[0].Id;

        await svc.UpdateJobStatusAsync(projectId, jobId, PrintJobStatus.Completed);
        Assert.Equal(PrintJobStatus.Completed, svc.Projects[0].Jobs[0].Status);
    }

    [Fact]
    public async Task OpenJobFileAsync_ReturnsStoredContent()
    {
        var svc = Create();
        await svc.InitializeAsync();
        await svc.AddProjectAsync("Project");
        var projectId = svc.Projects[0].Id;
        const string gcode = "G28\nM104 S200\nM109 S200";
        await svc.AddJobAsync(projectId, "test.gcode", TextStream(gcode));
        var jobId = svc.Projects[0].Jobs[0].Id;

        await using var stream = await svc.OpenJobFileAsync(projectId, jobId);
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();
        Assert.Equal(gcode, content);
    }

    [Fact]
    public async Task OpenJobFileAsync_UnknownJob_ReturnsNull()
    {
        var svc = Create();
        await svc.InitializeAsync();
        var result = await svc.OpenJobFileAsync(Guid.NewGuid(), Guid.NewGuid());
        Assert.Null(result);
    }
}
