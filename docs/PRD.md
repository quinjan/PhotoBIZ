# Product Requirements Document

## Product Name

PhotoBIZ

## Purpose

PhotoBIZ is a multi-tenant SaaS platform for photobooth businesses in the Philippines. The Application Owner sells software subscriptions to client businesses. Each client uses PhotoBIZ to manage their own locations, booths, packages, staff, customer-facing Booth UI, payment approval workflow, transactions, and reports.

The booth workflow remains: customer chooses a package, chooses a payment method, payment is approved or confirmed, LumaBooth starts on Windows, photos are captured, prints are produced through LumaBooth, and digital copies are delivered through LumaBooth/Fotoshare.

## Current Operating Assumptions

- Market: Philippines.
- Business model: SaaS subscription sold to photobooth operators.
- MVP subscription model: manual subscription management by the Application Owner.
- Subscription unit: per active booth.
- Initial booth environment: mall or staffed retail location.
- Initial staffing model: one cashier per booth.
- Initial payment model: cash approval only.
- Coming soon payment model: `MAYA_CHECKOUT_QR` and `MAYA_TERMINAL_ECR`.
- Photo booth software: LumaBooth on Windows.
- Digital delivery: LumaBooth/Fotoshare.
- Starter hardware: DNP RX1 printer and Canon R50 camera.
- Booth computer: Windows laptop.
- Display setup: laptop plus extended customer-facing screen.
- Booth UI: web application or kiosk-mode browser shown on the extended screen.
- Central Web App: SaaS admin, client admin, and cashier/POS application.

## Goals

1. Let the Application Owner manage client accounts and manual per-booth subscriptions.
2. Let Client Owners manage their own users, locations, booths, packages, active sessions, and reports.
3. Let Client Admins manage booth operations inside one client account.
4. Let Cashiers approve payments and recover sessions for their assigned booth.
5. Let customers select packages and payment methods on the Booth UI.
6. Integrate with LumaBooth to start sessions and receive session completion events.
7. Track tenant-isolated transactions, booth states, session states, audit logs, and reports.
8. Support minimal tenant Booth UI customization for MVP.

## Non-Goals For MVP

- No automated billing provider integration.
- No client self-signup or automated trial onboarding.
- No automated invoice collection.
- No bill reader or coin acceptor integration.
- No real Maya production integration yet.
- No custom digital photo gallery, because LumaBooth/Fotoshare handles digital delivery.
- No custom camera, printer, template, or capture engine.
- No customer accounts.
- No loyalty system.
- No arbitrary client CSS, custom layouts, or advanced theme builder.
- No retake or extra-copy upsell flow.
- No fully unattended recovery workflow.

## User Roles

### Application Owner

The Application Owner is the PhotoBIZ platform owner.

Permissions:

- Manage all client accounts.
- Create, edit, suspend, reactivate, and archive clients.
- Manage manual subscriptions and per-booth allowances.
- View platform-wide client, booth, and subscription health.
- View cross-client reports.
- Access client accounts for support when needed.
- Manage global SaaS settings.
- View platform audit logs.

### Client Owner

The Client Owner owns one subscribed client account.

Permissions:

- Manage client profile and client Booth UI branding.
- Manage client users.
- Manage locations.
- Register, unregister, enable, disable, and end booths.
- Create, edit, activate, and deactivate packages.
- Assign packages to booths.
- Manage active booth session appearance.
- View transactions, reports, and audit logs for their client account.
- Configure client-level payment/session settings allowed by PhotoBIZ.

### Client Admin

The Client Admin manages booth operations for one client account.

Permissions:

- Manage Client Admin and Cashier users inside the client account.
- Manage assigned client locations and booths.
- Manage packages and booth package assignments.
- Manage active session appearance.
- View client transactions and reports.
- Perform booth recovery actions.
- Cannot manage subscription, billing status, or Application Owner support access.

### Cashier

The Cashier is assigned to exactly one booth.

Permissions:

- View only the assigned booth.
- View current booth state.
- View pending payment requests for the assigned booth.
- Approve cash payments.
- Cancel or expire a pending transaction.
- View today's transactions and sales for the assigned booth.
- Trigger basic recovery actions, such as returning booth to welcome screen.

