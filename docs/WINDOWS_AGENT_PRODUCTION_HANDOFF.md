# Windows Agent Production Handoff

This document is a handoff for the next AI or human agent implementing the production-ready PhotoBIZ Windows Agent experience.

Read these files first, in order:

1. `docs/ARCHITECTURE.md`
2. `docs/PRD.md`
3. `docs/CODING_GUIDELINES.md`
4. `docs/WINDOWS_AGENT_RELEASE_RUNBOOK.md`
5. This file

`docs/ARCHITECTURE.md` is the source of truth. It now matches this handoff's user-session Agent Control Center direction. If future implementation work changes the runtime model again, update `docs/ARCHITECTURE.md` first and then bring this handoff back into sync.

## Goal

Build a production-ready Windows Agent package for booth laptops.

The Agent must be easy for a PhotoBIZ technician to install, pair, start, monitor, stop, diagnose, and re-pair. The Agent and kiosk Chrome must be operationally tied: the booth is online only when the Agent runtime and kiosk Chrome are intentionally started together.

## Current Implementation Status

Status: production Agent is near release-candidate shape for internal lab ZIP distribution, but not yet validated as a signed client install.

Overall distance to desired production state: about 95% complete. The documentation, backend lifecycle contract, core start/stop runtime service, local configuration pathing, encrypted secret storage, Pair/Re-pair application service, runtime config bridge, WPF Control Center shell, editable local settings, re-pair-required credential state, tray/minimize behavior, staff/technician mode split, persisted booth display name, Relaunch Booth action, LumaBooth API reachability test, sanitized diagnostics export, persisted kiosk launch cleanup state, repeatable self-contained publish script, install/uninstall/autostart release scripts, and draft GitHub Release workflow are now in place. The remaining work is mostly external validation: richer GUI tests if desired, Admin Web status rendering, real code-signing certificate validation, clean Windows QA, and real LumaBooth hardware QA.

Completed:

