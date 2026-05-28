namespace MakerPrompt.UI.MAUI.Services;

/// <summary>
/// Platform-conditional ISerialService members.
/// iOS does not support direct USB/serial — all other platforms do.
/// </summary>
public partial class SerialService
{
#if IOS
    public bool IsSupported => false;
    public Task<bool> CheckSupportedAsync() => Task.FromResult(false);
    public Task RequestPortAsync() => throw new PlatformNotSupportedException(
        "Direct USB/serial connections are not supported on iOS.");
#else
    public bool IsSupported => true;
    public Task<bool> CheckSupportedAsync() => Task.FromResult(true);
    public Task RequestPortAsync() => Task.CompletedTask;
#endif
}
