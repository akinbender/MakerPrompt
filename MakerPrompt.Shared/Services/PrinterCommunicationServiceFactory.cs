namespace MakerPrompt.Shared.Services
{
    public class PrinterCommunicationServiceFactory(
        ISerialService serialService,
        PrusaLinkApiService prusaLinkApiService,
        MoonrakerApiService moonrakerApiService,
        BambuLabApiService bambuLabApiService) : IAsyncDisposable
    {
        public event EventHandler<bool>? ConnectionStateChanged;
        public bool IsConnected { get; private set; }
        public IPrinterCommunicationService? Current { get; private set; }

		private readonly ISerialService serialService = serialService;
		private readonly PrusaLinkApiService prusaLinkApiService = prusaLinkApiService;
		private readonly MoonrakerApiService moonrakerApiService = moonrakerApiService;
		private readonly BambuLabApiService bambuLabApiService = bambuLabApiService;

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

        public async ValueTask DisposeAsync()
        {
            await DisconnectAsync();
			await serialService.DisposeAsync();
			await prusaLinkApiService.DisposeAsync();
			await moonrakerApiService.DisposeAsync();
			await bambuLabApiService.DisposeAsync();
            GC.SuppressFinalize(this);
        }
    }
}
