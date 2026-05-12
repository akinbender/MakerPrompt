namespace MakerPrompt.Core.Models;

/// <summary>
/// Operational status of a managed printer.
/// </summary>
public enum PrinterStatus
{
    /// <summary>No connection has been established.</summary>
    Disconnected,

    /// <summary>Connected and idle — ready to accept commands.</summary>
    Connected,

    /// <summary>A print job is actively running.</summary>
    Printing,

    /// <summary>Print job is paused (awaiting user action or filament change).</summary>
    Paused,

    /// <summary>The printer has reported a fault condition.</summary>
    Error,
}
