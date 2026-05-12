using MakerPrompt.Infrastructure.Serial;

namespace MakerPrompt.UI.MAUI.Services;

/// <summary>
/// Platform-specific serial communication service for the new /src architecture.
///
/// This is a partial class split across per-platform files:
///   SerialCommunicationService.Windows.cs   – System.IO.Ports.SerialPort
///   SerialCommunicationService.Android.cs   – UsbSerialForAndroid.Net
///   SerialCommunicationService.MacOS.cs     – UsbSerialForMacOS
///   SerialCommunicationService.iOS.cs       – stub (not supported)
///
/// The base class (<see cref="SerialCommunicationServiceBase"/>) handles all G-code
/// command building and Marlin response parsing.  Each platform implementation
/// overrides only the three transport hooks:
///   • OpenTransportAsync  – open the hardware port
///   • CloseTransportAsync – close and dispose hardware resources
///   • WriteTransportAsync – write a single G-code line to the port
///
/// The receive loop is also started per-platform in <see cref="OpenTransportAsync"/>
/// and cancelled in <see cref="CloseTransportAsync"/>.
/// </summary>
public partial class SerialCommunicationService : SerialCommunicationServiceBase
{
    // Enumerates available ports / device identifiers on the current platform.
    public static partial Task<IReadOnlyList<string>> GetAvailablePortsAsync();
}
