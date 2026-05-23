# Windows Agent Production Handoff

This document is a handoff for the next AI or human agent implementing the production-ready PhotoBIZ Windows Agent experience.

Read these files first, in order:

1. `docs/ARCHITECTURE.md`
2. `docs/PRD.md`
3. `docs/CODING_GUIDELINES.md`
4. This file

`docs/ARCHITECTURE.md` is still the source of truth today, but it currently describes the Windows Agent as a `.NET 10 LTS Windows Service`. The decisions below intentionally change that production shape. The first implementation step must update `docs/ARCHITECTURE.md` and any conflicting docs so the source of truth matches this handoff before code changes are merged.

## Goal

Build a production-ready Windows Agent package for booth laptops.

The Agent must be easy for a PhotoBIZ technician to install, pair, start, monitor, stop, diagnose, and re-pair. The Agent and kiosk Chrome must be operationally tied: the booth is online only when the Agent runtime and kiosk Chrome are intentionally started together.

## Decisions Already Made

- Production package is a signed, self-contained Windows `.exe` installer.
- Do not implement silent install for v1.
- Do not preserve pairing/config on uninstall.
- Manual installer updates are enough for v1; no auto-update yet.
- Code signing is required before live/client booth installation.
- The production runtime is a logged-in Windows user-session app, not an always-on Windows Service.
- The UI is a WPF tray/control-center app with a Material-style theme.
- The tray app auto-opens after Windows login.
- The booth does not need to work before Windows login.
- The user must click `Start Booth` to bring the booth online.
- `Start Booth` launches kiosk Chrome and starts heartbeat/command polling.
- `Stop Booth` gracefully stops heartbeat/command polling and closes the kiosk Chrome instance launched by PhotoBIZ.
- Closing the control-center window while online minimizes to tray.
- Pairing is done by pasting booth code and agent credential issued from Admin Web.
- Re-pairing must be supported because Admin Web can re-issue new booth credentials.
- Production API URL is the default; local/dev API URL belongs in technician advanced settings.
- Store agent credential and LumaBooth API password encrypted locally with Windows DPAPI.
- Support both Chrome auto-detection and manual Chrome path override.
- The GUI should include all core screens in v1.
- Normal staff mode and technician/admin mode are both needed.
- The GUI should expose service/runtime actions, sanitized logs, and diagnostics export.
- Admin Web should eventually show agent version, last heartbeat, and health/update status.

## Target Runtime Model

The production Agent should be a user-session Agent Control Center.

The app owns the runtime lifecycle:

1. Technician or staff opens/signs into Windows.
2. PhotoBIZ Agent Control Center auto-opens.
3. User clicks `Start Booth`.
4. App validates saved pairing/config.
5. App requests a fresh Booth UI kiosk token from the API.
6. App launches Chrome in kiosk mode at `BoothUiBaseUrl/{kioskToken}` with an isolated user data directory.
7. App starts heartbeat and command polling.
8. App listens for LumaBooth trigger callbacks.
9. App handles LumaBooth API/simulator commands and focus handoff.
10. User clicks `Stop Booth`.
11. App stops polling/heartbeat, closes only the PhotoBIZ-launched kiosk Chrome process, and informs the backend that the booth is offline.

Do not build an always-on background service for v1. Do not let heartbeat continue when kiosk Chrome is intentionally stopped.

## Required Documentation Updates

Before or alongside code implementation, update:

- `docs/ARCHITECTURE.md`
  - Replace the Windows Service decision with the Agent Control Center runtime model.
  - Explain that heartbeat means the booth runtime is online, not merely that credentials were paired.
  - Explain that kiosk Chrome and heartbeat are started/stopped together.
  - Update Agent configuration and deployment sections.
- `docs/PRD.md`
  - Update Windows Agent responsibilities to include local GUI, pairing, diagnostics, and start/stop booth lifecycle.
- `docs/CODING_GUIDELINES.md`
  - Replace service-only language with Windows Agent Control Center guidance.
- `docs/LOCAL_DEVELOPMENT.md`
  - Document how to run the WPF app locally, simulator mode, dev config, and normal-window Chrome mode.
- `docs/DEPLOYMENT.md`
  - Add the signed self-contained installer as the booth-laptop deployment artifact.

## Backend API Changes

Current API endpoints live in `services/api/src/PhotoBIZ.Api/PhotoBizApiEndpoints.cs`.

Implement these behavior changes:

- Pairing must validate booth code and agent credential, but pairing alone must not mark the booth online.
- Booth UI launch token creation must not by itself imply the booth is online unless accompanied by runtime start/heartbeat semantics.
- Heartbeat is the authoritative online signal.
- Add an authenticated agent offline/shutdown endpoint, for example `POST /api/agent/offline`.
  - It must validate the same `X-Agent-Credential` and booth code as current agent endpoints.
  - It should mark the booth unavailable immediately instead of waiting for the stale-heartbeat timeout.
  - It must not cancel active transactions unless existing backend recovery rules explicitly allow that.
