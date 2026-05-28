namespace MakerPrompt.Core.Models;

/// <summary>
/// Groups a set of G-code files under a single project name.
/// Files are dispatched to printers; status is tracked per job.
/// </summary>
public sealed class PrintProject
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<PrintJob> Jobs { get; set; } = [];
}

/// <summary>
/// A single G-code file within a <see cref="PrintProject"/>.
/// </summary>
public sealed class PrintJob
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Original filename (e.g. "benchy.gcode").</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Storage path within IStorageProvider (e.g. "PrintProjects/{projectId}/{filename}").</summary>
    public string StoragePath { get; set; } = string.Empty;

    /// <summary>File size in bytes (0 if not measurable at upload time).</summary>
    public long Size { get; set; }

    public PrintJobStatus Status { get; set; } = PrintJobStatus.Queued;

    /// <summary>The printer this job is assigned to (null = unassigned).</summary>
    public Guid? AssignedPrinterId { get; set; }

    /// <summary>Friendly printer name kept for display when the printer is offline.</summary>
    public string? AssignedPrinterName { get; set; }
}

public enum PrintJobStatus
{
    Queued,
    Printing,
    Completed,
    Failed,
}
