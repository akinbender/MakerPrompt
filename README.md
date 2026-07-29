# MakerPrompt

[![CI](https://github.com/akinbender/MakerPrompt/actions/workflows/ci.yml/badge.svg)](https://github.com/akinbender/MakerPrompt/actions/workflows/ci.yml)
[![codecov](https://codecov.io/gh/akinbender/MakerPrompt/branch/main/graph/badge.svg)](https://codecov.io/gh/akinbender/MakerPrompt)
[![Build](https://github.com/akinbender/MakerPrompt/actions/workflows/build.yml/badge.svg)](https://github.com/akinbender/MakerPrompt/actions/workflows/build.yml)

MakerPrompt is an open-source, cross-platform control and monitoring application
for mixed 3D-printer fleets. The same Blazor UI runs as a WebAssembly application
and in a .NET MAUI native shell.

The public WASM build is available at
[akinbender.github.io/MakerPrompt](https://akinbender.github.io/MakerPrompt/).
MakerPrompt is still pre-1.0 software: verify commands and safety limits on your
printer before relying on it unattended.

## Capabilities

- Multiple saved printers, active-printer selection, auto-connect, and farm profiles.
- Direct serial control through Web Serial, Windows serial ports, Android USB, and
  Mac Catalyst USB adapters.
- Network adapters for Moonraker, PrusaLink, Prusa Connect, and OctoPrint.
- Telemetry dashboards, motion and temperature controls, command console, printer
  storage, G-code preview, webcams, and print-project queues.
- Optional filament inventory, usage analytics, notifications, calculators,
  localization, themes, and BrailleRAP G-code generation.
- A demo backend for development and automated testing without printer hardware.

Hardware support varies by firmware, API version, and platform. The Bambu LAN
prototype is not presented as a supported production backend.

## Clients

| Client | Status | Notes |
| --- | --- | --- |
| Blazor WebAssembly | Supported | Web Serial requires a Chromium-based browser and a secure context. Network APIs remain subject to browser CORS rules. |
| MAUI Windows | Supported | Native serial through `System.IO.Ports`. |
| MAUI Mac Catalyst | Experimental | Native USB serial through `UsbSerialForMacOS`; hardware testing is still limited. |
| MAUI Android | Experimental | USB host support through `UsbSerialForAndroid.Net`; device/driver coverage varies. |
| iOS / Tizen | Not targeted | Platform scaffolding exists, but these targets are not built or released. |

## Repository layout

Production projects live under `src/`; tests live under `tests/`.

```text
src/
  MakerPrompt.Core/                  Domain models and host-independent contracts
  MakerPrompt.Application/           Cross-host orchestration
  MakerPrompt.Infrastructure/        Printer, camera, and transport adapters
  MakerPrompt.Infrastructure.Sqlite/ Optional durable telemetry/camera stores
  MakerPrompt.UI.Components/         Shared Razor UI and UI-facing services
  MakerPrompt.UI.Blazor/             WebAssembly host
  MakerPrompt.UI.MAUI/               Native MAUI host
  MakerPrompt.Cloud/                 Experimental authenticated read API/ingest host
  MakerPrompt.EdgeAgent/             Experimental outbound site agent
tests/
  MakerPrompt.Tests.Unit/
  MakerPrompt.Tests.Integration/
  MakerPrompt.Tests.E2E.Wasm/
  MakerPrompt.Tests.E2E.Maui/
```

See [Architecture](docs/ARCHITECTURE.md) for dependency rules and the exact
Cloud/Edge trust boundary. The recovery decisions behind this layout are recorded
in [Recovery Audit](docs/RECOVERY_AUDIT.md).

## Build and test

Install the .NET 10 SDK. The browser and unit-test path is portable:

```bash
dotnet restore tests/MakerPrompt.Tests.Unit/MakerPrompt.Tests.Unit.csproj
dotnet build src/MakerPrompt.UI.Blazor/MakerPrompt.UI.Blazor.csproj -c Release
dotnet test tests/MakerPrompt.Tests.Unit/MakerPrompt.Tests.Unit.csproj -c Release
```

MAUI builds require the matching workload and host operating system. For example,
on Windows:

```powershell
dotnet workload install maui-windows
dotnet build src/MakerPrompt.UI.MAUI/MakerPrompt.UI.MAUI.csproj `
  -c Release `
  -f net10.0-windows10.0.19041.0
```

Use target-specific builds on non-Windows hosts; a whole-solution build can require
MAUI workloads that are unavailable on the current operating system. E2E setup and
commands are documented in
[tests/MakerPrompt.Tests.E2E.Wasm/README.md](tests/MakerPrompt.Tests.E2E.Wasm/README.md).

## Cloud and Edge status

Cloud and EdgeAgent are an experimental, read-only remote-monitoring slice—not a
remote-control product:

- EdgeAgent initiates outbound connections, polls configured local printers, and
  forwards telemetry and camera snapshots.
- Cloud is an API-only host with durable SQLite telemetry and camera storage.
  Agent ingest accepts a scoped JWT or a configured pre-shared key; read endpoints
  require an authenticated member JWT.
- There is no Cloud-to-Edge command channel and printer credentials are not meant
  to leave the local site.
- The standalone WASM app still talks directly to local printers. It is not a
  Cloud dashboard and no end-user remote-data client is implemented.
- Agent enrollment, key rotation, tenant/resource ownership, and a durable outbound
  delivery queue remain outside the implemented slice.

The Docker Compose file is development scaffolding with a documented, public
development key. Override both `MAKERPROMPT_AGENT_API_KEY` and its SHA-256
`MAKERPROMPT_AGENT_API_KEY_HASH` before using it outside an isolated local machine.

## Security notes

- Use HTTPS for printer APIs and Cloud/Edge traffic whenever the device supports it.
- Browser storage is controlled by the browser user and must not be treated as a
  hardware-backed secret store.
- Treat exported farm profiles and diagnostic output as sensitive.
- Do not expose printer administration APIs or EdgeAgent directly to the public
  internet.

## Acknowledgements

MakerPrompt was started for the 3D lab at [xHain](https://x-hain.de/) and was
inspired by long-term use of [Printrun/Pronterface](https://github.com/kliment/Printrun).
The BrailleRAP integration adapts logic from
[AccessBrailleRAP](https://github.com/braillerap/AccessBrailleRAP).

The UI uses Bootstrap, Bootstrap Icons, BlazorBootstrap, and
[gcode-preview](https://github.com/remcoder/gcode-preview). See each dependency and
bundled asset for its upstream license.
