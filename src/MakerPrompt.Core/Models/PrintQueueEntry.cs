namespace MakerPrompt.Core.Models;

/// <summary>
/// A print job waiting in a printer-side queue.
/// </summary>
public sealed class PrintQueueEntry
{
    /// <summary>Backend-assigned queue identifier.</summary>
    public string JobId { get; init; } = string.Empty;

    /// <summary>Path or display name of the queued G-code file.</summary>
    public string FileName { get; init; } = string.Empty;

    /// <summary>Unix timestamp at which the backend queued the job.</summary>
    public double TimeAdded { get; init; }
}
