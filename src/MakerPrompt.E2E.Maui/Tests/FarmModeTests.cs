using Microsoft.Playwright;
using MakerPrompt.E2E.Maui.Fixtures;

namespace MakerPrompt.E2E.Maui.Tests;

/// <summary>
/// End-to-end tests for the farm mode feature in the MAUI app.
/// Covers: toggle farm mode, farm name display, default route redirect,
/// "Add Printer" button visibility, and farm configuration management UI.
///
/// Uses Playwright connected to the MAUI app's WebView2 via CDP.
/// Navigation uses AppiumSetup.NavigateAsync for client-side routing.
/// </summary>
[Collection("Appium")]
[Trait("Category", "E2E-Maui")]
[TestCaseOrderer("MakerPrompt.E2E.Maui.Fixtures.AlphabeticalOrderer", "MakerPrompt.E2E.Maui")]
public class FarmModeTests
{
    private static IPage Page => AppiumSetup.Page;

    // ── Farm Mode Toggle ──

    [Fact]
    public async Task FarmMode_A_Toggle_ExistsInSettings()
    {
        await NavigateToSettingsAsync();

        var toggle = Page.Locator("#farmModeEnabled");
        await toggle.WaitForAsync(new LocatorWaitForOptions { Timeout = 5_000 });
        Assert.True(await toggle.IsVisibleAsync(), "Farm mode toggle should be visible in settings");
    }

    [Fact]
    public async Task FarmMode_B_Disabled_RedirectsToDashboard()
    {
        await NavigateToSettingsAsync();

        // Farm mode defaults to disabled — ensure it is off
        var toggle = Page.Locator("#farmModeEnabled");
        if (await toggle.IsCheckedAsync())
        {
            await toggle.UncheckAsync();
            await SaveSettingsAsync();
        }

        // Navigate to root — should redirect to dashboard.
        // We check the URL rather than the welcome banner, which only renders
        // when no printer is connected (Demo printers from other tests may persist).
        await AppiumSetup.NavigateAsync("/");
        await Page.WaitForURLAsync("**/dashboard", new PageWaitForURLOptions { Timeout = 5_000 });
        Assert.Contains("dashboard", Page.Url, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FarmMode_C_Enabled_RedirectsToFleet()
    {
        await NavigateToSettingsAsync();

        // Enable farm mode
        var toggle = Page.Locator("#farmModeEnabled");
        if (!await toggle.IsCheckedAsync())
        {
            await toggle.CheckAsync();
        }
        await SaveSettingsAsync();

        // Navigate to root — should redirect to fleet
        await AppiumSetup.NavigateAsync("/");
        var addButton = Page.Locator("[data-testid='fleet-add-btn']");
        await addButton.WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });
        Assert.True(await addButton.IsVisibleAsync(), "Root should redirect to Fleet when farm mode is on");

        // Restore default (disabled)
        await NavigateToSettingsAsync();
        toggle = Page.Locator("#farmModeEnabled");
        if (await toggle.IsCheckedAsync())
        {
            await toggle.UncheckAsync();
        }
        await SaveSettingsAsync();
    }