## Subscription Model

MVP subscriptions are manually managed by the Application Owner.

Subscription statuses:

- `TRIAL`
- `ACTIVE`
- `PAST_DUE`
- `SUSPENDED`
- `CANCELLED`

Rules:

- Subscription allowance is based on active booth count.
- Client accounts cannot activate more booths than their subscription allowance.
- Suspended clients cannot start new booth sessions.
- Suspended or cancelled clients retain historical transactions and reports unless access is explicitly restricted by the Application Owner.
- Subscription changes are audit logged.

## Core Product Areas

### 1. Central Web App

Required MVP sections:

- Login.
- Application Owner platform dashboard.
- Client accounts.
- Manual subscription editor.
- Client Owner dashboard.
- Cashier POS view.
- Users.
- Locations.
- Booths.
- Packages.
- Session appearance.
- Transactions.
- Reports.
- Settings.
- Audit logs.

### 2. Booth UI

The Booth UI is the customer-facing interface shown on the booth's extended screen.

Required MVP screens:

- Welcome screen.
- Package selection.
- Payment method selection.
- Cash payment waiting screen.
- Coming soon cashless payment method preview.
- Payment approved screen.
- Starting session screen.
- Session in progress or LumaBooth handoff screen.
- Expired transaction screen.
- Error/recovery screen.

Rules:

- Booth UI is accessible only through a booth-scoped kiosk token.
- Booth UI does not require cashier login or daily unlock.
- Booth UI displays only active packages assigned to the booth.
- Booth UI reflects client-level branding and active session overrides.
- Booth UI cannot directly approve payment or start LumaBooth.

### 3. Windows Booth Agent

The Windows Booth Agent runs locally on the booth laptop.

Responsibilities:

- Authenticate and pair with a booth record.
- Maintain heartbeat connection with backend.
- Receive commands from backend.
- Start LumaBooth sessions through LumaBooth integration.
- Receive LumaBooth triggers or webhooks.
- Report session state changes.
- Report basic local health.
- Manage Booth UI and LumaBooth window focus where possible.
- Recover to welcome screen after session completion or failure.

### 4. Tenant Booth UI Customization

MVP customization is intentionally limited and safe.

Client-level customization:

- Brand/display name.
- Logo.
- Background image.
- Theme preset: `VINTAGE_FILM` or `MODERN_POP`.
- Primary color.
- Accent color.
- Default welcome headline.
- Default welcome subtitle.

Active session overrides:

- Session label.
- Welcome headline.
- Welcome subtitle.
- Assigned packages.

Guardrails:

- No arbitrary CSS.
- No custom layouts.
- No uploaded executable/script content.
- Colors and image URLs must be validated.
- Theme changes are client-scoped and audit logged.

## Key Workflows

### Workflow A: Client Subscription Setup

1. Application Owner creates a client account.
2. Application Owner assigns a manual subscription plan and active booth allowance.
3. Application Owner creates or invites the first Client Owner.
4. Client Owner signs in and configures locations, users, packages, and booths.

### Workflow B: Booth Registration To Live

1. Client Owner or Client Admin creates a location.
2. Client Owner or Client Admin registers a booth.
3. Backend validates subscription status and active booth allowance.
4. System creates agent credentials and booth-scoped kiosk token.
5. Windows Agent pairs with the booth record.
6. Booth UI opens using the booth token.
7. Client assigns active packages to the booth.
8. Client configures client branding and active session appearance.
9. Booth becomes available.

### Workflow C: Package Assignment To Booth

1. Client Owner or Client Admin creates a package.
2. Client Owner or Client Admin assigns package to one or more booths.
3. Booth UI loads active packages assigned to its booth.
4. Customer can choose only from those packages.

### Workflow D: Cash Payment

