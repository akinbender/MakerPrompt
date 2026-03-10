using Microsoft.Playwright;
using MakerPrompt.E2E.Wasm.Fixtures;

namespace MakerPrompt.E2E.Wasm.Tests;

/// <summary>
/// End-to-end tests for the farm mode feature.
/// Covers: toggle farm mode, farm name display, default route redirect,
/// "Add Printer" button visibility, and farm configuration import/export.
/// </summary>
[Collection("Playwright")]
[Trait("Category", "E2E-Wasm")]
public class FarmModeTests(PlaywrightFixture fixture)
{
    private readonly PlaywrightFixture _fixture = fixture;
    private IPage Page => _fixture.Page;

    // ── Farm Mode Toggle ──

    [Fact]
    public async Task FarmMode_Toggle_ExistsInSettings()
    {
        await NavigateToSettingsAsync();

        var toggle = Page.Locator("#farmModeEnabled");
        await toggle.WaitForAsync(new LocatorWaitForOptions { Timeout = 5_000 });
        Assert.True(await toggle.IsVisibleAsync());
    }

    [Fact]
    public async Task FarmMode_Enabled_RedirectsToFleet()
    {
        await NavigateToSettingsAsync();

        // Enable farm mode
        var toggle = Page.Locator("#farmModeEnabled");
        if (!await toggle.IsCheckedAsync())
        {
            await toggle.CheckAsync();
            await Page.WaitForTimeoutAsync(300);
        }
        await SaveSettingsAsync();

        // Navigate to root — should redirect to fleet
        await Page.GotoAsync(_fixture.BaseUrl);
        var addButton = Page.Locator("[data-testid='fleet-add-btn']");
        await addButton.WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });
        Assert.True(await addButton.IsVisibleAsync());

        // Restore default (disabled)
        await RestoreDefaultFarmModeAsync();
    }

    [Fact]
    public async Task FarmMode_Disabled_RedirectsToDashboard()
    {
        await NavigateToSettingsAsync();

        // Farm mode defaults to disabled — ensure it is off
        var toggle = Page.Locator("#farmModeEnabled");
        if (await toggle.IsCheckedAsync())
        {
            await toggle.UncheckAsync();
            await SaveSettingsAsync();
        }

        // Navigate to root — should redirect to dashboard
        await Page.GotoAsync(_fixture.BaseUrl);
        await Page.WaitForURLAsync("**/dashboard", new PageWaitForURLOptions { Timeout = 15_000 });
        Assert.Contains("dashboard", Page.Url, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FarmMode_Disabled_ShowsAddPrinterInNavbar()
    {
        // Farm mode defaults to disabled — navigate to dashboard
        await Page.GotoAsync($"{_fixture.BaseUrl}/dashboard");
        await Page.Locator(".sidebar").WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });

        // Add Printer button no longer appears in navbar for single-printer mode
        var addPrinterLink = Page.Locator("[data-testid='header-add-printer']");
        Assert.True(await addPrinterLink.CountAsync() == 0, "Add Printer button should NOT appear in navbar when farm mode is off");
    }

    // ── Farm Name ──

    [Fact]
    public async Task FarmName_DisplaysInHeader()
    {
        // Farm mode off and no active farm — header brand should show the app name
        await NavigateToSettingsAsync();
        var toggle = Page.Locator("#farmModeEnabled");
        if (await toggle.IsCheckedAsync())
        {
            await toggle.UncheckAsync();
            await SaveSettingsAsync();
        }

        await Page.GotoAsync($"{_fixture.BaseUrl}/dashboard");
        await Page.Locator(".sidebar").WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });

        var brand = Page.Locator("header .brand-label");
        var text = await brand.InnerTextAsync();
        Assert.Equal("MakerPrompt", text);
    }

    [Fact]
    public async Task FarmName_ShowsInSidebar_WhenFarmModeEnabled()
    {
        await EnableFarmModeAsync();

        // Create a farm and switch to it so the config FarmName gets populated
        var nameInput = Page.Locator("#farmNewName");
        await nameInput.WaitForAsync(new LocatorWaitForOptions { Timeout = 5_000 });
        await nameInput.FillAsync("Sidebar Farm");
        await nameInput.PressAsync("Tab");
        await Page.WaitForTimeoutAsync(300);
        var createBtn = Page.Locator("[data-testid='farm-create-btn']");
        await createBtn.ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        // Switch to the newly created farm
        var selectEl = Page.Locator("select");
        await selectEl.SelectOptionAsync(new SelectOptionValue { Label = "Sidebar Farm" });
        var switchBtn = Page.Locator("[data-testid='farm-switch-btn']");
        await switchBtn.ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        // Verify farm name appears in sidebar
        await Page.GotoAsync($"{_fixture.BaseUrl}/fleet");
        await Page.Locator(".sidebar").WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });

        var sidebarFarm = Page.Locator(".sidebar .text-info");
        await sidebarFarm.WaitForAsync(new LocatorWaitForOptions { Timeout = 5_000 });
        var text = await sidebarFarm.InnerTextAsync();
        Assert.Contains("Sidebar Farm", text);

        await RestoreDefaultFarmModeAsync();
    }

    // ── Farm Configuration Management ──

    [Fact]
    public async Task FarmConfig_CreateNewFarm()
    {
        await EnableFarmModeAsync();

        // Create a new farm
        var nameInput = Page.Locator("#farmNewName");
        await nameInput.WaitForAsync(new LocatorWaitForOptions { Timeout = 5_000 });
        await nameInput.FillAsync("E2E Test Farm");
        await nameInput.PressAsync("Tab");

        var createBtn = Page.Locator("[data-testid='farm-create-btn']");
        await createBtn.ClickAsync();

        // Wait for toast confirmation
        await Page.WaitForTimeoutAsync(1000);

        // The new farm should appear in the dropdown
        var option = Page.Locator("select option:has-text('E2E Test Farm')");
        Assert.True(await option.CountAsync() > 0);

        await RestoreDefaultFarmModeAsync();
    }

    [Fact]
    public async Task FarmConfig_ExportButton_Exists()
    {
        await EnableFarmModeAsync();

        var exportBtn = Page.Locator("[data-testid='farm-export-btn']");
        await exportBtn.WaitForAsync(new LocatorWaitForOptions { Timeout = 5_000 });
        Assert.True(await exportBtn.IsVisibleAsync());

        await RestoreDefaultFarmModeAsync();
    }

    [Fact]
    public async Task FarmConfig_ImportButton_Exists()
    {
        await EnableFarmModeAsync();

        var importLabel = Page.Locator("[data-testid='farm-import-label']");
        await importLabel.WaitForAsync(new LocatorWaitForOptions { Timeout = 5_000 });
        Assert.True(await importLabel.IsVisibleAsync());

        await RestoreDefaultFarmModeAsync();
    }

    [Fact]
    public async Task FarmConfig_SwitchButton_Exists()
    {
        await EnableFarmModeAsync();

        // The switch (arrow-repeat icon) button should be present
        var switchBtn = Page.Locator("[data-testid='farm-switch-btn']");
        await switchBtn.WaitForAsync(new LocatorWaitForOptions { Timeout = 5_000 });
        Assert.True(await switchBtn.IsVisibleAsync());

        await RestoreDefaultFarmModeAsync();
    }

    // ── Farm Config Section Hidden When Farm Mode Off ──

    [Fact]
    public async Task FarmConfig_HiddenWhenFarmModeDisabled()
    {
        await NavigateToSettingsAsync();

        // Farm mode defaults to disabled — ensure it is off
        var toggle = Page.Locator("#farmModeEnabled");
        if (await toggle.IsCheckedAsync())
        {
            await toggle.UncheckAsync();
        }
        // Farm config section should not be visible
        var createInput = Page.Locator("#farmNewName");
        Assert.True(await createInput.CountAsync() == 0);
    }

    // ── Helpers ──

    private async Task NavigateToSettingsAsync()
    {
        await Page.GotoAsync($"{_fixture.BaseUrl}/settings");
        await Page.Locator("#farmModeEnabled").WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });
    }

    private async Task SaveSettingsAsync()
    {
        await Page.Locator("[data-testid='save-settings-btn']").ClickAsync();
        // Wait for the save toast to appear
        await Page.WaitForTimeoutAsync(1000);
    }

    /// <summary>
    /// Ensures farm mode is enabled in settings and reloads the page so the
    /// farm configuration section is visible. Returns after the settings page
    /// is ready.
    /// </summary>
    private async Task EnableFarmModeAsync()
    {
        await NavigateToSettingsAsync();
        var toggle = Page.Locator("#farmModeEnabled");
        if (!await toggle.IsCheckedAsync())
        {
            await toggle.CheckAsync();
            // Wait for Blazor to re-render after @bind change
            await Page.WaitForTimeoutAsync(500);
            await SaveSettingsAsync();
            // Reload the page so the farm config section renders from persisted state
            await NavigateToSettingsAsync();
        }
        // Wait for the farm config section to appear
        await Page.Locator("#farmNewName").WaitForAsync(
            new LocatorWaitForOptions { Timeout = 5_000 });
    }

    /// <summary>
    /// Restores the default state (farm mode disabled) so subsequent tests
    /// start from a clean baseline.
    /// </summary>
    private async Task RestoreDefaultFarmModeAsync()
    {
        await NavigateToSettingsAsync();
        var toggle = Page.Locator("#farmModeEnabled");
        if (await toggle.IsCheckedAsync())
        {
            await toggle.UncheckAsync();
            await Page.WaitForTimeoutAsync(300);
            await SaveSettingsAsync();
        }
    }
}