    [Fact]
    public async Task FarmMode_D_Disabled_ShowsControlPanel()
    {
        // Farm mode defaults to disabled — navigate to dashboard
        await AppiumSetup.NavigateAsync("/dashboard");
        await Page.Locator(".sidebar").WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });

        // Add Printer button appears in the navbar in single-printer (non-farm) mode
        var addPrinterLink = Page.Locator("[data-testid='header-add-printer']");
        await addPrinterLink.WaitForAsync(new LocatorWaitForOptions { Timeout = 5_000 });
        Assert.True(await addPrinterLink.IsVisibleAsync(), "Add Printer button should appear in navbar when farm mode is off");
    }

    // ── Farm Name ──

    [Fact]
    public async Task FarmName_E_DefaultBrandLabel_ShowsMakerPrompt()
    {
        // Farm mode off and no active farm — header brand should show the app name
        await NavigateToSettingsAsync();
        var toggle = Page.Locator("#farmModeEnabled");
        if (await toggle.IsCheckedAsync())
        {
            await toggle.UncheckAsync();
            await SaveSettingsAsync();
        }

        await AppiumSetup.NavigateAsync("/dashboard");
        await Page.Locator(".sidebar").WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });

        var brand = Page.Locator("header .brand-label");
        var text = await brand.InnerTextAsync();
        Assert.Equal("MakerPrompt", text);
    }

    [Fact]
    public async Task FarmName_F_ShowsInSidebar_WhenFarmModeEnabled()
    {
        await EnableFarmModeAsync();

        // Create a farm and switch to it so the config FarmName gets populated
        var nameInput = Page.Locator("#farmNewName");
        await nameInput.WaitForAsync(new LocatorWaitForOptions { Timeout = 5_000 });
        await nameInput.FillAsync("Sidebar Test Farm");
        await nameInput.PressAsync("Tab");
        await Page.WaitForTimeoutAsync(300);
        var createBtn = Page.Locator("[data-testid='farm-create-btn']");
        await createBtn.ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        // Switch to the newly created farm
        var selectEl = Page.Locator("select");
        await selectEl.SelectOptionAsync(new SelectOptionValue { Label = "Sidebar Test Farm" });
        var switchBtn = Page.Locator("[data-testid='farm-switch-btn']");
        await switchBtn.ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        // Navigate to fleet and verify sidebar shows the farm name
        await AppiumSetup.NavigateAsync("/fleet");
        await Page.Locator(".sidebar").WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });

        var sidebarFarm = Page.Locator(".sidebar .text-info");
        await sidebarFarm.WaitForAsync(new LocatorWaitForOptions { Timeout = 5_000 });
        var text = await sidebarFarm.InnerTextAsync();
        Assert.Contains("Sidebar Test Farm", text);

        await RestoreDefaultFarmModeAsync();
    }

    // ── Farm Configuration Management ──

    [Fact]
    public async Task FarmConfig_G_CreateNewFarm()
    {
        await EnableFarmModeAsync();

        // Create a new farm
        var nameInput = Page.Locator("#farmNewName");
        await nameInput.WaitForAsync(new LocatorWaitForOptions { Timeout = 5_000 });
        await nameInput.FillAsync("MAUI E2E Farm");
        // Blazor @bind uses onchange — Tab triggers blur which fires the event,
        // causing _newFarmName to update and the Create button to become enabled.
        await nameInput.PressAsync("Tab");
        await Page.WaitForTimeoutAsync(300);

        var createBtn = Page.Locator("[data-testid='farm-create-btn']");
        await createBtn.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 3_000 });
        await createBtn.ClickAsync();

        // Wait for toast confirmation
        await Page.WaitForTimeoutAsync(1000);

        // The new farm should appear in the dropdown
        var option = Page.Locator("select option:has-text('MAUI E2E Farm')");
        Assert.True(await option.CountAsync() > 0, "Created farm should appear in the dropdown");

        await RestoreDefaultFarmModeAsync();
    }

    [Fact]
    public async Task FarmConfig_H_ExportButton_Exists()
    {
        await EnableFarmModeAsync();

        var exportBtn = Page.Locator("[data-testid='farm-export-btn']");
        await exportBtn.WaitForAsync(new LocatorWaitForOptions { Timeout = 5_000 });
        Assert.True(await exportBtn.IsVisibleAsync(), "Export button should be visible when farm mode is on");

        await RestoreDefaultFarmModeAsync();
    }

    [Fact]
    public async Task FarmConfig_I_ImportButton_Exists()
    {
        await EnableFarmModeAsync();

        var importLabel = Page.Locator("[data-testid='farm-import-label']");
        await importLabel.WaitForAsync(new LocatorWaitForOptions { Timeout = 5_000 });
        Assert.True(await importLabel.IsVisibleAsync(), "Import button should be visible when farm mode is on");

        await RestoreDefaultFarmModeAsync();
    }

    [Fact]
    public async Task FarmConfig_J_SwitchButton_Exists()
    {
        await EnableFarmModeAsync();

        var switchBtn = Page.Locator("[data-testid='farm-switch-btn']");
        await switchBtn.WaitForAsync(new LocatorWaitForOptions { Timeout = 5_000 });
        Assert.True(await switchBtn.IsVisibleAsync(), "Switch farm button should be visible when farm mode is on");

        await RestoreDefaultFarmModeAsync();
    }

    // ── Farm Config Section Hidden When Farm Mode Off ──

    [Fact]
    public async Task FarmConfig_K_HiddenWhenFarmModeDisabled()
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
        Assert.True(await createInput.CountAsync() == 0, "Farm config section should be hidden when farm mode is off");
    }

    // ── Helpers ──

    private static async Task NavigateToSettingsAsync()
    {
        await AppiumSetup.NavigateAsync("/settings");
        await Page.Locator("#farmModeEnabled").WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });
    }

    private static async Task SaveSettingsAsync()
    {
        await Page.Locator("[data-testid='save-settings-btn']").ClickAsync();
        await Page.WaitForTimeoutAsync(1000);
    }

    /// <summary>
    /// Ensures farm mode is enabled and the farm configuration section is visible.
    /// </summary>
    private static async Task EnableFarmModeAsync()
    {
        await NavigateToSettingsAsync();
        var toggle = Page.Locator("#farmModeEnabled");
        if (!await toggle.IsCheckedAsync())
        {
            await toggle.CheckAsync();
            await SaveSettingsAsync();
            await NavigateToSettingsAsync();
        }
    }

    /// <summary>
    /// Restores the default state (farm mode disabled).
    /// </summary>
    private static async Task RestoreDefaultFarmModeAsync()
    {
        await NavigateToSettingsAsync();
        var toggle = Page.Locator("#farmModeEnabled");
        if (await toggle.IsCheckedAsync())
        {
            await toggle.UncheckAsync();
            await SaveSettingsAsync();
        }
    }
}
