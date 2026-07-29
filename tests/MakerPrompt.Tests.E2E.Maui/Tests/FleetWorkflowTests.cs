using Microsoft.Playwright;
using MakerPrompt.Test.E2E.Maui.Fixtures;

namespace MakerPrompt.Test.E2E.Maui.Tests;

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
///
/// IAsyncLifetime.DisposeAsync deletes all remaining printer cards via the
/// fleet UI so test runs don't accumulate stale printers.
/// </summary>
[Collection("Appium")]
[Trait("Category", "E2E-Maui")]
[TestCaseOrderer("MakerPrompt.E2E.Maui.Fixtures.AlphabeticalOrderer", "MakerPrompt.E2E.Maui")]
public class FleetWorkflowTests : IAsyncLifetime
{
    private static IPage Page => AppiumSetup.Page;

    // Unique suffix per test run so locators never match old printers
    private static readonly string S = DateTime.UtcNow.Ticks.ToString()[^6..];

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await DeleteAllPrintersAsync();

    [Fact]
    public async Task Fleet_AddPrinter_Demo_Mode()
    {
        await NavigateToFleetAsync();

        var name = $"Add {S}";
        await Page.Locator("[data-testid='fleet-add-btn']").ClickAsync();

        var nameInput = Page.Locator("#printerNameInput");
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

        // After connecting the demo printer the Fleet page switches to the inline
        // ControlPanel view. The Heating card header is the first stable landmark.
        var heatingHeader = Page.Locator(".card-header span.fw-semibold",
            new PageLocatorOptions { HasText = "Heating" });
        await heatingHeader.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
        Assert.True(await heatingHeader.IsVisibleAsync());

        // The temperature input groups each end with an °C span.
        // Use :has-text('°C') which matches the literal degree symbol rendered by Blazor.
        var tempUnit = Page.Locator(".input-group-text:has-text('°C')");
        await tempUnit.First.WaitForAsync(new LocatorWaitForOptions { Timeout = 5_000 });
        Assert.True(await tempUnit.First.IsVisibleAsync());
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
    /// Ensures farm mode is on, then navigates to the Fleet page.
    /// Fleet's add-printer button is only present when farm mode is enabled.
    /// Uses DOM-based waits throughout — WaitForURLAsync is unreliable in
    /// WebView2/CDP because Blazor's NavigationManager uses history.pushState
    /// which does not fire CDP network navigation events.
    /// </summary>
    private static async Task NavigateToFleetAsync()
    {
        // Navigate to settings and wait until the farm mode toggle is rendered
        await AppiumSetup.NavigateAsync("/settings", waitForSelector: "#farmModeEnabled");

        var toggle = Page.Locator("#farmModeEnabled");
        if (!await toggle.IsCheckedAsync())
        {
            await toggle.CheckAsync();
            // OnFarmModeChangedAsync saves config then calls NavigationManager.NavigateTo("/fleet").
            // That is another pushState — wait for the fleet add-btn to appear instead
            // of WaitForURLAsync which won't fire for Blazor client-side navigation.
            await Page.Locator("[data-testid='fleet-add-btn']").WaitForAsync(
                new LocatorWaitForOptions { Timeout = 15_000 });
        }
        else
        {
            // Already enabled — navigate to fleet and wait for the add-btn
            await AppiumSetup.NavigateAsync("/fleet", waitForSelector: "[data-testid='fleet-add-btn']");
        }
    }

    private static async Task AddDemoPrinterAsync(string name)
    {
        await Page.Locator("[data-testid='fleet-add-btn']").ClickAsync();
        var nameInput = Page.Locator("#printerNameInput");
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

    /// <summary>
    /// Deletes every printer card visible on the fleet page.
    /// Called from DisposeAsync so each test class instance leaves the app clean.
    /// Silently exits if the fleet page is not reachable or no printers are present.
    /// </summary>
    private static async Task DeleteAllPrintersAsync()
    {
        try
        {
            // Navigate back to fleet (may already be there after the last test)
            await AppiumSetup.NavigateAsync("/fleet", waitForSelector: ".container-fluid");

            // If we landed on the inline ControlPanel (a printer is selected + connected),
            // go back to the card grid first
            var backBtn = Page.Locator("button.btn-outline-secondary:has(.bi-arrow-left)");
            if (await backBtn.IsVisibleAsync())
                await backBtn.ClickAsync();

            // Repeatedly select the first card and delete it until none remain
            while (true)
            {
                var cards = Page.Locator(".row.row-cols-1 .card");
                if (await cards.CountAsync() == 0)
                    break;

                // Select the card to reveal the delete button
                await cards.First.ClickAsync();

                var deleteBtn = Page.Locator("button.btn-outline-danger:has(.bi-trash)");
                await deleteBtn.WaitForAsync(new LocatorWaitForOptions { Timeout = 3_000 });
                await deleteBtn.ClickAsync();

                // Wait for that card to disappear before checking again
                await Page.WaitForTimeoutAsync(500);
            }
        }
        catch
        {
            // Best-effort cleanup — never fail the test run on teardown errors
        }
    }
}
