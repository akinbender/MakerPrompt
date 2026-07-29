using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace MakerPrompt.UI.Components.Services
{
    /// <summary>
    /// Manages multiple printer connections. Loads/saves definitions from local storage,
    /// creates per-printer backend instances, tracks live state, and exposes events for UI.
    /// 
    /// Design inspired by:
    /// - PrintQue multi-printer dashboard patterns
    /// - OctoPrint connection profiles + REST/WS integration
    /// - BambuCAM/BambuFarm multi-printer telemetry management
    /// 
    /// Works alongside the existing PrinterCommunicationServiceFactory for backward compatibility.
    /// The factory's `Current` property is kept in sync with the active printer.
    /// </summary>
    public sealed class PrinterConnectionManager : IAsyncDisposable
    {
        private const string StorageKey = "MakerPrompt.PrinterConnections";

        private readonly IAppLocalStorageProvider _storage;
        private readonly PrinterConfigurationPersistence _persistence;
        private readonly IPrinterBackendFactory _backendFactory;
        private readonly PrinterCommunicationServiceFactory _factory;
        private readonly ILogger<PrinterConnectionManager> _logger;
        private readonly FilamentInventoryService _filamentInventoryService;
        private readonly AnalyticsService _analyticsService;
        private readonly NotificationService _notificationService;
        private readonly IAppConfigurationService _configService;
        private readonly List<ManagedPrinterState> _printers = [];
        private readonly SemaphoreSlim _lock = new(1, 1);
        private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _lifecycleLocks = new();
        private readonly ConcurrentDictionary<IPrinterCommunicationService, ServiceSubscriptions>
            _subscriptions = new(ReferenceEqualityComparer.Instance);
        private int _disposeState;

        /// <summary>
        /// Fires when any printer's state changes (connected, disconnected, telemetry update, etc.).
        /// </summary>
        public event EventHandler? PrintersChanged;

        /// <summary>
        /// Fires when the active printer selection changes.
        /// </summary>
        public event EventHandler<ManagedPrinterState?>? ActivePrinterChanged;

        /// <summary>
        /// Read-only snapshot of all managed printers.
        /// </summary>
        public IReadOnlyList<ManagedPrinterState> Printers => _printers.AsReadOnly();

        /// <summary>
        /// The currently selected "active" printer (used by single-printer views like Dashboard, ControlPanel).
        /// </summary>
        public ManagedPrinterState? ActivePrinter => _printers.FirstOrDefault(p => p.IsActive);

        public PrinterConnectionManager(
            IAppLocalStorageProvider storage,
            PrinterConfigurationPersistence persistence,
            IPrinterBackendFactory backendFactory,
            PrinterCommunicationServiceFactory factory,
            ILogger<PrinterConnectionManager> logger,
            FilamentInventoryService filamentInventoryService,
            AnalyticsService analyticsService,
            NotificationService notificationService,
            IAppConfigurationService configService)
        {
            _storage = storage;
            _persistence = persistence;
            _backendFactory = backendFactory;
            _factory = factory;
            _logger = logger;
            _filamentInventoryService = filamentInventoryService;
            _analyticsService = analyticsService;
            _notificationService = notificationService;
            _configService = configService;
        }

        /// <summary>
        /// Loads saved printer definitions from storage. Called once at app startup.
        /// </summary>
        public async Task InitializeAsync()
        {
            ThrowIfDisposed();
            await _lock.WaitAsync();
            try
            {
                ThrowIfDisposed();
                if (_printers.Count > 0)
                    return;

                var result = await LoadDefinitionsAsync();
                foreach (var definition in result.Items)
                {
                    _printers.Add(new ManagedPrinterState
                    {
                        Definition = definition,
                        Status = PrinterStatus.Disconnected
                    });
                }

                // Set first printer as active if any exist
                if (_printers.Count > 0)
                {
                    _printers[0].IsActive = true;
                }

                if (result.RequiresMigration)
                    await SaveDefinitionsUnsafeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load printer definitions from storage");
            }
            finally
            {
                _lock.Release();
            }

            RaisePrintersChanged();
        }

        /// <summary>
        /// Disconnects all printers, clears state, and re-initializes from storage.
        /// Used when switching farm configurations.
        /// </summary>
        public async Task ReloadAsync()
        {
            ThrowIfDisposed();

            PersistenceReadResult<PrinterConnectionDefinition> result;
            try
            {
                // Validate the incoming store before tearing down live connections.
                result = await LoadDefinitionsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load printer definitions during reload");
                return;
            }

            List<ManagedPrinterState> previous;
            await _lock.WaitAsync();
            try
            {
                previous = _printers.ToList();
            }
            finally
            {
                _lock.Release();
            }

            var gates = previous
                .Select(state => GetLifecycleLock(state.Definition.Id))
                .Distinct()
                .ToList();
            foreach (var gate in gates)
                await gate.WaitAsync();

            try
            {
                foreach (var state in previous)
                    await ReleaseServiceAsync(state, disconnect: true, "reload");

                await _lock.WaitAsync();
                try
                {
                    _printers.Clear();
                    _printers.AddRange(result.Items.Select(definition => new ManagedPrinterState
                    {
                        Definition = definition,
                        Status = PrinterStatus.Disconnected
                    }));

                    if (_printers.Count > 0)
                        _printers[0].IsActive = true;

                    if (result.RequiresMigration)
                        await SaveDefinitionsUnsafeAsync();
                }
                finally
                {
                    _lock.Release();
                }
            }
            finally
            {
                foreach (var gate in gates)
                    gate.Release();
            }

            SyncActiveToFactory(ActivePrinter);
            ActivePrinterChanged?.Invoke(this, ActivePrinter);
            RaisePrintersChanged();
        }

        /// <summary>
        /// Auto-connects all printers that have AutoConnect enabled.
        /// Should be called after InitializeAsync.
        /// </summary>
        public async Task AutoConnectAsync()
        {
            ThrowIfDisposed();
            var autoConnectPrinters = _printers
                .Where(p => p.Definition.AutoConnect && p.Status == PrinterStatus.Disconnected)
                .ToList();

            // Connect in parallel with per-printer error isolation
            var tasks = autoConnectPrinters.Select(p => ConnectPrinterAsync(p.Definition.Id));
            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Adds a new printer definition and persists it.
        /// </summary>
        public async Task<PrinterConnectionDefinition> AddPrinterAsync(PrinterConnectionDefinition definition)
        {
            ThrowIfDisposed();
            await _lock.WaitAsync();
            try
            {
                ThrowIfDisposed();
                if (definition.Id == Guid.Empty)
                    definition.Id = Guid.NewGuid();
                if (_printers.Any(item => item.Definition.Id == definition.Id))
                    throw new InvalidOperationException(
                        $"A printer with ID {definition.Id} already exists.");

                var state = new ManagedPrinterState
                {
                    Definition = definition,
                    Status = PrinterStatus.Disconnected
                };

                _printers.Add(state);

                // If this is the first printer, make it active
                if (_printers.Count == 1)
                {
                    state.IsActive = true;
                }

                try
                {
                    await SaveDefinitionsUnsafeAsync();
                }
                catch
                {
                    _printers.Remove(state);
                    throw;
                }
            }
            finally
            {
                _lock.Release();
            }

            RaisePrintersChanged();
            return definition;
        }

        /// <summary>
        /// Updates an existing printer definition and persists the change.
        /// </summary>
        public async Task UpdatePrinterAsync(PrinterConnectionDefinition definition)
        {
            ThrowIfDisposed();
            await _lock.WaitAsync();
            try
            {
                ThrowIfDisposed();
                var state = _printers.FirstOrDefault(p => p.Definition.Id == definition.Id);
                if (state == null)
                {
                    _logger.LogWarning("Attempted to update non-existent printer {Id}", definition.Id);
                    return;
                }

                var previous = state.Definition;
                state.Definition = definition;
                try
                {
                    await SaveDefinitionsUnsafeAsync();
                }
                catch
                {
                    state.Definition = previous;
                    throw;
                }
            }
            finally
            {
                _lock.Release();
            }

            RaisePrintersChanged();
        }

        /// <summary>
        /// Removes a printer definition, disconnects if connected, and persists.
        /// </summary>
        public async Task RemovePrinterAsync(Guid printerId)
        {
            ThrowIfDisposed();
            var lifecycleLock = GetLifecycleLock(printerId);
            await lifecycleLock.WaitAsync();
            try
            {
                ThrowIfDisposed();
                ManagedPrinterState? state;
                await _lock.WaitAsync();
                try
                {
                    state = _printers.FirstOrDefault(p => p.Definition.Id == printerId);
                }
                finally
                {
                    _lock.Release();
                }

                if (state == null)
                    return;

                state.IsBusy = true;
                RaisePrintersChanged();
                await ReleaseServiceAsync(state, disconnect: true, "removal");

                await _lock.WaitAsync();
                try
                {
                    var index = _printers.IndexOf(state);
                    if (index < 0)
                        return;

                    var wasActive = state.IsActive;
                    _printers.RemoveAt(index);
                    if (wasActive && _printers.Count > 0)
                        _printers[0].IsActive = true;

                    try
                    {
                        await SaveDefinitionsUnsafeAsync();
                    }
                    catch
                    {
                        state.IsBusy = false;
                        _printers.Insert(index, state);
                        if (wasActive)
                        {
                            foreach (var printer in _printers)
                                printer.IsActive = ReferenceEquals(printer, state);
                        }
                        throw;
                    }
                }
                finally
                {
                    _lock.Release();
                }

                SyncActiveToFactory(ActivePrinter);
                ActivePrinterChanged?.Invoke(this, ActivePrinter);
            }
            finally
            {
                lifecycleLock.Release();
            }

            RaisePrintersChanged();
        }

        /// <summary>
        /// Connects a specific printer by its definition ID.
        /// </summary>
        public async Task ConnectPrinterAsync(Guid printerId)
        {
            ThrowIfDisposed();
            var lifecycleLock = GetLifecycleLock(printerId);
            await lifecycleLock.WaitAsync();
            try
            {
                ThrowIfDisposed();
                ManagedPrinterState? state;
                await _lock.WaitAsync();
                try
                {
                    state = _printers.FirstOrDefault(p => p.Definition.Id == printerId);
                }
                finally
                {
                    _lock.Release();
                }

                if (state == null || state.Service?.IsConnected == true)
                    return;

                state.IsBusy = true;
                state.LastError = null;
                RaisePrintersChanged();

                if (state.Service != null)
                    await ReleaseServiceAsync(state, disconnect: true, "reconnect");

                IPrinterCommunicationService? service = null;
                try
                {
                    service = _backendFactory.Create(state.Definition.ConnectionType);
                    state.Service = service;
                    Subscribe(state, service);

                    if (!await service.ConnectAsync(state.Definition.Settings))
                    {
                        state.LastError = "Connection failed";
                        state.Status = PrinterStatus.Disconnected;
                        state.Service = null;
                        await DisposeServiceAsync(
                            state,
                            service,
                            disconnect: true,
                            "failed connection");
                        service = null;
                        return;
                    }

                    state.Status = PrinterStatus.Connected;
                    state.Definition.LastConnectedAt = DateTime.UtcNow;

                    if (state.IsActive)
                        SyncActiveToFactory(state);

                    try
                    {
                        await SaveDefinitionsAsync();
                    }
                    catch (Exception ex)
                    {
                        // A storage problem must not turn a live printer connection
                        // into a reported connection failure.
                        _logger.LogWarning(
                            ex,
                            "Connected printer {Name}, but failed to persist its last-connected timestamp",
                            state.Definition.Name);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to connect printer {Name}", state.Definition.Name);
                    state.LastError = ex.Message;
                    state.Status = PrinterStatus.Error;

                    if (service != null)
                    {
                        if (ReferenceEquals(state.Service, service))
                            state.Service = null;
                        await DisposeServiceAsync(
                            state,
                            service,
                            disconnect: true,
                            "connection error");
                    }
                }
                finally
                {
                    state.IsBusy = false;
                    if (state.IsActive)
                        SyncActiveToFactory(state);
                    RaisePrintersChanged();
                }
            }
            finally
            {
                lifecycleLock.Release();
            }
        }

        private async Task HandleTelemetryUpdateAsync(ManagedPrinterState state, PrinterTelemetry telemetry)
        {
            var previousStatus = state.Status;
            state.Telemetry = telemetry;
            state.Status = telemetry.Status;

            // Track print job start
            if (previousStatus != PrinterStatus.Printing && state.Status == PrinterStatus.Printing)
            {
                state.PrintStartTime = DateTime.UtcNow;
                state.AccumulatedExtrusion = 0;

                if (_configService.Configuration.EnableFilamentInventory && state.Definition.AssignedFilamentSpoolId == null)
                {
                    await _notificationService.NotifyAsync(NotificationLevel.Warning, "Print Started", "Print started without assigned filament.", state.Definition.Id);
                }
            }

            // Track print job end
            if (previousStatus == PrinterStatus.Printing && state.Status != PrinterStatus.Printing)
            {
                var duration = state.PrintStartTime.HasValue ? DateTime.UtcNow - state.PrintStartTime.Value : TimeSpan.Zero;
                var filamentUsed = telemetry.FilamentUsed > 0 ? telemetry.FilamentUsed : state.AccumulatedExtrusion;

                if (state.Status == PrinterStatus.Connected || state.Status == PrinterStatus.Disconnected)
                {
                    await _notificationService.NotifyAsync(NotificationLevel.Info, "Print Completed", $"Print job '{telemetry.PrintJobName}' completed.", state.Definition.Id);
                }
                else if (state.Status == PrinterStatus.Error)
                {
                    await _notificationService.NotifyAsync(NotificationLevel.Error, "Print Failed", $"Print job '{telemetry.PrintJobName}' failed.", state.Definition.Id);
                }

                if (state.Definition.AssignedFilamentSpoolId.HasValue && filamentUsed > 0)
                {
                    var spoolId = state.Definition.AssignedFilamentSpoolId.Value;

                    if (_configService.Configuration.EnablePrintAnalytics)
                    {
                        var record = new PrintJobUsageRecord
                        {
                            PrinterId = state.Definition.Id,
                            FilamentSpoolId = spoolId,
                            JobName = telemetry.PrintJobName,
                            Duration = duration,
                            EstimatedFilamentUsedGrams = filamentUsed,
                            ActualFilamentUsedGrams = filamentUsed
                        };
                        await _analyticsService.RecordUsageAsync(record);
                    }

                    if (_configService.Configuration.EnableFilamentInventory)
                    {
                        await _filamentInventoryService.DeductFilamentAsync(spoolId, filamentUsed);

                        var spool = _filamentInventoryService.GetSpool(spoolId);
                        if (spool != null)
                        {
                            if (spool.RemainingWeightGrams <= 0)
                            {
                                await _notificationService.NotifyAsync(NotificationLevel.Critical, "Spool Empty", $"Filament spool '{spool.Name}' is empty.", state.Definition.Id, spoolId);
                            }
                            else if (spool.RemainingWeightGrams < spool.TotalWeightGrams * 0.1) // 10% threshold
                            {
                                await _notificationService.NotifyAsync(NotificationLevel.Warning, "Low Filament", $"Filament spool '{spool.Name}' is running low.", state.Definition.Id, spoolId);
                            }
                        }
                    }
                }

                state.PrintStartTime = null;
                state.AccumulatedExtrusion = 0;
            }

            RaisePrintersChanged();
        }

        /// <summary>
        /// Disconnects a specific printer by its definition ID.
        /// </summary>
        public async Task DisconnectPrinterAsync(Guid printerId)
        {
            ThrowIfDisposed();
            var lifecycleLock = GetLifecycleLock(printerId);
            await lifecycleLock.WaitAsync();
            try
            {
                ThrowIfDisposed();
                ManagedPrinterState? state;
                await _lock.WaitAsync();
                try
                {
                    state = _printers.FirstOrDefault(p => p.Definition.Id == printerId);
                }
                finally
                {
                    _lock.Release();
                }

                if (state?.Service == null)
                    return;

                state.IsBusy = true;
                RaisePrintersChanged();

                await ReleaseServiceAsync(state, disconnect: true, "disconnect");
                state.Status = PrinterStatus.Disconnected;
                state.IsBusy = false;

                if (state.IsActive)
                    SyncActiveToFactory(state);

                RaisePrintersChanged();
            }
            finally
            {
                lifecycleLock.Release();
            }
        }

        /// <summary>
        /// Sets which printer is the "active" one for single-printer views.
        /// Also syncs the legacy factory's Current property.
        /// </summary>
        public void SetActivePrinter(Guid printerId)
        {
            ThrowIfDisposed();
            foreach (var p in _printers)
            {
                p.IsActive = p.Definition.Id == printerId;
            }

            var active = ActivePrinter;
            SyncActiveToFactory(active);
            ActivePrinterChanged?.Invoke(this, active);
            RaisePrintersChanged();
        }

        private void Subscribe(
            ManagedPrinterState state,
            IPrinterCommunicationService service)
        {
            EventHandler<PrinterTelemetry> telemetryHandler = (_, telemetry) =>
                _ = HandleTelemetryUpdateSafelyAsync(state, service, telemetry);
            EventHandler<bool> connectionHandler = (_, isConnected) =>
                HandleConnectionStateChanged(state, service, isConnected);

            _subscriptions[service] = new ServiceSubscriptions(
                telemetryHandler,
                connectionHandler);
            service.TelemetryUpdated += telemetryHandler;
            service.ConnectionStateChanged += connectionHandler;
        }

        private async Task HandleTelemetryUpdateSafelyAsync(
            ManagedPrinterState state,
            IPrinterCommunicationService service,
            PrinterTelemetry telemetry)
        {
            if (!ReferenceEquals(state.Service, service))
                return;

            try
            {
                await HandleTelemetryUpdateAsync(state, telemetry);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to process telemetry for printer {Name}",
                    state.Definition.Name);
            }
        }

        private void HandleConnectionStateChanged(
            ManagedPrinterState state,
            IPrinterCommunicationService service,
            bool isConnected)
        {
            if (!ReferenceEquals(state.Service, service))
                return;

            state.Status = isConnected
                ? PrinterStatus.Connected
                : PrinterStatus.Disconnected;
            RaisePrintersChanged();

            if (state.IsActive)
                SyncActiveToFactory(state);
        }

        private async Task ReleaseServiceAsync(
            ManagedPrinterState state,
            bool disconnect,
            string operation)
        {
            var service = state.Service;
            if (service == null)
                return;

            state.Service = null;
            if (state.IsActive)
                SyncActiveToFactory(state);

            await DisposeServiceAsync(state, service, disconnect, operation);
        }

        private async Task DisposeServiceAsync(
            ManagedPrinterState state,
            IPrinterCommunicationService service,
            bool disconnect,
            string operation)
        {
            if (_subscriptions.TryRemove(service, out var subscriptions))
            {
                service.TelemetryUpdated -= subscriptions.TelemetryHandler;
                service.ConnectionStateChanged -= subscriptions.ConnectionHandler;
            }

            if (disconnect)
            {
                try
                {
                    await service.DisconnectAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Error disconnecting printer {Name} during {Operation}",
                        state.Definition.Name,
                        operation);
                }
            }

            try
            {
                await service.DisposeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error disposing printer {Name} during {Operation}",
                    state.Definition.Name,
                    operation);
            }
        }

        private SemaphoreSlim GetLifecycleLock(Guid printerId) =>
            _lifecycleLocks.GetOrAdd(printerId, static _ => new SemaphoreSlim(1, 1));

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(
                Volatile.Read(ref _disposeState) != 0,
                this);
        }

        /// <summary>
        /// Keeps the legacy PrinterCommunicationServiceFactory in sync with the active printer.
        /// This preserves backward compatibility with all existing single-printer UI components.
        /// </summary>
        private void SyncActiveToFactory(ManagedPrinterState? state)
        {
            // The factory exposes Current + IsConnected + ConnectionStateChanged.
            // We update these to match the active managed printer.
            _factory.SetManagedCurrent(state?.Service);
        }

        private async Task<PersistenceReadResult<PrinterConnectionDefinition>>
            LoadDefinitionsAsync()
        {
            var files = await _storage.ListFilesAsync();
            var file = files.FirstOrDefault(item =>
                IsStorageFile(item.FullPath, StorageKey));
            if (file == null)
                return new([], false);

            using var stream = await _storage.OpenReadAsync(file.FullPath);
            if (stream == null)
                return new([], false);

            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync();
            return _persistence.DeserializeConnections(json);
        }

        private async Task SaveDefinitionsAsync()
        {
            await _lock.WaitAsync();
            try
            {
                await SaveDefinitionsUnsafeAsync();
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task SaveDefinitionsUnsafeAsync()
        {
            var json = _persistence.SerializeConnections(
                _printers.Select(state => state.Definition));
            await WriteStorageFileAsync(StorageKey, json);
        }

        private async Task WriteStorageFileAsync(string storageKey, string json)
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            using var stream = new MemoryStream(bytes, writable: false);
            await _storage.SaveFileAsync(storageKey, stream);
        }

        private static bool IsStorageFile(string fullPath, string storageKey) =>
            string.Equals(fullPath, storageKey, StringComparison.Ordinal)
            || string.Equals(
                Path.GetFileName(fullPath),
                storageKey,
                StringComparison.Ordinal);

        private void RaisePrintersChanged()
        {
            PrintersChanged?.Invoke(this, EventArgs.Empty);
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposeState, 1) != 0)
                return;

            List<ManagedPrinterState> printers;
            await _lock.WaitAsync();
            try
            {
                printers = _printers.ToList();
            }
            finally
            {
                _lock.Release();
            }

            var gates = printers
                .Select(state => GetLifecycleLock(state.Definition.Id))
                .Distinct()
                .ToList();
            foreach (var gate in gates)
                await gate.WaitAsync();

            try
            {
                foreach (var printer in printers)
                    await ReleaseServiceAsync(printer, disconnect: true, "manager disposal");

                await _lock.WaitAsync();
                try
                {
                    _printers.Clear();
                }
                finally
                {
                    _lock.Release();
                }
            }
            finally
            {
                foreach (var gate in gates)
                    gate.Release();
            }

            _factory.SetManagedCurrent(null);
            _subscriptions.Clear();
        }

        private sealed record ServiceSubscriptions(
            EventHandler<PrinterTelemetry> TelemetryHandler,
            EventHandler<bool> ConnectionHandler);
    }
}
