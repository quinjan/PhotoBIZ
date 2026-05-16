# Product Requirements Document

## Product Name

PhotoBIZ

## Purpose

PhotoBIZ is a multi-tenant SaaS platform for photobooth businesses in the Philippines. The Application Owner sells software subscriptions to client businesses. Each client uses PhotoBIZ to manage their own locations, booths, active booth offers, staff, customer-facing Booth UI, payment approval workflow, transactions, and reports.

The booth workflow remains: customer reviews the booth's active offer, chooses a payment method when payment is required, payment is approved or confirmed, LumaBooth starts on Windows, photos are captured, prints are produced through LumaBooth, and digital copies are delivered through LumaBooth/Fotoshare.

## Current Operating Assumptions

- Market: Philippines.
- Business model: SaaS subscription sold to photobooth operators.
- MVP subscription model: manual subscription management by the Application Owner.
- Subscription unit: per active booth.
- Initial booth environment: mall or staffed retail location.
- Initial staffing model: one cashier per booth.
- Initial payment model: cash approval only for real transactions.
- Coming soon payment model: `MAYA_CHECKOUT_QR` and `MAYA_TERMINAL_ECR`.
- Payment setup model: client-level payment resources are registered first, then assigned per booth before they can appear in runtime payment choices.
- Photo booth software: LumaBooth on Windows.
- Digital delivery: LumaBooth/Fotoshare.
- Starter hardware: DNP RX1 printer and Canon R50 camera.
- Booth computer: Windows laptop.
- Display setup: laptop plus extended customer-facing screen.
- Booth UI: web application or kiosk-mode browser shown on the extended screen.
- Central Web App: SaaS admin, client admin, and cashier/POS application.

## LumaBooth Constraints

PhotoBIZ MVP must respect the current LumaBooth operating model.

- Each booth machine runs one active LumaBooth event/configuration at a time.
- PhotoBIZ must not promise per-transaction LumaBooth template or package switching in MVP.
- Template and layout choices available inside LumaBooth are controlled by the active LumaBooth event, not by PhotoBIZ Booth UI customer selection.
- PhotoBIZ tracks the commercial booth offer, payment, allowance, session usage, eligible add-on print sales, tenant reporting, and auditability.
- The Windows Agent may start the configured LumaBooth session type and may request additional print copies where the LumaBooth API supports it.

## Goals

1. Let the Application Owner manage client accounts and manual per-booth subscriptions.
2. Let Client Owners manage their own users, locations, booths, booth offers, active sessions, and reports.
3. Let Client Admins manage booth operations inside one client account.
4. Let Cashiers approve payments and recover sessions for their assigned booth.
5. Let customers review the booth's active offer and select available booth payment methods on the Booth UI when payment is required.
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
- No retake flow.
- No extra print add-ons for time-unlimited or session-count offers.
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
- Create, edit, activate, and deactivate booth offers.
- Activate exactly one booth offer per booth.
- Manage active booth session appearance.
- View transactions, reports, and audit logs for their client account.
- Configure client-level payment resources and assign allowed payment options per booth.

### Client Admin

The Client Admin manages booth operations for one client account.

Permissions:

- Manage Client Admin and Cashier users inside the client account.
- Manage assigned client locations and booths.
- Manage booth offers and active booth offer assignment.
- Manage active session appearance.
- Manage booth-level payment option assignments from active client payment resources.
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
- Booth offers.
- Session appearance.
- Transactions.
- Reports.
- Settings.
- Audit logs.

### 2. Booth UI

The Booth UI is the customer-facing interface shown on the booth's extended screen.

Required MVP screens:

- Welcome screen.
- Active offer review.
- Payment method selection for payable per-session flows.
- Cash payment waiting screen.
- Payment approved screen.
- Starting session screen.
- Session in progress or LumaBooth handoff screen.
- Expired transaction screen.
- Error/recovery screen.

Rules:

- Booth UI is accessible only through a booth-scoped kiosk token.
- Booth UI does not require cashier login or daily unlock.
- Booth UI remains accessible with a valid kiosk token when the Windows Agent is closed, but it must show an agent-offline unavailable state and block transaction start.
- Booth UI displays the booth's single active offer.
- Booth UI must not allow a second payable session flow to start while the booth already has a non-terminal transaction.
- Booth UI reflects client-level branding and active session overrides.
- Booth UI displays only booth-assigned payment methods that are enabled for runtime use.
- Booth UI cannot directly approve payment or start LumaBooth.

