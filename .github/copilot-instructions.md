# Copilot Instructions

## Project Overview

MakerPrompt is a **cross-platform 3D printer control app** built on Blazor WASM + .NET MAUI, targeting .NET 10.

- `MakerPrompt.Shared` — Blazor Razor class library (all shared UI, services, models)
- `MakerPrompt.Blazor` — Blazor WASM browser host
- `MakerPrompt.MAUI` — .NET MAUI native host (BlazorWebView)
- `MakerPrompt.Tests` — xUnit test project

## Core Rules

- Prefer **additive changes**. Do not refactor unless explicitly asked.
- **Do not break existing printer backends**.
- Keep diffs small and reviewable. One concern per change.
- Do not add speculative features.

## Architecture

### Service Registration
All shared services register through `RegisterMakerPromptSharedServices<P,L>()` in
`MakerPrompt.Shared/Utils/ServiceCollectionExtensions.cs`.
New shared services go here. Host-specific services go in `Program.cs` (Blazor) or `MauiProgram.cs` (MAUI).

### Printer Backends
All printer logic flows through:
- `IPrinterCommunicationService` — the contract every backend implements
- `BasePrinterConnectionService` — base with shared telemetry, events, and connection state
- `BaseSerialService` — extends the base for serial/USB backends

Existing backends (do not break these):
| Backend | Class | Protocol |
|---|---|---|
| PrusaLink | `PrusaLinkApiService` | HTTP/JSON |
| Moonraker | `MoonrakerApiService` | WebSocket + HTTP |
| BambuLab | `BambuLabApiService` | proprietary |
| WebSerial | `WebSerialService` (Blazor only) | WebUSB/Serial via JS interop |
| Serial | `SerialService.<Platform>.cs` (MAUI) | Native per-platform |
| Demo | `DemoPrinterService` | in-memory |

Active backend resolved via `PrinterCommunicationServiceFactory` / `PrinterServiceFactory.Current`.

New backends must follow this pattern and register in `RegisterMakerPromptSharedServices`.
Telemetry must be async and **must not spam logs or command output** (swallow polling errors silently).

### Error Handling
- `GlobalErrorBoundary` wraps the `<Router>` in both hosts. Catches unhandled Blazor exceptions → `ILogger.LogError` + `ToastService.Notify` + `Recover()`.
- `ProcessError` provides a `[CascadingParameter]` `Handle(Exception)` for manual try/catch reporting.
- **Never** put stack traces in UI. Toast = friendly message. Logger = full exception.

### Layout
```
MainLayout
  ├─ <header> sticky navbar — NavConnection (printer dropdown)
  ├─ .layout-body (flex row, fills remaining height)
  │     ├─ NavMenu  — collapseable sidebar (220px expanded / 48px icon-only)
  │     └─ <main class="layout-main">
  │           └─ .layout-content
  │                 ├─ .layout-page → @Body
  │                 └─ .layout-right (CommandPrompt + GCodeViewer, hidden <992px)
  └─ <Toasts> BottomRight, AutoHide 4s
```

Sidebar collapse state: `_navCollapsed` bool in `MainLayout`, passed as `[Parameter] bool Collapsed` to `NavMenu`.
CSS layout uses flexbox (`flex: 1; min-height: 0`). Do NOT use `vh-100` / `min-vh-100` in the shell.
The `#app` element is scaled 80% via CSS transform — logical dimensions are 125% of viewport.

### UI Rules
- Use **BlazorBootstrap** for modals, toasts, and alerts.
- Toast: `ToastService.Notify(new ToastMessage(ToastType.X, "title", "message"))`.
- Conditional UI (buttons, tabs, cards) only when the feature is supported — check `IsConnected`, `IsPrinting`, etc.
- Bootstrap Icons (`bi bi-*`) are available for icons.

### Localization
- Strings: `MakerPrompt.Shared/Properties/Resources.resx` (and locale files).
- In Razor: `@inject IStringLocalizer<Resources> Localizer` → `@Localizer[Resources.SomeKey]`.
- Always add the key to the .resx file before using it.

### Storage / Config
- `IAppLocalStorageProvider` — localStorage (Blazor) / Preferences (MAUI).
- `IAppConfigurationService` / `AppConfiguration` — app-wide config, initialized on startup.

## Key Files

| File | Purpose |
|---|---|
| `MakerPrompt.Shared/Infrastructure/IPrinterCommunicationService.cs` | Backend contract |
| `MakerPrompt.Shared/Infrastructure/BasePrinterConnectionService.cs` | Backend base class |
| `MakerPrompt.Shared/Utils/ServiceCollectionExtensions.cs` | Shared DI registration |
| `MakerPrompt.Shared/Layout/MainLayout.razor` | App shell + sidebar toggle |
| `MakerPrompt.Shared/Layout/NavMenu.razor` | Collapseable sidebar |
| `MakerPrompt.Shared/Components/GlobalErrorBoundary.cs` | Global unhandled exception → toast |
| `MakerPrompt.Shared/Components/ProcessError.razor` | Cascading manual error handler |
| `MakerPrompt.Shared/wwwroot/css/app.css` | Flexbox layout, sidebar, theme CSS vars |
| `MakerPrompt.Blazor/App.razor` | Blazor WASM root (ProcessError > GlobalErrorBoundary > Router) |
| `MakerPrompt.MAUI/Components/Routes.razor` | MAUI root (same wrapping) |