- Extend heartbeat payload with non-secret metadata:
  - `boothCode`
  - `agentVersion`
  - `runtimeKind`, for example `ControlCenter`
  - `kioskRunning`
  - `lumaBoothMode`
  - basic health flags such as API reachable, Chrome launched, trigger listener running, LumaBooth reachable when in API mode
- Persist or expose the latest agent metadata enough for Admin Web to show:
  - agent version
  - last heartbeat timestamp
  - kiosk running status
  - runtime health/update status

Keep tenant isolation and backend authority intact. Agent endpoints remain privileged and authenticated by the booth agent credential.

## Agent Project Changes

Current Agent project:

- `agent/windows-agent/src/PhotoBIZ.WindowsAgent`
- Current shape is a worker/service-capable project using `Microsoft.Extensions.Hosting.WindowsServices`.
- Current worker logic already supports pairing, heartbeat, command polling, kiosk launch, simulator/API mode, trigger listener, active session store, print copies, and focus handoff.

Refactor instead of rewriting:

- Extract existing `Worker` behavior into reusable runtime services that can be started/stopped by the WPF app.
- Keep core logic testable without UI:
  - pairing client
  - heartbeat loop
  - command polling loop
  - kiosk launch/close service
  - LumaBooth simulator/API clients
  - trigger listener
  - active session store
  - focus service
  - diagnostics/log sanitizer
- Add a WPF executable project for the Agent Control Center.
- Keep a console/dev host only if it remains useful for automated tests and fast debugging.
- Remove production dependency on Windows Service lifecycle unless a future architecture revision reintroduces it.

## GUI Requirements

Build a WPF tray/control-center app using a Material-style theme.

Core screens:

- Dashboard
  - Paired booth name/code
  - Online/offline state
  - API connection
  - Kiosk Chrome state
  - LumaBooth state
  - Last heartbeat
  - Agent version
  - Primary `Start Booth` / `Stop Booth` action
- Pair/Re-pair
  - Booth code input
  - Agent credential paste input
  - Production API URL shown as default
  - Advanced local/dev API URL override
  - Pair validation
  - Re-pair flow that stops booth runtime first and replaces local credential
- Kiosk/Display
  - Booth UI base URL
  - Kiosk mode toggle
  - Chrome auto-detect result
  - Manual Chrome path override
  - Chrome user data directory
  - Relaunch kiosk action
- LumaBooth
  - Simulator/API mode
  - LumaBooth API base URL
  - Masked API password
  - Trigger listener URL
  - Start timeout
  - Test API connection action
- Logs
  - Recent sanitized runtime events
  - No raw credentials, kiosk tokens, passwords, or provider secrets
- Diagnostics
  - Export sanitized diagnostic bundle
  - Include config summary, version, recent logs, health checks, and environment info
- About/Update
  - Version
  - Installer/build info
  - Manual update guidance

Staff mode:

- Start Booth
- Stop Booth
- Relaunch Kiosk
- Status
- Sanitized logs
- Export diagnostics

Technician/admin mode:

- Pair/re-pair
- API URL
- LumaBooth settings
- Chrome settings
- Advanced diagnostics

Do not expose saved secret values after save. Mask them, and require re-entry to change.

## Local Configuration And Secrets

Production:

- Store config under `C:\ProgramData\PhotoBIZ\Agent`.
- Encrypt agent credential and LumaBooth API password with Windows DPAPI.
- Use restrictive ACLs appropriate for the installed app and local machine.
- Store non-secret config as structured JSON.
- Store active local session context under the existing ProgramData agent path, or migrate consistently.

Local development:

- Prefer user secrets or a local dev profile for booth code, agent credential, and LumaBooth password.
- Use `ApiBaseUrl = http://localhost:5082`.
- Use `BoothUiBaseUrl = http://localhost:4201`.
- Default `LumaBooth:Mode = Simulator`.
- Default `KioskMode = false` for daily development so Chrome opens as a normal window.
- Allow toggling real kiosk mode for booth behavior testing.

## Installer Requirements

Use a signed Windows `.exe` installer for production.

Installer behavior:

- Install self-contained `win-x64` app under `Program Files`.
- Add auto-start entry so Agent Control Center opens after Windows login.
- Create required ProgramData folders.
- Do not require a separate .NET runtime install.
- Preserve config during manual installer update.
- Remove local pairing/config on uninstall.
- Do not implement silent install in v1.

Code signing:

- Sign the Agent app and installer before client/live booth installation.
- Unsigned builds may be used only for local development or internal lab testing.

## Start Booth Behavior

`Start Booth` must:

