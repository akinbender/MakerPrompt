namespace MakerPrompt.Shared.Models
{
    /// <summary>
    /// Runtime state for a managed printer — combines the persisted definition with
    /// live connection state and latest telemetry snapshot.
    /// </summary>
    public class ManagedPrinterState
    {
        /// <summary>
        /// The persisted definition this state corresponds to.
        /// </summary>
        public PrinterConnectionDefinition Definition { get; set; } = new();

        /// <summary>
        /// The live backend service instance (null when not connected).
        /// </summary>
        public IPrinterCommunicationService? Service { get; set; }

        /// <summary>
        /// Current connection status.
        /// </summary>
        public PrinterStatus Status { get; set; } = PrinterStatus.Disconnected;

        /// <summary>
        /// Latest telemetry snapshot from this printer.
        /// </summary>
        public PrinterTelemetry Telemetry { get; set; } = new();

        /// <summary>
        /// Whether a connection/disconnection operation is in progress.
        /// </summary>
        public bool IsBusy { get; set; }

        /// <summary>
        /// Last error message for this printer, if any (friendly, not a stack trace).
        /// </summary>
        public string? LastError { get; set; }

        /// <summary>
        /// Whether this printer is currently the "active" printer (selected for single-printer views).
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Tracks the accumulated E-axis extrusion for the current print job.
        /// </summary>
        public double AccumulatedExtrusion { get; set; }

        /// <summary>
        /// Tracks the start time of the current print job.
        /// </summary>
        public DateTime? PrintStartTime { get; set; }
    }
}
