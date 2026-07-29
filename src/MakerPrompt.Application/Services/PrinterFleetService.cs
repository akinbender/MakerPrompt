using MakerPrompt.Core.Abstractions;
using MakerPrompt.Core.Models;
using Microsoft.Extensions.Logging;

namespace MakerPrompt.Application.Services;

/// <summary>
/// Owns the active printer connections for a non-UI host.
/// </summary>
/// <remarks>
/// A service is added to the fleet only after it connects successfully. Failed
/// candidates are disposed, and an existing healthy connection is retained until
/// its replacement is ready.
/// </remarks>
public sealed class PrinterFleetService(ILogger<PrinterFleetService> logger) : IAsyncDisposable
{
    private readonly ILogger<PrinterFleetService> _logger = logger;
    private readonly Dictionary<string, IPrinterCommunicationService> _connections =
        new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly object _connectionsLock = new();
    private bool _disposed;

    /// <summary>Raised after the fleet membership or printer state changes.</summary>
    public event EventHandler? FleetChanged;

    /// <summary>Returns a stable snapshot of registered printer identifiers.</summary>
    public IReadOnlyCollection<string> PrinterIds
    {
        get
        {
            lock (_connectionsLock)
            {
                return _connections.Keys.ToArray();
            }
        }
    }

    /// <summary>Returns the active connection for <paramref name="printerId"/>.</summary>
    public IPrinterCommunicationService? GetConnection(string printerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(printerId);

        lock (_connectionsLock)
        {
            return _connections.GetValueOrDefault(printerId);
        }
    }

    /// <summary>
    /// Connects and registers <paramref name="service"/>.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when the candidate connected and became the active
    /// owner; otherwise <see langword="false"/>.
    /// </returns>
    public async Task<bool> AddAndConnectAsync(
        string printerId,
        IPrinterCommunicationService service,
        PrinterConnectionSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(printerId);
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(settings);

        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            IPrinterCommunicationService? existing;
            lock (_connectionsLock)
            {
                existing = _connections.GetValueOrDefault(printerId);
            }

            if (ReferenceEquals(existing, service))
            {
                return service.IsConnected;
            }

            bool connected;
            try
            {
                connected = await service.ConnectAsync(settings, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                await DisconnectAndDisposeAsync(service, CancellationToken.None)
                    .ConfigureAwait(false);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect printer {PrinterId}", printerId);
                await DisconnectAndDisposeAsync(service, CancellationToken.None)
                    .ConfigureAwait(false);
                return false;
            }

            if (!connected || !service.IsConnected)
            {
                await DisconnectAndDisposeAsync(service, CancellationToken.None)
                    .ConfigureAwait(false);
                return false;
            }

            service.ConnectionStateChanged += HandleConnectionStateChanged;
            service.TelemetryUpdated += HandleTelemetryUpdated;

            lock (_connectionsLock)
            {
                _connections[printerId] = service;
            }

            if (existing is not null)
            {
                Unsubscribe(existing);
                await DisconnectAndDisposeAsync(existing, CancellationToken.None)
                    .ConfigureAwait(false);
            }

            OnFleetChanged();
            return true;
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    /// <summary>Disconnects, disposes, and removes a registered printer.</summary>
    public async Task RemoveAsync(
        string printerId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(printerId);

        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            IPrinterCommunicationService? service;
            lock (_connectionsLock)
            {
                if (!_connections.Remove(printerId, out service))
                {
                    return;
                }
            }

            Unsubscribe(service);
            try
            {
                await DisconnectAndDisposeAsync(service, cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                OnFleetChanged();
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    /// <summary>Returns current telemetry for connected printers.</summary>
    public IReadOnlyDictionary<string, PrinterTelemetry> GetFleetTelemetry()
    {
        lock (_connectionsLock)
        {
            return _connections
                .Where(pair => pair.Value.IsConnected)
                .ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value.LastTelemetry,
                    StringComparer.Ordinal);
        }
    }

    private void HandleConnectionStateChanged(object? sender, bool connected) =>
        OnFleetChanged();

    private void HandleTelemetryUpdated(object? sender, PrinterTelemetry telemetry) =>
        OnFleetChanged();

    private void OnFleetChanged() => FleetChanged?.Invoke(this, EventArgs.Empty);

    private void Unsubscribe(IPrinterCommunicationService service)
    {
        service.ConnectionStateChanged -= HandleConnectionStateChanged;
        service.TelemetryUpdated -= HandleTelemetryUpdated;
    }

    private async Task DisconnectAndDisposeAsync(
        IPrinterCommunicationService service,
        CancellationToken cancellationToken)
    {
        try
        {
            await service.DisconnectAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to disconnect printer connection");
        }
        finally
        {
            try
            {
                await service.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to dispose printer connection");
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _lifecycleGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            IPrinterCommunicationService[] services;
            lock (_connectionsLock)
            {
                services = _connections.Values.ToArray();
                _connections.Clear();
            }

            foreach (var service in services)
            {
                Unsubscribe(service);
                await DisconnectAndDisposeAsync(service, CancellationToken.None)
                    .ConfigureAwait(false);
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }

        GC.SuppressFinalize(this);
    }
}
