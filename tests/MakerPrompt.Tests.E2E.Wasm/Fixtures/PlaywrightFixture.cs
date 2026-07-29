using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.Playwright;

namespace MakerPrompt.Tests.E2E.Wasm.Fixtures;

/// <summary>
/// Shared fixture that starts the Blazor WASM dev server and a single Playwright browser.
/// One browser + one page is reused across all tests in the collection.
/// </summary>
public class PlaywrightFixture : IAsyncLifetime
{
    private readonly ConcurrentQueue<string> _serverOutput = new();
    private Process? _serverProcess;
    private Task? _stdoutTask;
    private Task? _stderrTask;
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
        var configuredBaseUrl = Environment.GetEnvironmentVariable("E2E_BASE_URL");
        if (!string.IsNullOrWhiteSpace(configuredBaseUrl))
        {
            BaseUrl = configuredBaseUrl.TrimEnd('/');
        }
        else
        {
            BaseUrl = ReserveLoopbackUrl();
            StartServer(BaseUrl);
        }

        await WaitForServerAsync(BaseUrl, TimeSpan.FromSeconds(90));

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
        try
        {
            if (_context != null)
                await _context.DisposeAsync();
            if (_browser != null)
                await _browser.DisposeAsync();
            _playwright?.Dispose();
        }
        finally
        {
            if (_serverProcess != null)
            {
                try
                {
                    if (!_serverProcess.HasExited)
                    {
                        _serverProcess.Kill(entireProcessTree: true);
                        await _serverProcess.WaitForExitAsync();
                    }

                    if (_stdoutTask != null)
                        await _stdoutTask;
                    if (_stderrTask != null)
                        await _stderrTask;
                }
                finally
                {
                    _serverProcess.Dispose();
                }
            }
        }
    }

    private void StartServer(string baseUrl)
    {
        var projectPath = FindProjectPath();
        var startInfo = new ProcessStartInfo
        {
            FileName = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(projectPath);
        startInfo.ArgumentList.Add("--urls");
        startInfo.ArgumentList.Add(baseUrl);
        startInfo.ArgumentList.Add("--no-launch-profile");
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";

        _serverProcess = new Process { StartInfo = startInfo };
        if (!_serverProcess.Start())
            throw new InvalidOperationException("Failed to start the Blazor WASM dev server.");

        _stdoutTask = DrainOutputAsync(_serverProcess.StandardOutput, "stdout");
        _stderrTask = DrainOutputAsync(_serverProcess.StandardError, "stderr");
    }

    private async Task DrainOutputAsync(StreamReader reader, string source)
    {
        while (await reader.ReadLineAsync() is { } line)
            _serverOutput.Enqueue($"[{source}] {line}");
    }

    private async Task WaitForServerAsync(string url, TimeSpan timeout)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (_serverProcess?.HasExited == true)
            {
                throw new InvalidOperationException(
                    $"Blazor WASM server exited with code {_serverProcess.ExitCode} " +
                    $"before responding at {url}.{FormatServerOutput()}");
            }

            try
            {
                using var response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                    return;
            }
            catch (HttpRequestException)
            {
                // The server is still starting.
            }
            catch (TaskCanceledException)
            {
                // The individual probe timed out; retry until the fixture timeout.
            }

            await Task.Delay(500);
        }

        throw new TimeoutException(
            $"Blazor WASM server did not respond within {timeout.TotalSeconds}s at {url}." +
            FormatServerOutput());
    }

    private string FormatServerOutput()
    {
        var lines = _serverOutput.ToArray();
        if (lines.Length == 0)
            return string.Empty;

        return Environment.NewLine
            + "Last server output:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, lines.TakeLast(100));
    }

    private static string ReserveLoopbackUrl()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            return $"http://127.0.0.1:{port}";
        }
        finally
        {
            listener.Stop();
        }
    }

    private static string FindProjectPath()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            var candidate = Path.Combine(
                dir,
                "src",
                "MakerPrompt.UI.Blazor",
                "MakerPrompt.UI.Blazor.csproj");
            if (File.Exists(candidate))
                return candidate;

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new FileNotFoundException(
            "Could not find MakerPrompt.UI.Blazor.csproj. " +
            "Run tests from the solution root or set E2E_BASE_URL to a running instance.");
    }
}

[CollectionDefinition("Playwright")]
public class PlaywrightCollection : ICollectionFixture<PlaywrightFixture> { }
