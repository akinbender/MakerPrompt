using MakerPrompt.Core.Abstractions;
using MakerPrompt.Core.Models;

namespace MakerPrompt.Infrastructure;

/// <summary>
/// In-memory implementation of <see cref="ICameraSnapshotStore"/>.
/// Retains the last <c>N</c> snapshots per camera in a bounded ring buffer.
/// Suitable for tests and EdgeAgent scenarios where the process is long-running.
/// </summary>
public sealed class InMemoryCameraSnapshotStore : ICameraSnapshotStore
{
    private readonly int _maxPerCamera;
    private readonly Dictionary<string, LinkedList<CameraSnapshot>> _data = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <param name="maxPerCamera">Maximum snapshots retained per camera (default 50).</param>
    public InMemoryCameraSnapshotStore(int maxPerCamera = 50)
    {
        _maxPerCamera = maxPerCamera;
    }

    public async Task SaveAsync(CameraSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!_data.TryGetValue(snapshot.CameraId, out var list))
            {
                list = new LinkedList<CameraSnapshot>();
                _data[snapshot.CameraId] = list;
            }

            list.AddFirst(snapshot);
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
        await _lock.WaitAsync(cancellationToken);
        try
        {
            return _data.TryGetValue(cameraId, out var list) ? list.First?.Value : null;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<CameraSnapshot>> GetHistoryAsync(
        string cameraId, int count = 20, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!_data.TryGetValue(cameraId, out var list))
                return [];

            // Return metadata only (no JPEG data) to reduce memory pressure.
            return list.Take(count).Select(s => new CameraSnapshot
            {
                CameraId = s.CameraId,
                Label = s.Label,
                CapturedAt = s.CapturedAt,
                Width = s.Width,
                Height = s.Height,
                JpegData = []
            }).ToList().AsReadOnly();
        }
        finally
        {
            _lock.Release();
        }
    }
}
