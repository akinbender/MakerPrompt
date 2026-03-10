using MakerPrompt.Shared.Models;
using MakerPrompt.Shared.Services;
using static MakerPrompt.Shared.Utils.Enums;

namespace MakerPrompt.Tests;

public class GCodeCommandTests
{
    // ── GCodeCommand.ToString ──

    [Fact]
    public void ToString_NoParameters_ReturnsCommandOnly()
    {
        var cmd = new GCodeCommand("G90", "Absolute positioning", [GCodeCategory.Movement]);
        Assert.Equal("G90", cmd.ToString());
    }

    [Fact]
    public void ToString_WithSetParameter_IncludesLabelAndValue()
    {
        var cmd = new GCodeCommand("M104", "Set hotend temp", [GCodeCategory.Temperature],
            [new GCodeParameter('S', "Target temp")]);
        cmd.Parameters[0].Value = "200";
        Assert.Equal("M104 S200", cmd.ToString());
    }

    [Fact]
    public void ToString_ResetsParametersAfterCall()
    {
        var cmd = new GCodeCommand("M104", "Set hotend temp", [GCodeCategory.Temperature],
            [new GCodeParameter('S', "Target temp")]);
        cmd.Parameters[0].Value = "200";
        _ = cmd.ToString();  // first call — parameters reset here
        // After reset, all parameter values are empty: ToString produces "M104 " (trailing space from empty join)
        var result = cmd.ToString().Trim();
        Assert.Equal("M104", result);
    }

    [Fact]
    public void ToString_SkipsParametersWithEmptyValue()
    {
        var cmd = new GCodeCommand("G28", "Home", [GCodeCategory.Movement],
            [new GCodeParameter('X', "X"), new GCodeParameter('Y', "Y"), new GCodeParameter('Z', "Z")]);
        cmd.Parameters[0].Value = " ";  // only X
        var result = cmd.ToString();
        Assert.Contains("X ", result);
        Assert.DoesNotContain("Y", result);
        Assert.DoesNotContain("Z", result);
    }

    [Fact]
    public void ToString_MultipleParameters_JoinedCorrectly()
    {
        var cmd = new GCodeCommand("G1", "Linear move", [GCodeCategory.Movement],
            [new GCodeParameter('X', "X"), new GCodeParameter('Y', "Y"), new GCodeParameter('F', "Feed")]);
        cmd.Parameters[0].Value = "10.0";
        cmd.Parameters[1].Value = "20.0";
        cmd.Parameters[2].Value = "1500";
        var result = cmd.ToString();
        Assert.Equal("G1 X10.0 Y20.0 F1500", result);
    }

    // ── GCodeDoc.EnumerateCommandsAsync ──

    [Fact]
    public async Task GCodeDoc_EnumerateCommandsAsync_SkipsCommentsAndEmptyLines()
    {
        var doc = new GCodeDoc("; start\nG28\n\n; home axes\nM104 S200\n");
        var cmds = new List<string>();
        await foreach (var c in doc.EnumerateCommandsAsync())
            cmds.Add(c);
        Assert.Equal(["G28", "M104 S200"], cmds);
    }

    [Fact]
    public async Task GCodeDoc_EnumerateCommandsAsync_EmptyContent_YieldsNothing()
    {
        var doc = new GCodeDoc(string.Empty);
        var cmds = new List<string>();
        await foreach (var c in doc.EnumerateCommandsAsync())
            cmds.Add(c);
        Assert.Empty(cmds);
    }

    [Fact]
    public async Task GCodeDoc_EnumerateCommandsAsync_OnlyComments_YieldsNothing()
    {
        var doc = new GCodeDoc("; just a comment\n; another comment\n");
        var cmds = new List<string>();
        await foreach (var c in doc.EnumerateCommandsAsync())
            cmds.Add(c);
        Assert.Empty(cmds);
    }

    [Fact]
    public async Task GCodeDoc_EnumerateCommandsAsync_TrimsWhitespace()
    {
        var doc = new GCodeDoc("  G28  \n  M104 S200  \n");
        var cmds = new List<string>();
        await foreach (var c in doc.EnumerateCommandsAsync())
            cmds.Add(c);
        Assert.Equal(["G28", "M104 S200"], cmds);
    }

    // ── GCodeDocumentService ──

    [Fact]
    public async Task GCodeDocumentService_Document_ReflectsCurrentGCode()
    {
        var svc = new GCodeDocumentService();
        svc.SetGCode("G28\nM104 S200");
        var cmds = new List<string>();
        await foreach (var c in svc.Document.EnumerateCommandsAsync())
            cmds.Add(c);
        Assert.Equal(2, cmds.Count);
    }

    [Fact]
    public void GCodeDocumentService_SetGCode_FiresChangedEvent()
    {
        var svc = new GCodeDocumentService();
        bool changed = false;
        svc.Changed += () => changed = true;
        svc.SetGCode("G28");
        Assert.True(changed);
    }

    [Fact]
    public async Task GCodeDocumentService_Clear_ResetsGCode()
    {
        var svc = new GCodeDocumentService();
        svc.SetGCode("G28");
        svc.Clear();
        var cmds = new List<string>();
        await foreach (var c in svc.Document.EnumerateCommandsAsync())
            cmds.Add(c);
        Assert.Empty(cmds);
    }

    [Fact]
    public void GCodeDocumentService_Clear_FiresChangedEvent()
    {
        var svc = new GCodeDocumentService();
        svc.SetGCode("G28");
        bool changed = false;
        svc.Changed += () => changed = true;
        svc.Clear();
        Assert.True(changed);
    }
}
