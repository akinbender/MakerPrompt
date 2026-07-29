using MakerPrompt.Application.Services;
using MakerPrompt.Core.Abstractions;
using MakerPrompt.Core.Models;
using MakerPrompt.EdgeAgent.Models;
using MakerPrompt.Infrastructure.Services.Printers;

namespace MakerPrompt.EdgeAgent.Workers;

/// <summary>
/// Establishes configured printer connections and recreates them after a
/// disconnect. A fresh adapter is used for every retry.
/// </summary>
public sealed class PrinterConnectionWorker : BackgroundService
{
    private readonly PrinterFleetService _fleet;
    private readonly ILogger<PrinterConnectionWorker> _logger;
    private readonly IReadOnlyList<ConfiguredPrinter> _printers;
    private readonly TimeSpan _retryInterval;
    private readonly Dictionary<string, int> _failureCounts =
        new(StringComparer.Ordinal);

    public PrinterConnectionWorker(
        PrinterFleetService fleet,
        IConfiguration configuration,
        ILogger<PrinterConnectionWorker> logger)
    {
        _fleet = fleet;
        _logger = logger;
        _retryInterval = TimeSpan.FromSeconds(Math.Max(
            1,
            configuration.GetValue("EdgeAgent:ReconnectIntervalSeconds", 15)));
        _printers = ParseConfiguration(configuration);
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_printers.Count == 0)
        {
            _logger.LogWarning("No valid EdgeAgent printers are configured");
            return;
        }

        await EnsureConnectionsAsync(stoppingToken);

        using var timer = new PeriodicTimer(_retryInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await EnsureConnectionsAsync(stoppingToken);
        }
    }

    private async Task EnsureConnectionsAsync(CancellationToken cancellationToken)
    {
        foreach (var configured in _printers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var existing = _fleet.GetConnection(configured.Configuration.PrinterId);
            if (existing?.IsConnected is true)
            {
                _failureCounts[configured.Configuration.PrinterId] = 0;
                continue;
            }

            if (existing is not null)
            {
                await _fleet.RemoveAsync(
                    configured.Configuration.PrinterId,
                    cancellationToken);
            }

            var service = CreateService(configured.ConnectionType);
            var settings = CreateSettings(configured);
            var connected = await _fleet.AddAndConnectAsync(
                configured.Configuration.PrinterId,
                service,
                settings,
                cancellationToken);

            if (connected)
            {
                if (_failureCounts.GetValueOrDefault(
                        configured.Configuration.PrinterId) > 0)
                {
                    _logger.LogInformation(
                        "Reconnected printer {PrinterId}",
                        configured.Configuration.PrinterId);
                }

                _failureCounts[configured.Configuration.PrinterId] = 0;
                continue;
            }

            var failures = _failureCounts.GetValueOrDefault(
                configured.Configuration.PrinterId) + 1;
            _failureCounts[configured.Configuration.PrinterId] = failures;

            if (failures == 1)
            {
                _logger.LogWarning(
                    "Could not connect printer {PrinterId}; retrying every {RetryInterval}",
                    configured.Configuration.PrinterId,
                    _retryInterval);
            }
            else
            {
                _logger.LogDebug(
                    "Printer {PrinterId} connection attempt {Attempt} failed",
                    configured.Configuration.PrinterId,
                    failures);
            }
        }
    }

    private IReadOnlyList<ConfiguredPrinter> ParseConfiguration(
        IConfiguration configuration)
    {
        var result = new List<ConfiguredPrinter>();
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        var entries = configuration.GetSection("EdgeAgent:Printers")
            .Get<List<PrinterConfig>>() ?? [];

        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.PrinterId))
            {
                _logger.LogWarning("Skipping printer configuration without PrinterId");
                continue;
            }

            if (!seenIds.Add(entry.PrinterId))
            {
                _logger.LogWarning(
                    "Skipping duplicate printer configuration {PrinterId}",
                    entry.PrinterId);
                continue;
            }

            if (!Enum.TryParse<PrinterConnectionType>(
                    entry.Protocol,
                    ignoreCase: true,
                    out var connectionType))
            {
                _logger.LogWarning(
                    "Skipping printer {PrinterId}: unknown protocol {Protocol}",
                    entry.PrinterId,
                    entry.Protocol);
                continue;
            }

            if (connectionType is
                PrinterConnectionType.Serial or
                PrinterConnectionType.BambuLab)
            {
                _logger.LogWarning(
                    "Skipping printer {PrinterId}: protocol {Protocol} is not supported by EdgeAgent",
                    entry.PrinterId,
                    connectionType);
                continue;
            }

            result.Add(new ConfiguredPrinter(entry, connectionType));
        }

        return result;
    }

    private static IPrinterCommunicationService CreateService(
        PrinterConnectionType connectionType) =>
        connectionType switch
        {
            PrinterConnectionType.Demo => new DemoPrinterService(),
            PrinterConnectionType.Moonraker => new MoonrakerApiService(),
            PrinterConnectionType.PrusaLink => new PrusaLinkApiService(),
            PrinterConnectionType.PrusaConnect => new PrusaConnectPrinterService(),
            PrinterConnectionType.OctoPrint => new OctoPrintApiService(),
            _ => throw new NotSupportedException(
                $"Printer protocol {connectionType} is not supported by EdgeAgent."),
        };

    private static PrinterConnectionSettings CreateSettings(
        ConfiguredPrinter configured) =>
        new()
        {
            ConnectionType = configured.ConnectionType,
            ApiUrl = configured.Configuration.ApiUrl,
            UserName = configured.Configuration.UserName,
            Password = configured.Configuration.Password,
            ProviderId = configured.Configuration.ProviderId,
        };

    private sealed record ConfiguredPrinter(
        PrinterConfig Configuration,
        PrinterConnectionType ConnectionType);
}
