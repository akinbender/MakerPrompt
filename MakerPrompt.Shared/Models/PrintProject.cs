namespace MakerPrompt.Shared.Models
{
    /// <summary>
    /// A print project groups multiple G-code files under a folder/name.
    /// Files are uploaded locally to app storage and can be dispatched to any connected printer.
    /// </summary>
    public class PrintProject
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Human-readable project name (also acts as the folder name).
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Optional description or notes.
        /// </summary>
        public string? Notes { get; set; }

        /// <summary>
        /// When the project was created.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// The individual print jobs in this project.
        /// </summary>
        public List<PrintJob> Jobs { get; set; } = new();
    }

    /// <summary>
    /// A single print job within a project — one G-code file plus tracking state.
    /// </summary>
    public class PrintJob
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Original uploaded filename (e.g. "benchy.gcode").
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// Storage path within IAppLocalStorageProvider (e.g. "PrintProjects/{projectId}/{filename}").
        /// </summary>
        public string StoragePath { get; set; } = string.Empty;

        /// <summary>
        /// File size in bytes.
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        /// Current status of this job.
        /// </summary>
        public PrintJobStatus Status { get; set; } = PrintJobStatus.Queued;

        /// <summary>
        /// The printer this job was assigned/sent to (null = unassigned).
        /// </summary>
        public Guid? AssignedPrinterId { get; set; }

        /// <summary>
        /// Friendly name of the assigned printer (for display when printer is offline).
        /// </summary>
        public string? AssignedPrinterName { get; set; }
    }

    public enum PrintJobStatus
    {
        Queued,
        Printing,
        Completed,
        Failed
    }
}
