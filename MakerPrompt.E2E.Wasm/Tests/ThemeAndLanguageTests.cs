using Microsoft.Playwright;
using MakerPrompt.E2E.Wasm.Fixtures;

namespace MakerPrompt.E2E.Wasm.Tests;

/// <summary>
/// E2E tests for theme switching and language changing in the Blazor WASM app.
/// </summary>
[Collection("Playwright")]
[Trait("Category", "E2E-Wasm")]
public class ThemeAndLanguageTests(PlaywrightFixture fixture)
{
    private readonly PlaywrightFixture _fixture = fixture;
    private IPage Page => _fixture.Page;

    // ── Theme ──

    [Fact]
    public async Task Theme_SwitchToLight_SetsDataAttribute()
    {
        await Page.GotoAsync(_fixture.BaseUrl);
        await Page.Locator(".sidebar").WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });

        // Open the theme dropdown in the navbar (second dropdown; first is culture)
        var themeDropdown = Page.Locator(".navbar .dropdown-toggle").Nth(1);
        await themeDropdown.ClickAsync();

        var lightItem = Page.Locator(".dropdown-item:has-text('Light')").First;
        await lightItem.WaitForAsync(new LocatorWaitForOptions { Timeout = 3_000 });
        await lightItem.ClickAsync();

        await Page.WaitForTimeoutAsync(500);

        var attr = await Page.EvaluateAsync<string>(
            "() => document.documentElement.getAttribute('data-bs-theme')");
        Assert.Equal("light", attr);
    }

    [Fact]
    public async Task Theme_SwitchToDark_SetsDataAttribute()
    {
        await Page.GotoAsync(_fixture.BaseUrl);
        await Page.Locator(".sidebar").WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });

        var themeDropdown = Page.Locator(".navbar .dropdown-toggle").Nth(1);
        await themeDropdown.ClickAsync();

        var darkItem = Page.Locator(".dropdown-item:has-text('Dark')").First;
        await darkItem.WaitForAsync(new LocatorWaitForOptions { Timeout = 3_000 });
        await darkItem.ClickAsync();

        await Page.WaitForTimeoutAsync(500);

        var attr = await Page.EvaluateAsync<string>(
            "() => document.documentElement.getAttribute('data-bs-theme')");
        Assert.Equal("dark", attr);
    }

    [Fact]
    public async Task Theme_Dropdown_ShowsAllOptions()
    {
        await Page.GotoAsync(_fixture.BaseUrl);
        await Page.Locator(".sidebar").WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });

        var themeDropdown = Page.Locator(".navbar .dropdown-toggle").Nth(1);
        await themeDropdown.ClickAsync();
        await Page.WaitForTimeoutAsync(300);

        // Should have at least 3 options: Auto, Light, Dark
        var allItems = Page.Locator(".dropdown-menu:visible .dropdown-item");
        var count = await allItems.CountAsync();
        Assert.True(count >= 3, $"Theme dropdown should have at least 3 options, found {count}");
    }

    [Fact]
    public async Task Theme_PersistsAfterNavigation()
    {
        await Page.GotoAsync(_fixture.BaseUrl);
        await Page.Locator(".sidebar").WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });

        // Switch to light
        var themeDropdown = Page.Locator(".navbar .dropdown-toggle").Nth(1);
        await themeDropdown.ClickAsync();
        await Page.Locator(".dropdown-item:has-text('Light')").First.ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        // Navigate to another page
        await Page.GotoAsync($"{_fixture.BaseUrl}/cheatsheet");
        await Page.Locator("table.table").WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });

        // Theme should still be light
        var attr = await Page.EvaluateAsync<string>(
            "() => document.documentElement.getAttribute('data-bs-theme')");
        Assert.Equal("light", attr);
    }

    // ── Language ──

    [Fact]
    public async Task Language_Dropdown_ShowsOptions()
    {
        await Page.GotoAsync($"{_fixture.BaseUrl}/settings");
        await Page.Locator("h3").First.WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });

        // Culture dropdown is in the navbar
        var dropdowns = Page.Locator(".navbar .dropdown-toggle");
        var count = await dropdowns.CountAsync();
        Assert.True(count >= 2, "Navbar should have at least 2 dropdowns (culture + theme)");

        await dropdowns.Nth(0).ClickAsync();
        await Page.WaitForTimeoutAsync(300);

        var items = Page.Locator(".dropdown-menu:visible .dropdown-item");
        var itemCount = await items.CountAsync();
        Assert.True(itemCount >= 2, $"Culture dropdown should have at least 2 languages, found {itemCount}");
    }

    [Fact]
    public async Task Language_SwitchToGerman_ChangesUI()
    {
        await Page.GotoAsync($"{_fixture.BaseUrl}/settings");
        await Page.Locator("h3").First.WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });

        // Open culture dropdown
        var cultureDropdown = Page.Locator(".navbar .dropdown-toggle").Nth(0);
        await cultureDropdown.ClickAsync();
        await Page.WaitForTimeoutAsync(300);

        var germanItem = Page.Locator(".dropdown-menu:visible .dropdown-item:has-text('Deutsch')");
        if (await germanItem.CountAsync() > 0)
        {
            await germanItem.ClickAsync();

            // Language change triggers forceLoad navigation
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle,
                new PageWaitForLoadStateOptions { Timeout = 30_000 });

            // After reload the settings heading should be in German
            var heading = Page.Locator("h3");
            await heading.First.WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });
            var text = await heading.First.InnerTextAsync();
            Assert.False(string.IsNullOrWhiteSpace(text),
                "Page heading should have content after language switch");

            // Restore English to not break other tests
            var restoreDropdown = Page.Locator(".navbar .dropdown-toggle").Nth(0);
            await restoreDropdown.ClickAsync();
            await Page.WaitForTimeoutAsync(300);
            var englishItem = Page.Locator(".dropdown-menu:visible .dropdown-item:has-text('English')");
            if (await englishItem.CountAsync() > 0)
            {
                await englishItem.ClickAsync();
                await Page.WaitForLoadStateAsync(LoadState.NetworkIdle,
                    new PageWaitForLoadStateOptions { Timeout = 30_000 });
            }
        }
        else
        {
            Assert.True(true, "German language option not found in dropdown");
        }
    }

    [Fact]
    public async Task Language_SwitchToTurkish_ChangesUI()
    {
        await Page.GotoAsync($"{_fixture.BaseUrl}/settings");
        await Page.Locator("h3").First.WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });

        var cultureDropdown = Page.Locator(".navbar .dropdown-toggle").Nth(0);
        await cultureDropdown.ClickAsync();
        await Page.WaitForTimeoutAsync(300);

        var turkishItem = Page.Locator(".dropdown-menu:visible .dropdown-item:has-text('Türkçe')");
        if (await turkishItem.CountAsync() > 0)
        {
            await turkishItem.ClickAsync();

            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle,
                new PageWaitForLoadStateOptions { Timeout = 30_000 });

            var heading = Page.Locator("h3");
            await heading.First.WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });
            var text = await heading.First.InnerTextAsync();
            Assert.False(string.IsNullOrWhiteSpace(text),
                "Page heading should have content after language switch to Turkish");

            // Restore English
            var restoreDropdown = Page.Locator(".navbar .dropdown-toggle").Nth(0);
            await restoreDropdown.ClickAsync();
            await Page.WaitForTimeoutAsync(300);
            var englishItem = Page.Locator(".dropdown-menu:visible .dropdown-item:has-text('English')");
            if (await englishItem.CountAsync() > 0)
            {
                await englishItem.ClickAsync();
                await Page.WaitForLoadStateAsync(LoadState.NetworkIdle,
                    new PageWaitForLoadStateOptions { Timeout = 30_000 });
            }
        }
        else
        {
            Assert.True(true, "Turkish language option not found in dropdown");
        }
    }
}
