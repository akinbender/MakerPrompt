# Architecture

MakerPrompt is a .NET 10 application with two interactive clients and an
experimental remote-monitoring path. Production projects live under `src/`;
tests live under `tests/`.

## Project boundaries

```text
MakerPrompt.Core
├── MakerPrompt.Application
│   └── MakerPrompt.EdgeAgent
├── MakerPrompt.Infrastructure
│   ├── MakerPrompt.UI.Components
│   │   ├── MakerPrompt.UI.Blazor
│   │   └── MakerPrompt.UI.MAUI
│   ├── MakerPrompt.Cloud
│   └── MakerPrompt.EdgeAgent
└── MakerPrompt.Infrastructure.Sqlite
    ├── MakerPrompt.Cloud
    └── MakerPrompt.EdgeAgent
```

The diagram shows compile-time dependencies, not runtime data flow:

| Project | Responsibility |
| --- | --- |
| `MakerPrompt.Core` | Printer, telemetry, camera, farm, file, and queue models; host-independent contracts. It has no project dependencies. |
| `MakerPrompt.Application` | Cross-host connection lifecycle and fleet orchestration. It depends only on Core. |
| `MakerPrompt.Infrastructure` | Protocol adapters, serial base behavior, MJPEG capture, Cloud HTTP transport, and in-memory stores. |
| `MakerPrompt.Infrastructure.Sqlite` | Bounded durable implementations of the Core telemetry and camera-store contracts. |
| `MakerPrompt.UI.Components` | Shared Razor application, presentation models, UI state, local persistence workflows, localization, and shared client composition. |
| `MakerPrompt.UI.Blazor` | WebAssembly startup, Web Serial, browser storage, and browser-specific credential encoding. |
| `MakerPrompt.UI.MAUI` | Native BlazorWebView startup, platform serial implementations, native storage, camera proxying, and credential encryption. |
| `MakerPrompt.Cloud` | API-only telemetry/camera ingest and read host. It does not serve the interactive UI. |
| `MakerPrompt.EdgeAgent` | Outbound-only local worker that connects to printers, retains observations locally, and optionally forwards them to Cloud. |

Lower-level projects must not reference a UI or host. New host-independent
contracts and data types belong in Core; cross-host use-case coordination belongs
in Application; protocol and storage implementations belong in Infrastructure.
UI-only state and persistence remain in UI.Components.

## Interactive client composition

Both interactive clients render `MakerPrompt.UI.Components.App` and register the
shared services through
`RegisterMakerPromptSharedServices<P, L>()`. Each host supplies its own
`IAppConfigurationService`, serial implementation, and local storage provider.
MAUI additionally replaces the passthrough camera proxy with a native HTTP proxy
and uses AES-GCM credential protection; WebAssembly uses browser storage and a
compatibility encoding because browser WebCrypto is not available through the
same .NET API surface.

Printer protocol behavior is exposed through
`IPrinterCommunicationService`. Concrete network adapters and the reusable serial
base live in Infrastructure. The UI owns saved-printer/farm workflows and creates
an adapter for each managed printer. Connection instances are per printer and
must be disposed when replaced or removed.

## Cloud and Edge trust boundary

Cloud/Edge is deliberately a monitoring slice. It does not extend the local
control UI over the internet.

```text
local printer / camera
          │ local credentials
          ▼
   EdgeAgent process
   ├── reconnect + poll
   ├── bounded local SQLite history
   └── outbound HTTP(S) only
          │ telemetry / JPEG snapshots
          │ scoped JWT or pre-shared agent key
          ▼
      Cloud API
      ├── validate resource IDs and payloads
      ├── bounded SQLite history
      └── member-JWT read endpoints
```

The EdgeAgent does not listen for inbound requests and Cloud has no command,
printer-configuration, or Cloud-to-Edge control endpoint. Printer API credentials
remain in Edge configuration and are never part of telemetry or camera payloads.
Production deployments must terminate HTTPS; plain HTTP is supported only so the
two containers can communicate on an isolated development network.

### Implemented

- Edge reconnects configured HTTP printer adapters, polls telemetry and cameras,
  records bounded local history, and forwards observations when Cloud is configured.
- Cloud accepts agent JWTs carrying the `makerprompt:ingest` scope. For small
  deployments it also accepts a pre-shared bearer key whose SHA-256 digest is
  configured server-side.
- Cloud read endpoints require a validated member JWT. The health endpoint is
  anonymous.
- Cloud validates resource identifiers, normalizes timestamps, marks stale
  telemetry disconnected, limits request size, and validates JPEG framing.
- SQLite is the default durable store for both processes. In-memory Cloud storage
  is restricted to Development.

### Deliberately not implemented

- The standalone WebAssembly client has no Cloud data client. It continues to
  connect to printers from the browser and is not hosted by Cloud.
- There is no remote command/control channel.
- There is no tenant, makerspace, or per-resource authorization model; an
  authenticated member can currently read any known resource identifier.
- There is no agent enrollment, key rotation, revocation service, or durable
  outbound delivery queue. Forwarding is best-effort and a failed observation is
  not replayed from the local store.

Those omissions are security boundaries, not hidden capabilities. Adding remote
control or multi-tenant access requires an explicit protocol, ownership model,
authorization design, audit trail, and threat review.

## Cloud/Edge configuration

Cloud requires `MakerPrompt:Auth:Authority` and `Audience` outside Development.
Configure `MakerPrompt:Auth:AgentApiKeyHash` with a SHA-256 hex digest if using
the pre-shared agent-key option. The default store is
`data/makerprompt.db`; retention can be set with
`MakerPrompt:Storage:TelemetryRetention` and `CameraRetention`.

Edge reads printer definitions from `EdgeAgent:Printers`. Setting
`CloudApi:BaseUrl` also requires `CloudApi:ApiToken`; without a base URL it runs
locally without a Cloud client. Its default store is
`data/makerprompt-edge.db`.

The repository's `docker-compose.yml` is local-development scaffolding. Its
well-known default key is not a production secret.

## Tests and validation

- `MakerPrompt.Tests.Unit` covers domain, orchestration, adapters, UI services,
  persistence compatibility, and SQLite stores.
- `MakerPrompt.Tests.Integration` hosts the Cloud API in-process and verifies
  authentication, validation, ingest, and durable read behavior.
- `MakerPrompt.Tests.E2E.Wasm` drives the WebAssembly client with Playwright.
- `MakerPrompt.Tests.E2E.Maui` drives the Windows MAUI WebView2 through
  Playwright's Chrome DevTools Protocol connection.

Build individual targets on platforms without every MAUI workload; CI provides
separate browser, Cloud/Edge, Windows, Android, and Mac Catalyst jobs.
