using MakerPrompt.Application.Services;
using MakerPrompt.Tests.Unit.Helpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace MakerPrompt.Tests.Unit.Services;

/// <summary>
/// Unit tests for <see cref="PrinterFleetService"/>.
/// </summary>
public sealed class PrinterFleetServiceTests : IAsyncDisposable
{
    private readonly PrinterFleetService _sut = new(NullLogger<PrinterFleetService>.Instance);

    // ── AddAndConnect ────────────────────────────────────────────────────────

    [Fact]
    public async Task AddAndConnect_SuccessfulConnect_ReturnsTrueAndPrinterIsTracked()
    {
        var fake = new FakePrinterService { ConnectShouldSucceed = true };

        var result = await _sut.AddAndConnectAsync("p1", fake, new PrinterConnectionSettings());

        Assert.True(result);
        Assert.Contains("p1", _sut.PrinterIds);
    }

    [Fact]
    public async Task AddAndConnect_FailedConnect_ReturnsFalseAndDisposesCandidate()
    {
        var fake = new FakePrinterService { ConnectShouldSucceed = false };

        var result = await _sut.AddAndConnectAsync("p1", fake, new PrinterConnectionSettings());

        Assert.False(result);
        Assert.DoesNotContain("p1", _sut.PrinterIds);
        Assert.True(fake.IsDisposed);
    }

    [Fact]
    public async Task AddAndConnect_ReplacesExistingConnection()
    {
        var first = new FakePrinterService();
        var second = new FakePrinterService();

        await _sut.AddAndConnectAsync("p1", first, new PrinterConnectionSettings());
        await _sut.AddAndConnectAsync("p1", second, new PrinterConnectionSettings());

        // first should have been disconnected when replaced
        Assert.False(first.IsConnected);
        Assert.True(second.IsConnected);
        Assert.True(first.IsDisposed);
    }

    // ── Remove ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Remove_DisconnectsAndUnregisters()
    {
        var fake = new FakePrinterService();
        await _sut.AddAndConnectAsync("p1", fake, new PrinterConnectionSettings());

        await _sut.RemoveAsync("p1");

        Assert.DoesNotContain("p1", _sut.PrinterIds);
        Assert.False(fake.IsConnected);
        Assert.True(fake.IsDisposed);
    }

    [Fact]
    public async Task Remove_UnknownId_DoesNotThrow()
    {
        await _sut.RemoveAsync("nonexistent");
        // No exception expected
    }

    // ── GetConnection ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetConnection_ReturnsServiceForKnownId()
    {
        var fake = new FakePrinterService();
        await _sut.AddAndConnectAsync("p1", fake, new PrinterConnectionSettings());

        var retrieved = _sut.GetConnection("p1");

        Assert.Same(fake, retrieved);
    }

    [Fact]
    public void GetConnection_ReturnsNullForUnknownId()
    {
        Assert.Null(_sut.GetConnection("unknown"));
    }

    // ── GetFleetTelemetry ────────────────────────────────────────────────────

    [Fact]
    public async Task GetFleetTelemetry_IncludesOnlyConnectedPrinters()
    {
        var connected = new FakePrinterService { ConnectShouldSucceed = true };
        var disconnected = new FakePrinterService { ConnectShouldSucceed = false };

        await _sut.AddAndConnectAsync("p-connected", connected, new PrinterConnectionSettings());
        await _sut.AddAndConnectAsync("p-disconnected", disconnected, new PrinterConnectionSettings());

        var telemetry = _sut.GetFleetTelemetry();

        Assert.Contains("p-connected", telemetry.Keys);
        Assert.DoesNotContain("p-disconnected", telemetry.Keys);
    }

    // ── FleetChanged event ───────────────────────────────────────────────────

    [Fact]
    public async Task AddAndConnect_RaisesFleetChangedEvent()
    {
        var fake = new FakePrinterService();
        var raised = false;
        _sut.FleetChanged += (_, _) => raised = true;

        await _sut.AddAndConnectAsync("p1", fake, new PrinterConnectionSettings());

        Assert.True(raised);
    }

    // ── Disposal ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Dispose_DisconnectsAllPrinters()
    {
        var p1 = new FakePrinterService();
        var p2 = new FakePrinterService();
        await _sut.AddAndConnectAsync("p1", p1, new PrinterConnectionSettings());
        await _sut.AddAndConnectAsync("p2", p2, new PrinterConnectionSettings());

        await _sut.DisposeAsync();

        Assert.False(p1.IsConnected);
        Assert.False(p2.IsConnected);
    }

    public async ValueTask DisposeAsync() => await _sut.DisposeAsync();
}
