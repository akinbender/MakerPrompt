# End-to-end tests

MakerPrompt has Playwright suites for the WebAssembly client and the Windows MAUI
BlazorWebView. Always target one test project explicitly; the two suites have
different prerequisites.

## WebAssembly

The WASM fixture starts `src/MakerPrompt.UI.Blazor` on an available loopback port,
waits for it to become ready, and drives Chromium with the Demo backend.

```bash
dotnet build tests/MakerPrompt.Tests.E2E.Wasm/MakerPrompt.Tests.E2E.Wasm.csproj
pwsh tests/MakerPrompt.Tests.E2E.Wasm/bin/Debug/net10.0/playwright.ps1 \
  install chromium
dotnet test tests/MakerPrompt.Tests.E2E.Wasm/MakerPrompt.Tests.E2E.Wasm.csproj
```

| Variable | Default | Purpose |
| --- | --- | --- |
| `E2E_BASE_URL` | fixture-managed loopback URL | Use an already running client instead of starting one. |
| `E2E_HEADLESS` | `false` | Set to `true` for headless CI execution. |
| `DOTNET_HOST_PATH` | `dotnet` | Select the .NET host used to start the client. |

The suite covers application boot, static pages, farm-mode routing and
persistence, theme/language behavior, and the add/connect/telemetry/disconnect
fleet workflow.

## Windows MAUI

The MAUI suite launches a Debug build of the unpackaged Windows app, then connects
Playwright to its embedded WebView2 over Chrome DevTools Protocol. It does not use
Appium or WinAppDriver.

Prerequisites:

- Windows with the .NET 10 MAUI Windows workload and WebView2.
- A Debug MAUI build. Debug enables the fixture's CDP port.
- The Playwright Chromium support files installed for the test project.

```powershell
dotnet workload install maui-windows
dotnet build src/MakerPrompt.UI.MAUI/MakerPrompt.UI.MAUI.csproj `
  -c Debug `
  -f net10.0-windows10.0.19041.0 `
  -p:RuntimeIdentifierOverride=win-x64

dotnet build tests/MakerPrompt.Tests.E2E.Maui/MakerPrompt.Tests.E2E.Maui.csproj
pwsh tests/MakerPrompt.Tests.E2E.Maui/bin/Debug/net10.0/playwright.ps1 `
  install chromium
dotnet test tests/MakerPrompt.Tests.E2E.Maui/MakerPrompt.Tests.E2E.Maui.csproj
```

Set `MAUI_APP_PATH` when the executable is outside the usual
`src/MakerPrompt.UI.MAUI/bin/<Configuration>/<TFM>[/<RID>]` output path. The value
must point to `MakerPrompt.UI.MAUI.exe`.

The suite covers application boot, navigation, settings, farm mode, themes and
languages, and the same Demo fleet workflow as the WASM suite.

Both projects use a shared browser/page fixture and should not be run in parallel
with another invocation of the same suite. The MAUI suite also requires exclusive
access to TCP port 9222.
