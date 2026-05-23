# Local Development Guide

This guide is the practical "how to run PhotoBIZ locally" reference for the current codebase.

## 1) Current Codebase State

PhotoBIZ is currently an MVP vertical slice in progress:

- API starts and exposes health/status, auth/session, setup, kiosk, cashier, and agent endpoints.
- Data layer is present with EF Core model + migrations for the MVP schema.
- Worker expires pending cash transactions, returns completed booths to welcome after the 15-second extra-print prompt, and marks stale idle booths offline.
- Angular `admin-web` and `booth-ui` apps run the current setup, cashier, and kiosk flows.
- Windows Agent can run in simulator mode or local dslrBooth/LumaBooth API mode.
- Reporting, real booth hardware smoke validation, and real Maya runtime payments remain incomplete.

Recent validation baseline is recorded in `docs/MVP_PROGRESS.md`.

Useful checks:

- `dotnet test PhotoBIZ.slnx --configuration Release --verbosity minimal`
- `npm run build` from `apps`
- `npm run test:ci` from `apps`

## 2) Prerequisites

- Windows with PowerShell (recommended, since the agent is Windows-targeted).
- .NET SDK `10.0.202` (or compatible .NET 10 SDK via `global.json` roll-forward).
- Node.js `24.x` and npm `11.x`.
- Docker Desktop with Docker Compose (for PostgreSQL + Redis, and optional API/worker containers).

Quick version check:

```powershell
dotnet --version
npm --version
docker compose version
```

## 3) Repository Bootstrap

From repo root:

```powershell
dotnet restore PhotoBIZ.slnx
dotnet tool restore
Set-Location apps
npm ci
Set-Location ..
```

## 4) Start Local Dependencies (Postgres + Redis)

Recommended for day-to-day dev: run infrastructure in Docker, run .NET + Angular locally.

```powershell
docker compose up -d postgres redis
```

Ports:

- PostgreSQL: `localhost:55432`
- Redis: `localhost:6379`

## 5) Apply Database Migrations

`PhotoBIZ.Api` does **not** auto-apply migrations in Development by default (`Database:ApplyMigrationsOnStartup=false`), so run this explicitly:

```powershell
dotnet tool run dotnet-ef database update --project services/api/src/PhotoBIZ.Api/PhotoBIZ.Api.csproj --startup-project services/api/src/PhotoBIZ.Api/PhotoBIZ.Api.csproj
```

## 6) Run Services Locally

Use separate terminals.

### Terminal A: API

```powershell
dotnet run --project services/api/src/PhotoBIZ.Api/PhotoBIZ.Api.csproj
```

Default local URL from launch settings: `http://localhost:5082`

### Terminal B: Worker

```powershell
dotnet run --project services/api/src/PhotoBIZ.Worker/PhotoBIZ.Worker.csproj
```

### Terminal C: Admin Web

```powershell
Set-Location apps
npm run start:admin
```

Admin URL: `http://localhost:4200`

### Terminal D: Booth UI

```powershell
Set-Location apps
npm run start:booth
```

Booth URL: `http://localhost:4201`

### Optional Terminal E: Windows Agent Control Center / dev host

```powershell
dotnet run --project agent/windows-agent/src/PhotoBIZ.WindowsAgent/PhotoBIZ.WindowsAgent.csproj
```

The production Agent shape is a logged-in Windows user-session Control Center. During the transition to the WPF shell, the existing dev host remains useful for simulator and API-mode testing. The important runtime contract is the same: validate pairing, request a launch token, launch Chrome, then heartbeat and poll commands only while the local booth runtime is intentionally started.

## 7) Configure Windows Agent LumaBooth Settings

The Agent reads settings from the `PhotoBIZ` configuration section. Local defaults live in:

```text
agent/windows-agent/src/PhotoBIZ.WindowsAgent/appsettings.Development.json
```

Use user secrets for real booth credentials or local API passwords so they do not land in git:

```powershell
dotnet user-secrets set "PhotoBIZ:BoothCode" "<booth-code>" --project agent/windows-agent/src/PhotoBIZ.WindowsAgent/PhotoBIZ.WindowsAgent.csproj
dotnet user-secrets set "PhotoBIZ:AgentCredential" "<agent-credential>" --project agent/windows-agent/src/PhotoBIZ.WindowsAgent/PhotoBIZ.WindowsAgent.csproj
dotnet user-secrets set "PhotoBIZ:LumaBooth:ApiPassword" "<local-lumabooth-api-password>" --project agent/windows-agent/src/PhotoBIZ.WindowsAgent/PhotoBIZ.WindowsAgent.csproj
dotnet user-secrets set "PhotoBIZ:Display:BoothUiBaseUrl" "http://localhost:4201" --project agent/windows-agent/src/PhotoBIZ.WindowsAgent/PhotoBIZ.WindowsAgent.csproj
dotnet user-secrets set "PhotoBIZ:Display:KioskMode" "false" --project agent/windows-agent/src/PhotoBIZ.WindowsAgent/PhotoBIZ.WindowsAgent.csproj
```

