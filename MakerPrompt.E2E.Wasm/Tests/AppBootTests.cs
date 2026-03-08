using Microsoft.Playwright;
using MakerPrompt.E2E.Wasm.Fixtures;

namespace MakerPrompt.E2E.Wasm.Tests;

/// <summary>
/// Verifies the Blazor WASM app boots without errors.
/// All tests share a single browser tab via the collection fixture.
/// </summary>
[Collection("Playwright")]
[Trait("Category", "E2E-Wasm")]
public class AppBootTests(PlaywrightFixture fixture)
{
    private readonly PlaywrightFixture _fixture = fixture;
    private IPage Page => _fixture.Page;

    [Fact]
    public async Task App_Boots_Without_Console_Errors()
    {
        var consoleErrors = new List<string>();

        Page.Console += Handler;
        try
        {
            await Page.GotoAsync(_fixture.BaseUrl);

            // Wait for Blazor to finish loading — the sidebar nav should render
            await Page.WaitForSelectorAsync(".sidebar", new PageWaitForSelectorOptions
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

    [Fact]
    public async Task Fleet_Page_Is_Default_Route()
    {
        await Page.GotoAsync(_fixture.BaseUrl);

        // Fleet page renders at "/" — look for the Add Printer button
        var addButton = Page.Locator("button.btn-outline-primary", new PageLocatorOptions
        {
            HasTextRegex = new System.Text.RegularExpressions.Regex("Add", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
        });

        await addButton.WaitForAsync(new LocatorWaitForOptions { Timeout = 30_000 });
        Assert.True(await addButton.IsVisibleAsync());
    }
}
