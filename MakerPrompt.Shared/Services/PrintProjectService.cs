using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace MakerPrompt.Shared.Services
{
    /// <summary>
    /// Manages print projects — CRUD for projects and their jobs, persists to IAppLocalStorageProvider.
    /// G-code file contents are stored as separate files; the project manifest is a JSON index.
    /// </summary>
    public sealed class PrintProjectService
    {
        private const string ManifestKey = "MakerPrompt.PrintProjects";

        private readonly IAppLocalStorageProvider _storage;
        private readonly ILogger<PrintProjectService> _logger;
        private List<PrintProject> _projects = new();
        private bool _initialized;

        public IReadOnlyList<PrintProject> Projects => _projects.AsReadOnly();

        public event EventHandler? ProjectsChanged;

        public PrintProjectService(IAppLocalStorageProvider storage, ILogger<PrintProjectService> logger)
        {
            _storage = storage;
            _logger = logger;
        }

        /// <summary>
        /// Load projects from storage. Call once at startup.
        /// </summary>
        public async Task InitializeAsync()
        {
            if (_initialized) return;

            try
            {
                var files = await _storage.ListFilesAsync();
                var manifest = files.FirstOrDefault(f => f.FullPath.Contains(ManifestKey));
                if (manifest != null)
                {
                    using var stream = await _storage.OpenReadAsync(manifest.FullPath);
                    if (stream != null)
                    {
                        using var reader = new StreamReader(stream);
                        var json = await reader.ReadToEndAsync();
                        _projects = JsonSerializer.Deserialize<List<PrintProject>>(json) ?? new();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load print projects");
            }

            _initialized = true;
        }

        // ── Project CRUD ──

        public async Task AddProjectAsync(string name, string? notes = null)
        {
            var project = new PrintProject
            {
                Name = name.Trim(),
                Notes = notes
            };
            _projects.Add(project);
            await SaveManifestAsync();
            ProjectsChanged?.Invoke(this, EventArgs.Empty);
        }

        public async Task RenameProjectAsync(Guid projectId, string newName)
        {
            var project = _projects.FirstOrDefault(p => p.Id == projectId);
            if (project == null) return;
            project.Name = newName.Trim();
            await SaveManifestAsync();
            ProjectsChanged?.Invoke(this, EventArgs.Empty);
        }

        public async Task DeleteProjectAsync(Guid projectId)
        {
            var project = _projects.FirstOrDefault(p => p.Id == projectId);
            if (project == null) return;

            // Delete all stored files for this project
            foreach (var job in project.Jobs)
            {
                try
                {
                    await _storage.DeleteFileAsync(job.StoragePath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete job file {Path}", job.StoragePath);
                }
            }

            _projects.Remove(project);
            await SaveManifestAsync();
            ProjectsChanged?.Invoke(this, EventArgs.Empty);
        }

        // ── Job management ──

        /// <summary>
        /// Upload a G-code file into a project.
        /// </summary>
        public async Task AddJobAsync(Guid projectId, string fileName, Stream fileContent)
        {
            var project = _projects.FirstOrDefault(p => p.Id == projectId);
            if (project == null) throw new InvalidOperationException("Project not found");

            var storagePath = $"PrintProjects/{projectId}/{fileName}";

            await _storage.SaveFileAsync(storagePath, fileContent);

            var job = new PrintJob
            {
                FileName = fileName,
                StoragePath = storagePath,
                Size = fileContent.CanSeek ? fileContent.Length : 0
            };
            project.Jobs.Add(job);
            await SaveManifestAsync();
            ProjectsChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Remove a job from a project and delete its stored file.
        /// </summary>
        public async Task RemoveJobAsync(Guid projectId, Guid jobId)
        {
            var project = _projects.FirstOrDefault(p => p.Id == projectId);
            var job = project?.Jobs.FirstOrDefault(j => j.Id == jobId);
            if (project == null || job == null) return;

            try
            {
                await _storage.DeleteFileAsync(job.StoragePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete job file {Path}", job.StoragePath);
            }

            project.Jobs.Remove(job);
            await SaveManifestAsync();
            ProjectsChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Assign a job to a printer (mark it as printing).
        /// </summary>
        public async Task AssignJobAsync(Guid projectId, Guid jobId, Guid printerId, string printerName)
        {
            var project = _projects.FirstOrDefault(p => p.Id == projectId);
            var job = project?.Jobs.FirstOrDefault(j => j.Id == jobId);
            if (job == null) return;

            job.AssignedPrinterId = printerId;
            job.AssignedPrinterName = printerName;
            job.Status = PrintJobStatus.Printing;
            await SaveManifestAsync();
            ProjectsChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Mark a job as completed or failed.
        /// </summary>
        public async Task UpdateJobStatusAsync(Guid projectId, Guid jobId, PrintJobStatus status)
        {
            var project = _projects.FirstOrDefault(p => p.Id == projectId);
            var job = project?.Jobs.FirstOrDefault(j => j.Id == jobId);
            if (job == null) return;

            job.Status = status;
            await SaveManifestAsync();
            ProjectsChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Open a job's G-code file content from storage.
        /// </summary>
        public Task<Stream?> OpenJobFileAsync(Guid projectId, Guid jobId)
        {
            var project = _projects.FirstOrDefault(p => p.Id == projectId);
            var job = project?.Jobs.FirstOrDefault(j => j.Id == jobId);
            if (job == null) return Task.FromResult<Stream?>(null);
            return _storage.OpenReadAsync(job.StoragePath);
        }

        // ── Persistence ──

        private async Task SaveManifestAsync()
        {
            try
            {
                var json = JsonSerializer.Serialize(_projects, new JsonSerializerOptions { WriteIndented = true });
                var bytes = Encoding.UTF8.GetBytes(json);
                using var stream = new MemoryStream(bytes);
                await _storage.SaveFileAsync(ManifestKey, stream);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save print projects manifest");
            }
        }
    }
}
