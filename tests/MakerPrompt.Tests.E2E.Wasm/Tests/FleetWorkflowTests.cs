using Microsoft.Playwright;
using MakerPrompt.Tests.E2E.Wasm.Fixtures;

namespace MakerPrompt.Tests.E2E.Wasm.Tests;

/// <summary>
/// End-to-end tests for the multi-printer fleet workflow using the Demo backend.
/// Covers: add printer → connect → telemetry updates → disconnect.
/// All tests share a single browser tab via the collection fixture.
///
/// IMPORTANT: After connecting a printer and clicking its card, Fleet switches
/// from the card grid to an inline ControlPanel view. Tests must account for
/// this view transition.
///
/// IAsyncLifetime.DisposeAsync removes stored printer connections from localStorage
/// so each test run starts with an empty fleet.
/// </summary>
[Collection("Playwright")]
[Trait("Category", "E2E-Wasm")]
public class FleetWorkflowTests(PlaywrightFixture fixture) : IAsyncLifetime
{
    private readonly PlaywrightFixture _fixture = fixture;
    private IPage Page => _fixture.Page;

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        try
        {
            await Page.EvaluateAsync("() => localStorage.removeItem('MakerPrompt.PrinterConnections')");
        }
        catch { /* best-effort */ }
    }

    [Fact]
    public async Task Fleet_AddPrinter_Demo_Mode()
    {
        await NavigateToFleetAsync();

        // Click "Add Printer"
        await Page.Locator("[data-testid='fleet-add-btn']").ClickAsync();

        // Modal should appear — fill in the name
        var nameInput = Page.Locator("#printerNameInput");
        await nameInput.WaitForAsync(new LocatorWaitForOptions { Timeout = 5_000 });
        await nameInput.FillAsync("E2E Test Printer");

        // Demo is the default connection type — click Save
        await Page.Locator("[data-testid='fleet-save-printer-btn']").ClickAsync();

        // Verify the printer card appears
        var card = Page.Locator(".card strong:has-text('E2E Test Printer')").First;
        await card.WaitForAsync(new LocatorWaitForOptions { Timeout = 5_000 });
        Assert.True(await card.IsVisibleAsync());
    }

    [Fact]
    public async Task Fleet_ConnectMockPrinter()
    {
        await NavigateToFleetAsync();
        await AddDemoPrinterAsync("Connect Test");
        await SelectAndConnectAsync("Connect Test");

        // After connecting, the view switches to inline ControlPanel.
        // Verify the connected status badge appears in the header.
        var badge = Page.Locator(".badge.bg-success");
        await badge.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
        Assert.True(await badge.IsVisibleAsync());
    }

    [Fact]
    public async Task Fleet_TelemetryUpdates()
    {
        await NavigateToFleetAsync();
        await AddDemoPrinterAsync("Telemetry Test");
        await SelectAndConnectAsync("Telemetry Test");

        // ControlPanel renders a Heating card header once the Demo backend is live.
        var heatingHeader = Page.Locator(".card-header span.fw-semibold",
            new PageLocatorOptions { HasText = "Heating" });
        await heatingHeader.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
        Assert.True(await heatingHeader.IsVisibleAsync());

        // Each temperature input group ends with an °C span.
        var tempUnit = Page.Locator(".input-group-text:has-text('°C')");
        await tempUnit.First.WaitForAsync(new LocatorWaitForOptions { Timeout = 5_000 });
        Assert.True(await tempUnit.First.IsVisibleAsync());
    }

    [Fact]
    public async Task Fleet_DisconnectPrinter()
    {
        await NavigateToFleetAsync();
        await AddDemoPrinterAsync("Disconnect Test");
        await SelectAndConnectAsync("Disconnect Test");

        // After connecting, the inline ControlPanel header has a Disconnect button.
        var disconnectBtn = Page.Locator("button.btn-outline-danger:has(.bi-x-circle)");
        await disconnectBtn.WaitForAsync(new LocatorWaitForOptions { Timeout = 5_000 });
        await disconnectBtn.ClickAsync();

        // After disconnect, the view returns to the card grid.
        // The card should show the disconnected plug icon.
        var disconnectedIcon = Page.Locator(".card:has-text('Disconnect Test') .bi-plug.text-muted").First;
        await disconnectedIcon.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
        Assert.True(await disconnectedIcon.IsVisibleAsync());
    }

    // ── Helpers ──

    /// <summary>
    /// Ensures farm mode is on, then navigates to the Fleet page with a clean
    /// slate (clears only stored printer connections). Uses a targeted
    /// localStorage.removeItem so the AppConfig key (which holds FarmModeEnabled)
    /// is not wiped before the reload.
    /// </summary>
    private async Task NavigateToFleetAsync()
    {
        // Ensure farm mode is enabled — fleet UI requires it
        await Page.GotoAsync($"{_fixture.BaseUrl}/settings");
        await Page.Locator("#farmModeEnabled").WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });
        var toggle = Page.Locator("#farmModeEnabled");
        if (!await toggle.IsCheckedAsync())
        {
            await toggle.CheckAsync();
            // OnFarmModeChangedAsync saves config and auto-navigates to /fleet
            await Page.WaitForURLAsync("**/fleet", new PageWaitForURLOptions { Timeout = 10_000 });
        }
        else
        {
            await Page.GotoAsync($"{_fixture.BaseUrl}/fleet");
            await Page.Locator("[data-testid='fleet-add-btn']").WaitForAsync(
                new LocatorWaitForOptions { Timeout = 15_000 });
        }

        // Remove only the printer connections key — do NOT clear() the whole
        // storage or AppConfig (FarmModeEnabled) will be wiped on reload.
        await Page.EvaluateAsync("() => localStorage.removeItem('MakerPrompt.PrinterConnections')");
        await Page.ReloadAsync();
        await Page.Locator("[data-testid='fleet-add-btn']").WaitForAsync(
            new LocatorWaitForOptions { Timeout = 30_000 });
    }

    private async Task AddDemoPrinterAsync(string name)
    {
        await Page.Locator("[data-testid='fleet-add-btn']").ClickAsync();
        var nameInput = Page.Locator("#printerNameInput");
        await nameInput.WaitForAsync(new LocatorWaitForOptions { Timeout = 5_000 });
        await nameInput.FillAsync(name);
        await Page.Locator("[data-testid='fleet-save-printer-btn']").ClickAsync();
        // Wait for the card to appear
        await Page.Locator($".card:has-text('{name}')").First.WaitForAsync(
            new LocatorWaitForOptions { Timeout = 5_000 });
    }

    /// <summary>
    /// Selects a printer card and clicks Connect. After the Demo backend connects,
    /// the view automatically switches to the inline ControlPanel. This helper
    /// waits for that transition to complete.
    /// </summary>
    private async Task SelectAndConnectAsync(string name)
    {
        // Click the card to select it (shows action buttons)
        await Page.Locator($".card:has-text('{name}')").First.ClickAsync();

        // Click the connect button on the card
        var connectBtn = Page.Locator($".card:has-text('{name}') button.btn-outline-success").First;
        await connectBtn.WaitForAsync(new LocatorWaitForOptions { Timeout = 5_000 });
        await connectBtn.ClickAsync();

        // After connecting, Fleet switches from card grid to inline ControlPanel.
        // Wait for the connected status badge in the inline header.
        await Page.Locator(".badge.bg-success").WaitForAsync(
            new LocatorWaitForOptions { Timeout = 10_000 });
    }
}