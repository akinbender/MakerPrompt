namespace MakerPrompt.Core.Models;

/// <summary>
/// Describes a printer discovered from a provider account (e.g. PrusaConnect, OctoPrint farm).
/// This model is intentionally minimal — the provider fills in whatever the upstream API exposes.
/// </summary>
public sealed class PrinterInfo
{
    /// <summary>
    /// Unique identifier assigned by the provider (e.g. the PrusaConnect UUID, OctoPrint printer key).
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>User-visible printer name as reported by the provider account.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Hardware model string (e.g. "MK4", "XL", "X1C"). May be empty.</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>Raw status string returned by the provider. Use <see cref="Status"/> for typed access.</summary>
    public string RawStatus { get; set; } = string.Empty;

    /// <summary>Typed printer status derived from <see cref="RawStatus"/>.</summary>
    public PrinterStatus Status { get; set; } = PrinterStatus.Disconnected;

    /// <summary>The provider type that surfaced this printer.</summary>
    public PrinterConnectionType ProviderType { get; set; }
}
