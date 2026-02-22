using Microsoft.Playwright;
using MakerPrompt.E2E.Wasm.Fixtures;

namespace MakerPrompt.E2E.Wasm.Tests;

/// <summary>
/// Verifies the Blazor WASM app boots without errors.
/// </summary>
[Collection("Playwright")]
public class AppBootTests(PlaywrightFixture fixture)
{
    private readonly PlaywrightFixture _fixture = fixture;

    [Fact]
    public async Task App_Boots_Without_Console_Errors()
    {
        var page = await _fixture.NewPageAsync();
        var consoleErrors = new List<string>();

        page.Console += (_, msg) =>
        {
            if (msg.Type == "error")
                consoleErrors.Add(msg.Text);
        };

        await page.GotoAsync(_fixture.BaseUrl);

        // Wait for Blazor to finish loading — the sidebar nav should render
        await page.WaitForSelectorAsync(".sidebar", new PageWaitForSelectorOptions
        {
            Timeout = 30_000
        });

        // Filter out known benign errors (e.g. service worker, favicon)
        var realErrors = consoleErrors
            .Where(e => !e.Contains("service-worker", StringComparison.OrdinalIgnoreCase))
            .Where(e => !e.Contains("favicon", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Empty(realErrors);
    }

    [Fact]
    public async Task Fleet_Page_Is_Default_Route()
    {
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync(_fixture.BaseUrl);

        // Fleet page renders at "/" — look for the Add Printer button
        var addButton = page.Locator("button.btn-outline-primary", new PageLocatorOptions
        {
            HasTextRegex = new System.Text.RegularExpressions.Regex("Add", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
        });

        await addButton.WaitForAsync(new LocatorWaitForOptions { Timeout = 30_000 });
        Assert.True(await addButton.IsVisibleAsync());
    }
}