### Simulator Mode

Simulator mode is the default and is best for normal backend/UI development without LumaBooth installed:

```json
{
  "PhotoBIZ": {
    "ApiBaseUrl": "http://localhost:5082",
    "BoothCode": "<booth-code>",
    "AgentCredential": "<agent-credential>",
    "PollIntervalSeconds": 5,
    "SimulatedSessionDurationSeconds": 6,
    "LumaBooth": {
      "Mode": "Simulator",
      "ApiBaseUrl": "http://localhost:1500",
      "ApiPassword": "",
      "TriggerListenerUrl": "http://127.0.0.1:5617/lumabooth/events",
      "StartTimeoutSeconds": 15
    },
    "Display": {
      "LumaBoothWindowTitle": "dslrBooth",
      "BoothUiWindowTitle": "BoothUi",
      "BoothUiBaseUrl": "http://localhost:4201",
      "ChromeExecutablePath": "",
      "ChromeUserDataDir": "",
      "LaunchBoothUiOnStartup": true,
      "KioskMode": false
    }
  }
}
```

In simulator mode, the Agent still pairs, requests a fresh Booth UI launch token, opens Chrome to `PhotoBIZ:Display:BoothUiBaseUrl/{token}` when `LaunchBoothUiOnStartup` is true, heartbeats, polls for commands, reports session started/completed, completes `PRINT_COPIES` commands without calling LumaBooth, and returns Booth UI focus after `SimulatedSessionDurationSeconds`. Pairing and launch-token creation do not make the booth online; the backend treats the booth as online only after heartbeat begins. Use `POST /api/agent/offline` or stop the local Agent runtime to take the booth offline immediately.

The Booth UI does not show a manual kiosk-token input. For normal booth testing, start the Windows Agent and let it launch Chrome to the token route, for example `http://localhost:4201/43E836977240C5E33F3C56581DC59E07A15BB08F8512C6C2`. That token route is what loads the booth's selected theme and active transaction state. The Agent correlates the launch token by sending its configured `BoothCode` and `AgentCredential` to the backend, then the backend returns the booth-scoped kiosk token for that booth.

When `PhotoBIZ:Display:KioskMode` is `true`, the Agent starts Chrome with kiosk flags and an isolated profile directory. Leave `PhotoBIZ:Display:ChromeUserDataDir` empty to use the default `%ProgramData%\PhotoBIZ\chrome-kiosk`, or set it to a booth-specific folder when testing multiple booths on one machine. Alt-Tab remains available because this is Chrome kiosk mode, not Windows assigned access. Keep `KioskMode` false for daily development so Chrome opens as a normal window.

### LumaBooth API Mode

Use API mode only on a Windows booth laptop with LumaBooth/dslrBooth Professional API enabled:

```powershell
dotnet user-secrets set "PhotoBIZ:LumaBooth:Mode" "Api" --project agent/windows-agent/src/PhotoBIZ.WindowsAgent/PhotoBIZ.WindowsAgent.csproj
dotnet user-secrets set "PhotoBIZ:LumaBooth:ApiBaseUrl" "http://localhost:1500" --project agent/windows-agent/src/PhotoBIZ.WindowsAgent/PhotoBIZ.WindowsAgent.csproj
dotnet user-secrets set "PhotoBIZ:LumaBooth:TriggerListenerUrl" "http://127.0.0.1:5617/lumabooth/events" --project agent/windows-agent/src/PhotoBIZ.WindowsAgent/PhotoBIZ.WindowsAgent.csproj
dotnet user-secrets set "PhotoBIZ:LumaBooth:StartTimeoutSeconds" "15" --project agent/windows-agent/src/PhotoBIZ.WindowsAgent/PhotoBIZ.WindowsAgent.csproj
dotnet user-secrets set "PhotoBIZ:Display:LumaBoothWindowTitle" "dslrBooth" --project agent/windows-agent/src/PhotoBIZ.WindowsAgent/PhotoBIZ.WindowsAgent.csproj
dotnet user-secrets set "PhotoBIZ:Display:BoothUiWindowTitle" "BoothUi" --project agent/windows-agent/src/PhotoBIZ.WindowsAgent/PhotoBIZ.WindowsAgent.csproj
dotnet user-secrets set "PhotoBIZ:Display:BoothUiBaseUrl" "http://localhost:4201" --project agent/windows-agent/src/PhotoBIZ.WindowsAgent/PhotoBIZ.WindowsAgent.csproj
dotnet user-secrets set "PhotoBIZ:Display:ChromeExecutablePath" "C:\Program Files\Google\Chrome\Application\chrome.exe" --project agent/windows-agent/src/PhotoBIZ.WindowsAgent/PhotoBIZ.WindowsAgent.csproj
dotnet user-secrets set "PhotoBIZ:Display:ChromeUserDataDir" "C:\ProgramData\PhotoBIZ\chrome-kiosk" --project agent/windows-agent/src/PhotoBIZ.WindowsAgent/PhotoBIZ.WindowsAgent.csproj
dotnet user-secrets set "PhotoBIZ:Display:LaunchBoothUiOnStartup" "true" --project agent/windows-agent/src/PhotoBIZ.WindowsAgent/PhotoBIZ.WindowsAgent.csproj
dotnet user-secrets set "PhotoBIZ:Display:KioskMode" "true" --project agent/windows-agent/src/PhotoBIZ.WindowsAgent/PhotoBIZ.WindowsAgent.csproj
```

