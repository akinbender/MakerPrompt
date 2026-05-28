namespace MakerPrompt.Core.Models;

/// <summary>
/// Audit record for a completed (or in-progress) print job.
/// Used for analytics — print hours, filament consumption per printer/spool.
/// </summary>
public sealed class PrintJobUsageRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>ID of the printer that ran this job.</summary>
    public Guid PrinterId { get; set; }

    /// <summary>ID of the filament spool consumed by this job (empty = unknown).</summary>
    public Guid FilamentSpoolId { get; set; }

    public string JobName { get; set; } = string.Empty;

    /// <summary>Total elapsed print time.</summary>
    public TimeSpan Duration { get; set; }

    /// <summary>Pre-slice estimated filament consumption in grams.</summary>
    public double EstimatedFilamentUsedGrams { get; set; }

    /// <summary>Actual filament consumed in grams (0 = not measured).</summary>
    public double ActualFilamentUsedGrams { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Returns the best available filament figure: actual if measured, otherwise estimated.
    /// </summary>
    public double EffectiveFilamentGrams =>
        ActualFilamentUsedGrams > 0 ? ActualFilamentUsedGrams : EstimatedFilamentUsedGrams;
}
