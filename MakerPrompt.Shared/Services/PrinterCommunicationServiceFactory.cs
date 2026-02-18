namespace MakerPrompt.Shared.Services
{
    public class PrinterCommunicationServiceFactory(
        PrinterConnectionManager connectionManager) : IAsyncDisposable
    {
        public event EventHandler<bool>? ConnectionStateChanged;
        private bool _lastConnected;
        private IPrinterCommunicationService? _lastCurrent;

        public bool IsConnected => Current?.IsConnected == true;
        public IPrinterCommunicationService? ActivePrinter => connectionManager.ActivePrinter;
        public IPrinterCommunicationService? Current => ActivePrinter;

        public async Task ConnectAsync(PrinterConnectionSettings connectionSettings)
        {
            await connectionManager.ConnectLegacyAsync(connectionSettings);
            RaiseIfStateChanged();
        }

        public async Task DisconnectAsync()
        {
            await connectionManager.DisconnectActiveAsync();
            RaiseIfStateChanged();
        }

        public Task InitializeAsync()
        {
            connectionManager.StatesChanged -= OnManagerStatesChanged;
            connectionManager.StatesChanged += OnManagerStatesChanged;
            return connectionManager.InitializeAsync();
        }

        private void OnManagerStatesChanged(object? sender, IReadOnlyDictionary<Guid, PrinterConnectionRuntimeState> e)
        {
            RaiseIfStateChanged();
        }

        private void RaiseIfStateChanged()
        {
            var current = Current;
            var connected = current?.IsConnected == true;
            if (!ReferenceEquals(_lastCurrent, current) || _lastConnected != connected)
            {
                _lastCurrent = current;
                _lastConnected = connected;
                ConnectionStateChanged?.Invoke(this, connected);
            }
        }

        public async ValueTask DisposeAsync()
        {
            connectionManager.StatesChanged -= OnManagerStatesChanged;
            await DisconnectAsync();
            GC.SuppressFinalize(this);
        }
    }
}
