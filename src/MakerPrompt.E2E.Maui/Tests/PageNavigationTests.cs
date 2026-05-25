using Microsoft.Playwright;
using MakerPrompt.E2E.Maui.Fixtures;

namespace MakerPrompt.E2E.Maui.Tests;

/// <summary>
/// MAUI page navigation smoke tests.
///
/// Uses Playwright connected to the MAUI app's WebView2 via Chrome DevTools
/// Protocol (CDP). Navigation uses AppiumSetup.NavigateAsync which sets
/// WaitUntilState.DOMContentLoaded to avoid hangs on WebView2's virtual file server.
/// </summary>
[Collection("Appium")]
[Trait("Category", "E2E-Maui")]
[TestCaseOrderer("MakerPrompt.E2E.Maui.Fixtures.AlphabeticalOrderer", "MakerPrompt.E2E.Maui")]
public class PageNavigationTests
{
    private static readonly string ScreenshotDir = Path.Combine(AppContext.BaseDirectory, "screenshots");
    private static IPage Page => AppiumSetup.Page;

    public PageNavigationTests()
    {
        Directory.CreateDirectory(ScreenshotDir);
    }

    // ── G-Code Cheat Sheet ──

    [Fact]
    public async Task CheatSheet_Page_Loads()
    {
        await AppiumSetup.NavigateAsync("/cheatsheet");
        var table = Page.Locator("table.table");
        await table.WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });
        Assert.True(await table.IsVisibleAsync(), "CheatSheet page should render a G-code table");
        await SaveScreenshot("cheatsheet");
    }

    [Fact]
    public async Task CheatSheet_Has_GCode_Rows()
    {
        await AppiumSetup.NavigateAsync("/cheatsheet");
        await Page.Locator("table.table").WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });

        var commandCells = Page.Locator("td.font-monospace");
        var count = await commandCells.CountAsync();
        Assert.True(count > 5, $"Expected many G-code rows, found {count}");
    }

    [Fact]
    public async Task CheatSheet_Has_Category_Badges()
    {
        await AppiumSetup.NavigateAsync("/cheatsheet");
        await Page.Locator("table.table").WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });

        var badges = Page.Locator("table.table .badge.bg-primary");
        var count = await badges.CountAsync();
        Assert.True(count > 0, "G-code commands should have category badges");
    }

    // ── Calculators ──

    [Fact]
    public async Task Calculators_Page_Loads()
    {
        await AppiumSetup.NavigateAsync("/calculators");
        var accordion = Page.Locator(".accordion");
        await accordion.WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });
        Assert.True(await accordion.IsVisibleAsync());
        await SaveScreenshot("calculators");
    }

    [Fact]
    public async Task Calculators_BeltSteps_Shows_Result()
    {
        await AppiumSetup.NavigateAsync("/calculators");
        await Page.Locator(".accordion").WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });

        var gcodeExample = Page.Locator("code:has-text('M92')").First;
        Assert.True(await gcodeExample.IsVisibleAsync(), "Belt Steps calculator should show M92 G-code");
    }

    [Fact]
    public async Task Calculators_M500_Reminder_Visible()
    {
        await AppiumSetup.NavigateAsync("/calculators");
        await Page.Locator(".accordion").WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });

        var m500Alert = Page.Locator("code:has-text('M500')");
        Assert.True(await m500Alert.IsVisibleAsync(), "M500 save reminder should be displayed");
    }

    // ── About ──

    [Fact]
    public async Task About_Page_Loads()
    {
        await AppiumSetup.NavigateAsync("/about");
        var content = Page.Locator(".col-12");
        await content.WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });
        var text = await content.InnerTextAsync();
        Assert.False(string.IsNullOrWhiteSpace(text), "About page should have content");
        await SaveScreenshot("about");
    }

    // ── Settings ──

    [Fact]
    public async Task Settings_Page_Loads()
    {
        await AppiumSetup.NavigateAsync("/settings");
        // The page title is rendered as <h1 class="h2"> in MainLayout, not an h3.
        var heading = Page.Locator("h1:has-text('Settings')");
        await heading.WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });
        Assert.True(await heading.IsVisibleAsync());
        await SaveScreenshot("settings");
    }

    [Fact]
    public async Task Settings_Has_Feature_Toggles()
    {
        await AppiumSetup.NavigateAsync("/settings");
        await Page.Locator("h1:has-text('Settings')").WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });

        var filamentToggle = Page.Locator("#enableFilamentInventory");
        Assert.True(await filamentToggle.IsVisibleAsync(), "Filament Inventory toggle should exist");

        var analyticsToggle = Page.Locator("#enablePrintAnalytics");
        Assert.True(await analyticsToggle.IsVisibleAsync(), "Print Analytics toggle should exist");
    }

    [Fact]
    public async Task Settings_Has_Save_Button()
    {
        await AppiumSetup.NavigateAsync("/settings");
        await Page.Locator("h1:has-text('Settings')").WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });

        var saveBtn = Page.Locator("button.btn-primary:has-text('Save')");
        Assert.True(await saveBtn.IsVisibleAsync(), "Save Settings button should exist");
    }

    // ── Fleet (home) ──

    [Fact]
    public async Task Fleet_Page_Loads()
    {
        await AppiumSetup.NavigateAsync("/fleet");
        var addBtn = Page.Locator("button:has-text('Add Printer')");
        await addBtn.WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });
        Assert.True(await addBtn.IsVisibleAsync(), "Fleet page should have an Add Printer button");
        await SaveScreenshot("fleet");
    }

    // ── Sidebar ──

    [Fact]
    public async Task Sidebar_Has_Nav_Links()
    {
        await AppiumSetup.NavigateAsync("/");
        await Page.Locator(".sidebar").WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });

        var navLinks = new[] { "cheatsheet", "calculators", "about", "settings" };
        foreach (var href in navLinks)
        {
            var link = Page.Locator($".sidebar a[href='{href}']");
            Assert.True(await link.CountAsync() >= 1, $"Sidebar should have a link to '/{href}'");
        }

        // Dashboard or Fleet link depending on farm mode
        var homeLink = Page.Locator(".sidebar a[href='dashboard'], .sidebar a[href='fleet']");
        Assert.True(await homeLink.CountAsync() >= 1, "Sidebar should have a Dashboard or Fleet link");
    }

    [Fact]
    public async Task Sidebar_Navigation_Works()
    {
        await AppiumSetup.NavigateAsync("/");
        await Page.Locator(".sidebar").WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });

        // Click cheatsheet link in sidebar (client-side Blazor navigation — no reload)
        await Page.Locator(".sidebar a[href='cheatsheet']").ClickAsync();
        var table = Page.Locator("table.table");
        await table.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
        Assert.True(await table.IsVisibleAsync());

        // Click calculators link
        await Page.Locator(".sidebar a[href='calculators']").ClickAsync();
        var accordion = Page.Locator(".accordion");
        await accordion.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
        Assert.True(await accordion.IsVisibleAsync());
    }

    // ── Helpers ──

    private static async Task SaveScreenshot(string name)
    {
        await Page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(ScreenshotDir, $"{name}.png")
        });
    }
}
