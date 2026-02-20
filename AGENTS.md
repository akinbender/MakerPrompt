# MakerPrompt — Agent Guide

Cross-platform 3D printer control app (Blazor WASM + .NET MAUI, .NET 10).
See [CLAUDE.md](CLAUDE.md) for full architecture context before starting any task.

## Skill Routing

| Domain | Use these skills / agents |
|---|---|
| Blazor components, render modes, state | `dotnet-blazor-specialist`, `dotnet-blazor-patterns`, `dotnet-blazor-components` |
| MAUI platform-specific code | `dotnet-maui-specialist`, `dotnet-maui-development` |
| C# async/await, concurrency | `dotnet-csharp-async-patterns`, `dotnet-async-performance-specialist` |
| Dependency injection, service lifetime | `dotnet-csharp-dependency-injection` |
| Error handling, resilience | `dotnet-architecture-patterns`, `dotnet-blazor-patterns` |
| Localization / i18n | `dotnet-localization` |
| HTTP clients, retry, timeout | `dotnet-architecture-patterns` (HttpClient patterns section) |
| MSBuild / multi-targeting | `dotnet-multi-targeting`, `dotnet-msbuild-authoring` |
| Testing | `dotnet-testing-specialist`, `dotnet-xunit`, `dotnet-integration-testing` |
| Performance | `dotnet-performance-analyst`, `dotnet-benchmarkdotnet` |
| Security | `dotnet-security-reviewer` |
| Architecture decisions | `dotnet-architect` (central routing agent) |
| Code review | `dotnet-code-review-agent` |

## Project-Specific Routing

### Adding a new printer backend
1. Read `IPrinterCommunicationService` and `BasePrinterConnectionService` first.
2. Use `dotnet-blazor-specialist` for any UI surface.
3. Use `dotnet-csharp-async-patterns` for the polling/telemetry loop.
4. Backend must be registered in `RegisterMakerPromptSharedServices` — use `dotnet-csharp-dependency-injection`.

### UI / layout changes
- Agent: `dotnet-blazor-specialist`
- Constraints: additive only, use BlazorBootstrap, no new layout frameworks.
- CSS lives in `MakerPrompt.Shared/wwwroot/css/app.css` (flexbox layout, no Bootstrap grid for the shell).

### Error handling
- `GlobalErrorBoundary` + `ProcessError` are already in place.
- Do NOT add custom exception middleware or new logging providers.
- Agent: `dotnet-blazor-specialist` for component-level; `dotnet-architecture-patterns` for service-level.

### Platform-specific MAUI code
- Files: `SerialService.<Platform>.cs` pattern in `MakerPrompt.MAUI/Services/`.
- Agent: `dotnet-maui-specialist`.
- Must compile conditionally — do not break other platform targets.

### Localization / new strings
- Agent: `dotnet-localization`
- Add to `MakerPrompt.Shared/Properties/Resources.resx` first, then reference via `Resources.Key`.

## Quality Gates

Run before committing any code change:

```bash
dotnet build MakerPrompt.sln
dotnet test MakerPrompt.Tests/MakerPrompt.Tests.csproj
```

## Hard Constraints (enforce always)

- **Additive only** — do not refactor unless explicitly asked.
- **No stack traces in UI** — `ToastMessage` gets a friendly string, `ILogger` gets the full exception.
- **No telemetry spam** — background polling errors are swallowed silently.
- **No speculative features** — implement only what is explicitly requested.
- **One concern per change** — keep diffs small and reviewable.
