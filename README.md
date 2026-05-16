# PhotoBIZ

This project contains the product documentation, architecture, UI mockups, and future source code for PhotoBIZ, a multi-tenant SaaS platform for photobooth operators in the Philippines.

PhotoBIZ is designed around Windows-based booths using LumaBooth for photo capture, printing, and Fotoshare digital delivery. The custom system provides the customer-facing Booth UI, central SaaS/client/cashier web application, payment orchestration, booth monitoring, and tenant-scoped workflows.

## Primary Documents

- [Product Requirements Document](docs/PRD.md)
- [Architecture and Diagrams](docs/ARCHITECTURE.md)
- [Hosting and Deployment Plan](docs/DEPLOYMENT.md)
- [UI Mockups](docs/design/README.md)
- [UI Workflow Mockups](docs/design/WORKFLOWS.md)

## Planned Source Layout

```text
photobooth-platform/
  apps/
    admin-web/       # Central Web App for Application Owner, client users, and cashiers
    booth-ui/        # Customer-facing booth screen
  services/
    api/             # Backend API, auth, transactions, payments, reporting
  agent/
    windows-agent/   # Local Windows booth agent for LumaBooth integration
  docs/
    PRD.md
    ARCHITECTURE.md
```

## Local Development

Prerequisites:

- .NET SDK 10.0.202 or compatible .NET 10 SDK.
- Node.js 24 and npm 11.
- Docker with Docker Compose.

Common commands:

```powershell
dotnet restore PhotoBIZ.slnx
dotnet build PhotoBIZ.slnx
dotnet test PhotoBIZ.slnx
dotnet tool restore
dotnet tool run dotnet-ef database update --project services/api/src/PhotoBIZ.Api/PhotoBIZ.Api.csproj --startup-project services/api/src/PhotoBIZ.Api/PhotoBIZ.Api.csproj

Set-Location apps
npm ci
npm run build
npm run test:ci

Set-Location ..
docker compose up --build
```

Local endpoints:

- API health: `http://localhost:5082/health`
- API status: `http://localhost:5082/api/platform/status`
- PostgreSQL: `localhost:55432`
- Reverse proxy scaffold: `http://localhost:8080`

## Product Summary

The Application Owner sells PhotoBIZ subscriptions to client businesses on a per-active-booth basis. Clients manage their own locations, booths, configurable booth offer packages, staff, booth-level appearance, payment option assignments, transactions, and reports.

For MVP, customers use the Booth UI to review the booth's active offer and complete cash payment when payment is required. Time-unlimited and session-count plans are activated by the cashier, then the Booth UI goes straight from welcome to LumaBooth and back. Maya Checkout QR and Maya Terminal ECR are documented as locked setup flows for future integration. LumaBooth handles capture, printing through a DNP RX1 printer, and digital sharing through LumaBooth/Fotoshare.