- Source-of-truth documentation now describes the production Agent as a logged-in Windows user-session Control Center instead of an always-on Windows Service.
- `docs/ARCHITECTURE.md`, `docs/PRD.md`, `docs/CODING_GUIDELINES.md`, `docs/LOCAL_DEVELOPMENT.md`, and `docs/DEPLOYMENT.md` were updated for the Control Center lifecycle, local dev defaults, and installer direction.
- Backend pairing now validates booth code and Agent credential without marking the booth online.
- Backend Booth UI launch-token creation no longer implies online status.
- Backend heartbeat remains the authoritative online signal.
- Backend now has authenticated graceful shutdown through `POST /api/agent/offline`.
- Backend persists non-secret Agent runtime metadata: Agent version, runtime kind, kiosk running flag, LumaBooth mode, health flags, health status, and metadata timestamp.
- Admin overview DTOs expose the latest Agent status summary for future Admin Web display.
- EF migration `20260523220631_AddAgentRuntimeMetadata` adds the Agent metadata fields to `booths`.
- The current Agent dev host sends the new heartbeat payload and calls the offline endpoint on shutdown.
- Agent runtime orchestration has been extracted into `AgentBoothRuntime` with explicit `StartAsync` and `StopAsync` methods for the future WPF app.
- The current worker is now a thin dev host over `AgentBoothRuntime`.
- LumaBooth trigger listening is no longer an always-on hosted service; the runtime starts and stops it with the booth lifecycle.
- Kiosk Chrome launch now tracks the PhotoBIZ-launched process and can close only that owned process on stop/relaunch.
- The current Agent project no longer registers Windows Service hosting or references `Microsoft.Extensions.Hosting.WindowsServices`.
- Local Agent data paths now resolve to `C:\ProgramData\PhotoBIZ\Agent` by default, with test/dev override support.
- Local Agent configuration is stored as structured JSON at `config.json` under the Agent data directory.
- Agent credential and LumaBooth API password are protected through a DPAPI-backed `IAgentSecretProtector`.
- Config snapshots expose only non-secret values and secret presence flags, while runtime options can decrypt secrets for Agent use.
- Active local session context now uses the same Agent data path abstraction.
- Agent API pairing now returns the backend booth summary and supports validating an explicit API URL, booth code, and pasted Agent credential before anything is saved locally.
- Pair/Re-pair application service now validates credentials, saves the Agent credential through encrypted local configuration, preserves existing non-secret settings, and preserves the LumaBooth API password unless explicitly changed elsewhere.
- Re-pair application service now stops a running booth runtime, clears active local session context, and closes PhotoBIZ-launched kiosk state before replacing the saved credential.
- Agent runtime settings now load from encrypted local configuration through `IAgentRuntimeOptionsProvider`, while still falling back to configured development options when no local pairing exists.
- PhotoBIZ API calls, kiosk launch settings, and LumaBooth API settings now resolve runtime options dynamically instead of being frozen to startup configuration.
- WPF `PhotoBIZ.WindowsAgent.ControlCenter` project now exists and is included in `PhotoBIZ.slnx`.
- The Control Center starts through the standard .NET host, registers the Agent runtime services, and uses production/local appsettings defaults.
- Initial WPF screens now exist for Dashboard, Pair/Re-pair, Kiosk/Display, LumaBooth, Logs, Diagnostics, and About.
- Dashboard Start/Stop actions call `AgentBoothRuntime`, while Pair/Re-pair actions call `IAgentPairingService`.
- Kiosk/Display settings are now editable in the Control Center, including Booth UI URL, Chrome executable override, Chrome profile directory, Booth UI launch toggle, and kiosk/windowed mode.
- Kiosk/Display settings include a Chrome detect action that uses the same executable resolution logic as kiosk launch.
- LumaBooth settings are now editable in the Control Center, including simulator/API mode, local API URL, trigger listener URL, start timeout, and masked API password replacement.
- LumaBooth tab now includes a safe API reachability test that does not trigger a booth session.
- Pair/Re-pair now saves and displays the backend booth display name with the booth code.
- Unauthorized Agent API responses now map to a dedicated re-pair-required exception and GUI state that disables Start Booth until credentials are replaced.
- Control Center now has tray integration. Minimizing hides to tray, closing while online minimizes instead of exiting, and tray Exit stops the runtime before closing.
- Control Center now has a staff/technician mode toggle. Staff mode keeps daily operation focused on Dashboard, Logs, Diagnostics, and About; technician mode exposes Pair/Re-pair, Kiosk/Display, and LumaBooth settings.
- Dashboard and tray now include a Relaunch Booth action that stops and restarts the local runtime, relaunching kiosk Chrome with a fresh booth UI token.
- Diagnostics export now writes a sanitized text bundle under the Agent data directory and redacts known credentials, LumaBooth passwords, password query parameters, secret headers, and long kiosk-token URL segments.
- Agent release packaging now has `agent/windows-agent/scripts/publish-control-center.ps1`, which creates a self-contained `win-x64` single-file Control Center release ZIP and manifest under `artifacts/windows-agent/packages`.
- Published Agent ZIPs exclude `appsettings.Development.json` and include only the production `appsettings.json` defaults next to `PhotoBIZ.WindowsAgent.ControlCenter.exe`.
- Published Agent ZIPs now include `Install-PhotoBIZAgent.ps1` and `Uninstall-PhotoBIZAgent.ps1`. The install script copies the release to `C:\Program Files\PhotoBIZ\Windows Agent`, creates `C:\ProgramData\PhotoBIZ\Agent`, adds a current-user login auto-start entry, and creates a Start Menu shortcut. The uninstall script removes app files, auto-start, shortcut, and local data unless `-PreserveData` is passed.
- GitHub Actions workflow `.github/workflows/windows-agent-release.yml` builds the Agent ZIP on `agent-v*` tags or manual dispatch, uploads workflow artifacts, and creates a draft prerelease with the ZIP and manifest attached.
- The Agent release workflow supports optional Authenticode signing when `WINDOWS_AGENT_SIGNING_CERTIFICATE_BASE64` and `WINDOWS_AGENT_SIGNING_CERTIFICATE_PASSWORD` secrets are configured.
- Kiosk Chrome launch state is now persisted at `booth-ui-launch.json` under the Agent data directory so a restarted Agent can identify and close a still-running PhotoBIZ-launched Chrome process.
- Focused backend tests cover pair/launch-not-online, heartbeat metadata, immediate offline behavior, and preserving active transactions during graceful offline.
- Focused Agent tests cover kiosk launch before heartbeat, graceful stop calling offline and closing the owned kiosk process, and failed kiosk launch preventing heartbeat.
- Focused config tests cover encrypted-at-rest secrets, masked snapshots, runtime decryption, secret preservation on partial settings updates, secret clearing, and config deletion.
- Focused Pair/Re-pair tests cover validation-before-save, failed validation preserving the existing encrypted config, runtime/session/kiosk cleanup during re-pair, and non-secret setting preservation.
- Focused runtime option tests cover saved local config taking precedence and development appsettings fallback when no local pairing exists.
- Focused Agent API client tests cover unauthorized credential responses mapping to the re-pair-required exception.
- Focused LumaBooth connection tests cover API reachability without session start and simulator-mode skip behavior.
- Focused diagnostics tests cover export output and redaction of credentials, kiosk tokens, passwords, and secret headers.
- Current validation passed:
  - `dotnet test services/api/tests/PhotoBIZ.Api.Tests/PhotoBIZ.Api.Tests.csproj --no-restore`
  - `dotnet test agent/windows-agent/tests/PhotoBIZ.WindowsAgent.Tests/PhotoBIZ.WindowsAgent.Tests.csproj --no-restore`
  - `dotnet build agent/windows-agent/src/PhotoBIZ.WindowsAgent.ControlCenter/PhotoBIZ.WindowsAgent.ControlCenter.csproj --no-restore`
  - `dotnet build PhotoBIZ.slnx --no-restore`
  - `npm run build:admin`
  - `npm run format:check`
  - `dotnet ef database update`

