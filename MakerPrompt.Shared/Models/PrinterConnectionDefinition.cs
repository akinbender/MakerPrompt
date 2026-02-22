namespace MakerPrompt.Shared.Models
{
    /// <summary>
    /// Persistent model for a saved printer connection. Stored via IAppLocalStorageProvider.
    /// Inspired by PrintQue multi-printer management and OctoPrint connection profiles.
    /// </summary>
    public class PrinterConnectionDefinition
    {
        /// <summary>
        /// Unique identifier for this printer connection.
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// User-friendly display name (e.g. "Workshop Prusa MK4", "BambuLab X1C #2").
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Backend type for this printer connection.
        /// </summary>
        public PrinterConnectionType ConnectionType { get; set; } = PrinterConnectionType.Demo;

        /// <summary>
        /// Connection details — API settings for HTTP/WS backends, serial settings for USB.
        /// </summary>
        public PrinterConnectionSettings Settings { get; set; } = new();

        /// <summary>
        /// Whether MakerPrompt should attempt to auto-connect this printer on startup.
        /// </summary>
        public bool AutoConnect { get; set; }

        /// <summary>
        /// Optional user-assigned color for the printer card in the Fleet dashboard.
        /// </summary>
        public string? Color { get; set; }

        /// <summary>
        /// Optional notes for this printer connection.
        /// </summary>
        public string? Notes { get; set; }

        /// <summary>
        /// Timestamp of when this definition was created.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Timestamp of the last successful connection.
        /// </summary>
        public DateTime? LastConnectedAt { get; set; }

        /// <summary>
        /// The ID of the currently assigned filament spool.
        /// </summary>
        public Guid? AssignedFilamentSpoolId { get; set; }
    }
}
