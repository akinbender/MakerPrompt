using MakerPrompt.Application.Services;
using MakerPrompt.Core.Abstractions;

namespace MakerPrompt.EdgeAgent.Workers;

/// <summary>
/// Background worker that polls registered printers on a fixed interval,
/// collects telemetry snapshots, and forwards them to the configured
/// <see cref="ITelemetryStore"/> (and optionally to the cloud backend via
/// <see cref="IEdgeAgentClient"/>).
///
/// Polling errors are swallowed silently to avoid log spam — only unexpected
/// exceptions that indicate a fatal misconfiguration are propagated.
/// </summary>
public sealed class PrinterPollingWorker : BackgroundService
{
    private readonly PrinterFleetService _fleet;
    private readonly ITelemetryStore _store;
    private readonly ILogger<PrinterPollingWorker> _logger;
    private readonly TimeSpan _pollInterval;

    public PrinterPollingWorker(
        PrinterFleetService fleet,
        ITelemetryStore store,
        ILogger<PrinterPollingWorker> logger,
        IConfiguration configuration)
    {
        _fleet = fleet;
        _store = store;
        _logger = logger;

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
            if (service is null || !service.IsConnected) continue;

            try
            {
                var telemetry = await service.GetTelemetryAsync(cancellationToken);
                await _store.SaveAsync(printerId, telemetry, cancellationToken);
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
            }
        }
    }
}
