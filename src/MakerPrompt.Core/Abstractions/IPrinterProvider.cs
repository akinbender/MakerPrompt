using MakerPrompt.Core.Models;

namespace MakerPrompt.Core.Abstractions;

/// <summary>
/// Abstracts a service that can enumerate printers from a remote provider account
/// (e.g. PrusaConnect, OctoPrint farm, future cloud providers).
///
/// Architecture note — provider vs. connection
/// --------------------------------------------
/// An <see cref="IPrinterProvider"/> answers the question:
///   "Which printers does this account know about?"
///
/// An <see cref="IPrinterCommunicationService"/> answers the question:
///   "How do I talk to this specific printer?"
///
/// These concerns are intentionally separate so that one provider can expose
/// many printers, each controlled by its own communication service instance.
///
/// Usage pattern
/// -------------
///   1. Call <see cref="ConfigureAsync"/> with a bearer/API token.
///   2. Call <see cref="GetPrintersAsync"/> to enumerate available printers.
///   3. Pass a <see cref="PrinterInfo.Id"/> to the Application layer to create
///      the matching <see cref="IPrinterCommunicationService"/> for that printer.
/// </summary>
public interface IPrinterProvider
{
    /// <summary>The provider type this implementation represents.</summary>
    PrinterConnectionType ProviderType { get; }

    /// <summary>
    /// Configures the provider with the credentials needed to reach the upstream API.
    /// Must be called before <see cref="GetPrintersAsync"/>.
    /// </summary>
    Task ConfigureAsync(string bearerToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the list of printers available under the configured account.
    /// Returns an empty list on authentication failure or transient network errors
    /// (callers should not treat an empty result as fatal).
    /// </summary>
    Task<IReadOnlyList<PrinterInfo>> GetPrintersAsync(CancellationToken cancellationToken = default);
}
