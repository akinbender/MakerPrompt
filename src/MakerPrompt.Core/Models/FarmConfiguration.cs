namespace MakerPrompt.Core.Models;

/// <summary>
/// A saved farm profile that bundles a named group of printer connections.
/// Users can switch between farms (e.g. "Workshop A" vs "Hackerspace B").
/// </summary>
public sealed class FarmConfiguration
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Snapshot of printer connection definitions belonging to this farm.
    /// Populated when the farm is saved or exported.
    /// </summary>
    public List<PrinterConnectionDefinition> Printers { get; set; } = [];
}

/// <summary>
/// Persistent connection profile for a single printer.
/// </summary>
public sealed class PrinterConnectionDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>User-friendly display name (e.g. "Workshop Prusa MK4").</summary>
    public string Name { get; set; } = string.Empty;

    public PrinterConnectionType ConnectionType { get; set; } = PrinterConnectionType.Demo;

    /// <summary>Connection settings (URL, credentials, or serial port).</summary>
    public PrinterConnectionSettings Settings { get; set; } = new();

    /// <summary>Auto-connect this printer on app startup.</summary>
    public bool AutoConnect { get; set; }

    /// <summary>Optional hex color for the Fleet card UI.</summary>
    public string? Color { get; set; }

    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastConnectedAt { get; set; }

    /// <summary>The filament spool currently loaded in this printer.</summary>
    public Guid? AssignedFilamentSpoolId { get; set; }
}
