using MakerPrompt.Core.Models;

namespace MakerPrompt.Core.Abstractions;

/// <summary>
/// Manages print projects and their associated G-code jobs.
/// Business logic lives in the Application layer; this interface
/// describes the persistence contract for both storage and application use.
/// </summary>
public interface IPrintProjectRepository
{
    Task<IReadOnlyList<PrintProject>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<PrintProject?> GetByIdAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task SaveAsync(PrintProject project, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens the binary content of a job's G-code file from storage.
    /// Returns <c>null</c> if the file does not exist.
    /// </summary>
    Task<Stream?> OpenJobFileAsync(string storagePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores the binary content of a G-code file and returns the storage path assigned to it.
    /// </summary>
    Task<string> SaveJobFileAsync(Guid projectId, string fileName, Stream content,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes the physical G-code file for a job.</summary>
    Task DeleteJobFileAsync(string storagePath, CancellationToken cancellationToken = default);
}
