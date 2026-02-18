using Microsoft.Extensions.DependencyInjection;

namespace MakerPrompt.Shared.Services
{
    public sealed class PrinterConnectionManager : IAsyncDisposable
    {
        private const string ConnectionsStorageKey = "printer-connections";
        private const string LegacyConnectionSettingsKey = "printer-connection-settings";

        private readonly object _syncRoot = new();
        private readonly IServiceProvider _serviceProvider;
        private readonly ISerialService _serialService;
        private readonly IAppStorage _appStorage;

        private readonly Dictionary<Guid, PrinterConnectionDefinition> _definitions = new();
        private readonly Dictionary<Guid, IPrinterCommunicationService> _connections = new();
        private readonly Dictionary<Guid, PrinterConnectionRuntimeState> _runtimeStates = new();
        private readonly Dictionary<Guid, (EventHandler<bool> ConnectionHandler, EventHandler<PrinterTelemetry> TelemetryHandler)> _subscriptions = new();

        private bool _initialized;

        private static readonly Guid LegacyConnectionId = Guid.Parse("1c5c8d1d-1724-4de4-a2ba-8ec7d97af7f1");

        public event EventHandler<IReadOnlyDictionary<Guid, PrinterConnectionRuntimeState>>? StatesChanged;

        public Guid? ActivePrinterId { get; private set; }

        public IReadOnlyDictionary<Guid, IPrinterCommunicationService> ActiveConnections
        {
            get
            {
                lock (_syncRoot)
                {
                    return new Dictionary<Guid, IPrinterCommunicationService>(_connections);
                }
            }
        }

        public IReadOnlyList<PrinterConnectionDefinition> ConnectionDefinitions
        {
            get
            {
                lock (_syncRoot)
                {
                    return [.. _definitions.Values.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)];
                }
            }
        }

        public IReadOnlyDictionary<Guid, PrinterConnectionRuntimeState> RuntimeStates
        {
            get
            {
                lock (_syncRoot)
                {
                    return new Dictionary<Guid, PrinterConnectionRuntimeState>(_runtimeStates);
                }
            }
        }

        public IPrinterCommunicationService? ActivePrinter
        {
            get
            {
                lock (_syncRoot)
                {
                    if (ActivePrinterId.HasValue && _connections.TryGetValue(ActivePrinterId.Value, out var selected) && selected.IsConnected)
                    {
                        return selected;
                    }

                    return _connections.Values.FirstOrDefault(x => x.IsConnected);
                }
            }
        }

        public PrinterConnectionManager(IServiceProvider serviceProvider, ISerialService serialService, IAppStorage appStorage)
        {
            _serviceProvider = serviceProvider;
            _serialService = serialService;
            _appStorage = appStorage;
        }

        public async Task InitializeAsync()
        {
            if (_initialized)
            {
                return;
            }

            var loadedDefinitions = await _appStorage.GetItemAsync<List<PrinterConnectionDefinition>>(ConnectionsStorageKey) ?? [];

            if (loadedDefinitions.Count == 0)
            {
                var legacyConnectionSettings = await _appStorage.GetItemAsync<PrinterConnectionSettings>(LegacyConnectionSettingsKey);
                if (legacyConnectionSettings != null)
                {
                    loadedDefinitions.Add(PrinterConnectionDefinition.FromSettings(legacyConnectionSettings, "Default Printer"));
                    await _appStorage.RemoveItemAsync(LegacyConnectionSettingsKey);
                }
            }

            lock (_syncRoot)
            {
                _definitions.Clear();
                _runtimeStates.Clear();

                foreach (var definition in loadedDefinitions)
                {
                    if (definition.Id == Guid.Empty)
                    {
                        definition.Id = Guid.NewGuid();
                    }

                    _definitions[definition.Id] = definition;
                    _runtimeStates[definition.Id] = BuildDisconnectedState(definition);
                }

                _initialized = true;
            }

            await AutoConnectAsync();
            NotifyStatesChanged();
        }

