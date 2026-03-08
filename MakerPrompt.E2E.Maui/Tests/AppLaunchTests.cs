using Microsoft.Playwright;
using MakerPrompt.E2E.Maui.Fixtures;

namespace MakerPrompt.E2E.Maui.Tests;

/// <summary>
/// Verifies the MAUI app launches and the Blazor WebView renders correctly.
/// All interaction goes through Playwright connected to WebView2 via CDP.
/// </summary>
[Collection("Appium")]
[Trait("Category", "E2E-Maui")]
[TestCaseOrderer("MakerPrompt.E2E.Maui.Fixtures.AlphabeticalOrderer", "MakerPrompt.E2E.Maui")]
public class AppLaunchTests
{
    private static IPage Page => AppiumSetup.Page;

    [Fact]
    public void App_Launches_Successfully()
    {
        // If the fixture wired up Page, the app started and CDP connected
        Assert.NotNull(Page);
    }

    [Fact]
    public async Task Page_Has_Title()
    {
        var title = await Page.TitleAsync();
        Assert.False(string.IsNullOrWhiteSpace(title), "Page should have a title");
    }

    [Fact]
    public async Task Sidebar_Renders()
    {
        // Fixture already verified sidebar is visible; confirm it's still there
        var sidebar = Page.Locator(".sidebar");
        await sidebar.WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });
        Assert.True(await sidebar.IsVisibleAsync(), "Sidebar should be visible after app launch");
    }

    [Fact]
    public async Task Content_Renders_Screenshot()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "screenshots");
        Directory.CreateDirectory(path);
        var filePath = Path.Combine(path, "app_launch.png");
        await Page.ScreenshotAsync(new PageScreenshotOptions { Path = filePath });
        Assert.True(File.Exists(filePath), "Screenshot file should exist");
    }

    [Fact]
    public async Task Fleet_Page_Is_Default_Route()
    {
        // Navigate away first so Fleet component remounts fresh — a previous
        // test may have left it in ControlPanel view with a connected printer.
        await AppiumSetup.NavigateAsync("/settings");
        await Page.WaitForTimeoutAsync(300);
        await AppiumSetup.NavigateAsync("/");
        var addBtn = Page.Locator("button:has-text('Add Printer')");
        await addBtn.WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });
        Assert.True(await addBtn.IsVisibleAsync(), "Fleet page (default route) should have an Add Printer button");
    }

    [Fact]
    public async Task App_Boots_Without_Console_Errors()
    {
        var consoleErrors = new List<string>();

        Page.Console += Handler;
        try
        {
            await AppiumSetup.NavigateAsync("/");
            await Page.Locator(".sidebar").WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });

            var realErrors = consoleErrors
                .Where(e => !e.Contains("service-worker", StringComparison.OrdinalIgnoreCase))
                .Where(e => !e.Contains("favicon", StringComparison.OrdinalIgnoreCase))
                .ToList();

            Assert.Empty(realErrors);
        }
        finally
        {
            Page.Console -= Handler;
        }

        void Handler(object? _, IConsoleMessage msg)
        {
            if (msg.Type == "error")
                consoleErrors.Add(msg.Text);
        }
    }
}
