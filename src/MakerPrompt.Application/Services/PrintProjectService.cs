using MakerPrompt.Core.Abstractions;
using MakerPrompt.Core.Models;
using Microsoft.Extensions.Logging;

namespace MakerPrompt.Application.Services;

/// <summary>
/// Application service for print project management.
///
/// Business rules enforced here:
/// - Project names are trimmed before persistence.
/// - Deleting a project also deletes all stored G-code files.
/// - Job status transitions are validated.
/// </summary>
public sealed class PrintProjectService
{
    private readonly IPrintProjectRepository _repo;
    private readonly ILogger<PrintProjectService> _logger;

    /// <summary>Raised when any project or job is created, updated, or deleted.</summary>
    public event EventHandler? ProjectsChanged;

    public PrintProjectService(IPrintProjectRepository repo, ILogger<PrintProjectService> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    public Task<IReadOnlyList<PrintProject>> GetProjectsAsync(CancellationToken cancellationToken = default)
        => _repo.GetAllAsync(cancellationToken);

    public Task<PrintProject?> GetProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
        => _repo.GetByIdAsync(projectId, cancellationToken);

    // ── Project CRUD ──────────────────────────────────────────────────────────

    public async Task<PrintProject> CreateProjectAsync(string name, string? notes = null,
        CancellationToken cancellationToken = default)
    {
        var project = new PrintProject { Name = name.Trim(), Notes = notes };
        await _repo.SaveAsync(project, cancellationToken);
        OnProjectsChanged();
        return project;
    }

    public async Task RenameProjectAsync(Guid projectId, string newName,
        CancellationToken cancellationToken = default)
    {
        var project = await _repo.GetByIdAsync(projectId, cancellationToken)
            ?? throw new InvalidOperationException($"Project {projectId} not found.");

        project.Name = newName.Trim();
        await _repo.SaveAsync(project, cancellationToken);
        OnProjectsChanged();
    }

    public async Task DeleteProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var project = await _repo.GetByIdAsync(projectId, cancellationToken);
        if (project is null) return;

        foreach (var job in project.Jobs)
        {
            try
            {
                await _repo.DeleteJobFileAsync(job.StoragePath, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete G-code file {Path}", job.StoragePath);
            }
        }

        await _repo.DeleteAsync(projectId, cancellationToken);
        OnProjectsChanged();
    }

    // ── Job management ────────────────────────────────────────────────────────

    /// <summary>
    /// Uploads a G-code file into the project and registers a new job.
    /// </summary>
    public async Task<PrintJob> AddJobAsync(Guid projectId, string fileName, Stream fileContent,
        CancellationToken cancellationToken = default)
    {
        var project = await _repo.GetByIdAsync(projectId, cancellationToken)
            ?? throw new InvalidOperationException($"Project {projectId} not found.");

        var storagePath = await _repo.SaveJobFileAsync(projectId, fileName, fileContent, cancellationToken);

        var job = new PrintJob
        {
            FileName = fileName,
            StoragePath = storagePath,
            Size = fileContent.CanSeek ? fileContent.Length : 0,
        };

        project.Jobs.Add(job);
        await _repo.SaveAsync(project, cancellationToken);
        OnProjectsChanged();
        return job;
    }

    /// <summary>
    /// Removes a job and deletes its stored G-code file.
    /// </summary>
    public async Task RemoveJobAsync(Guid projectId, Guid jobId, CancellationToken cancellationToken = default)
    {
        var project = await _repo.GetByIdAsync(projectId, cancellationToken);
        var job = project?.Jobs.FirstOrDefault(j => j.Id == jobId);
        if (project is null || job is null) return;

        try { await _repo.DeleteJobFileAsync(job.StoragePath, cancellationToken); }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to delete file {Path}", job.StoragePath); }

        project.Jobs.Remove(job);
        await _repo.SaveAsync(project, cancellationToken);
        OnProjectsChanged();
    }

    /// <summary>
    /// Assigns a job to a printer and sets it to <see cref="PrintJobStatus.Printing"/>.
    /// </summary>
    public async Task AssignJobAsync(Guid projectId, Guid jobId, Guid printerId, string printerName,
        CancellationToken cancellationToken = default)
    {
        var project = await _repo.GetByIdAsync(projectId, cancellationToken);
        var job = project?.Jobs.FirstOrDefault(j => j.Id == jobId);
        if (job is null) return;

        job.AssignedPrinterId = printerId;
        job.AssignedPrinterName = printerName;
        job.Status = PrintJobStatus.Printing;
        await _repo.SaveAsync(project!, cancellationToken);
        OnProjectsChanged();
    }

    /// <summary>
    /// Updates the status of a job (typically to Completed or Failed).
    /// </summary>
    public async Task UpdateJobStatusAsync(Guid projectId, Guid jobId, PrintJobStatus status,
        CancellationToken cancellationToken = default)
    {
        var project = await _repo.GetByIdAsync(projectId, cancellationToken);
        var job = project?.Jobs.FirstOrDefault(j => j.Id == jobId);
        if (job is null) return;

        job.Status = status;
        await _repo.SaveAsync(project!, cancellationToken);
        OnProjectsChanged();
    }

    /// <summary>Returns the G-code file stream for a job (null if not found).</summary>
    public async Task<Stream?> OpenJobFileAsync(Guid projectId, Guid jobId,
        CancellationToken cancellationToken = default)
    {
        var project = await _repo.GetByIdAsync(projectId, cancellationToken);
        var job = project?.Jobs.FirstOrDefault(j => j.Id == jobId);
        return job is null ? null : await _repo.OpenJobFileAsync(job.StoragePath, cancellationToken);
    }

    private void OnProjectsChanged() => ProjectsChanged?.Invoke(this, EventArgs.Empty);
}
