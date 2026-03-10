using Microsoft.Playwright;
using MakerPrompt.E2E.Maui.Fixtures;

namespace MakerPrompt.E2E.Maui.Tests;

/// <summary>
/// Fleet workflow tests for the MAUI app via Playwright + CDP.
/// Covers: add printer → connect demo → telemetry → disconnect.
/// Mirrors the WASM FleetWorkflowTests but runs inside the MAUI WebView2.
///
/// Each test run uses a unique name suffix so printers from previous runs
/// don't cause strict-mode violations. MAUI stores printer data on the
/// filesystem (not browser localStorage), and the PrinterConnectionManager
/// singleton keeps state in memory — JS cleanup can't clear either. Unique
/// names sidestep this entirely.
/// </summary>
[Collection("Appium")]
[Trait("Category", "E2E-Maui")]
[TestCaseOrderer("MakerPrompt.E2E.Maui.Fixtures.AlphabeticalOrderer", "MakerPrompt.E2E.Maui")]
public class FleetWorkflowTests
{
    private static IPage Page => AppiumSetup.Page;

    // Unique suffix per test run so locators never match old printers
    private static readonly string S = DateTime.UtcNow.Ticks.ToString()[^6..];

    [Fact]
    public async Task Fleet_AddPrinter_Demo_Mode()
    {
        await NavigateToFleetAsync();

        var name = $"Add {S}";
        await Page.Locator("[data-testid='fleet-add-btn']").ClickAsync();

        var nameInput = Page.Locator("#printerName");
        await nameInput.WaitForAsync(new LocatorWaitForOptions { Timeout = 5_000 });
        await nameInput.FillAsync(name);

        // Demo is the default connection type — click Save
        await Page.Locator("[data-testid='fleet-save-printer-btn']").ClickAsync();

        var card = Page.Locator($".card strong:has-text('{name}')").First;
        await card.WaitForAsync(new LocatorWaitForOptions { Timeout = 5_000 });
        Assert.True(await card.IsVisibleAsync());
    }

    [Fact]
    public async Task Fleet_ConnectDemoPrinter()
    {
        await NavigateToFleetAsync();
        var name = $"Conn {S}";
        await AddDemoPrinterAsync(name);
        await SelectAndConnectAsync(name);

        var badge = Page.Locator(".badge.bg-success");
        await badge.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
        Assert.True(await badge.IsVisibleAsync());
    }

    [Fact]
    public async Task Fleet_TelemetryUpdates()
    {
        await NavigateToFleetAsync();
        var name = $"Tele {S}";
        await AddDemoPrinterAsync(name);
        await SelectAndConnectAsync(name);

        var heatingCard = Page.Locator(".card-header", new PageLocatorOptions
        {
            HasTextRegex = new System.Text.RegularExpressions.Regex(
                "Heating|Temperature",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase)
        });
        await heatingCard.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
        Assert.True(await heatingCard.IsVisibleAsync());

        var tempValue = Page.Locator(".input-group-text:has-text('C:')");
        await tempValue.First.WaitForAsync(new LocatorWaitForOptions { Timeout = 5_000 });
        Assert.True(await tempValue.First.IsVisibleAsync());
    }

    [Fact]
    public async Task Fleet_DisconnectPrinter()
    {
        await NavigateToFleetAsync();
        var name = $"Disc {S}";
        await AddDemoPrinterAsync(name);
        await SelectAndConnectAsync(name);

        var disconnectBtn = Page.Locator("button.btn-outline-danger:has(.bi-x-circle)");
        await disconnectBtn.WaitForAsync(new LocatorWaitForOptions { Timeout = 5_000 });
        await disconnectBtn.ClickAsync();

        var disconnectedIcon = Page.Locator($".card:has-text('{name}') .bi-plug.text-muted").First;
        await disconnectedIcon.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
        Assert.True(await disconnectedIcon.IsVisibleAsync());
    }

    // ── Helpers ──

    /// <summary>
    /// Navigates to the Fleet page with _selectedPrinter reset.
    /// Goes to /settings first to unmount Fleet, then back to / to remount fresh.
    /// This ensures the card grid (not ControlPanel) is shown even if a previous
    /// test left a printer connected.
    /// </summary>
    private static async Task NavigateToFleetAsync()
    {
        await AppiumSetup.NavigateAsync("/settings");
        await Page.WaitForTimeoutAsync(300);
        await AppiumSetup.NavigateAsync("/fleet");
        await Page.Locator("[data-testid='fleet-add-btn']").WaitForAsync(
            new LocatorWaitForOptions { Timeout = 30_000 });
    }

    private static async Task AddDemoPrinterAsync(string name)
    {
        await Page.Locator("[data-testid='fleet-add-btn']").ClickAsync();
        var nameInput = Page.Locator("#printerName");
        await nameInput.WaitForAsync(new LocatorWaitForOptions { Timeout = 5_000 });
        await nameInput.FillAsync(name);
        await Page.Locator("[data-testid='fleet-save-printer-btn']").ClickAsync();
        await Page.Locator($".card strong:has-text('{name}')").First.WaitForAsync(
            new LocatorWaitForOptions { Timeout = 5_000 });
    }

    private static async Task SelectAndConnectAsync(string name)
    {
        await Page.Locator($".card:has-text('{name}')").First.ClickAsync();

        var connectBtn = Page.Locator($".card:has-text('{name}') button.btn-outline-success").First;
        await connectBtn.WaitForAsync(new LocatorWaitForOptions { Timeout = 5_000 });
        await connectBtn.ClickAsync();

        await Page.Locator(".badge.bg-success").WaitForAsync(
            new LocatorWaitForOptions { Timeout = 10_000 });
    }
}
