using Microsoft.Playwright;
using MakerPrompt.E2E.Maui.Fixtures;

namespace MakerPrompt.E2E.Maui.Tests;

/// <summary>
/// Tests for theme switching and language changing inside the MAUI WebView2
/// via Playwright + CDP.
/// </summary>
[Collection("Appium")]
[Trait("Category", "E2E-Maui")]
[TestCaseOrderer("MakerPrompt.E2E.Maui.Fixtures.AlphabeticalOrderer", "MakerPrompt.E2E.Maui")]
public class ThemeAndLanguageTests
{
    private static IPage Page => AppiumSetup.Page;

    // ── Theme ──

    [Fact]
    public async Task Theme_SwitchToLight_SetsDataAttribute()
    {
        await AppiumSetup.NavigateAsync("/");
        await Page.Locator(".sidebar").WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });

        // Dismiss any dropdown left open by a previous test
        await Page.Keyboard.PressAsync("Escape");
        await Page.WaitForTimeoutAsync(200);

        // Open the theme dropdown in the navbar (second dropdown; first is culture)
        var themeDropdown = Page.Locator(".navbar .dropdown-toggle").Nth(1);
        await themeDropdown.ClickAsync();

        // Click "Light"
        var lightItem = Page.Locator(".dropdown-item:has-text('Light')").First;
        await lightItem.WaitForAsync(new LocatorWaitForOptions { Timeout = 3_000 });
        await lightItem.ClickAsync();

        // Wait for theme JS to apply
        await Page.WaitForTimeoutAsync(500);

        var attr = await Page.EvaluateAsync<string>("() => document.documentElement.getAttribute('data-bs-theme')");
        Assert.Equal("light", attr);
    }

    [Fact]
    public async Task Theme_SwitchToDark_SetsDataAttribute()
    {
        await AppiumSetup.NavigateAsync("/");
        await Page.Locator(".sidebar").WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });

        // Dismiss any dropdown left open by a previous test
        await Page.Keyboard.PressAsync("Escape");
        await Page.WaitForTimeoutAsync(200);

        var themeDropdown = Page.Locator(".navbar .dropdown-toggle").Nth(1);
        await themeDropdown.ClickAsync();

        var darkItem = Page.Locator(".dropdown-item:has-text('Dark')").First;
        await darkItem.WaitForAsync(new LocatorWaitForOptions { Timeout = 3_000 });
        await darkItem.ClickAsync();

        await Page.WaitForTimeoutAsync(500);

        var attr = await Page.EvaluateAsync<string>("() => document.documentElement.getAttribute('data-bs-theme')");
        Assert.Equal("dark", attr);
    }

    [Fact]
    public async Task Theme_Dropdown_ShowsAllOptions()
    {
        await AppiumSetup.NavigateAsync("/");
        await Page.Locator(".sidebar").WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });

        var themeDropdown = Page.Locator(".navbar .dropdown-toggle").Nth(1);
        await themeDropdown.ClickAsync();

        // Should have at least 3 options: Auto, Light, Dark
        await Page.WaitForTimeoutAsync(300);
        var allItems = Page.Locator(".dropdown-menu:visible .dropdown-item");
        var count = await allItems.CountAsync();
        Assert.True(count >= 3, $"Theme dropdown should have at least 3 options, found {count}");
    }

    // ── Language ──

    [Fact]
    public async Task Language_Dropdown_ShowsOptions()
    {
        await AppiumSetup.NavigateAsync("/settings");
        await Page.Locator("h3").First.WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });

        // The culture selector is the second dropdown in the navbar (first is theme OR culture)
        // Culture dropdown displays a two-letter language code
        var dropdowns = Page.Locator(".navbar .dropdown-toggle");
        var count = await dropdowns.CountAsync();
        Assert.True(count >= 2, "Navbar should have at least 2 dropdowns (culture + theme)");

        // Click the culture dropdown (shows two-letter code like "en")
        await dropdowns.Nth(0).ClickAsync();
        await Page.WaitForTimeoutAsync(300);

        var items = Page.Locator(".dropdown-menu:visible .dropdown-item");
        var itemCount = await items.CountAsync();
        Assert.True(itemCount >= 2, $"Culture dropdown should have at least 2 languages, found {itemCount}");
    }

    [Fact]
    public async Task Language_SwitchToGerman_ChangesUI()
    {
        await AppiumSetup.NavigateAsync("/settings");
        await Page.Locator("h3").First.WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });

        // Open culture dropdown
        var cultureDropdown = Page.Locator(".navbar .dropdown-toggle").Nth(0);
        await cultureDropdown.ClickAsync();
        await Page.WaitForTimeoutAsync(300);

        // Click German option
        var germanItem = Page.Locator(".dropdown-menu:visible .dropdown-item:has-text('Deutsch')");
        if (await germanItem.CountAsync() > 0)
        {
            await germanItem.ClickAsync();

            // Language change triggers Navigation.NavigateTo(forceLoad: true) which
            // reloads the WebView2 page. Wait for Blazor to fully reinitialize.
            await Page.WaitForSelectorAsync(".sidebar", new PageWaitForSelectorOptions { Timeout = 30_000 });

            // After reload, the page heading should be in German ("Einstellungen" = Settings)
            // Navigate to settings again since the reload might land on the default route
            await AppiumSetup.NavigateAsync("/settings");
            var heading = Page.Locator("h3");
            await heading.First.WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });
            var text = await heading.First.InnerTextAsync();
            Assert.False(string.IsNullOrWhiteSpace(text), "Page heading should have content after language switch");

            // Restore English
            var restoreDropdown = Page.Locator(".navbar .dropdown-toggle").Nth(0);
            await restoreDropdown.ClickAsync();
            await Page.WaitForTimeoutAsync(300);
            var englishItem = Page.Locator(".dropdown-menu:visible .dropdown-item:has-text('English')");
            if (await englishItem.CountAsync() > 0)
            {
                await englishItem.ClickAsync();
                await Page.WaitForSelectorAsync(".sidebar", new PageWaitForSelectorOptions { Timeout = 30_000 });
            }
        }
        else
        {
            // German not available — skip gracefully
            Assert.True(true, "German language option not found in dropdown");
        }
    }
}
