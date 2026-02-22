namespace MakerPrompt.Shared.Services
{
    public class PrinterCommunicationServiceFactory(
        ISerialService serialService,
        PrusaLinkApiService prusaLinkApiService,
        MoonrakerApiService moonrakerApiService,
        BambuLabApiService bambuLabApiService,
        OctoPrintApiService octoPrintApiService) : IAsyncDisposable
    {
        public event EventHandler<bool>? ConnectionStateChanged;
        public bool IsConnected { get; private set; }
        public IPrinterCommunicationService? Current { get; private set; }

		private readonly ISerialService serialService = serialService;
		private readonly PrusaLinkApiService prusaLinkApiService = prusaLinkApiService;
		private readonly MoonrakerApiService moonrakerApiService = moonrakerApiService;
		private readonly BambuLabApiService bambuLabApiService = bambuLabApiService;
		private readonly OctoPrintApiService octoPrintApiService = octoPrintApiService;

        public async Task ConnectAsync(PrinterConnectionSettings connectionSettings)
        {
            if (Current != null && Current.ConnectionType != connectionSettings.ConnectionType)
            {
                await Current.DisposeAsync();
            }

			IPrinterCommunicationService service = connectionSettings.ConnectionType switch
            {
                PrinterConnectionType.Demo => new DemoPrinterService(),
                PrinterConnectionType.Serial => serialService,
                PrinterConnectionType.PrusaLink => prusaLinkApiService,
				PrinterConnectionType.Moonraker => moonrakerApiService,
				PrinterConnectionType.BambuLab => bambuLabApiService,
				PrinterConnectionType.OctoPrint => octoPrintApiService,
                _ => throw new NotImplementedException(),
            };

            if (await service.ConnectAsync(connectionSettings))
            {
                Current = service;
                IsConnected = Current.IsConnected;
                ConnectionStateChanged?.Invoke(this, IsConnected);
            }
        }

        public async Task DisconnectAsync()
        {
            if (Current == null) return;
            await Current.DisconnectAsync();
            IsConnected = Current.IsConnected;
            ConnectionStateChanged?.Invoke(this, IsConnected);
        }

        /// <summary>
        /// Called by PrinterConnectionManager to keep this factory in sync with the
        /// currently active managed printer. Preserves backward compatibility with
        /// all existing single-printer UI components that read factory.Current.
        /// </summary>
        public void SetManagedCurrent(IPrinterCommunicationService? service)
        {
            Current = service;
            IsConnected = service?.IsConnected ?? false;
            ConnectionStateChanged?.Invoke(this, IsConnected);
        }

        public async ValueTask DisposeAsync()
        {
            await DisconnectAsync();
			await serialService.DisposeAsync();
			await prusaLinkApiService.DisposeAsync();
			await moonrakerApiService.DisposeAsync();
			await bambuLabApiService.DisposeAsync();
			await octoPrintApiService.DisposeAsync();
            GC.SuppressFinalize(this);
        }
    }
}