        public async Task<bool> ConnectAsync(Guid connectionId)
        {
            await InitializeAsync();

            PrinterConnectionDefinition? definition;
            IPrinterCommunicationService? existingService;
            lock (_syncRoot)
            {
                _definitions.TryGetValue(connectionId, out definition);
                _connections.TryGetValue(connectionId, out existingService);
            }

            if (definition is null || !definition.Enabled)
            {
                return false;
            }

            if (existingService?.IsConnected == true)
            {
                SetActivePrinter(connectionId);
                return true;
            }

            var service = existingService ?? CreateService(definition);
            if (existingService is null)
            {
                lock (_syncRoot)
                {
                    _connections[connectionId] = service;
                }

                AttachRuntimeHandlers(connectionId, service);
            }

            var isConnected = false;
            string? error = null;

            try
            {
                isConnected = await service.ConnectAsync(definition.ToSettings());
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }

            if (!isConnected)
            {
                await CleanupFailedConnectionAsync(connectionId, service);
                UpdateRuntimeState(connectionId, definition, false, error ?? "Connection failed");
                return false;
            }

            SetActivePrinter(connectionId);
            UpdateRuntimeState(connectionId, definition, true, null, service.LastTelemetry, service.IsPrinting);
            return true;
        }

        public async Task<bool> ConnectLegacyAsync(PrinterConnectionSettings settings)
        {
            await InitializeAsync();

            PrinterConnectionDefinition? legacyDefinition;
            lock (_syncRoot)
            {
                _definitions.TryGetValue(LegacyConnectionId, out legacyDefinition);
            }

            legacyDefinition ??= PrinterConnectionDefinition.FromSettings(settings, settings.ConnectionType.GetDisplayName());
            legacyDefinition.Id = LegacyConnectionId;
            legacyDefinition.Enabled = true;
            legacyDefinition.AutoConnect = false;
            legacyDefinition.PrinterType = settings.ConnectionType;

            if (settings.Serial is not null)
            {
                legacyDefinition.SerialPortName = settings.Serial.PortName;
                legacyDefinition.BaudRate = settings.Serial.BaudRate;
            }
            else
            {
                legacyDefinition.SerialPortName = string.Empty;
                legacyDefinition.BaudRate = 115200;
            }

            if (settings.Api is not null)
            {
                legacyDefinition.Address = settings.Api.Url;
                legacyDefinition.UserName = settings.Api.UserName;
                legacyDefinition.Password = settings.Api.Password;
            }
            else
            {
                legacyDefinition.Address = string.Empty;
                legacyDefinition.UserName = string.Empty;
                legacyDefinition.Password = string.Empty;
            }

            await UpsertDefinitionAsync(legacyDefinition);
            return await ConnectAsync(legacyDefinition.Id);
        }

        public async Task DisconnectAsync(Guid connectionId)
        {
            await InitializeAsync();

            IPrinterCommunicationService? service;
            PrinterConnectionDefinition? definition;
            lock (_syncRoot)
            {
                _connections.TryGetValue(connectionId, out service);
                _definitions.TryGetValue(connectionId, out definition);
            }

            if (service is null || definition is null)
            {
                return;
            }

            try
            {
                await service.DisconnectAsync();
            }
            catch
            {
                // Keep disconnect resilient.
            }

            await RemoveConnectionAsync(connectionId, service);

            UpdateRuntimeState(connectionId, definition, false, null, service.LastTelemetry, false);

            if (ActivePrinterId == connectionId)
            {
                lock (_syncRoot)
                {
                    ActivePrinterId = _connections.FirstOrDefault(x => x.Value.IsConnected).Key;
                    if (ActivePrinterId == Guid.Empty)
                    {
                        ActivePrinterId = null;
                    }
                }
            }

            NotifyStatesChanged();
        }

        public async Task DisconnectActiveAsync()
        {
            if (!ActivePrinterId.HasValue)
            {
                return;
            }

            await DisconnectAsync(ActivePrinterId.Value);
        }

        public async Task<bool> ReconnectAsync(Guid connectionId)
        {
            await DisconnectAsync(connectionId);
            return await ConnectAsync(connectionId);
        }

        public async Task UpsertDefinitionAsync(PrinterConnectionDefinition definition)
        {
            await InitializeAsync();

            if (definition.Id == Guid.Empty)
            {
                definition.Id = Guid.NewGuid();
            }

            lock (_syncRoot)
            {
                _definitions[definition.Id] = definition;
                if (!_runtimeStates.ContainsKey(definition.Id))
                {
                    _runtimeStates[definition.Id] = BuildDisconnectedState(definition);
                }
                else
                {
                    var existing = _runtimeStates[definition.Id];
                    existing.Name = definition.Name;
                    existing.PrinterType = definition.PrinterType;
                    existing.Enabled = definition.Enabled;
                    existing.LastUpdatedUtc = DateTimeOffset.UtcNow;
                }
            }

            await PersistDefinitionsAsync();
            NotifyStatesChanged();
        }