### 3. Windows Booth Agent

The Windows Booth Agent runs locally on the booth laptop.

Responsibilities:

- Authenticate and pair with a booth record.
- Maintain heartbeat connection with backend.
- Be treated as offline when no heartbeat has been received or the last heartbeat is older than 60 seconds.
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
- Primary color.
- Accent color.
- Default welcome headline.
- Default welcome subtitle.

Booth-level customization:

- PhotoBIZ-managed theme preset, such as `VINTAGE_FILM` or `MODERN_POP`.
- Active booth offer.
- Booth-specific session label, welcome headline, and welcome subtitle overrides.

Active session overrides:

- Session label.
- Welcome headline.
- Welcome subtitle.
- Active booth offer.
- Booth-level payment option assignments.

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
4. Client Owner signs in and configures locations, users, booth offers, and booths.

### Workflow B: Booth Registration To Live

1. Client Owner or Client Admin creates a location.
2. Client Owner or Client Admin registers a booth.
3. Backend validates subscription status and active booth allowance.
4. System creates agent credentials and booth-scoped kiosk token.
5. Windows Agent pairs with the booth record.
6. Booth UI opens using the booth token.
7. Client activates exactly one booth offer for the booth.
8. Client selects a PhotoBIZ-managed booth theme and active session appearance.
9. Client assigns booth payment options from client-level resources.
10. Booth becomes available.

### Workflow C: Active Offer Assignment To Booth

1. Client Owner or Client Admin creates a booth offer.
2. Client Owner or Client Admin activates the offer for one or more booths.
3. Backend ensures each booth has at most one active offer at a time.
4. Booth UI loads and displays the booth's active offer.
5. If no active offer is configured, Booth UI shows an unavailable state and does not allow checkout or session start.

### Workflow D: Payment Setup And Booth Assignment

1. Client Owner or Client Admin opens Payment Settings.
2. Cash is available as the MVP runtime payment method.
3. Client can draft one Maya Checkout QR configuration for the client account.
4. Client can draft multiple Maya Terminal ECR device configurations for the client account, each with a client-visible terminal name and required `deviceId`.
5. Client assigns payment options per booth.
6. Maya QR can be assigned only when the client Maya QR resource exists and is active or verified.
7. Maya ECR can be assigned only by selecting a specific active client ECR `deviceId`.
8. Maya QR and Maya ECR remain locked for runtime payment until PhotoBIZ enables the future provider integrations.

### Workflow E: Cash Payment

1. Booth UI shows the active booth offer.
2. Customer confirms the active offer.
3. Customer chooses cash payment when the offer requires payment for a per-session purchase.
4. Backend creates one transaction with status `PENDING_CASH` and rejects any additional kiosk session purchase attempts for the same booth until the current transaction reaches a terminal state.
5. Cashier sees the pending cash request in the Central Web App.
6. Cashier collects cash.
7. Cashier clicks `Approve Cash`.
8. Backend marks transaction as `PAID`.
9. Backend sends start-session command to the booth agent.
10. Agent starts the configured LumaBooth session for the booth's active LumaBooth event.
11. LumaBooth handles capture, printing, and Fotoshare sharing.
12. Agent receives session completion signal.
13. Backend marks transaction as `COMPLETED`.
14. Booth UI returns to welcome screen.

For `TIME_UNLIMITED` and `SESSION_COUNT` offers, plan activation is cashier-side and cash-only in the MVP. Once a timed or session-count offer is active and paid, Booth UI skips payment and offer review screens and follows welcome, LumaBooth handoff, and return-to-welcome states.

### Workflow F: Coming Soon Maya Setup

1. Client Owner opens Payment Settings.
2. Client Owner sees `MAYA_CHECKOUT_QR` and `MAYA_TERMINAL_ECR` as coming soon.
3. Client Owner can draft Maya Business credentials and one Maya Checkout QR configuration.
4. Client Owner can draft multiple Maya Terminal ECR device records with `deviceId` values.
5. Client Owner or Client Admin assigns configured payment resources per booth.
6. Cashless payment methods remain unavailable to customers and cashiers until PhotoBIZ enables real Maya integration in a future phase.

