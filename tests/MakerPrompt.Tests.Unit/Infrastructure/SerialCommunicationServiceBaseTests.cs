using MakerPrompt.Infrastructure.Services;

namespace MakerPrompt.Tests.Unit.Infrastructure;

public sealed class SerialCommunicationServiceBaseTests
{
    [Fact]
    public async Task Disconnect_AfterTransportFailure_StillClosesTransport()
    {
        var service = new TestSerialService();
        await service.ConnectAsync(Settings());

        service.TriggerTransportFailure();
        await service.DisconnectAsync();

        Assert.False(service.IsConnected);
        Assert.Equal(1, service.CloseCount);
    }

    [Fact]
    public async Task Connect_AfterTransportFailure_ClosesStaleTransportAndReopens()
    {
        var service = new TestSerialService();
        await service.ConnectAsync(Settings());
        service.TriggerTransportFailure();

        var connected = await service.ConnectAsync(Settings());

        Assert.True(connected);
        Assert.Equal(2, service.OpenCount);
        Assert.Equal(1, service.CloseCount);
        await service.DisposeAsync();
    }

    [Fact]
    public async Task Dispose_WhileConnected_ClosesBeforeDisposingTimer()
    {
        var service = new TestSerialService();
        await service.ConnectAsync(Settings());

        await service.DisposeAsync();
        await service.DisposeAsync();

        Assert.False(service.IsConnected);
        Assert.Equal(1, service.CloseCount);
    }

    private static PrinterConnectionSettings Settings() =>
        new()
        {
            ConnectionType = PrinterConnectionType.Serial,
            PortName = "test",
        };

    private sealed class TestSerialService : SerialCommunicationServiceBase
    {
        public int OpenCount { get; private set; }
        public int CloseCount { get; private set; }

        public void TriggerTransportFailure() => ReportTransportFailure();

        protected override Task OpenTransportAsync(
            PrinterConnectionSettings settings,
            CancellationToken cancellationToken)
        {
            OpenCount++;
            return Task.CompletedTask;
        }

        protected override Task CloseTransportAsync(CancellationToken cancellationToken)
        {
            CloseCount++;
            return Task.CompletedTask;
        }

        protected override Task WriteTransportAsync(
            string data,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
