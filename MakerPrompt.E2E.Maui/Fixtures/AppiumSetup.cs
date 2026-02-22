using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;

namespace MakerPrompt.E2E.Maui.Fixtures;

/// <summary>
/// xUnit collection fixture that starts the Appium server and Windows driver.
/// Follows the official .NET MAUI UI testing guidance with Appium.
///
/// Prerequisites:
///   1. Node.js installed
///   2. Appium installed: npm i -g appium
///   3. Windows driver: appium driver install --source=npm appium-windows-driver
///   4. WinAppDriver v1.2.1 installed
///   5. MAUI app built for Windows
///
/// Set MAUI_APP_PATH env var to the built exe path, or it will be auto-detected.
/// </summary>
public class AppiumSetup : IAsyncLifetime
{
    private static WindowsDriver? _driver;

    public static WindowsDriver App => _driver ?? throw new InvalidOperationException(
        "WindowsDriver is not initialized. Ensure AppiumSetup fixture is configured.");

    public Task InitializeAsync()
    {
        AppiumServerHelper.StartAppiumLocalServer();

        var appPath = ResolveAppPath();

        var windowsOptions = new AppiumOptions
        {
            AutomationName = "windows",
            PlatformName = "Windows",
            App = appPath
        };

        // Longer implicit wait for BlazorWebView content to render
        windowsOptions.AddAdditionalAppiumOption("ms:waitForAppLaunch", "10");

        _driver = new WindowsDriver(
            new Uri("http://127.0.0.1:4723/wd/hub"),
            windowsOptions,
            TimeSpan.FromSeconds(30));

        // Give the app time to initialize Blazor WebView
        Thread.Sleep(5000);

        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _driver?.Quit();
        _driver = null;

        AppiumServerHelper.DisposeAppiumLocalServer();

        return Task.CompletedTask;
    }

    private static string ResolveAppPath()
    {
        // Allow override via environment variable
        var envPath = Environment.GetEnvironmentVariable("MAUI_APP_PATH");
        if (!string.IsNullOrEmpty(envPath) && File.Exists(envPath))
            return envPath;

        // Auto-detect from build output
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            // Look for the MAUI Windows build output
            var candidates = new[]
            {
                Path.Combine(dir, "MakerPrompt.MAUI", "bin", "Debug", "net10.0-windows10.0.19041.0", "MakerPrompt.MAUI.exe"),
                Path.Combine(dir, "MakerPrompt.MAUI", "bin", "Debug", "net10.0-windows10.0.22000.0", "MakerPrompt.MAUI.exe"),
                Path.Combine(dir, "MakerPrompt.MAUI", "bin", "Release", "net10.0-windows10.0.19041.0", "MakerPrompt.MAUI.exe"),
                Path.Combine(dir, "MakerPrompt.MAUI", "bin", "Release", "net10.0-windows10.0.22000.0", "MakerPrompt.MAUI.exe"),
            };

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate)) return candidate;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new FileNotFoundException(
            "Could not find MakerPrompt.MAUI.exe. " +
            "Build the MAUI project for Windows first, or set MAUI_APP_PATH environment variable.");
    }
}

[CollectionDefinition("Appium")]
public class AppiumCollection : ICollectionFixture<AppiumSetup> { }
