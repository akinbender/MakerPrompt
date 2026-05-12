namespace MakerPrompt.Core.Models;

/// <summary>
/// Identifies the backend protocol used to communicate with a printer.
/// Single-printer backends connect directly to one machine; provider-backed
/// types (e.g. PrusaConnect, OctoPrint) expose multiple printers through a
/// single account and are resolved via <see cref="Abstractions.IPrinterProvider"/>.
/// </summary>
public enum PrinterConnectionType
{
    /// <summary>In-memory demo backend — no real hardware required.</summary>
    Demo,

    /// <summary>Direct USB/serial connection (Marlin, RepRap firmware, etc.).</summary>
    Serial,

    /// <summary>Moonraker HTTP + WebSocket backend (Klipper firmware).</summary>
    Moonraker,

    /// <summary>PrusaLink single-printer HTTP/JSON API (MK4, XL, etc.).</summary>
    PrusaLink,

    /// <summary>
    /// PrusaConnect cloud account — a provider that may expose multiple printers.
    /// Resolved via <see cref="Abstractions.IPrinterProvider"/>.
    /// </summary>
    PrusaConnect,

    /// <summary>BambuLab proprietary MQTT + HTTP backend.</summary>
    BambuLab,

    /// <summary>
    /// OctoPrint server — may act as a farm hub exposing multiple printers.
    /// Resolved via <see cref="Abstractions.IPrinterProvider"/>.
    /// </summary>
    OctoPrint,
}
