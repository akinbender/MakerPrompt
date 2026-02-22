using Microsoft.Extensions.Logging;

namespace MakerPrompt.Shared.Services
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
        private readonly IConnectionEncryptionService _encryption;
        private readonly PrinterCommunicationServiceFactory _factory;
        private readonly ISerialService _serialService;
        private readonly ILogger<PrinterConnectionManager> _logger;
        private readonly List<ManagedPrinterState> _printers = new();
        private readonly SemaphoreSlim _lock = new(1, 1);

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
            IConnectionEncryptionService encryption,
            PrinterCommunicationServiceFactory factory,
            ISerialService serialService,
            ILogger<PrinterConnectionManager> logger)
        {
            _storage = storage;
            _encryption = encryption;
            _factory = factory;
            _serialService = serialService;
            _logger = logger;
        }

        /// <summary>
        /// Loads saved printer definitions from storage. Called once at app startup.
        /// </summary>
        public async Task InitializeAsync()
        {
            await _lock.WaitAsync();
            try
            {
                var definitions = await LoadDefinitionsAsync();
                foreach (var def in definitions)
                {
                    DecryptSensitiveFields(def);
                    _printers.Add(new ManagedPrinterState
                    {
                        Definition = def,
                        Status = PrinterStatus.Disconnected
                    });
                }

                // Set first printer as active if any exist
                if (_printers.Count > 0)
                {
                    _printers[0].IsActive = true;
                }
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
        /// Auto-connects all printers that have AutoConnect enabled.
        /// Should be called after InitializeAsync.
        /// </summary>
        public async Task AutoConnectAsync()
        {
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
            await _lock.WaitAsync();
            try
            {
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

                await SaveDefinitionsAsync();
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
            await _lock.WaitAsync();
            try
            {
                var state = _printers.FirstOrDefault(p => p.Definition.Id == definition.Id);
                if (state == null)
                {
                    _logger.LogWarning("Attempted to update non-existent printer {Id}", definition.Id);
                    return;
                }

                state.Definition = definition;
                await SaveDefinitionsAsync();
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
            await _lock.WaitAsync();
            try
            {
                var state = _printers.FirstOrDefault(p => p.Definition.Id == printerId);
                if (state == null) return;

                // Disconnect if connected
                if (state.Service != null)
                {
                    try
                    {
                        await state.Service.DisconnectAsync();
                        await state.Service.DisposeAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error disconnecting printer {Name} during removal", state.Definition.Name);
                    }
                }

                bool wasActive = state.IsActive;
                _printers.Remove(state);

                // If we removed the active printer, select the first remaining one
                if (wasActive && _printers.Count > 0)
                {
                    _printers[0].IsActive = true;
                    SyncActiveToFactory(_printers[0]);
                }

                await SaveDefinitionsAsync();
            }
            finally
            {
                _lock.Release();
            }

            RaisePrintersChanged();
        }

        /// <summary>
        /// Connects a specific printer by its definition ID.
        /// </summary>
        public async Task ConnectPrinterAsync(Guid printerId)
        {
            var state = _printers.FirstOrDefault(p => p.Definition.Id == printerId);
            if (state == null) return;
            if (state.Service?.IsConnected == true) return;

            state.IsBusy = true;
            state.LastError = null;
            RaisePrintersChanged();

            try
            {
                var service = CreateBackendService(state.Definition.ConnectionType);
                var connected = await service.ConnectAsync(state.Definition.Settings);

                if (connected)
                {
                    state.Service = service;
                    state.Status = PrinterStatus.Connected;
                    state.Definition.LastConnectedAt = DateTime.UtcNow;

                    // Subscribe to telemetry
                    service.TelemetryUpdated += (_, telemetry) =>
                    {
                        state.Telemetry = telemetry;
                        state.Status = telemetry.Status;
                        RaisePrintersChanged();
                    };

                    service.ConnectionStateChanged += (_, isConnected) =>
                    {
                        state.Status = isConnected ? PrinterStatus.Connected : PrinterStatus.Disconnected;
                        if (!isConnected)
                        {
                            state.Service = null;
                        }
                        RaisePrintersChanged();

                        // Keep factory in sync
                        if (state.IsActive)
                        {
                            SyncActiveToFactory(state);
                        }
                    };

                    // Sync to factory if this is the active printer
                    if (state.IsActive)
                    {
                        SyncActiveToFactory(state);
                    }

                    // Persist last connected timestamp
                    await SaveDefinitionsAsync();
                }
                else
                {
                    state.LastError = "Connection failed";
                    state.Status = PrinterStatus.Disconnected;
                    await service.DisposeAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect printer {Name}", state.Definition.Name);
                state.LastError = ex.Message;
                state.Status = PrinterStatus.Error;
            }
            finally
            {
                state.IsBusy = false;
                RaisePrintersChanged();
            }
        }

        /// <summary>
        /// Disconnects a specific printer by its definition ID.
        /// </summary>
        public async Task DisconnectPrinterAsync(Guid printerId)
        {
            var state = _printers.FirstOrDefault(p => p.Definition.Id == printerId);
            if (state?.Service == null) return;

            state.IsBusy = true;
            RaisePrintersChanged();

            try
            {
                await state.Service.DisconnectAsync();
                await state.Service.DisposeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disconnecting printer {Name}", state.Definition.Name);
            }
            finally
            {
                state.Service = null;
                state.Status = PrinterStatus.Disconnected;
                state.IsBusy = false;

                // Keep factory in sync
                if (state.IsActive)
                {
                    SyncActiveToFactory(state);
                }

                RaisePrintersChanged();
            }
        }

        /// <summary>
        /// Sets which printer is the "active" one for single-printer views.
        /// Also syncs the legacy factory's Current property.
        /// </summary>
        public void SetActivePrinter(Guid printerId)
        {
            foreach (var p in _printers)
            {
                p.IsActive = p.Definition.Id == printerId;
            }

            var active = ActivePrinter;
            SyncActiveToFactory(active);
            ActivePrinterChanged?.Invoke(this, active);
            RaisePrintersChanged();
        }

        /// <summary>
        /// Creates a new backend service instance for the given connection type.
        /// Each printer gets its own instance — no singleton sharing.
        /// </summary>
        private IPrinterCommunicationService CreateBackendService(PrinterConnectionType type)
        {
            return type switch
            {
                PrinterConnectionType.Demo => new DemoPrinterService(),
                PrinterConnectionType.Serial => _serialService,
                PrinterConnectionType.PrusaLink => new PrusaLinkApiService(),
                PrinterConnectionType.Moonraker => new MoonrakerApiService(),
                PrinterConnectionType.BambuLab => new BambuLabApiService(),
                PrinterConnectionType.OctoPrint => new OctoPrintApiService(),
                _ => throw new NotSupportedException($"Unsupported connection type: {type}")
            };
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

        private async Task<List<PrinterConnectionDefinition>> LoadDefinitionsAsync()
        {
            try
            {
                var files = await _storage.ListFilesAsync();
                var file = files.FirstOrDefault(f => f.FullPath.Contains(StorageKey));
                if (file == null)
                    return new List<PrinterConnectionDefinition>();

                using var stream = await _storage.OpenReadAsync(file.FullPath);
                if (stream == null)
                    return new List<PrinterConnectionDefinition>();

                using var reader = new StreamReader(stream);
                var json = await reader.ReadToEndAsync();
                return JsonSerializer.Deserialize<List<PrinterConnectionDefinition>>(json) ?? new();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load printer definitions");
                return new List<PrinterConnectionDefinition>();
            }
        }

        private async Task SaveDefinitionsAsync()
        {
            try
            {
                var defs = _printers.Select(p =>
                {
                    // Clone definition and encrypt sensitive fields before saving
                    var clone = CloneDefinition(p.Definition);
                    EncryptSensitiveFields(clone);
                    return clone;
                }).ToList();

                var json = JsonSerializer.Serialize(defs, new JsonSerializerOptions { WriteIndented = true });
                var bytes = Encoding.UTF8.GetBytes(json);
                using var stream = new MemoryStream(bytes);
                await _storage.SaveFileAsync(StorageKey, stream);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save printer definitions");
            }
        }

        private void EncryptSensitiveFields(PrinterConnectionDefinition def)
        {
            if (def.Settings.Api != null)
            {
                if (!string.IsNullOrEmpty(def.Settings.Api.Password))
                    def.Settings.Api.Password = _encryption.Encrypt(def.Settings.Api.Password);
                if (!string.IsNullOrEmpty(def.Settings.Api.UserName))
                    def.Settings.Api.UserName = _encryption.Encrypt(def.Settings.Api.UserName);
            }
        }

        private void DecryptSensitiveFields(PrinterConnectionDefinition def)
        {
            if (def.Settings.Api != null)
            {
                if (!string.IsNullOrEmpty(def.Settings.Api.Password))
                    def.Settings.Api.Password = _encryption.Decrypt(def.Settings.Api.Password);
                if (!string.IsNullOrEmpty(def.Settings.Api.UserName))
                    def.Settings.Api.UserName = _encryption.Decrypt(def.Settings.Api.UserName);
            }
        }

        private static PrinterConnectionDefinition CloneDefinition(PrinterConnectionDefinition original)
        {
            var json = JsonSerializer.Serialize(original);
            return JsonSerializer.Deserialize<PrinterConnectionDefinition>(json)!;
        }

        private void RaisePrintersChanged()
        {
            PrintersChanged?.Invoke(this, EventArgs.Empty);
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var printer in _printers)
            {
                if (printer.Service != null)
                {
                    try
                    {
                        await printer.Service.DisposeAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error disposing printer {Name}", printer.Definition.Name);
                    }
                }
            }
            _printers.Clear();
            _lock.Dispose();
        }
    }
}
