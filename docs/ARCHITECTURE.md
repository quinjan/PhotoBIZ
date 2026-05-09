# Architecture And Diagrams

## Overview

This document is the source of truth for the PhotoBIZ platform architecture. Future implementation work must follow the decisions, boundaries, state machines, and phases defined here unless this document is explicitly updated. If another project document conflicts with this file, this file takes precedence.

PhotoBIZ is a multi-tenant SaaS platform. The Application Owner manages client accounts and manual subscriptions. Client users manage their own locations, booths, packages, sessions, cashier workflows, and reports. Booth UI and Windows Agent clients operate within one paired booth and one client account.

The platform has three primary runtime surfaces:

1. Central Web App: used by Application Owner, Client Owner, Client Admin, and Cashier users.
2. Booth UI: customer-facing screen on the booth's extended monitor.
3. Windows Booth Agent: local process on the booth laptop that controls LumaBooth integration.

The backend owns tenant isolation, subscription enforcement, transaction state, payment state, and booth commands.

## High-Level System Diagram

```mermaid
flowchart LR
  AppOwner["Application Owner<br/>PhotoBIZ Platform"] --> AdminWeb["Central Web App"]
  ClientOwner["Client Owner / Admin<br/>Client Account"] --> AdminWeb
  Cashier["Cashier<br/>Assigned Booth"] --> AdminWeb
  Customer["Customer<br/>Extended Screen"] --> BoothUI["Booth UI<br/>Kiosk App"]

  AdminWeb --> API["Backend API"]
  BoothUI --> API

  API --> DB[("PostgreSQL")]
  API --> Jobs["Hangfire Jobs<br/>PostgreSQL-backed"]
  API <--> Realtime["Realtime Gateway<br/>SignalR"]
  Realtime <--> Agent["Windows Booth Agent<br/>Laptop"]

  Agent <--> Luma["LumaBooth<br/>Windows"]
  Luma --> Printer["DNP RX1 Printer"]
  Luma --> Camera["Canon R50 Camera"]
  Luma --> Fotoshare["LumaBooth / Fotoshare<br/>Digital Delivery"]

  API -. "Coming Soon" .-> Maya["Maya Checkout QR / Terminal ECR"]
```

## Tenant And Subscription Flow

```mermaid
sequenceDiagram
  autonumber
  participant AO as Application Owner
  participant API as Backend API
  participant CO as Client Owner
  participant A as Windows Agent
  participant B as Booth UI

  AO->>API: Create client account
  AO->>API: Assign manual per-booth subscription
  AO->>API: Create/invite Client Owner
  CO->>API: Create location, users, packages
  CO->>API: Register booth
  API->>API: Validate subscription status and booth allowance
  API-->>CO: Agent credential + kiosk token
  A->>API: Pair booth agent
  B->>API: Load booth config with kiosk token
  API-->>B: Client theme, active session, packages, payment options
```

## MVP Runtime Flow

```mermaid
sequenceDiagram
  autonumber
  participant C as Customer
  participant B as Booth UI
  participant API as Backend API
  participant POS as Cashier POS
  participant A as Windows Agent
  participant L as LumaBooth

  C->>B: Choose package
  B->>API: Create transaction
  API->>API: Validate client subscription and booth state
  API-->>B: Transaction pending
  C->>B: Choose cash payment
  B->>API: Set payment method CASH
  API-->>POS: Show pending cash request
  POS->>API: Approve cash payment

  API->>API: Mark transaction PAID
  API->>A: Start LumaBooth session
  A->>L: Start configured session/preset
  L-->>A: Session events
  A-->>API: Session started / completed
  API-->>B: Return to welcome
```

## Booth UI Config Flow

```mermaid
sequenceDiagram
  autonumber
  participant B as Booth UI
  participant API as Backend API
  participant DB as PostgreSQL

  B->>API: GET /booth-ui/config with kiosk token
  API->>DB: Resolve booth, client account, subscription, active session
  API->>API: Validate booth token and subscription session permission
  API-->>B: Theme, session text, packages, payment options, booth state
  B->>B: Apply CSS variables and render current state
```

Minimum `GET /booth-ui/config` response shape:

