using MakerPrompt.Core.Models;

namespace MakerPrompt.UI.MAUI.Services;

/// <summary>
/// iOS serial service stub.
/// Direct USB/serial connections are not supported on iOS (sandboxing restrictions).
/// The class satisfies the partial class requirement but throws on any attempt to connect.
/// </summary>
public partial class SerialService
{
    protected override Task OpenTransportAsync(PrinterConnectionSettings settings,
        CancellationToken cancellationToken)
        => throw new PlatformNotSupportedException(
            "Direct USB/serial connections are not supported on iOS. " +
            "Use a network-based backend (Moonraker, PrusaLink) instead.");

    protected override Task CloseTransportAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    protected override Task WriteTransportAsync(string data, CancellationToken cancellationToken)
        => throw new PlatformNotSupportedException(
            "Direct USB/serial connections are not supported on iOS.");

    public static partial Task<IReadOnlyList<string>> GetAvailablePortsAsync()
        => Task.FromResult<IReadOnlyList<string>>([]);
}
