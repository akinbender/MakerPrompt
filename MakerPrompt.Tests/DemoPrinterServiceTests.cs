using MakerPrompt.Shared.Models;
using MakerPrompt.Shared.Services;
using static MakerPrompt.Shared.Utils.Enums;

namespace MakerPrompt.Tests;

public class DemoPrinterServiceTests
{
    private static PrinterConnectionSettings DemoSettings() => new();

    // ── Connect / Disconnect ──

    [Fact]
    public async Task ConnectAsync_SetsIsConnectedAndPopulatesTelemetry()
    {
        await using var svc = new DemoPrinterService();
        var connected = await svc.ConnectAsync(DemoSettings());
        svc.updateTimer.Stop();

        Assert.True(connected);
        Assert.True(svc.IsConnected);
        Assert.Equal("Demo 3D Printer", svc.LastTelemetry.PrinterName);
        Assert.Equal(PrinterStatus.Connected, svc.LastTelemetry.Status);
    }

    [Fact]
    public async Task ConnectAsync_FiresConnectionChangedEvent()
    {
        await using var svc = new DemoPrinterService();
        bool eventFired = false;
        svc.ConnectionStateChanged += (_, _) => eventFired = true;

        await svc.ConnectAsync(DemoSettings());
        svc.updateTimer.Stop();

        Assert.True(eventFired);
    }

    [Fact]
    public async Task DisconnectAsync_ClearsIsConnectedAndSetsStatus()
    {
        await using var svc = new DemoPrinterService();
        await svc.ConnectAsync(DemoSettings());
        svc.updateTimer.Stop();

        await svc.DisconnectAsync();

        Assert.False(svc.IsConnected);
        Assert.Equal(PrinterStatus.Disconnected, svc.LastTelemetry.Status);
    }

    // ── Temperature control ──

    [Fact]
    public async Task SetHotendTemp_ValidValue_UpdatesTelemetry()
    {
        await using var svc = new DemoPrinterService();
        await svc.SetHotendTemp(200);
        Assert.Equal(200.0, svc.LastTelemetry.HotendTarget);
    }

    [Fact]
    public async Task SetHotendTemp_TooHigh_ClampsTo300()
    {
        await using var svc = new DemoPrinterService();
        await svc.SetHotendTemp(500);
        Assert.Equal(300.0, svc.LastTelemetry.HotendTarget);
    }

    [Fact]
    public async Task SetHotendTemp_Negative_ClampsToZero()
    {
        await using var svc = new DemoPrinterService();
        await svc.SetHotendTemp(-50);
        Assert.Equal(0.0, svc.LastTelemetry.HotendTarget);
    }

    [Fact]
    public async Task SetBedTemp_ValidValue_UpdatesTelemetry()
    {
        await using var svc = new DemoPrinterService();
        await svc.SetBedTemp(60);
        Assert.Equal(60.0, svc.LastTelemetry.BedTarget);
    }

    [Fact]
    public async Task SetBedTemp_TooHigh_ClampsTo120()
    {
        await using var svc = new DemoPrinterService();
        await svc.SetBedTemp(200);
        Assert.Equal(120.0, svc.LastTelemetry.BedTarget);
    }

    // ── Motion ──

    [Fact]
    public async Task Home_AllAxes_ResetsPositionToZero()
    {
        await using var svc = new DemoPrinterService();
        // Move first so position is non-zero
        await svc.RelativeMove(3000, 10f, 20f, 5f);
        await svc.Home(true, true, true);
        Assert.Equal(0f, svc.LastTelemetry.Position.X, 2);
        Assert.Equal(0f, svc.LastTelemetry.Position.Y, 2);
        Assert.Equal(0f, svc.LastTelemetry.Position.Z, 2);
    }

