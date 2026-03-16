namespace MakerPrompt.Shared.Models;

/// <summary>
/// Printer discovered from a fleet provider (e.g. PrusaConnect account).
/// </summary>
public class RemotePrinterInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