        public async Task DeleteDefinitionAsync(Guid connectionId)
        {
            await InitializeAsync();

            await DisconnectAsync(connectionId);

            lock (_syncRoot)
            {
                _definitions.Remove(connectionId);
                _runtimeStates.Remove(connectionId);
            }

            await PersistDefinitionsAsync();
            NotifyStatesChanged();
        }

        public async Task SetEnabledAsync(Guid connectionId, bool enabled)
        {
            await InitializeAsync();

            PrinterConnectionDefinition? definition;
            IPrinterCommunicationService? service;
            lock (_syncRoot)
            {
                _definitions.TryGetValue(connectionId, out definition);
                _connections.TryGetValue(connectionId, out service);
            }

            if (definition is null)
            {
                return;
            }

            definition.Enabled = enabled;
            await PersistDefinitionsAsync();

            if (!enabled)
            {
                await DisconnectAsync(connectionId);
            }

            UpdateRuntimeState(connectionId, definition, service?.IsConnected == true, null, service?.LastTelemetry, service?.IsPrinting == true);
        }

        public void SetActivePrinter(Guid connectionId)
        {
            lock (_syncRoot)
            {
                if (_connections.TryGetValue(connectionId, out var service) && service.IsConnected)
                {
                    ActivePrinterId = connectionId;
                }
                else if (_connections.Values.Any(x => x.IsConnected))
                {
                    ActivePrinterId = _connections.First(x => x.Value.IsConnected).Key;
                }
                else
                {
                    ActivePrinterId = null;
                }
            }

            NotifyStatesChanged();
        }

        public async ValueTask DisposeAsync()
        {
            List<KeyValuePair<Guid, IPrinterCommunicationService>> connections;
            lock (_syncRoot)
            {
                connections = [.. _connections];
            }

            foreach (var (connectionId, service) in connections)
            {
                try
                {
                    await service.DisconnectAsync();
                }
                catch
                {
                    // Ignore disconnect errors during disposal.
                }

                await RemoveConnectionAsync(connectionId, service);
            }
        }

        private async Task AutoConnectAsync()
        {
            List<Guid> connectionIds;
            lock (_syncRoot)
            {
                connectionIds = [.. _definitions.Values
                    .Where(x => x.Enabled && x.AutoConnect)
                    .Select(x => x.Id)];
            }

            foreach (var id in connectionIds)
            {
                try
                {
                    await ConnectAsync(id);
                }
                catch
                {
                    // Startup auto-connect should never crash the app.
                }
            }
        }

        private IPrinterCommunicationService CreateService(PrinterConnectionDefinition definition)
        {
            if (definition.PrinterType == PrinterConnectionType.Serial)
            {
                lock (_syncRoot)
                {
                    var serialConnectedElsewhere = _connections.Any(x => x.Key != definition.Id && x.Value.ConnectionType == PrinterConnectionType.Serial && x.Value.IsConnected);
                    if (serialConnectedElsewhere)
                    {
                        throw new InvalidOperationException("Serial connection is already active for another printer definition.");
                    }
                }

                return _serialService;
            }

            return definition.PrinterType switch
            {
                PrinterConnectionType.Demo => new DemoPrinterService(),
                PrinterConnectionType.PrusaLink => ActivatorUtilities.CreateInstance<PrusaLinkApiService>(_serviceProvider),
                PrinterConnectionType.Moonraker => ActivatorUtilities.CreateInstance<MoonrakerApiService>(_serviceProvider),
                PrinterConnectionType.BambuLab => ActivatorUtilities.CreateInstance<BambuLabApiService>(_serviceProvider),
                _ => new DemoPrinterService()
            };
        }

