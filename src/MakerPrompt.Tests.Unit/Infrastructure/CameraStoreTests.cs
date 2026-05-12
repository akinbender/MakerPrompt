using MakerPrompt.Core.Models;
using MakerPrompt.Infrastructure.Camera;

namespace MakerPrompt.Tests.Unit.Infrastructure;

/// <summary>
/// Tests for the in-memory camera snapshot store and related types.
/// </summary>
public sealed class CameraStoreTests
{
    [Fact]
    public async Task InMemoryCameraStore_Save_And_GetLatest_RoundTrip()
    {
        var store = new InMemoryCameraSnapshotStore();
        var jpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 };
        var snapshot = new CameraSnapshot
        {
            CameraId = "cam-a",
            Label = "Test Camera",
            JpegData = jpeg,
            Width = 1280,
            Height = 720,
            CapturedAt = DateTimeOffset.UtcNow
        };

        await store.SaveAsync(snapshot);
        var latest = await store.GetLatestAsync("cam-a");

        Assert.NotNull(latest);
        Assert.Equal("Test Camera", latest.Label);
        Assert.Equal(jpeg, latest.JpegData);
        Assert.Equal(1280, latest.Width);
        Assert.Equal(720, latest.Height);
    }

    [Fact]
    public async Task InMemoryCameraStore_GetLatest_ReturnsNull_WhenEmpty()
    {
        var store = new InMemoryCameraSnapshotStore();
        Assert.Null(await store.GetLatestAsync("cam-nonexistent"));
    }

    [Fact]
    public async Task InMemoryCameraStore_GetHistory_ExcludesJpegBlob()
    {
        var store = new InMemoryCameraSnapshotStore();
        for (int i = 0; i < 3; i++)
        {
            await store.SaveAsync(new CameraSnapshot
            {
                CameraId = "cam-b",
                JpegData = new byte[500],
                CapturedAt = DateTimeOffset.UtcNow.AddSeconds(i)
            });
        }

        var history = await store.GetHistoryAsync("cam-b", count: 10);

        Assert.Equal(3, history.Count);
        Assert.All(history, s => Assert.Empty(s.JpegData));
    }

    [Fact]
    public async Task InMemoryCameraStore_RingBuffer_DropsOldestWhenFull()
    {
        const int max = 3;
        var store = new InMemoryCameraSnapshotStore(maxPerCamera: max);

        for (int i = 1; i <= max + 2; i++)
        {
            await store.SaveAsync(new CameraSnapshot
            {
                CameraId = "cam-c",
                Label = $"Snap-{i}",
                JpegData = new byte[] { (byte)i },
                CapturedAt = DateTimeOffset.UtcNow.AddSeconds(i)
            });
        }

        // Latest should be the newest (Snap-5).
        var latest = await store.GetLatestAsync("cam-c");
        Assert.Equal("Snap-5", latest!.Label);

        // History should only contain max entries.
        var history = await store.GetHistoryAsync("cam-c", count: 100);
        Assert.Equal(max, history.Count);
    }

    [Fact]
    public async Task InMemoryCameraStore_IsolatesCameras()
    {
        var store = new InMemoryCameraSnapshotStore();
        await store.SaveAsync(new CameraSnapshot { CameraId = "camX", Label = "X", JpegData = new byte[] { 1 } });
        await store.SaveAsync(new CameraSnapshot { CameraId = "camY", Label = "Y", JpegData = new byte[] { 2 } });

        var latestX = await store.GetLatestAsync("camX");
        var latestY = await store.GetLatestAsync("camY");

        Assert.Equal("X", latestX!.Label);
        Assert.Equal("Y", latestY!.Label);
    }
}