```json
{
  "client": {
    "displayName": "The Memory Box",
    "logoUrl": null
  },
  "theme": {
    "preset": "VINTAGE_FILM",
    "primaryColor": "#2f6868",
    "accentColor": "#f5d27e",
    "backgroundImageUrl": "/assets/themes/memory-box.jpg",
    "fontMode": "serif"
  },
  "session": {
    "label": "SM Manila - Vintage Summer",
    "welcomeHeadline": "Step Into The Memory Box",
    "welcomeSubtitle": "Choose your print package, pay at the counter, then strike your best pose."
  },
  "booth": {
    "id": "booth-id",
    "state": "WELCOME"
  },
  "packages": [],
  "paymentOptions": ["CASH"]
}
```

Future payment option values:

- `MAYA_CHECKOUT_QR`: returned only after client Maya Checkout configuration is verified and enabled in a future phase.
- `MAYA_TERMINAL_ECR`: returned only after client Maya configuration and booth terminal configuration are verified and enabled in a future phase.

## Cash Payment State Flow

```mermaid
stateDiagram-v2
  [*] --> WELCOME
  WELCOME --> PACKAGE_SELECTED: Customer chooses package
  PACKAGE_SELECTED --> PENDING_CASH: Customer chooses cash
  PENDING_CASH --> PAID: Cashier approves cash
  PENDING_CASH --> EXPIRED: Timeout
  PENDING_CASH --> CANCELLED: Cashier cancels
  PAID --> STARTING_SESSION: Backend commands agent
  STARTING_SESSION --> IN_SESSION: LumaBooth starts
  IN_SESSION --> COMPLETED: LumaBooth session ends
  STARTING_SESSION --> SESSION_FAILED: Agent/LumaBooth error
  IN_SESSION --> SESSION_FAILED: Agent/LumaBooth error
  COMPLETED --> WELCOME: Reset booth
  EXPIRED --> WELCOME: Reset booth
  CANCELLED --> WELCOME: Reset booth
  SESSION_FAILED --> WELCOME: Manual recovery
```

## Coming Soon Maya Checkout QR Flow

```mermaid
stateDiagram-v2
  [*] --> WELCOME
  WELCOME --> PACKAGE_SELECTED: Customer chooses package
  PACKAGE_SELECTED --> PENDING_MAYA_CHECKOUT_QR: Customer chooses Maya Checkout QR
  PENDING_MAYA_CHECKOUT_QR --> PAID: Maya webhook confirms paid
  PENDING_MAYA_CHECKOUT_QR --> EXPIRED: Maya webhook or timeout
  PENDING_MAYA_CHECKOUT_QR --> PAYMENT_FAILED: Maya webhook confirms failure
  PENDING_MAYA_CHECKOUT_QR --> CANCELLED: Customer or cashier cancels
  PAID --> STARTING_SESSION
  STARTING_SESSION --> IN_SESSION
  IN_SESSION --> COMPLETED
  COMPLETED --> WELCOME
  EXPIRED --> WELCOME
  PAYMENT_FAILED --> WELCOME
  CANCELLED --> WELCOME
```

## Coming Soon Maya Checkout QR Runtime Flow

```mermaid
sequenceDiagram
  autonumber
  participant C as Customer
  participant B as Booth UI
  participant API as Backend API
  participant M as Maya
  participant A as Windows Agent

  C->>B: Choose package
  B->>API: Create transaction
  C->>B: Choose Maya Checkout QR
  API->>API: Load encrypted client Maya credentials
  API->>M: Create Maya Checkout payment
  M-->>API: Payment reference + QR data
  API-->>B: Render QR code
  C->>M: Pay using wallet/bank app
  M-->>API: Webhook payment success
  API->>API: Verify and mark PAID
  API->>A: Start LumaBooth session
```

## Application Boundaries

### Central Web App

Stack:

- Angular.
- TypeScript.
- Angular Material.

Responsibilities:

- Authentication screens.
- Application Owner platform dashboard.
- Client account management.
- Manual subscription management.
- Client Owner dashboard.
- Cashier POS view.
- User management.
- Location management.
- Booth management.
- Package management.
- Booth UI theme/session appearance management.
- Transaction monitoring.
- Reports.
- Audit logs.

### Booth UI

Stack:

- Angular web app running in browser kiosk mode on the booth laptop's extended customer-facing screen.

Responsibilities:

- Authenticate with booth-scoped kiosk token.
- Load client/theme/session config from backend.
- Map theme values to CSS variables.
- Display welcome screen.
- Display assigned packages.
- Let customer select payment method.
- Display pending payment state.
- Display cash waiting state for MVP.
- Display coming soon cashless payment methods when configured as preview-only.
- Display expiration/error states.
- Return to welcome when backend state allows.

Rules:

- Booth UI must not require cashier login during daily use.
- Booth UI must not directly approve payment or start LumaBooth.
- Booth UI must not accept arbitrary CSS or script customization.

### Backend API

Stack:

- ASP.NET Core on .NET 8.
- PostgreSQL.
- Redis for realtime backplane, cache, and distributed locks.
- SignalR for realtime updates.
- Entity Framework Core for database access.
- Hangfire with PostgreSQL storage for background jobs and transaction expiration.

Responsibilities:

- Authentication and authorization.
- Tenant isolation.
- Role-based access control.
- Client account APIs.
- Manual subscription APIs.
- Client/location/booth/user/package APIs.
- Booth UI config API.
- Transaction state machine.
- Payment orchestration.
- Maya Checkout QR provider integration during Phase 5.
- Maya Terminal ECR provider integration during Phase 5.
- Agent command dispatch.
- Realtime updates to Booth UI and Cashier POS.
- Reporting.
- Audit logging.

### Windows Booth Agent

Stack:

- .NET 8.
- Windows Service.

Responsibilities:

- Pair with backend booth record.
- Maintain heartbeat.
- Listen for start-session commands.
- Call LumaBooth through the documented local API/integration path.
- Receive LumaBooth triggers/webhooks.
- Report session state.
- Manage local recovery.
- Manage Booth UI and LumaBooth app/window focus on the booth laptop.

## Repository Structure

```text
photobooth-platform/
  apps/
    admin-web/
      src/
    booth-ui/
      src/
  services/
    api/
      src/
  agent/
    windows-agent/
      src/
  docs/
    PRD.md
    ARCHITECTURE.md
```

## Data Model

```mermaid
erDiagram
  CLIENT_ACCOUNT ||--o{ LOCATION : owns
  CLIENT_ACCOUNT ||--o{ USER : has
  CLIENT_ACCOUNT ||--o{ PACKAGE : defines
  CLIENT_ACCOUNT ||--|| CLIENT_BOOTH_THEME : configures
  CLIENT_ACCOUNT ||--o{ CLIENT_SUBSCRIPTION : subscribes
  CLIENT_ACCOUNT ||--o{ CLIENT_PAYMENT_PROVIDER_CONFIG : configures
  SUBSCRIPTION_PLAN ||--o{ CLIENT_SUBSCRIPTION : assigned_as
  LOCATION ||--o{ BOOTH : contains
  BOOTH ||--o{ BOOTH_TERMINAL_CONFIG : configures
  BOOTH ||--o{ BOOTH_PACKAGE : offers
  PACKAGE ||--o{ BOOTH_PACKAGE : assigned_to
  BOOTH ||--o{ TRANSACTION : records
  USER ||--o{ TRANSACTION : approves
  TRANSACTION ||--o{ PAYMENT_ATTEMPT : has
  TRANSACTION ||--o{ BOOTH_SESSION : starts
  USER ||--o{ AUDIT_LOG : performs

  CLIENT_ACCOUNT {
    uuid id
    string name
    string status
    datetime created_at
  }

  SUBSCRIPTION_PLAN {
    uuid id
    string name
    int price_per_booth_cents
    string currency
    bool active
  }

  CLIENT_SUBSCRIPTION {
    uuid id
    uuid client_account_id
    uuid subscription_plan_id
    string status
    int active_booth_allowance
    date starts_on
    date ends_on
    string notes
  }

  CLIENT_BOOTH_THEME {
    uuid id
    uuid client_account_id
    string display_name
    string theme_preset
    string primary_color
    string accent_color
    string background_image_url
    string logo_url
    string default_welcome_headline
    string default_welcome_subtitle
  }

  CLIENT_PAYMENT_PROVIDER_CONFIG {
    uuid id
    uuid client_account_id
    string provider
    string integration_type
    string status
    string business_account_name
    string public_key_masked
    string encrypted_secret_key
    string webhook_url
    datetime verified_at
  }

  LOCATION {
    uuid id
    uuid client_account_id
    string name
    string address
    string status
  }

  USER {
    uuid id
    uuid client_account_id
    uuid assigned_booth_id
    string name
    string email
    string password_hash
    string role
    string status
  }

  BOOTH {
    uuid id
    uuid client_account_id
    uuid location_id
    string name
    string code
    string status
    string current_state
    datetime last_heartbeat_at
  }

  BOOTH_TERMINAL_CONFIG {
    uuid id
    uuid booth_id
    string provider
    string terminal_model
    string terminal_reference
    string serial_or_asset_tag
    string com_port
    string status
    datetime last_connection_test_at
  }

  PACKAGE {
    uuid id
    uuid client_account_id
    string name
    int price_cents
    string currency
    int print_count
    string paper_size
    string lumabooth_preset_ref
    bool active
  }

  BOOTH_PACKAGE {
    uuid booth_id
    uuid package_id
    bool active
  }

  TRANSACTION {
    uuid id
    uuid client_account_id
    uuid location_id
    uuid booth_id
    uuid package_id
    uuid approved_by_user_id
    string transaction_number
    string payment_method
    string status
    int amount_cents
    string currency
    json package_snapshot
    datetime expires_at
    datetime paid_at
    datetime completed_at
  }

  PAYMENT_ATTEMPT {
    uuid id
    uuid transaction_id
    string provider
    string provider_reference
    string status
    json raw_payload
    datetime created_at
  }

  BOOTH_SESSION {
    uuid id
    uuid transaction_id
    uuid booth_id
    string lumabooth_session_ref
    string status
    string welcome_headline
    string welcome_subtitle
    string session_label
    json assigned_package_ids
    datetime started_at
    datetime ended_at
  }

  AUDIT_LOG {
    uuid id
    uuid client_account_id
    uuid user_id
    string action
    string entity_type
    uuid entity_id
    json metadata
    datetime created_at
  }
```