        private void AttachRuntimeHandlers(Guid connectionId, IPrinterCommunicationService service)
        {
            EventHandler<bool> connectionHandler = (_, connected) =>
            {
                PrinterConnectionDefinition? definition;
                lock (_syncRoot)
                {
                    _definitions.TryGetValue(connectionId, out definition);
                }

                if (definition is null)
                {
                    return;
                }

                UpdateRuntimeState(connectionId, definition, connected, null, service.LastTelemetry, service.IsPrinting);
            };

            EventHandler<PrinterTelemetry> telemetryHandler = (_, telemetry) =>
            {
                PrinterConnectionDefinition? definition;
                lock (_syncRoot)
                {
                    _definitions.TryGetValue(connectionId, out definition);
                }

                if (definition is null)
                {
                    return;
                }

                UpdateRuntimeState(connectionId, definition, service.IsConnected, null, telemetry, service.IsPrinting);
            };

            service.ConnectionStateChanged += connectionHandler;
            service.TelemetryUpdated += telemetryHandler;

            lock (_syncRoot)
            {
                _subscriptions[connectionId] = (connectionHandler, telemetryHandler);
            }
        }

        private async Task CleanupFailedConnectionAsync(Guid connectionId, IPrinterCommunicationService service)
        {
            try
            {
                await service.DisconnectAsync();
            }
            catch
            {
                // Ignore cleanup failure.
            }

            await RemoveConnectionAsync(connectionId, service);
            NotifyStatesChanged();
        }

        private async Task RemoveConnectionAsync(Guid connectionId, IPrinterCommunicationService service)
        {
            lock (_syncRoot)
            {
                _connections.Remove(connectionId);

                if (_subscriptions.TryGetValue(connectionId, out var handlers))
                {
                    service.ConnectionStateChanged -= handlers.ConnectionHandler;
                    service.TelemetryUpdated -= handlers.TelemetryHandler;
                    _subscriptions.Remove(connectionId);
                }
            }

            if (!ReferenceEquals(service, _serialService))
            {
                await service.DisposeAsync();
            }
        }

        private void UpdateRuntimeState(Guid connectionId, PrinterConnectionDefinition definition, bool isConnected, string? error, PrinterTelemetry? telemetry = null, bool isPrinting = false)
        {
            lock (_syncRoot)
            {
                _runtimeStates[connectionId] = new PrinterConnectionRuntimeState
                {
                    ConnectionId = connectionId,
                    Name = definition.Name,
                    PrinterType = definition.PrinterType,
                    Enabled = definition.Enabled,
                    IsConnected = isConnected,
                    IsPrinting = isPrinting,
                    Telemetry = telemetry ?? _runtimeStates.GetValueOrDefault(connectionId)?.Telemetry ?? new PrinterTelemetry(),
                    LastError = error,
                    LastUpdatedUtc = DateTimeOffset.UtcNow
                };

                if (ActivePrinterId == connectionId && !isConnected)
                {
                    ActivePrinterId = _connections.FirstOrDefault(x => x.Value.IsConnected).Key;
                    if (ActivePrinterId == Guid.Empty)
                    {
                        ActivePrinterId = null;
                    }
                }

                if (!ActivePrinterId.HasValue && isConnected)
                {
                    ActivePrinterId = connectionId;
                }
            }

            NotifyStatesChanged();
        }

        private PrinterConnectionRuntimeState BuildDisconnectedState(PrinterConnectionDefinition definition)
        {
            return new PrinterConnectionRuntimeState
            {
                ConnectionId = definition.Id,
                Name = definition.Name,
                PrinterType = definition.PrinterType,
                Enabled = definition.Enabled,
                IsConnected = false,
                IsPrinting = false,
                Telemetry = new PrinterTelemetry(),
                LastUpdatedUtc = DateTimeOffset.UtcNow
            };
        }

        private async Task PersistDefinitionsAsync()
        {
            List<PrinterConnectionDefinition> values;
            lock (_syncRoot)
            {
                values = [.. _definitions.Values];
            }

            await _appStorage.SetItemAsync(ConnectionsStorageKey, values);
        }

        private void NotifyStatesChanged()
        {
            IReadOnlyDictionary<Guid, PrinterConnectionRuntimeState> snapshot;
            lock (_syncRoot)
            {
                snapshot = new Dictionary<Guid, PrinterConnectionRuntimeState>(_runtimeStates);
            }

            StatesChanged?.Invoke(this, snapshot);
        }
    }
}