### Workflow G: Transaction Expiration

1. Customer confirms the active booth offer and chooses a payment method when payment is required.
2. Transaction enters a pending payment status.
3. If payment is not approved before the configured expiration window, backend marks it `EXPIRED`.
4. Booth UI displays expiration message briefly.
5. Booth UI returns to welcome screen.
6. Booth becomes available for the next customer.

Default MVP expiration:

- Pending cash: 5 minutes.

### Workflow H: Session Recovery

1. Agent or backend detects that the booth is stuck, offline, or in an error state.
2. Cashier sees an alert in POS view.
3. Cashier can cancel, retry, or return booth to welcome.
4. All manual recovery actions are written to audit logs.

## Booth Offer Requirements

MVP booth offer types:

- `PER_SESSION`: configurable pay-per-session offer package. Each package has its own name, description, price, included print entitlement, LumaBooth session mode, and optional post-session extra print add-on price.
- `TIME_UNLIMITED`: configurable timed offer package. Each package has its own name, description, price, duration, included print entitlement per session, and LumaBooth session mode. Extra print add-ons are not allowed.
- `SESSION_COUNT`: configurable session-count offer package. Each package has its own name, description, price, session allowance, included print entitlement per session, and LumaBooth session mode. Extra print add-ons are not allowed.

Clients may create multiple packages for each offer type. A booth can activate exactly one package at a time from the full active package catalog, regardless of offer type.

MVP booth offer fields:

- Client account.
- Name.
- Description.
- Offer type.
- Price in PHP.
- Included print entitlement: `2 pcs 6x2` or `1 pc 6x4`.
- Duration in hours, required only for `TIME_UNLIMITED`.
- Session allowance, required only for `SESSION_COUNT`.
- Extra print add-on eligibility, true only for `PER_SESSION`.
- Extra print add-on price in PHP, required only when add-ons are enabled.
- LumaBooth session mode or preset reference.
- Active/inactive status.

Active offer assignment fields:

- Booth.
- Booth offer.
- Status.
- Activated timestamp.
- Deactivated timestamp.
- Starts timestamp, required for timed offers.
- Ends timestamp, required for timed offers.
- Session allowance and sessions used, required for session-count offers.

Booth offer activation uses a single dropdown/select control in admin workflows. Booth offer details must be snapshotted into transactions so later offer edits do not change historical records.

## Extra Print Add-On Requirements

Post-session extra print add-ons are supported only for completed `PER_SESSION` transactions.

Rules:

- Add-on transactions must be linked to the original completed session transaction.
- Add-on transactions do not start a new LumaBooth capture session.
- Add-on approval and payment must follow the cash payment authority rules for MVP.
- The Windows Agent may request additional LumaBooth print copies after backend payment approval.
- Add-ons are rejected for `TIME_UNLIMITED` and `SESSION_COUNT` offers.

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
- Active booth offer.
- Assigned payment options.
- Current booth state.

Booth states:

- `OFFLINE`
- `WELCOME`
- `OFFER_CONFIRMED`
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
- Offer snapshot.
- Parent transaction, when the transaction is a post-session extra print add-on.
- Transaction type: session purchase, plan activation, covered plan session, or extra print add-on.
- Extra print copy count, when applicable.
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

One booth may have only one non-terminal session purchase transaction at a time. In MVP, terminal transaction statuses are `COMPLETED`, `EXPIRED`, and `CANCELLED`.

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
- Booth offer sales and usage counts.
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
- Top booth offers sold.
- Sales by location.
- Recent transactions.
- Current booth statuses.

### Cashier POS View

- Assigned booth name and status.
- Current transaction card.
- Active booth offer.
- Amount due.
- Payment method, filtered by booth assignment and runtime availability.
- Countdown until expiration.
- `Approve Cash` action.
- `Cancel Transaction` action.
- Cash-only plan activation checkout for `TIME_UNLIMITED` and `SESSION_COUNT` during MVP.
- Future Maya QR/ECR methods visible only as locked assigned options until provider integration is enabled.
- Today's sales.
- Today's completed sessions.
- Recent transactions for the assigned booth.
- Basic recovery action: return to welcome screen.

