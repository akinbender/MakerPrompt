using MakerPrompt.Infrastructure.Services.Printers;
using Microsoft.Extensions.DependencyInjection;

namespace MakerPrompt.UI.Components.Services;

/// <summary>
/// Creates an independently owned backend for one managed printer connection.
/// </summary>
public interface IPrinterBackendFactory
{
    IPrinterCommunicationService Create(PrinterConnectionType connectionType);
}

/// <summary>
/// Default backend factory. Serial services are activated per printer so two
/// connections never share transport state.
/// </summary>
public sealed class PrinterBackendFactory<TSerialService>(IServiceProvider services)
    : IPrinterBackendFactory
    where TSerialService : class, ISerialService
{
    public IPrinterCommunicationService Create(PrinterConnectionType connectionType) =>
        connectionType switch
        {
            PrinterConnectionType.Demo => new DemoPrinterService(),
            PrinterConnectionType.Serial =>
                ActivatorUtilities.CreateInstance<TSerialService>(services),
            PrinterConnectionType.PrusaLink => new PrusaLinkApiService(),
            PrinterConnectionType.PrusaConnect =>
                ActivatorUtilities.CreateInstance<PrusaConnectPrinterService>(services),
            PrinterConnectionType.Moonraker => new MoonrakerApiService(),
            PrinterConnectionType.BambuLab => new BambuLabApiService(),
            PrinterConnectionType.OctoPrint => new OctoPrintApiService(),
            _ => throw new NotSupportedException(
                $"Unsupported connection type: {connectionType}")
        };
}
