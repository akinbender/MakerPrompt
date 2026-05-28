using MakerPrompt.Infrastructure.Printers;
using MakerPrompt.Infrastructure;

namespace MakerPrompt.UI.MAUI.Services;

/// <summary>
/// Platform-specific serial communication service.
///
/// Split across per-platform files:
///   SerialService.Windows.cs  – System.IO.Ports.SerialPort
///   SerialService.Android.cs  – UsbSerialForAndroid.Net
///   SerialService.MacOS.cs    – UsbSerialForMacOS
///   SerialService.iOS.cs      – stub (not supported)
///
/// <see cref="SerialCommunicationServiceBase"/> handles all G-code command
/// building and Marlin response parsing.  Each platform file overrides only:
///   • OpenTransportAsync  — open the hardware port
///   • CloseTransportAsync — close hardware resources
///   • WriteTransportAsync — write a G-code line to the port
/// </summary>
public partial class SerialService : SerialCommunicationServiceBase, ISerialService
{
    // Static platform-provided port enumeration.
    public static partial Task<IReadOnlyList<string>> GetAvailablePortsAsync();

    // ISerialService instance wrapper — delegates to the static platform method.
    Task<IEnumerable<string>> ISerialService.GetAvailablePortsAsync() =>
        GetAvailablePortsAsync().ContinueWith(t => t.Result.AsEnumerable());
}