1. Validate saved config.
2. Pair/validate with backend if needed.
3. Request fresh Booth UI launch token.
4. Launch Chrome with the isolated kiosk profile.
5. Verify Chrome process was launched and store its process identity.
6. Start heartbeat loop.
7. Start command polling loop.
8. Start LumaBooth trigger listener.
9. Show online state in the GUI.

If any required step fails, the app should show a clear local error and avoid claiming the booth is online.

## Stop Booth Behavior

`Stop Booth` must:

1. Stop command polling.
2. Stop heartbeat loop.
3. Stop trigger listener if owned by the runtime.
4. Call backend agent offline/shutdown endpoint.
5. Close only the kiosk Chrome process launched by PhotoBIZ.
6. Leave ordinary user Chrome windows alone.
7. Clear in-memory runtime state.
8. Show offline state in the GUI.

If backend offline call fails because the network is down, stop local runtime anyway and log a sanitized warning. Backend stale-heartbeat handling remains the fallback.

## Re-pair Behavior

Re-pair must:

1. Require booth runtime to be stopped, or stop it after explicit confirmation.
2. Clear old local agent credential and local kiosk launch state.
3. Accept new booth code and agent credential from Admin Web.
4. Validate with backend.
5. Save encrypted credential if validation succeeds.
6. Show paired booth summary.

Admin Web credential reissue invalidates the previous credential. The local app must handle unauthorized responses by showing a re-pair-required state.

## Admin Web Follow-Up

Admin Web should eventually show enough agent status for operations:

- Agent version
- Last heartbeat
- Kiosk running flag
- Basic health summary
- Update required indicator if backend knows a minimum supported version

Keep detailed local settings in the Windows Agent GUI. Admin Web should not expose local secrets or operationally sensitive paths.

## Local Development Flow

Run the standard local stack:

```powershell
docker compose up -d postgres redis
dotnet run --project services/api/src/PhotoBIZ.Api/PhotoBIZ.Api.csproj
dotnet run --project services/api/src/PhotoBIZ.Worker/PhotoBIZ.Worker.csproj
Set-Location apps
npm run start:admin
npm run start:booth
```

Then run the Agent Control Center from Visual Studio or `dotnet run`.

Expected dev workflow:

1. Register a booth in Admin Web.
2. Copy booth code and agent credential.
3. Open local Agent Control Center.
4. Pair with copied values.
5. Use local API URL `http://localhost:5082`.
6. Use local Booth UI URL `http://localhost:4201`.
7. Keep simulator mode enabled unless testing real LumaBooth hardware.
8. Click `Start Booth`.
9. App opens Chrome to `http://localhost:4201/{kioskToken}` and starts heartbeat/polling.
10. Test kiosk, cashier, and transaction flow.
11. Click `Stop Booth` to stop heartbeat and close the launched Chrome instance.

## Testing Requirements

Backend tests:

- Pairing validates credentials but does not mark booth online.
- Heartbeat marks booth online and stores metadata.
- Offline/shutdown endpoint marks booth unavailable immediately.
- Credential reissue makes old credential unauthorized.
- Agent metadata does not leak credentials, kiosk token, or LumaBooth password.

Agent tests:

- Start Booth launches Chrome before heartbeat is considered active.
- Stop Booth stops loops and closes only the PhotoBIZ-launched Chrome process.
- Unauthorized agent response puts app into re-pair-required state.
- Re-pair clears and replaces encrypted credential.
- DPAPI secret storage round-trips locally.
- Sanitized logs remove credentials, kiosk tokens, passwords, and secret headers.
- Simulator mode completes session and print-copy command flows.
- API mode builds correct LumaBooth start/print URLs without logging password.

Manual QA:

- Fresh install on clean Windows machine.
- Pair with Admin Web-issued credential.
- Start Booth after Windows login.
- Reboot, log in, verify app opens but booth remains stopped until user starts it.
- Start Booth after reboot.
- Stop Booth and verify Admin Web/Booth UI become offline/unavailable.
- Re-issue booth credentials from Admin Web and re-pair locally.
- Uninstall and verify local pairing/config is removed.
- Manual installer update preserves config.
- Real LumaBooth API start, trigger callback, focus handoff, and extra-print command smoke test.

## Acceptance Criteria

The work is complete when:

- Docs no longer conflict about Windows Service versus Agent Control Center.
- A technician can install the signed self-contained `.exe`.
- The app auto-opens after Windows login.
- Pair/re-pair works with Admin Web-issued credentials.
- `Start Booth` brings up kiosk Chrome and starts heartbeat/command polling.
- `Stop Booth` stops heartbeat/command polling and closes the kiosk Chrome instance.
- Backend reflects online/offline state without relying only on timeout after graceful stop.
- Simulator mode supports full local development without LumaBooth.
- Real API mode is ready for booth hardware smoke testing.
- Sensitive values are encrypted at rest and never shown in logs/diagnostics.
- Focused backend, agent, and GUI tests cover the lifecycle and failure modes above.

