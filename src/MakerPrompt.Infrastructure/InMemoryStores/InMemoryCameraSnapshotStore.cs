namespace MakerPrompt.Infrastructure.InMemoryStores;

/// <summary>
/// In-memory implementation of <see cref="ICameraSnapshotStore"/>.
/// Retains the last <c>N</c> snapshots per camera in a bounded ring buffer.
/// Suitable for tests and EdgeAgent scenarios where the process is long-running.
/// </summary>
/// <param name="maxPerCamera">Maximum snapshots retained per camera (default 50).</param>
public sealed class InMemoryCameraSnapshotStore(int maxPerCamera = 50) : ICameraSnapshotStore
{
    private readonly int _maxPerCamera = maxPerCamera > 0
        ? maxPerCamera
        : throw new ArgumentOutOfRangeException(nameof(maxPerCamera));
    private readonly Dictionary<string, LinkedList<CameraSnapshot>> _data = [];
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task SaveAsync(CameraSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshot.CameraId);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!_data.TryGetValue(snapshot.CameraId, out var list))
            {
                list = new LinkedList<CameraSnapshot>();
                _data[snapshot.CameraId] = list;
            }

            list.AddFirst(Clone(snapshot, includeJpeg: true));
            while (list.Count > _maxPerCamera)
                list.RemoveLast();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<CameraSnapshot?> GetLatestAsync(string cameraId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cameraId);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            return _data.TryGetValue(cameraId, out var list) && list.First is not null
                ? Clone(list.First.Value, includeJpeg: true)
                : null;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<CameraSnapshot>> GetHistoryAsync(
        string cameraId, int count = 20, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cameraId);
        count = Math.Clamp(count, 1, 1_000);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!_data.TryGetValue(cameraId, out var list))
                return [];

            // Return metadata only (no JPEG data) to reduce memory pressure.
            return list.Take(count)
                .Select(snapshot => Clone(snapshot, includeJpeg: false))
                .ToList()
                .AsReadOnly();
        }
        finally
        {
            _lock.Release();
        }
    }

    private static CameraSnapshot Clone(CameraSnapshot source, bool includeJpeg) =>
        new()
        {
            CameraId = source.CameraId,
            Label = source.Label,
            CapturedAt = source.CapturedAt,
            Width = source.Width,
            Height = source.Height,
            JpegData = includeJpeg ? source.JpegData.ToArray() : [],
        };
}
