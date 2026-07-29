namespace MakerPrompt.Infrastructure.InMemoryStores;

/// <summary>
/// In-memory implementation of <see cref="ITelemetryStore"/>.
/// Retains the last <c>N</c> snapshots per printer in a bounded ring buffer.
/// Suitable for development; does not survive process restart.
/// </summary>
/// <param name="maxPerPrinter">Maximum snapshots retained per printer ID (default 500).</param>
public sealed class InMemoryTelemetryStore(int maxPerPrinter = 500) : ITelemetryStore
{
    private readonly int _maxPerPrinter = maxPerPrinter > 0
        ? maxPerPrinter
        : throw new ArgumentOutOfRangeException(nameof(maxPerPrinter));
    private readonly Dictionary<string, LinkedList<PrinterTelemetry>> _data = [];
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task SaveAsync(string printerId, PrinterTelemetry telemetry,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(printerId);
        ArgumentNullException.ThrowIfNull(telemetry);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!_data.TryGetValue(printerId, out var list))
            {
                list = new LinkedList<PrinterTelemetry>();
                _data[printerId] = list;
            }

            list.AddFirst(Clone(telemetry));

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
        ArgumentException.ThrowIfNullOrWhiteSpace(printerId);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            return _data.TryGetValue(printerId, out var list) && list.First is not null
                ? Clone(list.First.Value)
                : null;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<PrinterTelemetry>> GetHistoryAsync(string printerId, int count,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(printerId);
        count = Math.Clamp(count, 1, 10_000);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!_data.TryGetValue(printerId, out var list))
                return [];

            return list.Take(count).Select(Clone).ToList().AsReadOnly();
        }
        finally
        {
            _lock.Release();
        }
    }

    private static PrinterTelemetry Clone(PrinterTelemetry source)
    {
        var clone = new PrinterTelemetry
        {
            LastResponse = source.LastResponse,
            ConnectionTime = source.ConnectionTime,
            Position = source.Position,
            PrinterName = source.PrinterName,
            HotendTemp = source.HotendTemp,
            HotendTarget = source.HotendTarget,
            BedTemp = source.BedTemp,
            BedTarget = source.BedTarget,
            ChamberTemp = source.ChamberTemp,
            ChamberTarget = source.ChamberTarget,
            Status = source.Status,
            FeedRate = source.FeedRate,
            FlowRate = source.FlowRate,
            FanSpeed = source.FanSpeed,
            PrintJobName = source.PrintJobName,
            PrintDuration = source.PrintDuration,
            FilamentUsed = source.FilamentUsed,
            PrintProgress = source.PrintProgress,
            CapturedAt = source.CapturedAt,
        };
        clone.SDCard.Present = source.SDCard.Present;
        clone.SDCard.Printing = source.SDCard.Printing;
        clone.SDCard.Progress = source.SDCard.Progress;
        return clone;
    }
}
