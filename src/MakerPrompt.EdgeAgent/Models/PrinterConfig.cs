namespace MakerPrompt.EdgeAgent.Models;

/// <summary>
/// Represents a single printer entry from <c>EdgeAgent:Printers</c> in appsettings.
/// </summary>
public sealed class PrinterConfig
{
    public string PrinterId { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Protocol name matching <see cref="MakerPrompt.Core.Models.PrinterConnectionType"/>:
    /// Demo | Moonraker | PrusaLink | PrusaConnect | BambuLab | OctoPrint
    /// </summary>
    public string Protocol { get; set; } = "Demo";

    public string ApiUrl { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ProviderId { get; set; } = string.Empty;

    /// <summary>Optional MJPEG snapshot URL for this printer's camera.</summary>
    public string CameraUrl { get; set; } = string.Empty;
}
