using System.Diagnostics;
using Microsoft.Playwright;

namespace MakerPrompt.E2E.Maui.Fixtures;

/// <summary>
/// xUnit collection fixture that launches the MAUI app as a native process and
/// connects Playwright to the embedded WebView2 via Chrome DevTools Protocol (CDP).
///
/// No Appium, WinAppDriver, or Node.js required — just the built MAUI app and
/// Playwright browsers.
///
/// Prerequisites:
///   1. MAUI app built for Windows in DEBUG (enables --remote-debugging-port=9222)
///   2. Playwright browsers installed: pwsh playwright.ps1 install chromium
///
/// Set MAUI_APP_PATH env var to override auto-detection.
/// </summary>
public class AppiumSetup : IAsyncLifetime
{
    private static Process? _appProcess;
    private static IPlaywright? _playwright;
    private static IBrowser? _cdpBrowser;

    /// <summary>CDP port that the MAUI app's WebView2 listens on (DEBUG builds).</summary>
    public const int CdpPort = 9222;

    /// <summary>
    /// Base URL detected from the actual WebView2 page origin.
    /// Typically https://0.0.0.0 for MAUI BlazorWebView on Windows.
    /// </summary>
    public static string BaseUrl { get; private set; } = "https://0.0.0.0";

    /// <summary>
    /// Playwright page connected to the WebView2 inside the MAUI app.
    /// Use this for all DOM interaction.
    /// </summary>
    public static IPage Page { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        // Kill any leftover MAUI app that may be hogging the CDP port
        KillExistingAppInstances();

        var appPath = ResolveAppPath();

        _appProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = appPath,
                WorkingDirectory = Path.GetDirectoryName(appPath)!,
                UseShellExecute = false,
                CreateNoWindow = false
            }
        };
        _appProcess.Start();

        // Give the MAUI app time to initialize Blazor WebView and open the CDP port
        await Task.Delay(8_000);

        // Connect Playwright to WebView2 via Chrome DevTools Protocol.
        // The MAUI app enables --remote-debugging-port in DEBUG builds
        // (set via WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS in MauiProgram.cs).
        _playwright = await Playwright.CreateAsync();
        _cdpBrowser = await ConnectCdpWithRetryAsync();

        // Find the actual Blazor page by URL — do NOT create a new blank page,
        // which would have no access to BlazorWebView's virtual file server.
        Page = await FindBlazorPageAsync();

        // Derive base URL from the actual page origin
        var pageUri = new Uri(Page.Url);
        BaseUrl = $"{pageUri.Scheme}://{pageUri.Authority}";

        // Ensure Blazor has fully loaded before any test runs
        await Page.WaitForSelectorAsync(".sidebar", new PageWaitForSelectorOptions
        {
            Timeout = 30_000
        });
    }

    public async Task DisposeAsync()
    {
        if (_cdpBrowser != null) await _cdpBrowser.CloseAsync();
        _playwright?.Dispose();

        if (_appProcess != null)
        {
            try
            {
                if (!_appProcess.HasExited)
                    _appProcess.Kill(entireProcessTree: true);
            }
            catch { /* best effort cleanup */ }
            finally
            {
                _appProcess.Dispose();
                _appProcess = null;
            }
        }
    }

    /// <summary>
    /// Navigates the Blazor app to the given relative path using client-side routing.
    /// Uses the History API + popstate event to trigger Blazor's router without
    /// a full WebView2 page reload. A full GotoAsync would destroy the Blazor circuit
    /// because blazor.webview.js has autostart="false" and the BlazorWebView handler
    /// may not reinitialize Blazor after a CDP-triggered navigation.
    /// </summary>
    public static async Task NavigateAsync(string relativePath)
    {
        var path = relativePath.StartsWith("/") ? relativePath : $"/{relativePath}";

        // pushState changes the URL; dispatching popstate notifies Blazor's
        // NavigationManager which re-evaluates the route through the Router component.
        await Page.EvaluateAsync(@$"
            (() => {{
                history.pushState(null, '', '{path}');
                window.dispatchEvent(new PopStateEvent('popstate'));
            }})()
        ");

        // Give Blazor's router time to process the route change and render
        await Page.WaitForTimeoutAsync(500);
    }

    /// <summary>
    /// Reloads the current page and waits for Blazor to fully re-render.
    /// </summary>
    public static async Task ReloadAsync()
    {
        await Page.ReloadAsync(new PageReloadOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = 15_000
        });

        // After reload, wait for Blazor to reinitialize and render the sidebar
        await Page.WaitForSelectorAsync(".sidebar", new PageWaitForSelectorOptions
        {
            Timeout = 30_000
        });
    }

    /// <summary>
    /// Retries the CDP connection until WebView2 is ready.
    /// </summary>
    private static async Task<IBrowser> ConnectCdpWithRetryAsync()
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                return await _playwright!.Chromium.ConnectOverCDPAsync(
                    $"http://localhost:{CdpPort}");
            }
            catch
            {
                await Task.Delay(1_000);
            }
        }
        throw new TimeoutException(
            $"Could not connect Playwright to WebView2 CDP on port {CdpPort}.\n" +
            "Ensure the MAUI app is built in DEBUG mode (enables --remote-debugging-port).");
    }

    /// <summary>
    /// Waits for the Blazor content page to appear in the WebView2 CDP targets.
    /// The page may not be immediately available after CDP connects because
    /// WebView2 creates it asynchronously during BlazorWebView initialization.
    /// Never creates a new blank page — that page would have no virtual file server.
    /// </summary>
    private static async Task<IPage> FindBlazorPageAsync()
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);

        while (DateTime.UtcNow < deadline)
        {
            foreach (var context in _cdpBrowser!.Contexts)
            {
                // Look for the page at the BlazorWebView origin (https://0.0.0.0)
                var blazorPage = context.Pages.FirstOrDefault(p =>
                    p.Url.StartsWith("https://0.0.0.0", StringComparison.OrdinalIgnoreCase));
                if (blazorPage != null)
                    return blazorPage;

                // Fallback: any HTTPS page that isn't devtools or blank
                var httpsPage = context.Pages.FirstOrDefault(p =>
                    p.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
                    !p.Url.Contains("devtools", StringComparison.OrdinalIgnoreCase));
                if (httpsPage != null)
                    return httpsPage;
            }

            // Page not ready yet — wait and retry
            await Task.Delay(500);
        }

        // Collect diagnostic info for the error message
        var allPages = _cdpBrowser!.Contexts
            .SelectMany(c => c.Pages)
            .Select(p => p.Url)
            .ToList();

        throw new TimeoutException(
            $"No Blazor page found in WebView2 after 30 seconds.\n" +
            $"Browser contexts: {_cdpBrowser.Contexts.Count}, " +
            $"Pages found: [{string.Join(", ", allPages.Select(u => $"'{u}'"))}].\n" +
            "Ensure the MAUI app is built in DEBUG mode and the BlazorWebView loaded.");
    }

    private static string ResolveAppPath()
    {
        // Allow override via environment variable
        var envPath = Environment.GetEnvironmentVariable("MAUI_APP_PATH");
        if (!string.IsNullOrEmpty(envPath) && File.Exists(envPath))
            return envPath;

        // Auto-detect from build output.
        // Visual Studio and `dotnet build` place the exe directly in the TFM
        // folder (no RID subfolder) unless built with an explicit -r flag.
        // Check both layouts: TFM-only and TFM/win-x64.
        var tfms = new[] { "net10.0-windows10.0.19041.0", "net10.0-windows10.0.22000.0" };
        var configs = new[] { "Debug", "Release" };

        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            var mauiBin = Path.Combine(dir, "MakerPrompt.MAUI", "bin");
            if (Directory.Exists(mauiBin))
            {
                foreach (var config in configs)
                {
                    foreach (var tfm in tfms)
                    {
                        // TFM-only (default VS / dotnet build output)
                        var direct = Path.Combine(mauiBin, config, tfm, "MakerPrompt.MAUI.exe");
                        if (File.Exists(direct)) return direct;

                        // TFM + RID (dotnet build -r win-x64)
                        var withRid = Path.Combine(mauiBin, config, tfm, "win-x64", "MakerPrompt.MAUI.exe");
                        if (File.Exists(withRid)) return withRid;
                    }
                }
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new FileNotFoundException(
            "Could not find MakerPrompt.MAUI.exe. " +
            "Build the MAUI project for Windows first:\n" +
            "  dotnet build MakerPrompt.MAUI -f net10.0-windows10.0.19041.0\n" +
            "Or set MAUI_APP_PATH environment variable to the built exe path.");
    }

    /// <summary>
    /// Kills any leftover MakerPrompt.MAUI processes that could be holding
    /// the CDP port from a previous test run.
    /// </summary>
    private static void KillExistingAppInstances()
    {
        try
        {
            foreach (var proc in Process.GetProcessesByName("MakerPrompt.MAUI"))
            {
                proc.Kill(entireProcessTree: true);
                proc.Dispose();
            }
        }
        catch { /* best effort */ }
    }
}

[CollectionDefinition("Appium")]
public class AppiumCollection : ICollectionFixture<AppiumSetup> { }
