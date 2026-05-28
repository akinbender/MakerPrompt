using MakerPrompt.Core.Abstractions;
using MakerPrompt.Core.Models;
using MakerPrompt.Infrastructure.Services;

namespace MakerPrompt.EdgeAgent.Workers;

/// <summary>
/// Background worker that polls registered printers on a fixed interval,
/// collects telemetry snapshots, and forwards them to the configured
/// <see cref="ITelemetryStore"/> (and optionally to the cloud backend via
/// <see cref="IEdgeAgentClient"/>).
///
/// Polling errors are swallowed silently to avoid log spam — only unexpected
/// exceptions that indicate a fatal misconfiguration are propagated.
/// After <see cref="OfflineThreshold"/> consecutive failures the printer is
/// marked offline in the store and the cloud is notified.
/// </summary>
public sealed class PrinterPollingWorker : BackgroundService
{
    private const int OfflineThreshold = 3;

    private readonly PrinterFleetService _fleet;
    private readonly ITelemetryStore _store;
    private readonly IEdgeAgentClient? _cloudClient;
    private readonly ILogger<PrinterPollingWorker> _logger;
    private readonly TimeSpan _pollInterval;

    // Tracks consecutive poll failures per printer.
    private readonly Dictionary<string, int> _failureCounts = new();

    public PrinterPollingWorker(
        PrinterFleetService fleet,
        ITelemetryStore store,
        ILogger<PrinterPollingWorker> logger,
        IConfiguration configuration,
        IEdgeAgentClient? cloudClient = null)
    {
        _fleet = fleet;
        _store = store;
        _logger = logger;
        _cloudClient = cloudClient;

        var intervalSeconds = configuration.GetValue("EdgeAgent:PollIntervalSeconds", 5);
        _pollInterval = TimeSpan.FromSeconds(Math.Max(1, intervalSeconds));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "PrinterPollingWorker started — poll interval {Interval}", _pollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            await PollAllPrintersAsync(stoppingToken);
            await Task.Delay(_pollInterval, stoppingToken).ConfigureAwait(false);
        }

        _logger.LogInformation("PrinterPollingWorker stopped");
    }

    private async Task PollAllPrintersAsync(CancellationToken cancellationToken)
    {
        var printerIds = _fleet.PrinterIds;
        if (printerIds.Count == 0) return;

        foreach (var printerId in printerIds)
        {
            var service = _fleet.GetConnection(printerId);
            if (service is null) continue;

            // If the service reports disconnected, count that as a failure without polling.
            if (!service.IsConnected)
            {
                await RecordFailureAsync(printerId, cancellationToken);
                continue;
            }

            try
            {
                var telemetry = await service.GetTelemetryAsync(cancellationToken);
                await _store.SaveAsync(printerId, telemetry, cancellationToken);

                // Successful poll — reset failure counter and push to cloud.
                _failureCounts[printerId] = 0;
                await TrySendToCloudAsync(printerId, telemetry, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Shutdown requested — exit cleanly.
                return;
            }
            catch (Exception ex)
            {
                // Swallow per-printer polling errors — log at Debug to avoid spam.
                _logger.LogDebug(ex, "Polling error for printer {PrinterId}", printerId);
                await RecordFailureAsync(printerId, cancellationToken);
            }
        }
    }

    private async Task RecordFailureAsync(string printerId, CancellationToken cancellationToken)
    {
        _failureCounts.TryGetValue(printerId, out var count);
        _failureCounts[printerId] = ++count;

        if (count < OfflineThreshold) return;

        // Hit the threshold — mark offline locally and notify cloud.
        _logger.LogWarning(
            "Printer {PrinterId} unreachable after {Count} consecutive failures — marking offline",
            printerId, count);

        var offlineTelemetry = new PrinterTelemetry
        {
            Status = PrinterStatus.Disconnected,
            CapturedAt = DateTime.UtcNow,
        };

        try
        {
            await _store.SaveAsync(printerId, offlineTelemetry, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to persist offline telemetry for {PrinterId}", printerId);
        }

        await TrySendToCloudAsync(printerId, offlineTelemetry, cancellationToken);
    }

    private async Task TrySendToCloudAsync(
        string printerId,
        PrinterTelemetry telemetry,
        CancellationToken cancellationToken)
    {
        if (_cloudClient is null) return;

        try
        {
            await _cloudClient.SendTelemetryAsync(printerId, telemetry, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Cloud push failed for printer {PrinterId}", printerId);
        }
    }
}

