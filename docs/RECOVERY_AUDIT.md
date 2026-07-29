# Recovery Audit

This document records the repository state and integration decisions made before
the interrupted architecture restructure was applied to the `main` working tree.
The local Git history and Desktop export were treated as the source of truth.
It is an audit record, not a description of the recovered tree; see
[Architecture](ARCHITECTURE.md) for the resulting design.

## Audited states

| State | Provenance | Finding |
| --- | --- | --- |
| Stable baseline | `main` at `966e31d` | Builds and passes 147 unit tests, with one Razor warning caused by a stale `ProjectHub` reference. |
| Interrupted restructure | `copilot/audit-and-architecture-restructure` at `2c12754` | A linear 18-commit descendant of `main`; 319 files changed. It establishes the intended `/src` and `/tests` layout, but leaves several migrations incomplete. |
| Prior Codex work | `/home/danil/Desktop/MakerPrompt` | An uncommitted hardening pass based exactly on `main` at `966e31d`; 21 modified and 4 new meaningful source files. Generated `bin`/`obj` content is excluded. Portable PDB hashes confirm that omitted Blazor source files are byte-identical to `main`. |

## Baseline validation

- `main`: 147 unit tests passed. The build emitted `RZ10012` for an unexpected
  `ProjectHub` component.
- Restructure: 179 unit tests passed. Blazor, Cloud, and EdgeAgent Release builds
  succeeded.
- The restructure restore reported a high-severity vulnerability through
  `SQLitePCLRaw.lib.e_sqlite3 2.1.10`, introduced by the old
  `Microsoft.Data.Sqlite 9.0.5` reference.
- MAUI could not be compiled in the audit environment because the .NET MAUI
  workload was not installed. Its project and CI configuration nevertheless
  contain independently verifiable path, asset, identity, and target mismatches.

## Intended architecture

The restructure's project split is materially better than the monolithic stable
layout and was retained:

```text
Core
├── domain models
└── host-independent contracts

Application
└── cross-host orchestration

Infrastructure
├── printer protocol adapters
├── camera transport
└── platform-neutral implementations

Infrastructure.Sqlite
└── durable Cloud/Edge stores

UI.Components
├── Razor UI
├── UI state and browser/native integrations
└── standalone application services

UI.Blazor / UI.MAUI
└── host composition

Cloud / EdgeAgent
└── authenticated read-only remote fleet slice
```

The branch created an Application layer and later removed it while comments and
dependency direction still referred to it. Recovery restores cross-host fleet
orchestration there. UI-only storage workflows remain in `UI.Components`.

## Valuable restructure work to retain

- Clear Core, Infrastructure, UI, host, Cloud, Edge, and test project boundaries.
- Asynchronous contracts with cancellation support.
- Provider-based printer discovery and import.
- In-memory and SQLite telemetry/camera stores.
- Cloud and outbound EdgeAgent deployment skeletons.
- MJPEG camera support.
- More complete unit and browser test coverage.
- Updated UI selectors and farm-mode autosave behavior.
- Docker and CI foundations for the new host split.

## Incomplete or regressed work

### Printer APIs and UI

- Direct-control capability flags and calibration/EEPROM operations were removed
  from `IPrinterCommunicationService`, although implementations and UI controls
  still depend on the behavior.
- Moonraker's printer queue was dropped.
- File listings were reduced from structured `FileEntry` values to file-name
  strings.
- The Control Panel no longer hides unsupported controls for read-only backends.
- The Prusa Connect import flow was deleted.
- Robust Prusa Connect response parsing from `main` was replaced with a narrower
  parser.
- Enum localization looks up raw enum names while resources use prefixed keys.
- The old and new Prusa Connect API implementations coexist with incompatible
  authentication models.

### Persistence and lifecycle

- Existing `main` connection JSON uses nested `Settings.Api` and
  `Settings.Serial` objects. The restructure only reads flattened properties,
  silently losing saved endpoints and credentials.
- Farm storage writes plaintext credentials and exports them without redaction.
- Deleting the active farm does not reliably replace or clear runtime printers.
- Connection attempts are not serialized per printer and can leak failed or
  stale service instances.
- A singleton serial backend violates per-printer ownership.
- Several HTTP adapters cannot reconnect because their lifetime cancellation
  source remains cancelled.
- MAUI local storage strips directories and can collide files belonging to
  different projects.

### Layering and dead code

- Fleet orchestration sits in Infrastructure after the Application project was
  removed mid-refactor.
- Unused repository abstractions and in-memory implementations remain without
  any production consumer.
- Two serial engines, two Prusa Connect clients, obsolete navigation components,
  and superseded file/project UI coexist.
- Several commented-out handlers, unused interop methods, raw console logging,
  and unimplemented placeholder actions remain.

### Cloud and Edge

- The pre-shared Edge API key cannot authenticate because the endpoint's normal
  authorization policy rejects it before the handler can inspect it.
- Scope parsing assumes a single exact scope rather than OAuth's space-delimited
  form.
- Compose supplies empty tokens, while configuration implies a usable setup.
- Edge sends telemetry but not camera frames, has no effective reconnect loop,
  and does not use the SQLite stores.
- The MJPEG reader mishandles split JPEG markers, treats some valid `HEAD`
  responses as permanent failure, and cannot recover after an outage.
- `CloudMakerspace` UI mode and OIDC configuration expose only visibility changes;
  no remote UI data client exists. Cloud will therefore be completed as an
  authenticated API host rather than presenting an unusable pseudo-remote UI.

### Hosts, tooling, and documentation

- MAUI real icons, splash art, and font were replaced with placeholder assets;
  its CSS bundle name, application identity, versioning, and CI target paths are
  inconsistent.
- GitHub Pages templates reference deleted project and static-asset names.
- CI does not compile Cloud or EdgeAgent, and MAUI workflow paths are stale.
- Coverage filters, VS Code tasks, Docker inputs, READMEs, and architecture
  instructions still reference the pre-restructure layout.
- Package versions are split between .NET 9 and early .NET 10 releases.

## Desktop work to recover

- Versioned, marked credential protection; protected persistence; redacted
  exports; and legacy plaintext compatibility.
- Correct active-farm deletion behavior.
- Per-printer lifecycle serialization, ownership, failure disposal, and fresh
  serial service instances.
- Reconnect-safe cancellation scopes for HTTP printer adapters.
- Hiding the non-functional Bambu LAN prototype.
- Dynamic-port Playwright startup with `DOTNET_HOST_PATH`, drained server output,
  external-base-URL support, and fail-fast diagnostics.
- Honest backend/security/hardware limitations and Edge/Cloud trust boundaries.

Desktop edits to the unused `NavPrinters` component and generated build output
will not be copied. Persistence metadata will be represented by a storage DTO,
not added to the Core domain model.

## Integration decisions

1. Use the restructure as the architectural base, represented as an uncommitted
   diff against `main`.
2. Restore the Application project for cross-host orchestration and remove
   abandoned repository abstractions that have no production path.
3. Preserve backward compatibility through explicit, versioned persistence DTOs
   rather than keeping duplicate legacy services.
4. Keep Cloud/Edge as a coherent read-only remote slice: durable stores,
   authentication, telemetry, camera relay, and reconnect behavior. Remove the
   unfinished Cloud UI mode instead of claiming unsupported functionality.
5. Remove the invalid Bambu prototype from selectable production backends.
6. Restore real MAUI assets and the established application identity.
7. Update packages to a consistent supported .NET 10 patch level and eliminate
   the known SQLite vulnerability.
8. Avoid broad stylistic churn; changes outside these recovery goals require a
   concrete correctness, maintainability, or validation benefit.
