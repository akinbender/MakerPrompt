using MakerPrompt.Shared.Utils;
using static MakerPrompt.Shared.Utils.Enums;

namespace MakerPrompt.Tests;

public class EnumExtensionsTests
{
    [Fact]
    public void GetAllValues_ReturnsAllConnectionTypes()
    {
        var values = EnumExtensions.GetAllValues<PrinterConnectionType>().ToList();
        Assert.Contains(PrinterConnectionType.Demo, values);
        Assert.Contains(PrinterConnectionType.Serial, values);
        Assert.Contains(PrinterConnectionType.Moonraker, values);
        Assert.Contains(PrinterConnectionType.PrusaLink, values);
        Assert.Contains(PrinterConnectionType.PrusaConnect, values);
        Assert.Contains(PrinterConnectionType.BambuLab, values);
        Assert.Contains(PrinterConnectionType.OctoPrint, values);
        Assert.Equal(7, values.Count);
    }

    [Fact]
    public void GetDisplayName_ReturnsAnnotatedName_ForKnownValues()
    {
        Assert.Equal("Demo", PrinterConnectionType.Demo.GetDisplayName());
        Assert.Equal("Serial", PrinterConnectionType.Serial.GetDisplayName());
        Assert.Equal("Moonraker", PrinterConnectionType.Moonraker.GetDisplayName());
        Assert.Equal("PrusaLink", PrinterConnectionType.PrusaLink.GetDisplayName());
        Assert.Equal("PrusaConnect", PrinterConnectionType.PrusaConnect.GetDisplayName());
        Assert.Equal("BambuLab", PrinterConnectionType.BambuLab.GetDisplayName());
        Assert.Equal("OctoPrint", PrinterConnectionType.OctoPrint.GetDisplayName());
    }

    [Fact]
    public void GetDisplayName_MicrosteppingMode_ReturnsFormattedNames()
    {
        Assert.Equal("1/1 (Full step)", MicrosteppingMode.FullStep.GetDisplayName());
        Assert.Equal("1/2 (Half step)", MicrosteppingMode.HalfStep.GetDisplayName());
        Assert.Equal("1/16", MicrosteppingMode.SixteenthStep.GetDisplayName());
    }

    [Fact]
    public void GetStepAngleValue_ReturnsCorrectDecimal()
    {
        Assert.Equal(1.80m, MotorStepAngle.Step1_8.GetStepAngleValue());
        Assert.Equal(0.90m, MotorStepAngle.Step0_9.GetStepAngleValue());
        Assert.Equal(0.75m, MotorStepAngle.Step7_5.GetStepAngleValue());
        Assert.Equal(1.50m, MotorStepAngle.Step15.GetStepAngleValue());
    }

    [Fact]
    public void GetMotorStepAngleOptions_ReturnsFourOptions_WithNonEmptyLabels()
    {
        var opts = EnumExtensions.GetMotorStepAngleOptions();
        Assert.Equal(4, opts.Count);
        Assert.All(opts, o => Assert.NotEmpty(o.Value));
        Assert.Contains(opts, o => o.Key == MotorStepAngle.Step1_8);
        Assert.Contains(opts, o => o.Key == MotorStepAngle.Step0_9);
    }

    [Fact]
    public void GetMicrosteppingOptions_ReturnsEightOptions_WithNonEmptyLabels()
    {
        var opts = EnumExtensions.GetMicrosteppingOptions();
        Assert.Equal(8, opts.Count);
        Assert.All(opts, o => Assert.NotEmpty(o.Value));
        Assert.Contains(opts, o => o.Key == MicrosteppingMode.FullStep);
        Assert.Contains(opts, o => o.Key == MicrosteppingMode.SixtyFourthStep);
    }

    [Fact]
    public void GetAllValues_PrinterStatus_ReturnsAllStatuses()
    {
        var values = EnumExtensions.GetAllValues<PrinterStatus>().ToList();
        Assert.Contains(PrinterStatus.Disconnected, values);
        Assert.Contains(PrinterStatus.Connected, values);
        Assert.Contains(PrinterStatus.Printing, values);
        Assert.Contains(PrinterStatus.Paused, values);
        Assert.Contains(PrinterStatus.Error, values);
    }
}
