using Microsoft.Playwright;
using MakerPrompt.E2E.Wasm.Fixtures;

namespace MakerPrompt.E2E.Wasm.Tests;

/// <summary>
/// End-to-end tests for the multi-printer fleet workflow using the Demo backend.
/// Covers: add printer → connect → telemetry updates → disconnect.
/// </summary>
[Collection("Playwright")]
public class FleetWorkflowTests(PlaywrightFixture fixture)
{
    private readonly PlaywrightFixture _fixture = fixture;

    [Fact]
    public async Task Fleet_AddPrinter_Demo_Mode()
    {
        var page = await _fixture.NewPageAsync();
        await NavigateToFleetAsync(page);

        // Click "Add Printer"
        await page.Locator("button.btn-outline-primary:has-text('Add')").ClickAsync();

        // Modal should appear — fill in the name
        var nameInput = page.Locator("input[placeholder='My 3D Printer']");
        await nameInput.WaitForAsync(new LocatorWaitForOptions { Timeout = 5_000 });
        await nameInput.FillAsync("E2E Test Printer");

        // Demo is the default connection type — click Save
        await page.Locator("button.btn-outline-primary:has-text('Save')").ClickAsync();

        // Verify the printer card appears
        var card = page.Locator(".card strong:has-text('E2E Test Printer')");
        await card.WaitForAsync(new LocatorWaitForOptions { Timeout = 5_000 });
        Assert.True(await card.IsVisibleAsync());
    }

    [Fact]
    public async Task Fleet_ConnectMockPrinter()
    {
        var page = await _fixture.NewPageAsync();
        await NavigateToFleetAsync(page);
        await AddDemoPrinterAsync(page, "Connect Test");

        // Click the card to select it (exposes action buttons)
        await page.Locator(".card:has-text('Connect Test')").ClickAsync();

        // Click the connect button (plug icon, outline-success)
        var connectBtn = page.Locator(".card:has-text('Connect Test') button.btn-outline-success");
        await connectBtn.WaitForAsync(new LocatorWaitForOptions { Timeout = 5_000 });
        await connectBtn.ClickAsync();

        // Wait for the printer to show Connected status — temperature indicators appear
        var tempIndicator = page.Locator(".card:has-text('Connect Test') .bi-thermometer-half");
        await tempIndicator.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
        Assert.True(await tempIndicator.IsVisibleAsync());
    }

    [Fact]
    public async Task Fleet_TelemetryUpdates()
    {
        var page = await _fixture.NewPageAsync();
        await NavigateToFleetAsync(page);
        await AddDemoPrinterAsync(page, "Telemetry Test");
        await ConnectPrinterAsync(page, "Telemetry Test");

        // After connecting, the Demo service sends telemetry with temperatures.
        // Verify temperature text is rendered (e.g. "25.0°C")
        var tempText = page.Locator(".card:has-text('Telemetry Test') strong", new PageLocatorOptions
        {
            HasTextRegex = new System.Text.RegularExpressions.Regex(@"\d+\.\d+°C")
        });
        await tempText.First.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
        Assert.True(await tempText.First.IsVisibleAsync());
    }

    [Fact]
    public async Task Fleet_DisconnectPrinter()
    {
        var page = await _fixture.NewPageAsync();
        await NavigateToFleetAsync(page);
        await AddDemoPrinterAsync(page, "Disconnect Test");
        await ConnectPrinterAsync(page, "Disconnect Test");

        // Click the card to select it (after connecting, Fleet shows ControlPanel inline)
        // We need to go back to the fleet card view first
        var backButton = page.Locator("button:has(.bi-arrow-left)");
        if (await backButton.IsVisibleAsync())
        {
            await backButton.ClickAsync();
        }

        // Select the printer card
        await page.Locator(".card:has-text('Disconnect Test')").ClickAsync();

        // Click disconnect button (outline-danger with x-circle icon)
        var disconnectBtn = page.Locator(".card:has-text('Disconnect Test') button.btn-outline-danger:has(.bi-x-circle)");
        await disconnectBtn.WaitForAsync(new LocatorWaitForOptions { Timeout = 5_000 });
        await disconnectBtn.ClickAsync();

        // After disconnect, the card should show the plug icon (disconnected state)
        var disconnectedIcon = page.Locator(".card:has-text('Disconnect Test') .bi-plug.text-muted");
        await disconnectedIcon.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
        Assert.True(await disconnectedIcon.IsVisibleAsync());
    }

    // ── Helpers ──

    private async Task NavigateToFleetAsync(IPage page)
    {
        await page.GotoAsync(_fixture.BaseUrl);
        // Wait for the Fleet page to be interactive
        await page.Locator("button.btn-outline-primary:has-text('Add')").WaitForAsync(
            new LocatorWaitForOptions { Timeout = 30_000 });
    }

    private static async Task AddDemoPrinterAsync(IPage page, string name)
    {
        await page.Locator("button.btn-outline-primary:has-text('Add')").ClickAsync();
        var nameInput = page.Locator("input[placeholder='My 3D Printer']");
        await nameInput.WaitForAsync(new LocatorWaitForOptions { Timeout = 5_000 });
        await nameInput.FillAsync(name);
        await page.Locator("button.btn-outline-primary:has-text('Save')").ClickAsync();
        // Wait for the card to appear
        await page.Locator($".card:has-text('{name}')").WaitForAsync(
            new LocatorWaitForOptions { Timeout = 5_000 });
    }

    private static async Task ConnectPrinterAsync(IPage page, string name)
    {
        // Select the card
        await page.Locator($".card:has-text('{name}')").ClickAsync();
        // Click connect
        var connectBtn = page.Locator($".card:has-text('{name}') button.btn-outline-success");
        await connectBtn.WaitForAsync(new LocatorWaitForOptions { Timeout = 5_000 });
        await connectBtn.ClickAsync();
        // Wait for connected state (temperature indicator)
        await page.Locator($".card:has-text('{name}') .bi-thermometer-half").WaitForAsync(
            new LocatorWaitForOptions { Timeout = 10_000 });
    }
}
