namespace MakerPrompt.UI.Components.Infrastructure;

/// <summary>
/// Abstracts services that manage a fleet of printers under a single account.
/// Call Configure(token) before GetPrintersAsync().
/// </summary>
public interface IPrinterProvider
{
    void Configure(string bearerToken);
    Task<IReadOnlyList<RemotePrinterInfo>> GetPrintersAsync();
}
