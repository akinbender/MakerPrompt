using MakerPrompt.Shared.Infrastructure;
using MakerPrompt.Shared.Models;

namespace MakerPrompt.Tests;

public class BaseSerialServiceTests
{
    /// <summary>Minimal concrete implementation exposing the abstract base for testing.</summary>
    private sealed class StubSerialService : BaseSerialService
    {
        public List<string> WrittenCommands { get; } = [];

        public override Task WriteDataAsync(string command)
        {
            WrittenCommands.Add(command);
            return Task.CompletedTask;
        }

        public override ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static StubSerialService Create() => new();

    // ── ParseResponse ──

    [Fact]
    public void ParseResponse_TempLine_ParsesAllFourFields()
    {
        var svc = Create();
        var telemetry = svc.ParseResponse("ok T:215 / 215 B:60 / 60");
        Assert.Equal(215.0, telemetry.HotendTemp, 1);
        Assert.Equal(215.0, telemetry.HotendTarget, 1);
        Assert.Equal(60.0, telemetry.BedTemp, 1);
        Assert.Equal(60.0, telemetry.BedTarget, 1);
    }

    [Fact]
    public void ParseResponse_TempLine_DifferentValues_ParsedCorrectly()
    {
        var svc = Create();
        var telemetry = svc.ParseResponse("ok T:200 / 210 B:55 / 60");
        Assert.Equal(200.0, telemetry.HotendTemp, 1);
        Assert.Equal(210.0, telemetry.HotendTarget, 1);
        Assert.Equal(55.0, telemetry.BedTemp, 1);
        Assert.Equal(60.0, telemetry.BedTarget, 1);
    }

    [Fact]
    public void ParseResponse_PositionLine_ParsesXYZ()
    {
        var svc = Create();
        var telemetry = svc.ParseResponse("X:10 Y:20 Z:5 E:0 Count X:10 Y:20 Z:5");
        Assert.Equal(10f, telemetry.Position.X, 2);
        Assert.Equal(20f, telemetry.Position.Y, 2);
        Assert.Equal(5f, telemetry.Position.Z, 2);
    }

    [Fact]
    public void ParseResponse_SdPrintingByteMessage_SetsSdCardPrinting()
    {
        var svc = Create();
        svc.ParseResponse("SD printing byte 1234/5678");
        Assert.True(svc.LastTelemetry.SDCard.Printing);
    }

    [Fact]
    public void ParseResponse_AnyLine_SetsLastResponse()
    {
        var svc = Create();
        svc.ParseResponse("ok");
        Assert.Equal("ok", svc.LastTelemetry.LastResponse);
    }

    // ── ProcessReceivedData ──

    [Fact]
    public void ProcessReceivedData_SingleCompleteLine_ParsesTelemetry()
    {
        var svc = Create();
        svc.ProcessReceivedData("ok T:200 / 200 B:50 / 50\n");
        Assert.Equal(200.0, svc.LastTelemetry.HotendTemp, 1);
    }

    [Fact]
    public void ProcessReceivedData_ChunkedInput_BuffersUntilNewline()
    {
        var svc = Create();
        svc.ProcessReceivedData("ok T:210 / 210 ");
        // No newline yet — buffer not flushed
        Assert.Equal(0.0, svc.LastTelemetry.HotendTemp, 1);

        svc.ProcessReceivedData("B:55 / 55\n");
        Assert.Equal(210.0, svc.LastTelemetry.HotendTemp, 1);
    }

    [Fact]
    public void ProcessReceivedData_MultipleLines_ParsesAll_LastWins()
    {
        var svc = Create();
        svc.ProcessReceivedData("ok T:200 / 200 B:50 / 50\nok T:220 / 220 B:60 / 60\n");
        Assert.Equal(220.0, svc.LastTelemetry.HotendTemp, 1);
    }

    [Fact]
    public void ProcessReceivedData_CrLfTerminated_ParsesLine()
    {
        var svc = Create();
        svc.ProcessReceivedData("ok T:205 / 205 B:58 / 58\r\n");
        Assert.Equal(205.0, svc.LastTelemetry.HotendTemp, 1);
    }

    // ── Command methods ──

    [Fact]
    public async Task SetHotendTemp_ValidTemp_WritesCorrectGcode()
    {
        var svc = Create();
        svc.IsConnected = true;
        await svc.SetHotendTemp(200);
        Assert.Contains(svc.WrittenCommands, c => c == "M104 S200");
    }

    [Fact]
    public async Task SetHotendTemp_OutOfRange_WritesNothing()
    {
        var svc = Create();
        svc.IsConnected = true;
        await svc.SetHotendTemp(350);  // max is 300
        Assert.Empty(svc.WrittenCommands);
    }

    [Fact]
    public async Task SetHotendTemp_NotConnected_WritesNothing()
    {
        var svc = Create();
        // IsConnected defaults to false
        await svc.SetHotendTemp(200);
        Assert.Empty(svc.WrittenCommands);
    }

    [Fact]
    public async Task SetBedTemp_ValidTemp_WritesCorrectGcode()
    {
        var svc = Create();
        svc.IsConnected = true;
        await svc.SetBedTemp(60);
        Assert.Contains(svc.WrittenCommands, c => c == "M140 S60");
    }

    [Fact]
    public async Task SetBedTemp_OutOfRange_WritesNothing()
    {
        var svc = Create();
        svc.IsConnected = true;
        await svc.SetBedTemp(150);  // max is 120
        Assert.Empty(svc.WrittenCommands);
    }

    [Fact]
    public async Task SetPrintSpeed_ValidSpeed_WritesGcode()
    {
        var svc = Create();
        svc.IsConnected = true;
        await svc.SetPrintSpeed(150);
        Assert.Contains(svc.WrittenCommands, c => c.StartsWith("M220"));
        Assert.Contains(svc.WrittenCommands, c => c.Contains("S150"));
    }

    [Fact]
    public async Task SetFanSpeed_ZeroPercent_WritesFanOffCommand()
    {
        var svc = Create();
        svc.IsConnected = true;
        await svc.SetFanSpeed(0);
        Assert.Contains(svc.WrittenCommands, c => c == "M107");
    }

    [Fact]
    public async Task SetFanSpeed_FullSpeed_WritesMappedValue()
    {
        var svc = Create();
        svc.IsConnected = true;
        await svc.SetFanSpeed(100);
        // 100% → (int)(100 * 2.55) = 254 due to float truncation
        Assert.Contains(svc.WrittenCommands, c => c.StartsWith("M106") && c.Contains("S254"));
    }
}
