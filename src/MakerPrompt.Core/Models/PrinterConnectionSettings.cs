namespace MakerPrompt.Core.Models;

/// <summary>
/// Settings required to establish a connection to a single printer backend.
/// </summary>
public sealed class PrinterConnectionSettings
{
    /// <summary>Backend protocol to use.</summary>
    public PrinterConnectionType ConnectionType { get; set; } = PrinterConnectionType.Demo;

    // ── HTTP / WebSocket backends ───────────────────────────────────────────

    /// <summary>Base URL of the printer's HTTP API (e.g. "http://192.168.1.10").</summary>
    public string? ApiUrl { get; set; }

    /// <summary>Username for API authentication (if required).</summary>
    public string? UserName { get; set; }

    /// <summary>Password or API key for authentication.</summary>
    public string? Password { get; set; }

    // ── Serial / USB backends ───────────────────────────────────────────────

    /// <summary>Serial port name (e.g. "COM3" on Windows, "/dev/ttyUSB0" on Linux).</summary>
    public string? PortName { get; set; }

    /// <summary>Serial baud rate (default 115 200).</summary>
    public int BaudRate { get; set; } = 115_200;

    // ── Provider-backed backends ────────────────────────────────────────────

    /// <summary>
    /// Provider printer identifier returned by <see cref="Abstractions.IPrinterProvider"/>.
    /// Required when connecting to a specific printer within a fleet provider.
    /// </summary>
    public string? ProviderId { get; set; }
}
