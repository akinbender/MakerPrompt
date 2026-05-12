namespace MakerPrompt.Core.Models;

/// <summary>
/// Represents a spool of filament tracked in the inventory.
/// </summary>
public sealed class FilamentSpool
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Material { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;

    /// <summary>Filament diameter in mm (typically 1.75 or 2.85).</summary>
    public double Diameter { get; set; } = 1.75;

    /// <summary>Total spool weight in grams as purchased.</summary>
    public double TotalWeightGrams { get; set; } = 1000;

    /// <summary>Estimated remaining weight in grams (decremented as jobs complete).</summary>
    public double RemainingWeightGrams { get; set; } = 1000;

    /// <summary>Cost paid for this spool.</summary>
    public decimal Cost { get; set; }

    public DateTime PurchaseDate { get; set; } = DateTime.UtcNow;

    /// <summary>Archived spools are hidden from active selections but kept for history.</summary>
    public bool IsArchived { get; set; }
}
