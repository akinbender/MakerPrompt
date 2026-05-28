namespace MakerPrompt.Core.Models;

public enum NotificationLevel
{
    Info,
    Warning,
    Error,
    Critical,
}

/// <summary>
/// A persisted notification event (print completion, error, low filament alert, etc.).
/// </summary>
public sealed class NotificationRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public NotificationLevel Level { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>Associated printer (if notification relates to a specific printer).</summary>
    public Guid? PrinterId { get; set; }

    /// <summary>Associated filament spool (e.g. low-filament warnings).</summary>
    public Guid? FilamentSpoolId { get; set; }

    public bool IsRead { get; set; }
}