1. Booth UI shows active packages.
2. Customer chooses package.
3. Customer chooses cash payment.
4. Backend creates a transaction with status `PENDING_CASH`.
5. Cashier sees the pending cash request in the Central Web App.
6. Cashier collects cash.
7. Cashier clicks `Approve Cash`.
8. Backend marks transaction as `PAID`.
9. Backend sends start-session command to the booth agent.
10. Agent starts the correct LumaBooth session.
11. LumaBooth handles capture, printing, and Fotoshare sharing.
12. Agent receives session completion signal.
13. Backend marks transaction as `COMPLETED`.
14. Booth UI returns to welcome screen.

### Workflow E: Coming Soon Maya Setup

1. Client Owner opens Payment Settings.
2. Client Owner sees `MAYA_CHECKOUT_QR` and `MAYA_TERMINAL_ECR` as coming soon.
3. Client Owner can review the future setup checklist for Maya Business credentials and ECR terminal requirements.
4. Cashless payment methods remain unavailable to customers until PhotoBIZ enables real Maya integration in a future phase.

### Workflow F: Transaction Expiration

1. Customer chooses a package and payment method.
2. Transaction enters a pending payment status.
3. If payment is not approved before the configured expiration window, backend marks it `EXPIRED`.
4. Booth UI displays expiration message briefly.
5. Booth UI returns to welcome screen.
6. Booth becomes available for the next customer.

Default MVP expiration:

- Pending cash: 5 minutes.

### Workflow G: Session Recovery

1. Agent or backend detects that the booth is stuck, offline, or in an error state.
2. Cashier sees an alert in POS view.
3. Cashier can cancel, retry, or return booth to welcome.
4. All manual recovery actions are written to audit logs.

## Package Requirements

MVP package fields:

- Client account.
- Name.
- Description.
- Price in PHP.
- Number of prints.
- Paper size.
- LumaBooth session mode or preset reference.
- Active/inactive status.
- Assigned booth list.

Package details must be snapshotted into transactions so later package edits do not change historical records.

## Booth Requirements

MVP booth fields:

- Client account.
- Booth name.
- Booth code.
- Location.
- Status.
- Assigned cashier.
- Agent pairing status.
- Kiosk token status.
- Last heartbeat timestamp.
- Active package list.
- Current booth state.

Booth states:

- `OFFLINE`
- `WELCOME`
- `PACKAGE_SELECTED`
- `PAYMENT_METHOD_SELECTED`
- `PAYMENT_PENDING`
- `PAID`
- `STARTING_LUMABOOTH`
- `IN_LUMABOOTH_SESSION`
- `PRINTING_OR_SHARING`
- `COMPLETED`
- `RETURNING_TO_WELCOME`
- `ERROR`

## Transaction Requirements

Transaction statuses:

- `CREATED`
- `PENDING_CASH`
- `PAID`
- `STARTING_SESSION`
- `IN_SESSION`
- `COMPLETED`
- `EXPIRED`
- `CANCELLED`
- `PAYMENT_FAILED`
- `SESSION_FAILED`

Each transaction must store:

- Transaction number.
- Client account.
- Location.
- Booth.
- Cashier or approving user, when applicable.
- Package snapshot.
- Payment method.
- Payment status.
- Session status.
- Amount.
- Currency.
- Created timestamp.
- Expiration timestamp.
- Paid timestamp.
- Completed timestamp.
- Cancelled or failed reason.

## Reporting Requirements

Application Owner reports:

- Active clients.
- Active booths.
- Subscription status counts.
- Manual MRR estimate.
- Client health and suspended accounts.

Client reports:

- Today's gross sales.
- Today's completed sessions.
- Cash sales.
- Transactions by booth.
- Transactions by location.
- Package sales counts.
- Failed, expired, and cancelled transactions.

Future reports:

- Automated billing/revenue recognition.
- Cashier shift report.
- Printer/media usage estimate.
- Refunds and adjustments.
- Peak hour analysis.

## Dashboard Requirements

### Application Owner Dashboard

- Active clients.
- Active booths.
- Trial, active, past-due, suspended, and cancelled subscriptions.
- Manual MRR estimate.
- Clients over booth allowance.
- Recent platform audit events.

### Client Owner Dashboard

