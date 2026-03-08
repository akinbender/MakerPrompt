using Microsoft.Playwright;
using MakerPrompt.E2E.Wasm.Fixtures;

namespace MakerPrompt.E2E.Wasm.Tests;

/// <summary>
/// E2E tests for stable, read-only pages that don't depend on printer connections.
/// These pages are unlikely to change and serve as a baseline regression net.
/// </summary>
[Collection("Playwright")]
[Trait("Category", "E2E-Wasm")]
public class StaticPageTests(PlaywrightFixture fixture)
{
    private readonly PlaywrightFixture _fixture = fixture;
    private IPage Page => _fixture.Page;

    // ── G-Code Cheat Sheet (/cheatsheet) ──

    [Fact]
    public async Task CheatSheet_Page_Loads()
    {
        await Page.GotoAsync($"{_fixture.BaseUrl}/cheatsheet");
        // The table with G-code commands should render
        var table = Page.Locator("table.table");
        await table.WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });
        Assert.True(await table.IsVisibleAsync());
    }

    [Fact]
    public async Task CheatSheet_Contains_GCode_Commands()
    {
        await Page.GotoAsync($"{_fixture.BaseUrl}/cheatsheet");
        await Page.Locator("table.table").WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });

        // Table should have rows with G-code commands (font-monospace cells like G0, G1, M104, etc.)
        var commandCells = Page.Locator("td.font-monospace");
        var count = await commandCells.CountAsync();
        Assert.True(count > 5, $"Expected many G-code commands but found {count}");
    }

    [Fact]
    public async Task CheatSheet_Search_Filters_Commands()
    {
        await Page.GotoAsync($"{_fixture.BaseUrl}/cheatsheet");
        await Page.Locator("table.table").WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });

        // Count rows before filtering
        var allRows = await Page.Locator("td.font-monospace").CountAsync();

        // Type a search term that matches only a few commands
        var searchInput = Page.Locator("input[type='text']").First;
        await searchInput.FillAsync("M104");
        // Blazor @bind fires on blur (change), not on input.
        // Press Tab to leave the field and trigger the binding.
        await searchInput.PressAsync("Tab");

        // Wait for filtering to take effect
        await Page.WaitForTimeoutAsync(500);

        // Filtered rows should be fewer
        var filteredRows = await Page.Locator("td.font-monospace").CountAsync();
        Assert.True(filteredRows < allRows, $"Search should filter: {filteredRows} filtered vs {allRows} total");
        Assert.True(filteredRows >= 1, "M104 should match at least one command");
    }

    [Fact]
    public async Task CheatSheet_Commands_Have_Category_Badges()
    {
        await Page.GotoAsync($"{_fixture.BaseUrl}/cheatsheet");
        await Page.Locator("table.table").WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });

        // Each command row should have at least one category badge
        var badges = Page.Locator("table.table .badge.bg-primary");
        var count = await badges.CountAsync();
        Assert.True(count > 0, "G-code commands should have category badges");
    }

    // ── Calculators (/calculators) ──

    [Fact]
    public async Task Calculators_Page_Loads()
    {
        await Page.GotoAsync($"{_fixture.BaseUrl}/calculators");
        // The accordion with calculator tabs should render
        var accordion = Page.Locator(".accordion");
        await accordion.WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });
        Assert.True(await accordion.IsVisibleAsync());
    }

    [Fact]
    public async Task Calculators_BeltSteps_Shows_Result()
    {
        await Page.GotoAsync($"{_fixture.BaseUrl}/calculators");
        await Page.Locator(".accordion").WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });

        // Belt Steps is the default open tab — verify the result section renders.
        // All 3 calculator panels have an h4 "Result" (hidden via CSS collapse),
        // so use .First to target the visible one in the open Belt Steps panel.
        var resultHeading = Page.Locator(".accordion-collapse.show h4").First;
        await resultHeading.WaitForAsync(new LocatorWaitForOptions { Timeout = 5_000 });
        Assert.True(await resultHeading.IsVisibleAsync());

        // Should show a G-code example like "M92 X80.00 Y80.00"
        var gcodeExample = Page.Locator("code:has-text('M92')").First;
        Assert.True(await gcodeExample.IsVisibleAsync(), "Belt steps calculator should show M92 G-code");
    }

    [Fact]
    public async Task Calculators_Accordion_Switches_Tabs()
    {
        await Page.GotoAsync($"{_fixture.BaseUrl}/calculators");
        await Page.Locator(".accordion").WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });

        // There are 3 accordion items in fixed order:
        // 0 = Belt Steps (default open), 1 = Lead Screw, 2 = Extruder Steps.
        // Button text is localized, so use positional selection.
        var accordionButtons = Page.Locator(".accordion-button");

        // Click the Lead Screw accordion button (index 1)
        await accordionButtons.Nth(1).ClickAsync();
        await Page.WaitForTimeoutAsync(300);

        // Click the Extruder Steps accordion button (index 2)
        await accordionButtons.Nth(2).ClickAsync();
        await Page.WaitForTimeoutAsync(300);

        // The extruder panel should now be visible
        var extruderPanel = Page.Locator(".accordion-collapse.show");
        Assert.True(await extruderPanel.CountAsync() >= 1, "Extruder Steps panel should be open");
    }

    [Fact]
    public async Task Calculators_M500_Reminder_Visible()
    {
        await Page.GotoAsync($"{_fixture.BaseUrl}/calculators");
        await Page.Locator(".accordion").WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });

        // The M500 reminder alert should be visible at the top
        var m500Alert = Page.Locator("code:has-text('M500')");
        Assert.True(await m500Alert.IsVisibleAsync(), "M500 save reminder should be displayed");
    }

    // ── About (/about) ──

    [Fact]
    public async Task About_Page_Loads()
    {
        await Page.GotoAsync($"{_fixture.BaseUrl}/about");
        // Should render content (the localized about HTML)
        var content = Page.Locator(".col-12");
        await content.WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });
        var text = await content.InnerTextAsync();
        Assert.False(string.IsNullOrWhiteSpace(text), "About page should have content");
    }

    // ── Settings (/settings) ──

    [Fact]
    public async Task Settings_Page_Loads()
    {
        await Page.GotoAsync($"{_fixture.BaseUrl}/settings");
        var heading = Page.Locator("h3:has-text('Settings')");
        await heading.WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });
        Assert.True(await heading.IsVisibleAsync());
    }

    [Fact]
    public async Task Settings_Has_Feature_Toggles()
    {
        await Page.GotoAsync($"{_fixture.BaseUrl}/settings");
        await Page.Locator("h3:has-text('Settings')").WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });

        // Feature toggle switches should be present
        var filamentToggle = Page.Locator("#enableFilamentInventory");
        Assert.True(await filamentToggle.IsVisibleAsync(), "Filament Inventory toggle should exist");

        var analyticsToggle = Page.Locator("#enablePrintAnalytics");
        Assert.True(await analyticsToggle.IsVisibleAsync(), "Print Analytics toggle should exist");
    }

    [Fact]
    public async Task Settings_Has_Save_Button()
    {
        await Page.GotoAsync($"{_fixture.BaseUrl}/settings");
        await Page.Locator("h3:has-text('Settings')").WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });

        var saveBtn = Page.Locator("button.btn-primary:has-text('Save')");
        Assert.True(await saveBtn.IsVisibleAsync(), "Save Settings button should exist");
    }

    // ── Sidebar Navigation ──

    [Fact]
    public async Task Sidebar_All_Nav_Links_Present()
    {
        await Page.GotoAsync(_fixture.BaseUrl);
        await Page.Locator(".sidebar").WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });

        // All core nav links should be in the sidebar
        var navLinks = new[] { "", "cheatsheet", "calculators", "about", "settings" };
        foreach (var href in navLinks)
        {
            var link = Page.Locator($".sidebar a[href='{href}']");
            Assert.True(await link.CountAsync() >= 1, $"Sidebar should have a link to '/{href}'");
        }
    }

    [Fact]
    public async Task Sidebar_Navigation_Works()
    {
        await Page.GotoAsync(_fixture.BaseUrl);
        await Page.Locator(".sidebar").WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });

        // Click the cheatsheet link in the sidebar
        await Page.Locator(".sidebar a[href='cheatsheet']").ClickAsync();
        // Should navigate — verify the G-code table loads
        var table = Page.Locator("table.table");
        await table.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
        Assert.True(await table.IsVisibleAsync());

        // Click calculators
        await Page.Locator(".sidebar a[href='calculators']").ClickAsync();
        var accordion = Page.Locator(".accordion");
        await accordion.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
        Assert.True(await accordion.IsVisibleAsync());
    }
}
