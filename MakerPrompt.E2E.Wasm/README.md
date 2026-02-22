# E2E Tests

Two cross-platform E2E test projects for MakerPrompt.

## MakerPrompt.E2E.Wasm — Playwright (Blazor WASM)

Headless browser tests against the Blazor WASM host using the **Demo** printer backend.

### Prerequisites

```
dotnet tool install --global Microsoft.Playwright.CLI
pwsh MakerPrompt.E2E.Wasm/bin/Debug/net10.0/playwright.ps1 install
```

### Run

```
dotnet test MakerPrompt.E2E.Wasm
```

| Variable | Default | Purpose |
|---|---|---|
| `E2E_BASE_URL` | `http://localhost:5059` | Blazor WASM dev server URL (fixture starts one automatically) |
| `E2E_HEADLESS` | `false` | Browser is **visible** by default. Set to `true` for CI. |

### Tests

| Test | What it verifies |
|---|---|
| `App_Boots_Without_Console_Errors` | No JS errors on startup |
| `Fleet_Page_Is_Default_Route` | Add Printer button visible at `/` |
| `Fleet_AddPrinter_Demo_Mode` | Add a Demo printer via modal |
| `Fleet_ConnectMockPrinter` | Connect → temperature indicators appear |
| `Fleet_TelemetryUpdates` | Temperature values rendered |
| `Fleet_DisconnectPrinter` | Disconnect → returns to disconnected state |

---

## MakerPrompt.E2E.Maui — Appium (MAUI Desktop)

Native UI tests for the Windows MAUI host.

### Prerequisites

1. **Node.js** — https://nodejs.org
2. **Appium** — `npm i -g appium`
3. **Windows driver** — `appium driver install --source=npm appium-windows-driver`
4. **WinAppDriver v1.2.1** — https://github.com/microsoft/WinAppDriver/releases/tag/v1.2.1
5. **Build the MAUI app for Windows** — `dotnet build MakerPrompt.MAUI -f net10.0-windows10.0.19041.0`

### Run

```
dotnet test MakerPrompt.E2E.Maui
```

| Variable | Default | Purpose |
|---|---|---|
| `MAUI_APP_PATH` | auto-detected | Path to `MakerPrompt.MAUI.exe` |

### Tests

| Test | What it verifies |
|---|---|
| `App_Launches_Successfully` | Window opens |
| `Window_Is_Displayed` | Positive width/height |
| `Window_Title_Contains_AppName` | Title includes "MakerPrompt" |
| `Content_Renders_Screenshot` | Screenshot saved for visual check |
| `Fleet_Button_Exists_In_AccessibilityTree` | Best-effort native accessibility lookup |
| `App_Responds_To_Window_Resize` | Window resizes without crash |

> **Note:** Deep WebView DOM automation is intentionally avoided. BlazorWebView
> content is verified via screenshots and native accessibility tree (best-effort).
