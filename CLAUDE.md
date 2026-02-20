# MakerPrompt — Claude Code Instructions

Cross-platform 3D printer control app. Blazor WASM + .NET MAUI hybrid, targeting .NET 10.

## Stack

| Layer | Technology |
|---|---|
| Shared UI | `MakerPrompt.Shared` — Blazor Razor class library |
| Web host | `MakerPrompt.Blazor` — Blazor WASM (browser) |
| Native host | `MakerPrompt.MAUI` — .NET MAUI BlazorWebView |
| UI components | BlazorBootstrap 3.5.0 |
| Localization | `IStringLocalizer<Resources>` + `.resx` files |
| Styling | Bootstrap 5 + custom `app.css` (80% scale via CSS transform on `#app`) |

## Architecture

### Service Registration
All shared services are registered via `RegisterMakerPromptSharedServices<P,L>()` in
`MakerPrompt.Shared/Utils/ServiceCollectionExtensions.cs`. Both hosts call this extension.
New shared services **must** be registered here, not in host-specific `Program.cs` / `MauiProgram.cs`.

### Printer Communication
All printer logic must flow through:
- `IPrinterCommunicationService` — the public contract
- `BasePrinterConnectionService` — base class with shared state, events, and helpers
- `BaseSerialService` — extends `BasePrinterConnectionService` for serial/USB backends

#### Existing Backends
| Backend | Protocol | Service Class |
|---|---|---|
| PrusaLink | HTTP/JSON | `PrusaLinkApiService` |
| Moonraker | WebSocket + HTTP | `MoonrakerApiService` |
| BambuLab | proprietary | `BambuLabApiService` |
| WebSerial (Blazor) | WebUSB/Serial via JS | `WebSerialService` |
| Serial (MAUI) | Native serial per platform | `SerialService.<Platform>.cs` |
| Demo | in-memory fake | `DemoPrinterService` |

Active backend is resolved via `PrinterCommunicationServiceFactory` → `PrinterServiceFactory.Current`.

### Error Handling
- `GlobalErrorBoundary` (`MakerPrompt.Shared/Components/`) — wraps `<Router>` in both hosts.
  Catches unhandled Blazor render/event exceptions, logs via `ILogger`, shows toast, calls `Recover()`.
- `ProcessError` component — cascading component, `Handle(Exception)` for manual try/catch reporting.
- **Never** expose stack traces to UI. Toast message = user-facing, log = full details.

### Layout
```
MainLayout
  ├─ <header> navbar (sticky-top)
  │     └─ NavConnection (printer connection dropdown)
  ├─ .layout-body (flex row)
  │     ├─ NavMenu (collapseable sidebar, 220px ↔ 48px icon strip)
  │     └─ <main class="layout-main">
  │           ├─ page title bar
  │           └─ .layout-content (flex row)
  │                 ├─ .layout-page  → @Body (page content)
  │                 └─ .layout-right (hidden <992px)
  │                       ├─ CommandPrompt (G-code terminal)
  │                       └─ GCodeViewer
  └─ <Toasts> (BottomRight, AutoHide 4s)
```

### UI Rules
- Use **BlazorBootstrap** for modals (`Modal`), toasts (`ToastService.Notify(new ToastMessage(...))`), and alerts.
- Conditional UI appears only when the feature is supported (check `IsConnected`, `IsPrinting`, etc.).
- Sidebar state: `_navCollapsed` bool in `MainLayout`, passed as `[Parameter]` to `NavMenu`.
- No `vh-100` / `min-vh-100` in layout — use flexbox `flex: 1; min-height: 0` instead.

### Localization
- String resources live in `MakerPrompt.Shared/Properties/Resources.resx`.
- Inject `IStringLocalizer<Resources> Localizer` or use `@Localizer[Resources.SomeKey]` in Razor.
- Add new keys to `Resources.resx` (and locale `.resx` files) before using them.

### Storage / Config
- `IAppLocalStorageProvider` — abstraction over localStorage (Blazor) / Preferences (MAUI).
- `IAppConfigurationService` — wraps `AppConfiguration` model, `InitializeAsync()` on startup.
- Platform implementations: `BlazorAppLocalStorageProvider`, `MauiAppLocalStorageProvider`.

## Constraints (Hard Rules)

- **Additive only.** Do not refactor existing code unless explicitly asked.
- **Do not break existing printer backends.**
- **No spammy telemetry logs.** Background polling errors are swallowed silently.
- **No stack traces in UI.** Toast = friendly message, Logger = full details.
- **No speculative features.** Only implement what is explicitly requested.
- **One concern per PR.** Keep diffs small and reviewable.

## Key Files

| Path | Purpose |
|---|---|
| `MakerPrompt.Shared/Infrastructure/IPrinterCommunicationService.cs` | Printer backend contract |
| `MakerPrompt.Shared/Infrastructure/BasePrinterConnectionService.cs` | Shared printer base |
| `MakerPrompt.Shared/Utils/ServiceCollectionExtensions.cs` | Shared DI registration |
| `MakerPrompt.Shared/Layout/MainLayout.razor` | App shell, sidebar toggle state |
| `MakerPrompt.Shared/Layout/NavMenu.razor` | Collapseable sidebar |
| `MakerPrompt.Shared/Components/GlobalErrorBoundary.cs` | Global error catch + toast |
| `MakerPrompt.Shared/Components/ProcessError.razor` | Manual error cascade |
| `MakerPrompt.Shared/wwwroot/css/app.css` | Layout flexbox + sidebar + theme vars |
| `MakerPrompt.Blazor/App.razor` | Blazor WASM root (ProcessError > GlobalErrorBoundary > Router) |
| `MakerPrompt.MAUI/Components/Routes.razor` | MAUI root (same wrapping) |
