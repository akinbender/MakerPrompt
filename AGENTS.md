# MakerPrompt — Agent Guide

MakerPrompt is a .NET 10 Blazor WebAssembly and MAUI application with an
experimental Cloud/Edge monitoring path. Start with
[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md); implementation conventions are in
[CLAUDE.md](CLAUDE.md).

## Change routing

| Change | Primary location |
| --- | --- |
| Domain model or host-independent contract | `src/MakerPrompt.Core` |
| Cross-host use-case orchestration | `src/MakerPrompt.Application` |
| Printer protocol, MJPEG, or HTTP transport | `src/MakerPrompt.Infrastructure` |
| SQLite telemetry/camera persistence | `src/MakerPrompt.Infrastructure.Sqlite` |
| Razor UI, localization, UI state, saved farms/projects | `src/MakerPrompt.UI.Components` |
| Browser serial/storage/bootstrap | `src/MakerPrompt.UI.Blazor` |
| MAUI serial/storage/platform behavior | `src/MakerPrompt.UI.MAUI` |
| Authenticated monitoring API | `src/MakerPrompt.Cloud` |
| Local polling and outbound forwarding | `src/MakerPrompt.EdgeAgent` |
| Tests | matching project under `tests/` |

New dependencies must preserve the direction documented in the architecture
guide. Do not reintroduce the deleted root-level `MakerPrompt.Shared`,
`MakerPrompt.Blazor`, `MakerPrompt.MAUI`, or `MakerPrompt.Tests` projects.

## Working rules

- Read the contract and its existing adapters before changing a printer backend.
- Preserve cancellation, disposal, per-printer ownership, persistence
  compatibility, and capability gating.
- Keep platform-specific code conditional and build an explicit MAUI target.
- Put user-facing strings in resources; keep diagnostic details in structured
  logs and out of toasts.
- Treat farm exports, printer credentials, Cloud tokens, and browser storage as
  sensitive.
- Cloud/Edge remains outbound, read-only monitoring. Remote control and
  multi-tenant access require a separately reviewed design.
- Avoid unrelated formatting churn and obsolete compatibility shims.

## Quality gates

```bash
dotnet build src/MakerPrompt.UI.Blazor/MakerPrompt.UI.Blazor.csproj -c Release
dotnet build src/MakerPrompt.Cloud/MakerPrompt.Cloud.csproj -c Release
dotnet build src/MakerPrompt.EdgeAgent/MakerPrompt.EdgeAgent.csproj -c Release
dotnet test tests/MakerPrompt.Tests.Unit/MakerPrompt.Tests.Unit.csproj -c Release
dotnet test tests/MakerPrompt.Tests.Integration/MakerPrompt.Tests.Integration.csproj -c Release
```

The solution includes several MAUI targets. A whole-solution build is only a
valid gate on a machine with the corresponding workloads. Browser and MAUI E2E
instructions live in
`tests/MakerPrompt.Tests.E2E.Wasm/README.md`.