Not complete yet:

- WPF Control Center still needs polished error states and stronger mode guarding if technician mode should require a local password or Windows role.
- Existing `PhotoBIZ.WindowsAgent` is still a worker/dev-host style project, although its core lifecycle now lives behind reusable runtime services.
- Logs tab is still basic and only shows the latest GUI status message; it is not yet backed by a rolling sanitized runtime log.
- Admin Web has the data contract for Agent status but does not yet render the new status fields.
- A ZIP-based installer/autostart/uninstall flow is implemented for internal lab/pilot validation. A branded MSI/MSIX/Inno-style installer is not implemented.
- Code signing support exists in the release workflow, but a real signing certificate has not been configured or validated.
- Clean Windows install QA and real LumaBooth hardware smoke testing are not done.

Recommended next implementation slices:

1. Add focused GUI/view-model tests for button enablement, settings save flows, re-pair-required behavior, diagnostics export, mode gating, relaunch, and tray-exit shutdown behavior where practical.
2. Decide whether technician mode needs a local password or Windows role guard, then add it if required for live booths.
3. Back the Logs tab with a rolling sanitized runtime log source instead of only the latest status message.
4. Render Agent status in Admin Web using the new overview DTO fields.
5. Decide whether the ZIP-based installer scripts are enough for v1 or replace them with a branded MSI/MSIX/Inno-style installer.
6. Configure and validate a real code-signing certificate in GitHub Actions.
7. Complete manual QA on a clean Windows machine, then smoke test real LumaBooth API mode with URL triggers, focus handoff, and extra-print commands.

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

## Documentation Update Status

Completed:

- `docs/ARCHITECTURE.md`
  - Replaced the Windows Service decision with the Agent Control Center runtime model.
  - Explained that heartbeat means the booth runtime is online, not merely that credentials were paired.
  - Explained that kiosk Chrome and heartbeat are started/stopped together.
  - Updated Agent configuration and deployment sections.
- `docs/PRD.md`
  - Updated Windows Agent responsibilities to include local GUI, pairing, diagnostics, and start/stop booth lifecycle.
- `docs/CODING_GUIDELINES.md`
  - Replaced service-only language with Windows Agent Control Center guidance.
- `docs/LOCAL_DEVELOPMENT.md`
  - Documented the current Agent Control Center/dev-host transition, simulator mode, dev config, and normal-window Chrome mode.
- `docs/DEPLOYMENT.md`
  - Added the signed self-contained installer as the booth-laptop deployment artifact.

Still needed once the WPF shell exists:

- Update `docs/LOCAL_DEVELOPMENT.md` with exact WPF run commands, project path, and screenshots or screen names if they differ from this handoff.
- Update `docs/DEPLOYMENT.md` with the actual installer technology, publish command, signing command, artifact names, and release process.

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
- Current shape is a worker/dev-host project over reusable runtime services. It no longer registers Windows Service hosting.
- Current runtime logic supports pairing, heartbeat, command polling, kiosk launch/owned-process close, simulator/API mode, trigger listener start/stop, active session store, print copies, and focus handoff.

Continue refactoring instead of rewriting:

- Keep `AgentBoothRuntime` as the lifecycle boundary the WPF app calls for Start/Stop Booth.
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
