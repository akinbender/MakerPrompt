using System.Diagnostics;

namespace MakerPrompt.E2E.Maui.Fixtures;

/// <summary>
/// Manages the local Appium server process lifecycle.
/// Starts Appium before tests and stops it after.
/// Requires Node.js and Appium installed globally: npm i -g appium
/// Also requires the Windows driver: appium driver install --source=npm appium-windows-driver
/// </summary>
public static class AppiumServerHelper
{
    private static Process? _appiumProcess;

    public static void StartAppiumLocalServer()
    {
        if (_appiumProcess != null) return;

        _appiumProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "appium",
                Arguments = "--relaxed-security --base-path /wd/hub",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        _appiumProcess.Start();

        // Wait for Appium to be ready
        WaitForAppiumServer(TimeSpan.FromSeconds(30));
    }

    public static void DisposeAppiumLocalServer()
    {
        if (_appiumProcess == null) return;

        try
        {
            if (!_appiumProcess.HasExited)
            {
                _appiumProcess.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
        finally
        {
            _appiumProcess.Dispose();
            _appiumProcess = null;
        }
    }

    private static void WaitForAppiumServer(TimeSpan timeout)
    {
        using var client = new HttpClient();
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var response = client.GetAsync("http://127.0.0.1:4723/wd/hub/status").Result;
                if (response.IsSuccessStatusCode) return;
            }
            catch
            {
                // Not ready yet
            }
            Thread.Sleep(1000);
        }

        throw new TimeoutException("Appium server did not start within the timeout period.");
    }
}