API mode behavior:

- Agent starts LumaBooth with `GET /api/start?mode={mode}&password={password}` against `PhotoBIZ:LumaBooth:ApiBaseUrl`.
- Agent prints paid post-session extra copies with `GET /api/print?count={count}` against `PhotoBIZ:LumaBooth:ApiBaseUrl`.
- When `PhotoBIZ:LumaBooth:ApiPassword` is set, the Agent appends `password={password}` to start and print-copy requests.
- Supported PhotoBIZ modes are `PRINT`, `GIF`, `BOOMERANG`, and `VIDEO`; legacy `SESSION_STANDARD` is normalized to `PRINT`.
- Agent starts a local trigger listener at `PhotoBIZ:LumaBooth:TriggerListenerUrl`.
- `session_start` trigger events report backend session started.
- `session_end` trigger events report backend session completed and clear local active session context.
- Non-terminal events such as `printing`, `file_upload`, and `sharing_screen` are logged only.
- Window focus handoff is best effort; focus failures are warnings, not transaction failures.

Configure LumaBooth URL triggers to call the Agent listener on the same laptop. Use one URL per trigger event, for example:

```text
http://127.0.0.1:5617/lumabooth/events?event_type=session_start
http://127.0.0.1:5617/lumabooth/events?event_type=session_end
http://127.0.0.1:5617/lumabooth/events?event_type=printing
http://127.0.0.1:5617/lumabooth/events?event_type=file_upload
http://127.0.0.1:5617/lumabooth/events?event_type=sharing_screen
```

Optional trigger parameters `param1`, `param2`, `param3`, and `param4` are accepted and logged where useful:

```text
http://127.0.0.1:5617/lumabooth/events?event_type=printing&param1=<value>
```

The Agent stores the active local session context at:

```text
%ProgramData%\PhotoBIZ\agent\active-session.json
```

If local development gets stuck after killing the Agent or LumaBooth mid-session, stop the Agent, clear the stuck transaction through the cashier recovery flow when possible, and delete this file before restarting the Agent.

## 8) Optional: Full Containerized Backend Stack

If Docker daemon is available, you can run backend + infra + reverse proxy:

```powershell
docker compose up --build
```

Exposed ports:

- API: `http://localhost:5082`
- Reverse proxy (Caddy): `http://localhost:8080`
- Postgres: `localhost:55432`
- Redis: `localhost:6379`

Current Caddy behavior:

- Proxies `/api/*` and `/health` to API.
- Returns `PhotoBIZ reverse proxy scaffold` for other paths.

## 9) Smoke Checks

API health:

```powershell
Invoke-RestMethod http://localhost:5082/health
```

API status:

```powershell
Invoke-RestMethod http://localhost:5082/api/platform/status
```

Expected status payload:

```json
{
  "service": "PhotoBIZ.Api",
  "status": "ok",
  "runtime": "net10.0"
}
```

## 10) Test Commands

Backend tests:

```powershell
dotnet test PhotoBIZ.slnx
```

Frontend checks:

```powershell
Set-Location apps
npm run build
npm run test:ci
```

## 11) Troubleshooting

### Docker compose cannot start (`dockerDesktopLinuxEngine` pipe error)

If you get an error like:

`failed to connect to the docker API at npipe:////./pipe/dockerDesktopLinuxEngine`

Then Docker Desktop is not running (or Linux engine is unavailable). Start Docker Desktop first, then retry.

### Reset local database volume

To fully reset local Postgres data:

```powershell
docker compose down -v
docker compose up -d postgres redis
dotnet tool run dotnet-ef database update --project services/api/src/PhotoBIZ.Api/PhotoBIZ.Api.csproj --startup-project services/api/src/PhotoBIZ.Api/PhotoBIZ.Api.csproj
```

## 12) Recommended Daily Flow

1. `docker compose up -d postgres redis`
2. `dotnet run` API + Worker
3. `npm run start:admin` + `npm run start:booth`
4. Verify `/health` and `/api/platform/status`
5. Run `dotnet test PhotoBIZ.slnx` before pushing

