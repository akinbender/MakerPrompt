# Copilot Instructions

MakerPrompt is a .NET 10 3D-printer control and monitoring application. Production
projects are under `src/`; tests are under `tests/`. Read
`docs/ARCHITECTURE.md` before changing dependencies or Cloud/Edge behavior.

## Boundaries

- `MakerPrompt.Core`: host-independent models and contracts; no project references.
- `MakerPrompt.Application`: cross-host orchestration; depends only on Core.
- `MakerPrompt.Infrastructure`: protocol, camera, and transport implementations.
- `MakerPrompt.Infrastructure.Sqlite`: durable Core store implementations.
- `MakerPrompt.UI.Components`: shared Razor application and UI workflows.
- `MakerPrompt.UI.Blazor` / `MakerPrompt.UI.MAUI`: platform composition.
- `MakerPrompt.Cloud`: authenticated, API-only remote monitoring.
- `MakerPrompt.EdgeAgent`: outbound local polling and forwarding.

Do not reintroduce the deleted root-level `MakerPrompt.Shared`,
`MakerPrompt.Blazor`, `MakerPrompt.MAUI`, or `MakerPrompt.Tests` projects.

## Implementation rules

- Printer backends implement
  `src/MakerPrompt.Core/Abstractions/IPrinterCommunicationService.cs`.
- Respect direct-control, command, print-start, and queue capability properties
  in both adapters and UI.
- Give every managed printer its own disposable adapter instance.
- Register shared interactive services through
  `src/MakerPrompt.UI.Components/Utils/ServiceCollectionExtensions.cs`; keep
  browser/native registrations in their host.
- Use BlazorBootstrap and existing error-boundary/toast patterns. Log exception
  details, but show only safe localized messages.
- Keep storage DTO migration compatible with existing saved configurations.
  Protect stored credentials and redact exports.
- Do not add inbound Edge endpoints, Cloud-to-Edge commands, or claims of
  multi-tenant isolation. Those capabilities are not implemented.
- Prefer focused changes with tests over broad stylistic rewrites.

## Validation

Build and test specific projects on hosts without every MAUI workload:

```bash
dotnet build src/MakerPrompt.UI.Blazor/MakerPrompt.UI.Blazor.csproj -c Release
dotnet build src/MakerPrompt.Cloud/MakerPrompt.Cloud.csproj -c Release
dotnet build src/MakerPrompt.EdgeAgent/MakerPrompt.EdgeAgent.csproj -c Release
dotnet test tests/MakerPrompt.Tests.Unit/MakerPrompt.Tests.Unit.csproj -c Release
dotnet test tests/MakerPrompt.Tests.Integration/MakerPrompt.Tests.Integration.csproj -c Release
```
