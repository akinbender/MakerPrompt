using MakerPrompt.Core.Abstractions;
using MakerPrompt.Core.Models;

namespace MakerPrompt.Infrastructure.Telemetry;

/// <summary>
/// In-memory implementation of <see cref="ITelemetryStore"/>.
/// Retains the last <c>N</c> snapshots per printer in a bounded ring buffer.
/// Suitable for tests and development; does not survive process restart.
/// </summary>
public sealed class InMemoryTelemetryStore : ITelemetryStore
{
    private readonly int _maxPerPrinter;
    private readonly Dictionary<string, LinkedList<PrinterTelemetry>> _data = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <param name="maxPerPrinter">Maximum snapshots retained per printer ID (default 500).</param>
    public InMemoryTelemetryStore(int maxPerPrinter = 500)
    {
        _maxPerPrinter = maxPerPrinter;
    }

    public async Task SaveAsync(string printerId, PrinterTelemetry telemetry,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!_data.TryGetValue(printerId, out var list))
            {
                list = new LinkedList<PrinterTelemetry>();
                _data[printerId] = list;
            }

            list.AddFirst(telemetry);

            while (list.Count > _maxPerPrinter)
                list.RemoveLast();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<PrinterTelemetry?> GetLatestAsync(string printerId,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            return _data.TryGetValue(printerId, out var list) ? list.First?.Value : null;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<PrinterTelemetry>> GetHistoryAsync(
        string printerId, int count = 100, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!_data.TryGetValue(printerId, out var list))
                return [];

            return list.Take(count).ToList().AsReadOnly();
        }
        finally
        {
            _lock.Release();
        }
    }
}