## Transaction State Machine Rules

Only the backend may transition transactions between states.

Allowed MVP transitions:

```text
CREATED -> PENDING_CASH
PENDING_CASH -> PAID
PENDING_CASH -> EXPIRED
PENDING_CASH -> CANCELLED
PAID -> STARTING_SESSION
STARTING_SESSION -> IN_SESSION
STARTING_SESSION -> SESSION_FAILED
IN_SESSION -> COMPLETED
IN_SESSION -> SESSION_FAILED
SESSION_FAILED -> CANCELLED
```

Rules:

- Booth UI cannot mark transactions as paid.
- Cashiers can approve only transactions for their assigned booth.
- Application Owner can manage clients/subscriptions but does not normally approve client booth transactions.
- Expired transactions release the booth.
- Completed transactions are immutable except for administrative notes or future refund records.

## Realtime Channels

Channels:

- `platform:dashboard`
- `client:{clientAccountId}:dashboard`
- `booth:{boothId}:state`
- `booth:{boothId}:commands`
- `cashier:{userId}:notifications`
- `location:{locationId}:dashboard`

Realtime events:

- `client.subscription.changed`
- `booth.state.changed`
- `transaction.created`
- `transaction.payment_pending`
- `transaction.paid`
- `transaction.expired`
- `transaction.cancelled`
- `session.starting`
- `session.started`
- `session.completed`
- `session.failed`
- `agent.heartbeat`
- `agent.offline`

## Deployment Architecture

The hosting plan is documented in [Hosting And Deployment Plan](DEPLOYMENT.md).

MVP deployment:

- DigitalOcean Basic Droplet in Singapore.
- Docker Compose.
- Host Angular Admin Web, Angular Booth UI, ASP.NET Core API, PostgreSQL, Redis, and reverse proxy on the same server.
- Deploy through GitHub Actions over SSH.
- Cloudflare DNS.
- VPS backups and nightly PostgreSQL dumps before live use.

