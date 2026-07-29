namespace MakerPrompt.Core.Abstractions;

/// <summary>
/// Persists and retrieves camera snapshots.
/// Implementations: in-memory (tests), SQLite (EdgeAgent), Cloud REST projection.
/// </summary>
public interface ICameraSnapshotStore
{
    /// <summary>
    /// Persists a snapshot. The store associates it with the
    /// <see cref="MakerPrompt.Core.Models.CameraSnapshot.CameraId"/> and the
    /// capture timestamp already embedded in the model.
    /// </summary>
    Task SaveAsync(Core.Models.CameraSnapshot snapshot, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the most recent snapshot for <paramref name="cameraId"/>,
    /// or <c>null</c> if none has been stored.
    /// </summary>
    Task<Core.Models.CameraSnapshot?> GetLatestAsync(string cameraId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns up to <paramref name="count"/> snapshots for <paramref name="cameraId"/>,
    /// ordered from most-recent to oldest.  Only metadata is returned by default;
    /// implementations may omit the <c>JpegData</c> blob to reduce memory pressure.
    /// </summary>
    Task<IReadOnlyList<Core.Models.CameraSnapshot>> GetHistoryAsync(
        string cameraId, int count = 20, CancellationToken cancellationToken = default);
}
