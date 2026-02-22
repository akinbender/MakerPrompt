using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MakerPrompt.Shared.Models;

namespace MakerPrompt.Shared.Services
{
    public interface IPrinterCameraProvider
    {
        Task<IReadOnlyList<PrinterCamera>> GetCamerasAsync(CancellationToken cancellationToken = default);

        Task<PrinterCamera?> GetPrimaryCameraAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Provides a backend-agnostic view of available printer cameras, delegating to
    /// backend-specific services via the existing PrinterCommunicationServiceFactory.
    /// </summary>
    public sealed class PrinterCameraProvider : IPrinterCameraProvider
    {
        private readonly PrinterCommunicationServiceFactory factory;

        public PrinterCameraProvider(PrinterCommunicationServiceFactory factory)
        {
            this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        public async Task<IReadOnlyList<PrinterCamera>> GetCamerasAsync(CancellationToken cancellationToken = default)
        {
            var current = factory.Current;
            if (current is null || !current.IsConnected)
            {
                return Array.Empty<PrinterCamera>();
            }

            try
            {
                return current.ConnectionType switch
                {
                    PrinterConnectionType.Moonraker when current is MoonrakerApiService moonraker =>
                        await moonraker.GetCamerasAsync(cancellationToken).ConfigureAwait(false),

                    PrinterConnectionType.PrusaLink when current is PrusaLinkApiService prusa =>
                        await prusa.GetCamerasAsync(cancellationToken).ConfigureAwait(false),

                    PrinterConnectionType.OctoPrint when current is OctoPrintApiService octoprint =>
                        await octoprint.GetCamerasAsync(cancellationToken).ConfigureAwait(false),

                    PrinterConnectionType.BambuLab when current is BambuLabApiService bambu =>
                        await bambu.GetCamerasAsync(cancellationToken).ConfigureAwait(false),

                    _ => Array.Empty<PrinterCamera>()
                };
            }
            catch
            {
                // Any failure to query cameras should be silent; the dashboard simply
                // omits the webcam card when discovery is not successful.
                return Array.Empty<PrinterCamera>();
            }
        }

        public async Task<PrinterCamera?> GetPrimaryCameraAsync(CancellationToken cancellationToken = default)
        {
            var cameras = await GetCamerasAsync(cancellationToken).ConfigureAwait(false);
            return cameras.Count > 0 ? cameras[0] : null;
        }
    }
}