```mermaid
flowchart TB
  subgraph VPS["DigitalOcean Singapore VPS"]
    Proxy["Caddy Reverse Proxy<br/>TLS + Routing"]
    Admin["Angular Admin Web<br/>Static Files"]
    Booth["Angular Booth UI<br/>Static Files"]
    API["ASP.NET Core API<br/>.NET 8"]
    Worker["Hangfire Worker<br/>Transaction Expiration + Jobs"]
    DB[("PostgreSQL")]
    Redis[("Redis")]
  end

  subgraph Mall["Mall Booth Location"]
    Laptop["Windows Laptop"]
    Agent["Windows Booth Agent"]
    Browser["Kiosk Browser<br/>Booth UI"]
    Luma["LumaBooth"]
    Printer["DNP RX1"]
    Camera["Canon R50"]
  end

  Proxy --> Admin
  Proxy --> Booth
  Proxy --> API
  API --> DB
  API --> Redis
  Worker --> DB
  Worker --> Redis
  Browser --> Proxy
  Agent <--> Proxy
  Agent <--> Luma
  Luma --> Printer
  Luma --> Camera
```

## Technology Decisions

- Repository: single repository containing Angular apps, ASP.NET Core API, Windows Agent, and documentation.
- Frontend workspace: one Angular workspace containing two separate applications: `admin-web` and `booth-ui`.
- Shared frontend code lives in Angular workspace libraries for API clients, DTOs, validation helpers, constants, and reusable UI primitives.
- Admin Web: Angular + TypeScript + Angular Material.
- Booth UI: Angular + TypeScript, optimized for kiosk browser use.
- Backend API: ASP.NET Core on .NET 8.
- Database: PostgreSQL.
- ORM: Entity Framework Core.
- Realtime: SignalR.
- Background jobs: Hangfire with PostgreSQL storage.
- Cache/locks/backplane: Redis.
- Windows Agent: .NET 8 Windows Service.
- Admin authentication: email/password login with secure HttpOnly cookie sessions.
- Booth UI authentication: booth-scoped kiosk token issued during booth pairing. No cashier unlock/login is required to show the Booth UI.
- Agent authentication: booth agent credential issued during pairing.
- Hosting: DigitalOcean Singapore VPS using Docker Compose.
- DNS: Cloudflare.
- CI/CD: GitHub Actions deploying over SSH.

## Environment Strategy

Environments:

- `local`: developer machine.
- `production`: live booths.

Each booth is paired to exactly one environment.

## Security Notes

- Store password hashes with a strong hashing algorithm.
- HTTPS is required in production.
- Admin sessions use secure HttpOnly cookies.
- Agent credentials are separate from user credentials.
- Treat booth agents as privileged clients.
- Enforce tenant isolation for all client-scoped data.
- Validate subscription status and booth allowance before activating booths or starting sessions.
- Validate colors and image URLs for Booth UI themes.
- Reject arbitrary client CSS, scripts, or layout definitions.
- All payment approvals and subscription changes must be audited.
- Cashiers must be scoped to assigned booths.
- Do not trust client-side transaction state.

## Implementation Phases

### Phase 1: MVP Foundations

- Monorepo setup.
- Database schema.
- Auth and roles.
- Client account management.
- Manual subscription management.
- Client user management.
- Location and booth management.
- Package management.
- Package assignment to booth.
- Minimal tenant Booth UI theme management.

### Phase 2: Transaction And POS

- Booth UI config endpoint.
- Booth UI package selection.
- Cash transaction flow.
- Cashier approval.
- Expiration jobs.
- Transaction dashboard.

### Phase 3: Agent And LumaBooth

- Agent pairing.
- Agent heartbeat.
- Backend command dispatch.
- Start LumaBooth session.
- Receive session completion.
- Booth state recovery.

### Phase 4: Reporting And Operations

- Application Owner dashboard.
- Client Owner dashboard.
- Cashier booth dashboard.
- Sales reports.
- Subscription health reports.
- Booth status reports.
- Audit logs.

### Phase 5: Coming Soon Real Payments

- Client-owned Maya Checkout QR integration.
- Maya webhook handling.
- Payment reconciliation.
- Maya Terminal ECR integration.

## Architectural Principles

- Backend owns truth.
- Client data is tenant-isolated by `client_account_id`.
- Application Owner manages SaaS clients and subscriptions.
- Client users manage only their own client account.
- Booth UI is a display and input surface, not a payment authority.
- Booth UI customization is config-driven and constrained.
- Agent owns local Windows and LumaBooth integration.
- LumaBooth remains responsible for capture, print, and Fotoshare.
- Package data is snapshotted into transactions.
- Subscription status and booth allowance protect platform access.
