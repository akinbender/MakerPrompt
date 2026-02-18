# MakerPrompt — Agent Guide

Use dotnet-artisan skills when available, especially for:

- `dotnet-blazor-patterns`
- `dotnet-maui-development`
- `dotnet-testing-strategy`
- `dotnet-csharp-async-patterns`
- `dotnet-architecture-patterns`

## Guardrails

1. Keep changes additive and narrow in scope.
2. Preserve existing backend behavior.
3. Route all printer communication changes through:
   - `IPrinterCommunicationService`
   - `BasePrinterConnectionService`
4. Prefer async telemetry patterns that avoid noisy output.
5. Keep UI changes minimal and capability-driven.

## Backend Notes

- PrusaLink: HTTP/JSON
- Moonraker: WebSocket + HTTP
- WebSerial: Serial/WebUSB

When adding features, check whether each backend supports it and conditionally render UI/actions.
