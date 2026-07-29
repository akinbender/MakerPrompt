# MakerPrompt — Claude Code Instructions

MakerPrompt is a cross-platform 3D-printer control and monitoring application
targeting .NET 10. Read [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) before
changing project boundaries or Cloud/Edge behavior.

## Projects

| Layer | Project |
| --- | --- |
| Domain contracts and models | `src/MakerPrompt.Core` |
| Cross-host orchestration | `src/MakerPrompt.Application` |
| Printer/camera/transport adapters | `src/MakerPrompt.Infrastructure` |
| Durable telemetry/camera stores | `src/MakerPrompt.Infrastructure.Sqlite` |
| Shared Razor application | `src/MakerPrompt.UI.Components` |
| Browser host | `src/MakerPrompt.UI.Blazor` |
| Native host | `src/MakerPrompt.UI.MAUI` |
| Remote-monitoring API | `src/MakerPrompt.Cloud` |
| Outbound site worker | `src/MakerPrompt.EdgeAgent` |
| Automated tests | `tests/` |

Dependencies point inward. Core has no project references; Application depends
only on Core. Put protocol implementations in Infrastructure, not in Core or
Application. Keep browser and native code in their hosts.

## Composition

The interactive clients call
`RegisterMakerPromptSharedServices<P, L>()` in
`src/MakerPrompt.UI.Components/Utils/ServiceCollectionExtensions.cs`. Register
shared UI services there. Register browser-only services in
`src/MakerPrompt.UI.Blazor/Program.cs` and native services in
`src/MakerPrompt.UI.MAUI/MauiProgram.cs`.

Cloud and EdgeAgent are independent hosts with composition in their respective
`Program.cs` files. Do not make either one depend on UI.Components.

## Printer communication

- `src/MakerPrompt.Core/Abstractions/IPrinterCommunicationService.cs` is the
  single-printer contract.
- `BasePrinterConnectionService` in Infrastructure owns common connection state,
  events, cancellation, and reconnect behavior.
- `BaseSerialService` in UI.Components adds G-code parsing and command helpers;
  platform serial transports live in the Blazor and MAUI hosts.
- Multi-printer lifecycle belongs in Application or in UI-facing orchestration,
  not in a protocol adapter.
- Respect capability properties such as `SupportsDirectControl`,
  `SupportsCommandPrompt`, `SupportsPrintStart`, and `SupportsPrinterQueue`.
  Unsupported controls must not be presented as functional.
- A managed printer owns its adapter instance. Dispose failed, replaced, and
  removed connections.

Supported interactive backends are Demo, Web/native serial, Moonraker,
PrusaLink, Prusa Connect, and OctoPrint. Bambu LAN remains an incomplete
prototype and must not be advertised as a supported backend.

## UI conventions

- Shared components, layouts, pages, resources, and CSS live in
  `src/MakerPrompt.UI.Components`.
- Use BlazorBootstrap for existing modal, toast, and alert patterns.
- `GlobalErrorBoundary` handles unhandled component errors. `ProcessError`
  provides manual cascading error reporting.
- Show a localized, user-safe message in the UI and log diagnostic exception
  details through `ILogger`; never render stack traces.
- Add resource keys to `Properties/Resources.resx` before referencing them.
- Keep the existing flex layout and responsive behavior; avoid viewport-height
  rules that break the BlazorWebView shell.

## Persistence and security

- Core models describe runtime state. Versioned storage DTOs and compatibility
  migration belong in UI.Components persistence services.
- Preserve compatibility with existing nested connection JSON.
- Persist credentials through `IConnectionEncryptionService`; exports must be
  redacted. Browser encoding is not encryption and must not be described as one.
- Do not log credentials, bearer tokens, exported farm contents, or raw
  authorization headers.
- Cloud/Edge is monitoring-only. Do not add an inbound Edge endpoint or remote
  command path without an explicit security and authorization design.

## Quality gates

Use target-specific commands on systems without all MAUI workloads:

```bash
dotnet restore tests/MakerPrompt.Tests.Unit/MakerPrompt.Tests.Unit.csproj
dotnet build src/MakerPrompt.UI.Blazor/MakerPrompt.UI.Blazor.csproj -c Release
dotnet build src/MakerPrompt.Cloud/MakerPrompt.Cloud.csproj -c Release
dotnet build src/MakerPrompt.EdgeAgent/MakerPrompt.EdgeAgent.csproj -c Release
dotnet test tests/MakerPrompt.Tests.Unit/MakerPrompt.Tests.Unit.csproj -c Release
dotnet test tests/MakerPrompt.Tests.Integration/MakerPrompt.Tests.Integration.csproj -c Release
```

Build MAUI with an explicit target framework on the matching host. See
`tests/MakerPrompt.Tests.E2E.Wasm/README.md` for E2E prerequisites.

Prefer focused, reviewable changes. Preserve unrelated work in a dirty tree,
avoid speculative abstractions, and add or update tests for behavior changes.