- Today's gross sales.
- Number of completed sessions.
- Cash sales.
- Active booths.
- Offline booths.
- Pending payment approvals.
- Failed or expired transactions.
- Top packages sold.
- Sales by location.
- Recent transactions.
- Current booth statuses.

### Cashier POS View

- Assigned booth name and status.
- Current transaction card.
- Package chosen by customer.
- Amount due.
- Payment method.
- Countdown until expiration.
- `Approve Cash` action.
- `Cancel Transaction` action.
- Today's sales.
- Today's completed sessions.
- Recent transactions for the assigned booth.
- Basic recovery action: return to welcome screen.

## Payment Requirements

### Cash MVP

Cash is approved manually by the cashier.

Requirements:

- Payment approval requires an authenticated cashier, Client Admin, or Client Owner.
- Approval must write an audit log.
- Approval must include timestamp and approving user ID.
- Cash transaction must expire if not approved in time.

### Coming Soon Maya Checkout QR

`MAYA_CHECKOUT_QR` is a future cashless payment method. It requires client-owned Maya Business credentials.

Future requirements:

- Client Owner supplies Maya Business account name.
- Client Owner supplies Maya public API key.
- Client Owner supplies Maya secret API key, stored encrypted and never returned to frontend clients.
- PhotoBIZ provides a webhook URL for the client to register in Maya Business Manager.
- Maya webhooks become the source of truth for payment success, failure, expiration, and cancellation.
- Payment attempts table supports auditability and retries.
- Booth UI displays this option only after the client payment config is verified and enabled.

### Coming Soon Maya Terminal ECR

`MAYA_TERMINAL_ECR` is a future physical terminal payment method. It requires client-owned Maya terminal hardware and booth-local ECR setup.

Future requirements:

- Client has a verified Maya Business account.
- Client has an active supported Maya terminal.
- Client has access to the Maya ECR Integration Kit.
- Client assigns the terminal to a PhotoBIZ booth.
- Windows Booth Agent stores local ECR connection settings, such as COM port.
- Booth UI displays this option only after client payment config and booth terminal config are verified and enabled.

## Audit Log Requirements

Audit logs should capture:

- User login.
- Client account creation/update/suspension/reactivation/archive.
- Subscription creation/update/status changes.
- User creation/update/deactivation.
- Client Booth UI theme changes.
- Package creation/update/deactivation.
- Booth registration/unregistration/end booth.
- Package assignment changes.
- Cash payment approval.
- Maya payment configuration changes.
- Maya ECR terminal configuration changes.
- Transaction cancellation.
- Manual recovery actions.
- Role changes.

## Security Requirements

- Password-based login for MVP.
- Role-based access control.
- Tenant isolation for all client-scoped data.
- Application Owner can access all clients for platform management and support.
- Client Owner and Client Admin can access only their client account.
- Cashiers can only access their assigned booth.
- Backend validates all payment transitions.
- Backend validates subscription status and booth allowance before activating booths or starting sessions.
- Booth UI cannot directly mark a transaction as paid.
- Agent commands require authenticated agent identity.
- Audit logs for sensitive actions.

## MVP Acceptance Criteria

The MVP is considered complete when:

1. Application Owner can create a client account.
2. Application Owner can assign manual subscription status and active booth allowance.
3. Client Owner can create a location, booth, user, and package inside their client account.
4. Client Owner or Client Admin can assign packages to a booth.
5. Cashier can be assigned to exactly one booth.
6. Booth UI displays only active packages assigned to its booth.
7. Booth UI displays client branding and active session text.
8. Customer can choose package and cash payment.
9. Cashier can approve cash payment.
10. Pending cash payments expire after the configured timeout.
11. Paid transactions command the booth agent to start a LumaBooth session.
12. Agent can report session completion.
13. Completed transactions appear in tenant-scoped reports.
14. Application Owner dashboard shows client and subscription health.
15. Client Owner dashboard shows client sales and booth status.
16. Cashier dashboard shows only the assigned booth.
17. Maya Checkout QR and Maya Terminal ECR are visible only as coming soon setup flows.

## Open Decisions

- Exact LumaBooth API command format and preset mapping.
- Exact Maya production verification steps with the client's Maya account manager.
