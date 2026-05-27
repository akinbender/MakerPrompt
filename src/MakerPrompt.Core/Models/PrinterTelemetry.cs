using System.Numerics;

namespace MakerPrompt.Core.Models;

/// <summary>
/// Snapshot of live telemetry data received from a connected printer.
/// </summary>
public class PrinterTelemetry
{
    /// <summary>Last raw response line received from the printer (G-code terminal output).</summary>
    public string LastResponse { get; set; } = string.Empty;

    /// <summary>Timestamp when the connection to this printer was established.</summary>
    public DateTime? ConnectionTime { get; set; }

    /// <summary>Current print-head position reported by the printer.</summary>
    public Vector3 Position { get; set; }

    /// <summary>SD card status reported by the printer.</summary>
    public SDCardStatus SDCard { get; } = new();

    /// <summary>Display name of the printer (populated by the backend).</summary>
    public string PrinterName { get; set; } = string.Empty;

    /// <summary>Current hotend temperature in °C.</summary>
    public double HotendTemp { get; set; }

    /// <summary>Hotend target temperature in °C (0 = heater off).</summary>
    public double HotendTarget { get; set; }

    /// <summary>Current heated-bed temperature in °C.</summary>
    public double BedTemp { get; set; }

    /// <summary>Heated-bed target temperature in °C (0 = off).</summary>
    public double BedTarget { get; set; }

    /// <summary>Chamber temperature in °C (0 if not supported).</summary>
    public double ChamberTemp { get; set; }

    /// <summary>Chamber target temperature in °C (0 = off or not supported).</summary>
    public double ChamberTarget { get; set; }

    /// <summary>Current operational status.</summary>
    public PrinterStatus Status { get; set; } = PrinterStatus.Disconnected;

    /// <summary>Feed-rate override percentage (100 = nominal speed).</summary>
    public int FeedRate { get; set; } = 100;

    /// <summary>Flow-rate override percentage (100 = nominal extrusion).</summary>
    public int FlowRate { get; set; } = 100;

    /// <summary>Part cooling fan speed (0–100 %).</summary>
    public int FanSpeed { get; set; }

    /// <summary>Name of the currently active print job (empty when idle).</summary>
    public string PrintJobName { get; set; } = string.Empty;

    /// <summary>Elapsed time since the print job started.</summary>
    public TimeSpan PrintDuration { get; set; }

    /// <summary>Filament consumed in the current job (mm).</summary>
    public double FilamentUsed { get; set; }

    /// <summary>Print progress (0–100 %). 0 when not printing.</summary>
    public double PrintProgress { get; set; }

    /// <summary>UTC timestamp when this snapshot was captured.</summary>
    public DateTimeOffset CapturedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>SD card status reported by firmware.</summary>
public class SDCardStatus
{
    /// <summary>Whether the SD card is present and mounted.</summary>
    public bool Present { get; set; }

    /// <summary>Whether a print from SD is currently active.</summary>
    public bool Printing { get; set; }

    /// <summary>SD print progress (0–100 %).</summary>
    public double Progress { get; set; }
}
