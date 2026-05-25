using System.Diagnostics;
using Microsoft.Playwright;

namespace MakerPrompt.E2E.Wasm.Fixtures;

/// <summary>
/// Shared fixture that starts the Blazor WASM dev server and a single Playwright browser.
/// One browser + one page is reused across all tests in the collection.
/// </summary>
public class PlaywrightFixture : IAsyncLifetime
{
    private Process? _serverProcess;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _context;

    /// <summary>
    /// Base URL of the running Blazor WASM dev server.
    /// Override via E2E_BASE_URL environment variable.
    /// </summary>
    public string BaseUrl { get; private set; } = null!;

    /// <summary>
    /// Single shared page reused across all tests. Navigate to BaseUrl at
    /// the start of each test to reset state.
    /// </summary>
    public IPage Page { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        BaseUrl = Environment.GetEnvironmentVariable("E2E_BASE_URL") ?? "http://localhost:5059";

        // Start the Blazor WASM dev server
        var projectPath = FindProjectPath();
        _serverProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --project \"{projectPath}\" --urls {BaseUrl} --no-launch-profile",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };
        _serverProcess.StartInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        _serverProcess.Start();

        // Wait for the server to respond
        await WaitForServerAsync(BaseUrl, TimeSpan.FromSeconds(90));

        // Create Playwright — one browser, one context, one page for the entire run
        var headless = Environment.GetEnvironmentVariable("E2E_HEADLESS") == "true";
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = headless,
            SlowMo = headless ? 0 : 300
        });
        _context = await _browser.NewContextAsync();
        Page = await _context.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        if (_context != null) await _context.DisposeAsync();
        if (_browser != null) await _browser.DisposeAsync();
        _playwright?.Dispose();

        if (_serverProcess != null && !_serverProcess.HasExited)
        {
            _serverProcess.Kill(entireProcessTree: true);
            _serverProcess.Dispose();
        }
    }

    private static async Task WaitForServerAsync(string url, TimeSpan timeout)
    {
        using var client = new HttpClient();
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode) return;
            }
            catch
            {
                // Server not ready yet
            }
            await Task.Delay(1000);
        }
        throw new TimeoutException($"Blazor WASM server did not start within {timeout.TotalSeconds}s at {url}");
    }

    private static string FindProjectPath()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            var candidate = Path.Combine(dir, "MakerPrompt.Blazor", "MakerPrompt.Blazor.csproj");
            if (File.Exists(candidate)) return candidate;
            dir = Directory.GetParent(dir)?.FullName;
        }
        throw new FileNotFoundException(
            "Could not find MakerPrompt.Blazor.csproj. " +
            "Run tests from the solution root or set E2E_BASE_URL to a running instance.");
    }
}

[CollectionDefinition("Playwright")]
public class PlaywrightCollection : ICollectionFixture<PlaywrightFixture> { }
