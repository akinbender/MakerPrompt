using System.Net.Http.Headers;
using MakerPrompt.Infrastructure.Utils;
using Microsoft.Extensions.Logging.Abstractions;

namespace MakerPrompt.Tests.Unit.Infrastructure;

public sealed class MjpegCameraProviderTests
{
    [Fact]
    public async Task CaptureSnapshot_ExtractsMarkersSplitAcrossReads()
    {
        var stream = new ChunkedReadStream(
        [
            [0x2D, 0x2D, 0x62, 0x0D, 0x0A, 0xFF],
            [0xD8, 0x01, 0x02, 0xFF],
            [0xD9, 0x0D, 0x0A],
        ]);
        var handler = new DelegateHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(stream),
            };
            response.Content.Headers.ContentType =
                MediaTypeHeaderValue.Parse("multipart/x-mixed-replace; boundary=frame");
            return response;
        });
        using var client = new HttpClient(handler);
        var provider = new MjpegCameraProvider(
            "camera-1",
            "Camera",
            "http://camera.test/stream",
            NullLogger<MjpegCameraProvider>.Instance,
            client);

        var frame = await provider.CaptureSnapshotAsync();

        Assert.Equal(
            new byte[] { 0xFF, 0xD8, 0x01, 0x02, 0xFF, 0xD9 },
            frame);
        Assert.True(provider.IsAvailable);
    }

    [Fact]
    public async Task CheckAvailability_HeadNotAllowed_FallsBackToGet()
    {
        var methods = new List<HttpMethod>();
        var handler = new DelegateHandler(request =>
        {
            methods.Add(request.Method);
            return new HttpResponseMessage(
                request.Method == HttpMethod.Head
                    ? HttpStatusCode.MethodNotAllowed
                    : HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([]),
            };
        });
        using var client = new HttpClient(handler);
        var provider = new MjpegCameraProvider(
            "camera-1",
            "Camera",
            "http://camera.test/stream",
            NullLogger<MjpegCameraProvider>.Instance,
            client);

        var available = await provider.CheckAvailabilityAsync();

        Assert.True(available);
        Assert.Equal([HttpMethod.Head, HttpMethod.Get], methods);
    }

    [Fact]
    public async Task CaptureSnapshot_RetriesAfterPreviousOutage()
    {
        var attempt = 0;
        var handler = new DelegateHandler(_ =>
        {
            attempt++;
            if (attempt == 1)
            {
                throw new HttpRequestException("offline");
            }

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([0xFF, 0xD8, 0x00, 0xFF, 0xD9]),
            };
            response.Content.Headers.ContentType =
                new MediaTypeHeaderValue("image/jpeg");
            return response;
        });
        using var client = new HttpClient(handler);
        var provider = new MjpegCameraProvider(
            "camera-1",
            "Camera",
            "http://camera.test/snapshot",
            NullLogger<MjpegCameraProvider>.Instance,
            client);

        Assert.Empty(await provider.CaptureSnapshotAsync());
        var recovered = await provider.CaptureSnapshotAsync();

        Assert.NotEmpty(recovered);
        Assert.True(provider.IsAvailable);
    }

    private sealed class DelegateHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(responseFactory(request));
    }

    private sealed class ChunkedReadStream(IReadOnlyList<byte[]> chunks) : Stream
    {
        private int _chunkIndex;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            if (_chunkIndex >= chunks.Count)
            {
                return ValueTask.FromResult(0);
            }

            var chunk = chunks[_chunkIndex++];
            chunk.AsSpan().CopyTo(buffer.Span);
            return ValueTask.FromResult(chunk.Length);
        }

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();
    }
}
