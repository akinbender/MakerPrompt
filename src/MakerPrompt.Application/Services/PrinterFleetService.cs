using MakerPrompt.Core.Abstractions;
using MakerPrompt.Core.Models;
using Microsoft.Extensions.Logging;

namespace MakerPrompt.Application.Services;

/// <summary>
/// Application service that manages a fleet of printers.
///
/// Responsibilities
/// ----------------
/// • Maintains a registry of active <see cref="IPrinterCommunicationService"/> instances,
///   keyed by a stable printer identifier.
/// • Surfaces aggregated fleet status and per-printer telemetry.
/// • Coordinates with <see cref="IPrinterProvider"/> implementations to discover
///   printers exposed by cloud/farm accounts, then creates the appropriate
///   backend connection service for each one.
///
/// Design note
/// -----------
/// This service lives in the Application layer and has no dependency on Blazor,
/// making it equally usable from the EdgeAgent, the Cloud backend, and any future
/// CLI or native UI host.
/// </summary>
public sealed class PrinterFleetService : IAsyncDisposable
{
    private readonly ILogger<PrinterFleetService> _logger;
    private readonly Dictionary<string, IPrinterCommunicationService> _connections = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>Raised when any printer in the fleet changes state.</summary>
    public event EventHandler? FleetChanged;

    public PrinterFleetService(ILogger<PrinterFleetService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Returns a read-only snapshot of all registered printer identifiers.
    /// </summary>
    public IReadOnlyCollection<string> PrinterIds
    {
        get
        {
            lock (_connections)
                return _connections.Keys.ToArray();
        }
    }

    /// <summary>
    /// Returns the communication service for the given printer, or <c>null</c> if not registered.
    /// </summary>
    public IPrinterCommunicationService? GetConnection(string printerId)
    {
        lock (_connections)
            return _connections.GetValueOrDefault(printerId);
    }

    /// <summary>
    /// Registers a printer and immediately attempts to connect using the supplied settings.
    /// If a connection for <paramref name="printerId"/> already exists it is disconnected
    /// and replaced.
    /// </summary>
    public async Task<bool> AddAndConnectAsync(
        string printerId,
        IPrinterCommunicationService service,
        PrinterConnectionSettings settings,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_connections.TryGetValue(printerId, out var existing))
            {
                await existing.DisconnectAsync(cancellationToken);
                await existing.DisposeAsync();
            }

            service.ConnectionStateChanged += (_, connected) => OnFleetChanged();
            service.TelemetryUpdated += (_, _) => OnFleetChanged();

            var connected = await service.ConnectAsync(settings, cancellationToken);
            _connections[printerId] = service;
            OnFleetChanged();
            return connected;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect printer {PrinterId}", printerId);
            return false;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Disconnects and removes the printer with the given identifier from the fleet.
    /// </summary>
    public async Task RemoveAsync(string printerId, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!_connections.Remove(printerId, out var service)) return;
            await service.DisconnectAsync(cancellationToken);
            await service.DisposeAsync();
            OnFleetChanged();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing printer {PrinterId}", printerId);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Returns the latest telemetry for all connected printers, keyed by printer identifier.
    /// </summary>
    public IReadOnlyDictionary<string, PrinterTelemetry> GetFleetTelemetry()
    {
        lock (_connections)
        {
            return _connections
                .Where(kv => kv.Value.IsConnected)
                .ToDictionary(kv => kv.Key, kv => kv.Value.LastTelemetry);
        }
    }

    private void OnFleetChanged() => FleetChanged?.Invoke(this, EventArgs.Empty);

    public async ValueTask DisposeAsync()
    {
        await _lock.WaitAsync();
        try
        {
            foreach (var service in _connections.Values)
            {
                try
                {
                    await service.DisconnectAsync();
                    await service.DisposeAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error disposing printer connection");
                }
            }
            _connections.Clear();
        }
        finally
        {
            _lock.Release();
            _lock.Dispose();
        }
    }
}