    [Fact]
    public async Task Home_OnlyX_ResetsOnlyXAxis()
    {
        await using var svc = new DemoPrinterService();
        await svc.RelativeMove(3000, 10f, 20f, 5f);
        await svc.Home(x: true, y: false, z: false);
        Assert.Equal(0f, svc.LastTelemetry.Position.X, 2);
        Assert.NotEqual(0f, svc.LastTelemetry.Position.Y);  // Y unchanged
    }

    [Fact]
    public async Task RelativeMove_UpdatesPosition()
    {
        await using var svc = new DemoPrinterService();
        await svc.RelativeMove(3000, x: 5f, y: 10f, z: 2f);
        Assert.Equal(5f, svc.LastTelemetry.Position.X, 2);
        Assert.Equal(10f, svc.LastTelemetry.Position.Y, 2);
        Assert.Equal(2f, svc.LastTelemetry.Position.Z, 2);
    }

    [Fact]
    public async Task RelativeMove_MultipleMoves_AccumulatesPosition()
    {
        await using var svc = new DemoPrinterService();
        await svc.RelativeMove(3000, x: 5f);
        await svc.RelativeMove(3000, x: 3f);
        Assert.Equal(8f, svc.LastTelemetry.Position.X, 2);
    }

    // ── Fan & speed ──

    [Fact]
    public async Task SetFanSpeed_ValidValue_ClampsAndUpdatesTelemetry()
    {
        await using var svc = new DemoPrinterService();
        await svc.SetFanSpeed(75);
        Assert.Equal(75, svc.LastTelemetry.FanSpeed);
    }

    [Fact]
    public async Task SetFanSpeed_TooHigh_ClampsTo100()
    {
        await using var svc = new DemoPrinterService();
        await svc.SetFanSpeed(200);
        Assert.Equal(100, svc.LastTelemetry.FanSpeed);
    }

    [Fact]
    public async Task SetPrintSpeed_ValidValue_UpdatesFeedRate()
    {
        await using var svc = new DemoPrinterService();
        await svc.SetPrintSpeed(150);
        Assert.Equal(150, svc.LastTelemetry.FeedRate);
    }

    [Fact]
    public async Task SetPrintFlow_ValidValue_UpdatesFlowRate()
    {
        await using var svc = new DemoPrinterService();
        await svc.SetPrintFlow(90);
        Assert.Equal(90, svc.LastTelemetry.FlowRate);
    }

    // ── File operations ──

    [Fact]
    public async Task GetFilesAsync_ReturnsTwoDemoFiles()
    {
        await using var svc = new DemoPrinterService();
        var files = await svc.GetFilesAsync();
        Assert.Equal(2, files.Count);
        Assert.Contains(files, f => f.FullPath.Contains("DemoCube"));
        Assert.Contains(files, f => f.FullPath.Contains("Benchy"));
    }

    [Fact]
    public async Task SaveFileAndOpenReadAsync_RoundTrip()
    {
        await using var svc = new DemoPrinterService();
        var content = System.Text.Encoding.UTF8.GetBytes("G28\nM104 S200");
        await svc.SaveFileAsync("/test/file.gcode", new MemoryStream(content));

        var stream = await svc.OpenReadAsync("/test/file.gcode");
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream);
        var text = await reader.ReadToEndAsync();
        Assert.Equal("G28\nM104 S200", text);
    }

    [Fact]
    public async Task DeleteFileAsync_RemovesFile()
    {
        await using var svc = new DemoPrinterService();
        var content = System.Text.Encoding.UTF8.GetBytes("G28");
        await svc.SaveFileAsync("/test/delete-me.gcode", new MemoryStream(content));

        await svc.DeleteFileAsync("/test/delete-me.gcode");

        var stream = await svc.OpenReadAsync("/test/delete-me.gcode");
        Assert.Null(stream);
    }

    [Fact]
    public async Task WriteDataAsync_SetsLastResponse()
    {
        await using var svc = new DemoPrinterService();
        await svc.WriteDataAsync("M105");
        Assert.Contains("M105", svc.LastTelemetry.LastResponse);
    }
}