## Payment Requirements

Payment configuration has two levels:

1. Client-level resources define what the client account has registered.
2. Booth-level assignments define which of those resources a booth may use when the method is runtime-enabled.

Backend payment validation must check booth-level assignment, provider/resource status, and runtime feature availability. Client-level setup alone is never enough to expose a payment method in Booth UI or Cashier POS.

### Cash MVP

Cash is approved manually by the cashier.

Requirements:

- Cash is the only real MVP payment method.
- Cash can be assigned per booth and is the only payment option that can be runtime-enabled in MVP.
- Payment approval requires an authenticated cashier, Client Admin, or Client Owner.
- Cash approval is blocked when the assigned booth's agent is offline, so staff do not collect cash for a session the agent cannot start.
- Approval must write an audit log.
- Approval must include timestamp and approving user ID.
- Cash transaction must expire if not approved in time.

### Coming Soon Maya Checkout QR

`MAYA_CHECKOUT_QR` is a future cashless payment method. It requires client-owned Maya Business credentials.

Future requirements:

- Client account can have one Maya Checkout QR configuration.
- Client Owner supplies Maya Business account name.
- Client Owner supplies Maya public API key.
- Client Owner supplies Maya secret API key, stored encrypted and never returned to frontend clients.
- PhotoBIZ provides a webhook URL for the client to register in Maya Business Manager.
- Booths can assign Maya Checkout QR only after the client-level resource exists and is active or verified.
- Maya webhooks become the source of truth for payment success, failure, expiration, and cancellation.
- Payment attempts table supports auditability and retries.
- Booth UI and Cashier POS display this option only after client payment config is verified, assigned to the booth, and runtime provider integration is enabled.

### Coming Soon Maya Terminal ECR

`MAYA_TERMINAL_ECR` is a future physical terminal payment method. It requires client-owned Maya terminal hardware and booth-local ECR setup.

Future requirements:

- Client has a verified Maya Business account.
- Client can register multiple active supported Maya terminal devices.
- Each terminal device stores a client-visible terminal name and required `deviceId`.
- Client has access to the Maya ECR Integration Kit.
- Client assigns specific terminal `deviceId` values to PhotoBIZ booths.
- Windows Booth Agent stores local ECR connection settings, such as COM port.
- Booth UI and Cashier POS display this option only after client payment config, selected booth ECR device assignment, and runtime provider integration are verified and enabled.

## Audit Log Requirements

Audit logs should capture:

- User login.
- Client account creation/update/suspension/reactivation/archive.
- Subscription creation/update/status changes.
- User creation/update/deactivation.
- Client Booth UI theme changes.
- Booth offer creation/update/deactivation.
- Booth registration/unregistration/end booth.
- Active booth offer assignment changes.
- Booth payment option assignment changes.
- Cash payment approval.
- Maya payment configuration changes.
- Maya ECR device configuration changes.
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
3. Client Owner can create a location, booth, user, and booth offer inside their client account.
4. Client Owner or Client Admin can activate exactly one booth offer for a booth.
5. Cashier can be assigned to exactly one booth.
6. Booth UI displays only the booth's active offer.
7. Booth UI displays client branding and active session text.
8. Customer can confirm the active offer and choose cash payment when payment is required.
9. Cashier can approve cash payment.
10. Pending cash payments expire after the configured timeout.
11. Paid transactions command the booth agent to start a LumaBooth session.
12. Agent can report session completion.
13. Completed transactions appear in tenant-scoped reports.
14. Application Owner dashboard shows client and subscription health.
15. Client Owner dashboard shows client sales and booth status.
16. Cashier dashboard shows only the assigned booth.
17. Maya Checkout QR and Maya Terminal ECR are visible only as coming soon setup flows and locked booth assignment options.
18. Per-session transactions can accept post-session extra print add-ons.
19. Time-unlimited and session-count offers reject extra print add-ons.
20. Booth payment options are filtered by booth assignment, and cash is the only runtime-enabled MVP payment option.

## Open Decisions

- Exact LumaBooth API command format, session mode mapping, print-copy command behavior, and event trigger payload mapping.
- Exact Maya production verification steps with the client's Maya account manager.
