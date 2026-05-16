# Local Development Guide

This guide is the practical "how to run PhotoBIZ locally" reference for the current codebase.

## 1) Current Codebase State (Validated May 16, 2026)

PhotoBIZ is currently in early scaffold/foundation state:

- API starts and exposes:
  - `GET /health`
  - `GET /api/platform/status`
- Data layer is present with EF Core model + migrations for the MVP foundation schema.
- Worker and Windows Agent run as heartbeat background services.
- Angular `admin-web` and `booth-ui` apps run with scaffold UI.
- Backend/business workflows from the PRD (transactions, cashier approvals, booth config APIs, realtime flows, etc.) are not fully implemented yet.

Validation run in this repo:

- `dotnet test PhotoBIZ.slnx`: passes.
- `npm ci`: passes.
- Angular development builds pass for both apps.
- `npm run build` and `npm run test:ci` currently fail due missing `css-select` module in the Angular toolchain path (details in Troubleshooting).

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

### Optional Terminal E: Windows Agent (local console mode)

```powershell
dotnet run --project agent/windows-agent/src/PhotoBIZ.WindowsAgent/PhotoBIZ.WindowsAgent.csproj
```

Note: on Windows, this project is service-capable (`AddWindowsService`) but runs fine from `dotnet run` during development.

## 7) Optional: Full Containerized Backend Stack

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

## 8) Smoke Checks

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

## 9) Test Commands

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

At current state, the frontend commands above fail with a `Cannot find module 'css-select'` error; see Troubleshooting.

## 10) Troubleshooting

### Docker compose cannot start (`dockerDesktopLinuxEngine` pipe error)

If you get an error like:

`failed to connect to the docker API at npipe:////./pipe/dockerDesktopLinuxEngine`

Then Docker Desktop is not running (or Linux engine is unavailable). Start Docker Desktop first, then retry.

### Angular production build/test fails with missing `css-select`

Observed on this codebase after fresh `npm ci`:

- `npm run build`
- `npm run test:ci`

Both fail with `Cannot find module 'css-select'` from Angular build tooling (`beasties` path).

Current workaround for day-to-day local progress:

- Use dev servers: `npm run start:admin`, `npm run start:booth`
- Use development build configuration:
  - `npx ng build admin-web --configuration development`
  - `npx ng build booth-ui --configuration development`

### Reset local database volume

To fully reset local Postgres data:

```powershell
docker compose down -v
docker compose up -d postgres redis
dotnet tool run dotnet-ef database update --project services/api/src/PhotoBIZ.Api/PhotoBIZ.Api.csproj --startup-project services/api/src/PhotoBIZ.Api/PhotoBIZ.Api.csproj
```

## 11) Recommended Daily Flow

1. `docker compose up -d postgres redis`
2. `dotnet run` API + Worker
3. `npm run start:admin` + `npm run start:booth`
4. Verify `/health` and `/api/platform/status`
5. Run `dotnet test PhotoBIZ.slnx` before pushing

