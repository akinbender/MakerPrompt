# Copilot Instructions

## Project-Specific Rules

- This is a **cross-platform 3D printer control app** (Blazor WASM + .NET MAUI).
- Prefer **additive changes**. Do not refactor unless explicitly asked.
- **Do not break existing printer backends**.

### Architecture
- All printer logic must go through:
  - `IPrinterCommunicationService`
  - `BasePrinterConnectionService`
- **analyze the existing infrastructure first** and reuse or extend it where appropriate 

### Printer Backends
- Existing backends:
  - PrusaLink → HTTP/JSON
  - Moonraker → WebSocket + HTTP
  - WebSerial → Serial/WebUSB
- New backends must follow existing patterns and registration flow.
- Telemetry must be async and must **not spam logs or command output**.

### UI
- UI changes must be minimal.
- Conditional UI (tabs, cards, buttons) appear only when supported.
- Use **BlazorBootstrap** for modals and toast notifications.
- 
### PR Rules
- One concern per PR.
- Keep diffs small and reviewable.
- Do not add speculative features.
