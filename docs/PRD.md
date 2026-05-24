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
- Initial payment model: cashier-approved cash plus client-owned PayMongo QR Ph.
- Cashless payment model: `PAYMONGO_QRPH` dynamic QR payments confirmed only by verified PayMongo webhooks.
- Payment setup model: tenant-level payment resources are managed in Settings, then assigned per booth before they can appear in runtime payment choices. Cash is always enabled for every tenant during MVP.
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
- The MVP integration uses the local dslrBooth/LumaBooth Windows API from the Agent, with `GET /api/start?mode={mode}&password={password}` for session start.
- Paid post-session extra print add-ons use the local dslrBooth/LumaBooth API from the Agent, with `GET /api/print?count={count}`. If the local API password is configured, the Agent appends it as a `password` query parameter.
- LumaBooth URL trigger events are sent to the local Agent listener at `http://127.0.0.1:5617/lumabooth/events`; the Agent maps `session_start` to backend session started and `session_end` to backend session completed.
- Allowed PhotoBIZ session modes are `PRINT`, `GIF`, `BOOMERANG`, and `VIDEO`. Existing `SESSION_STANDARD` values are treated as `PRINT`.
- The LumaBooth API password is stored only in local Agent configuration and is never returned to web clients.
- The Windows Agent owns focus handoff between Booth UI and LumaBooth. Focus failures are warnings and do not fail a paid transaction.

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
- No physical terminal or ECR payment integration in the first PayMongo feature.
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
- Create the first Client Owner login while onboarding a new client account; PhotoBIZ assigns the default initial password and forces first-login password change.
- Create, edit, suspend, reactivate, and archive clients.
- Manage manual subscriptions and per-booth allowances.
- View platform-wide client, booth, and subscription health.
- View cross-client reports.
- Access client accounts for support when needed.
- Manage global SaaS settings.
- View platform audit logs.

### Client Owner

The Client Owner owns one subscribed client account.
There is exactly one Client Owner per client account. The Application Owner-created onboarding user is the initial Client Owner, and only the Application Owner can transfer ownership to another same-client active user.

Permissions:

- Manage client profile and client Booth UI branding.
- Manage client users.
- Manage locations.
- Register, unregister, enable, disable, and end booths.
- Create, edit, activate, and deactivate booth offers.
- Activate exactly one booth offer per booth.
- Manage active booth session appearance.
- View transactions, reports, and audit logs for their client account.
- Configure tenant-level payment resources and assign allowed payment options per booth.
- Use Cashier POS for the assigned booth after being assigned as POS staff.
- Cannot create another Client Owner, change their own role, or deactivate their own account.

### Client Admin

The Client Admin manages booth operations for one client account.

Permissions:

- Manage Client Admin and Cashier users inside the client account.
- Manage assigned client locations and booths.
- Manage booth offers and active booth offer assignment.
- Manage active session appearance.
- Configure tenant-level payment resources and manage booth-level payment option assignments from usable client payment resources.
- View client transactions and reports.
- Perform booth recovery actions.
- Use Cashier POS for the assigned booth after being assigned as POS staff.
- Cannot manage subscription, billing status, or Application Owner support access.

### Cashier

The Cashier is assigned to exactly one booth.

Permissions:

- View only the assigned booth.
- View current booth state.
- View pending payment requests for the assigned booth.
- Approve cash payments when the saved cashier permission is enabled.
- Cancel or expire a pending transaction when the saved cashier permission is enabled.
- View today's transactions and sales for the assigned booth.
- Trigger basic recovery actions, such as returning booth to welcome screen and clearing a stuck active booth transaction, when the saved cashier permission is enabled.

POS staff workflow note:

- Cashier user records may be created before booth assignment.
- Client Owner, Client Admin, and Cashier users may be assigned as the booth's one POS staff user during booth registration or Manage Booth.
- Unassigned Client Owner/Admin users can manage tenant setup but cannot use Cashier POS actions until assigned.
- Client Owner/Admin user detail management persists cashier permissions for approve cash, cancel transaction, and return booth to welcome.

## Subscription Model

MVP subscriptions are manually managed by the Application Owner. A Subscription is the reusable catalog definition with a name, active/inactive state, currency, and monthly price per active booth. A client subscription assignment links a client account to one Subscription and records status plus active booth allowance.

Detailed lifecycle and parent availability rules are defined in [PhotoBIZ State Model](STATE_MODEL.md). `docs/ARCHITECTURE.md` remains authoritative if state guidance conflicts.

Subscription statuses:

- `TRIAL`
- `ACTIVE`
- `SUSPENDED`
- `CANCELLED`

Rules:

- Subscription price per booth is monthly and is used for Application Owner MRR estimates.
- Client subscription allowance is based on active booth count.
- Client accounts cannot activate more booths than their assigned subscription allowance.
- Suspended clients cannot start new booth sessions.
- Suspended or cancelled clients retain historical transactions and reports unless access is explicitly restricted by the Application Owner.
- Subscription changes are audit logged.

## Core Product Areas

### 1. Central Web App

Required MVP sections:

- Login.
- First-login forced password update.
- Application Owner platform dashboard.
- Client accounts.
- Subscription catalog list and editor.
- Client account subscription assignment controls.
- Application Owner subscription audit log.
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
- Account password change.
- Audit logs.

Role-specific navigation:

- Application Owner: Dashboard, Subscriptions, Clients, Account, Audit Log.
- Client Owner and Client Admin: Dashboard, Users, Locations, Booths, Packages, Transactions, Reports, Settings, Account, Audit Log.
- Cashier: Dashboard, Cashier POS, Reports, Account, Audit Log.

Admin Web account rules:

- Application Owner, Client Owner, Client Admin, and Cashier users can change their own password after sign-in.
- Client onboarding and user creation forms collect name/email/role details only, not custom passwords.
- New Admin Web users receive the default initial password `PhotoBIZ!123`.
- New users must change the default password before accessing dashboard, setup, cashier, reports, or audit log workflows.

In the client tenant UI, Packages is the user-facing label for booth offers. The Packages section also owns the client-scoped Print Entitlements modal used by package forms.

The Booths section is an inventory-first workflow. The list shows every booth with location, booth code, effective agent state, active package, runtime payment status, assigned cashier, lifecycle status, and a Manage action. Registration captures only MVP booth record fields: booth name, booth code, location, and optional assigned cashier. After creation, PhotoBIZ shows the one-time kiosk token and Windows Agent credential; Manage Booth can re-issue booth credentials when staff need to configure or recover the Windows Agent. Re-issuing credentials rotates both the Agent credential and kiosk token, and the old Agent credential stops working. Hardware inventory fields such as camera, printer, and notes are intentionally out of MVP scope.

The Manage Booth page owns booth record edits, cashier assignment, active package selection, cash payment assignment, and Booth UI appearance. It uses two tabs: Details for the booth record, package, and payment assignment; Session Setup for session label, welcome copy, completion thank-you message, theme, background image upload, and preview. The management preview must render through the same Booth UI stage presentation contract as the kiosk app, using current unsaved form values for preview and persisting appearance changes through the booth appearance endpoint.

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
- Completed session prompt for 15 seconds: "Need extra prints? Please go to the cashier."
- Expired transaction screen.
- Cancelled payment/request screen.
- Payment failed screen.
- Error/recovery screen.

Rules:

- Booth UI is accessible only through a booth-scoped kiosk token.
- Booth UI accepts the kiosk token from the first URL path segment, such as `http://localhost:4201/{token}`. The customer-facing Booth UI does not expose a manual kiosk token input.
- Booth UI config includes the client display name plus booth name, booth code, and location name so the customer-facing header can render tenant and booth context without hard-coded theme copy.
- Booth UI receives `activeTransaction` from config for refresh-safe payment selection, cash waiting, paid handoff, and in-session states.
- Booth UI may receive an unacknowledged `recentTransaction` from config for customer-facing terminal outcomes such as `CANCELLED`, `EXPIRED`, or `PAYMENT_FAILED`, and must show a clear full-screen status instead of silently returning to welcome.
- Booth UI does not require cashier login or daily unlock.
- Booth UI remains accessible with a valid kiosk token when the Windows Agent is closed, but it must show an agent-offline unavailable state and block transaction start.
- Booth UI displays the booth's single active offer.
- Booth UI must not allow a second payable session flow to start while the booth already has a non-terminal transaction.
- Booth UI reflects client-level branding and active session overrides.
- Booth UI displays only booth-assigned payment methods that are enabled for runtime use.
- Booth UI cannot directly approve payment or start LumaBooth.
- Payment Back and payment idle auto-return cancel backend `CREATED` transactions and return directly to Welcome without showing a cancelled outcome screen. The payment idle countdown is based on the backend transaction creation timestamp. Cash waiting Back can cancel backend `PENDING_CASH` transactions and may show the cancelled outcome screen. These kiosk actions are booth-scoped by kiosk token.
- `EXPIRED`, `CANCELLED`, and `PAYMENT_FAILED` screens clear only after Booth UI acknowledges the backend `recentTransaction` and config no longer returns it.
- Booth UI must not show customer-visible payment outcome toasts or an app-level busy overlay; duplicate-tap guards are allowed, but status and errors belong inside the stage.
- Admin Web Booth preview and Booth UI kiosk rendering share the same presentation component and `/api/booth-ui/config` data shape. Admin preview may omit kiosk-only controls, but stage layout, typography, preset colors, offer display, button styling, and state visuals must match the customer-facing Booth UI.

### 3. Windows Booth Agent

The Windows Booth Agent runs locally on the booth laptop.

Responsibilities:

- Provide a local Agent Control Center for staff and technicians in the logged-in Windows user session.
- Authenticate, pair, and re-pair with a booth record using Admin Web-issued booth code and Agent credential.
- Store saved Agent credential and LumaBooth API password encrypted locally.
- Show production API settings by default, with technician-only local/dev overrides.
- Request a fresh booth-scoped Booth UI launch token when `Start Booth` begins the runtime.
- Launch Chrome in kiosk mode to the configured Booth UI URL with the launch token, using an isolated Chrome profile so customers cannot access normal browser controls.
- Start kiosk Chrome, heartbeat, command polling, and the LumaBooth trigger listener together when staff click `Start Booth`.
- Stop heartbeat and command polling, call the backend offline endpoint, and close only the PhotoBIZ-launched Chrome instance when staff click `Stop Booth`.
- Maintain heartbeat connection with backend only while the booth runtime is intentionally online.
- Be treated as offline when no heartbeat has been received or the last heartbeat is older than 60 seconds.
- Receive commands from backend.
- Start LumaBooth sessions through the local dslrBooth/LumaBooth API or simulator mode.
- Receive LumaBooth URL trigger events through its local loopback listener.
- Report session state changes.
- Report basic local health and non-secret runtime metadata such as Agent version, kiosk running status, runtime kind, and LumaBooth mode.
- Manage Booth UI and LumaBooth window focus where possible.
- Recover to welcome screen after session completion or failure.
- Show sanitized logs and export diagnostics without exposing credentials, kiosk tokens, API passwords, or provider secrets.

### 4. Tenant Booth UI Customization

MVP customization is intentionally limited and safe.

Client-level customization:

- Brand/display name.
- Logo.
- Default welcome headline.
- Default welcome subtitle.
- Default completion thank-you message.

Booth-level customization:

- PhotoBIZ-managed theme preset: `VINTAGE` or `POP` for current Admin choices. `CLEAN_MODERN` is treated only as a legacy-tolerated saved value.
- Active booth offer.
- Booth-specific session label, welcome headline, welcome subtitle, and completion thank-you message overrides.
- Optional background image upload, stored as a constrained PNG/JPEG/WebP data URL and rendered by the selected theme.
- Theme colors, typography, and button styling are owned by PhotoBIZ presets. Tenants do not choose arbitrary primary/accent colors in MVP.

Active session overrides:

- Session label.
- Welcome headline.
- Welcome subtitle.
- Completion thank-you message.
- Active booth offer.
- Booth-level payment option assignments.

Guardrails:

- No arbitrary CSS.
- No custom layouts.
- No uploaded executable/script content.
- No arbitrary remote image URLs for Booth UI theme backgrounds in MVP.
- Colors and image URLs must be validated.
- Theme changes are client-scoped and audit logged.

## Key Workflows

### Workflow A: Client Subscription Setup

1. Application Owner creates a client account and first Client Owner login in the same onboarding flow by entering the owner name and email.
2. Application Owner creates or selects a Subscription, then assigns it to the client with manual status and active booth allowance.
3. PhotoBIZ assigns the Client Owner the default initial password `PhotoBIZ!123`.
4. Client Owner signs in, is forced to change the password, then configures locations, users, booth offers, and booths.

### Workflow B: Booth Registration To Live

1. Client Owner or Client Admin creates a location.
2. Client Owner or Client Admin registers a booth.
3. Backend validates subscription status and active booth allowance.
4. System creates agent credentials and booth-scoped kiosk token.
5. Windows Agent pairs with the booth record using the Agent credential.
6. Windows Agent requests a fresh Booth UI launch token and opens Chrome in kiosk mode to the Booth UI token URL.
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

1. Client Owner or Client Admin opens Settings > Payment Resources.
2. Settings shows tenant information and the tenant-level payment resources for the client account.
3. Cash is the MVP runtime payment method. It is always enabled for the tenant and cannot be disabled.
4. Client can configure one PayMongo QR Ph resource at the tenant level with mode, business account name, public key metadata, encrypted secret key, encrypted webhook secret, webhook URL, setup status, and verification timestamp.
5. The PayMongo setup panel explains how to copy API keys from `Settings > Developers`, create a same-mode webhook in `Developers > Webhooks`, subscribe to `payment.paid`, `payment.failed`, and `qrph.expired`, paste the webhook secret, save, and verify the setup.
6. Client assigns payment options per booth from usable tenant resources.
7. Provider-backed runtime payment requires a verified client resource, a booth assignment, runtime enablement, and the provider feature to be live.
8. PayMongo QR Ph can appear at runtime only after the tenant setup is verified, assigned to the booth, runtime-enabled, and all backend transaction gates pass.

### Workflow E: Cash Payment

1. Booth UI shows the active booth offer.
2. Customer confirms the active offer.
3. Backend creates one same-booth transaction with status `CREATED`, returns it as `activeTransaction`, and rejects any additional kiosk session purchase attempts for the same booth until the current transaction reaches a terminal state.
4. Customer chooses cash payment when the offer requires payment for a per-session purchase. Backend moves the transaction to `PENDING_CASH`, moves the booth to `PAYMENT_PENDING`, and keeps returning the transaction as `activeTransaction` with its transaction number and expiration time.
5. Cashier sees the pending cash request in the Central Web App.
6. Cashier collects cash.
7. Cashier clicks `Approve Cash`.
8. Backend marks transaction as `PAID`.
9. Backend sends start-session command to the booth agent.
10. Agent starts the configured LumaBooth session for the booth's active LumaBooth event through the local dslrBooth/LumaBooth API.
11. LumaBooth handles capture, printing, and Fotoshare sharing.
12. LumaBooth sends URL trigger events to the Agent's local listener; `session_start` reports backend session started and `session_end` reports backend session completed.
13. Backend marks transaction as `COMPLETED`.
14. Booth UI shows the configurable completion thank-you message, defaulting to `Thanks for sharing your smile.`
15. For `PER_SESSION`, Booth UI shows a 15-second completed prompt telling the customer to go to the cashier for extra prints if needed. Covered package sessions omit the extra-print copy.
16. Whichever happens first, the `Back To Start` click or the 15-second Booth UI timer, commands the backend to return the booth to welcome. Timer retries stay quiet; a failed customer click shows a clear error and keeps the completed screen until backend config reports the booth is no longer completed. The backend worker remains a fallback reset.

For `TIME_UNLIMITED` and `SESSION_COUNT` offers, Manage Booth only selects the package. The selected package is saved as `PENDING_PAYMENT` and is not usable until the assigned cashier clicks `Activate Package` in POS, creating one cash `PLAN_ACTIVATION` transaction for the package price. Cash approval completes that payment transaction, starts the timed window or session allowance, and marks the activation `ACTIVE`. Once a timed or session-count offer is active and paid, Booth UI creates zero-amount `COVERED_PLAN_SESSION` transactions, skips payment selection, and follows welcome, LumaBooth handoff, and return-to-welcome states. Completed covered sessions must not show the extra-print prompt because extra print add-ons are not available for those package types. `PLAN_ACTIVATION` transactions never command the Windows Agent.

### Workflow F: PayMongo QR Ph Setup

1. Client Owner or Client Admin opens Settings > Payment Resources.
2. Client sees cash plus a PayMongo QR Ph setup card.
3. Client selects test or live mode and enters business account name, public API key, secret API key, and webhook secret. Live mode warns that it processes real payments.
4. The form shows the PhotoBIZ webhook URL after the resource exists, and the instruction panel tells the client to create a same-mode PayMongo webhook for `payment.paid`, `payment.failed`, and `qrph.expired`.
5. Save stores secrets encrypted and returns only masked metadata. Verify PayMongo Setup performs a safe backend credential check before marking the resource verified.
6. Client Owner or Client Admin assigns verified PayMongo QR Ph per booth and enables runtime exposure where the booth should accept QR payments.

### Workflow G: Transaction Expiration

1. Customer confirms the active booth offer and chooses a payment method when payment is required.
2. Transaction enters a pending payment status.
3. If payment is not approved before the configured expiration window, backend marks it `EXPIRED`.
4. Booth UI displays a full-screen expiration message from backend `recentTransaction`.
5. The `Back To Start` action or 15-second terminal outcome timer acknowledges the recent transaction through the backend.
6. Booth becomes available for the next customer.

Default MVP expiration:

- Pending cash: 1 minute.

### Workflow H: Session Recovery

1. Agent or backend detects that the booth is stuck, offline, or in an error state.
2. Cashier sees an alert in POS view.
3. Cashier can cancel, retry, or return booth to welcome.
4. `Return to welcome` is a backend recovery action, not a visual-only kiosk reset. It cancels the booth's active non-terminal transaction so the booth becomes available again.
5. If a booth session record already exists for the active transaction, the recovery flow marks that booth session failed before the booth returns to `WELCOME`.
6. All manual recovery actions are written to audit logs.

## Booth Offer Requirements

MVP booth offer types:

- `PER_SESSION`: configurable pay-per-session offer package. Each package has its own name, description, price, included print entitlement, LumaBooth session mode, and optional post-session extra print add-on price.
- `TIME_UNLIMITED`: configurable timed offer package. Each package has its own name, description, price, duration, included print entitlement per session, and LumaBooth session mode. Extra print add-ons are not allowed.
- `SESSION_COUNT`: configurable session-count offer package. Each package has its own name, description, price, session allowance, included print entitlement per session, and LumaBooth session mode. Extra print add-ons are not allowed.

Clients may create multiple packages for each offer type. A booth can activate exactly one package at a time from the full active package catalog, regardless of offer type.

Clients manage a tenant-scoped print entitlement list from the Packages page. The package form uses print entitlements as its dropdown options. New client accounts start with common MVP defaults, including `2 pcs 6x2 or 1 pc 6x4`, `2 pcs 6x2`, and `1 pc 6x4`. Print entitlements do not have active/inactive lifecycle states. The print entitlement list shows `In Use` when any package references the entitlement and `Not Used` otherwise. Unused print entitlements may be deleted, while in-use entitlements cannot be deleted.

MVP booth offer fields:

- Client account.
- Name.
- Description.
- Offer type.
- Price in PHP.
- Included print entitlement: selected from the client print entitlement list.
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
- Add-ons are available only for the immediately previous booth session transaction when that previous session is an eligible completed `PER_SESSION` `SESSION_PURCHASE`; prior extra-print add-on transactions are add-ons, not sessions, and do not replace the previous session reference.
- If a newer current booth session exists but has not yet reached the LumaBooth handoff/session states, Cashier POS may still create extra prints for the immediately previous eligible session.
- If the immediately previous booth session transaction is not eligible, Cashier POS must show that there is no eligible transaction for extra print and must not scan older session history.
- Booth UI does not create add-ons; it only shows the 15-second post-session cashier prompt.
- Cashier POS creates the add-on and selects 1 to 5 copies.
- Add-ons are cash-only in MVP.
- Add-on transactions do not start a new LumaBooth capture session.
- Add-on approval and payment must follow the cash payment authority rules for MVP.
- The Windows Agent requests additional LumaBooth print copies after backend payment approval using `GET /api/print?count={count}`.
- Add-ons are rejected for `TIME_UNLIMITED` and `SESSION_COUNT` offers.

## Booth Requirements

Detailed booth lifecycle, runtime state, and location-to-booth availability rules are defined in [PhotoBIZ State Model](STATE_MODEL.md). `docs/ARCHITECTURE.md` remains authoritative if state guidance conflicts.

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
- `PAYMENT_PENDING`
- `PAID`
- `STARTING_LUMABOOTH`
- `IN_LUMABOOTH_SESSION`
- `PRINTING_OR_SHARING`
- `COMPLETED`
- `ERROR`

## Transaction Requirements

Detailed transaction, booth-session, and effective runtime availability rules are defined in [PhotoBIZ State Model](STATE_MODEL.md). `docs/ARCHITECTURE.md` remains authoritative if state guidance conflicts.

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
- Cancelled timestamp.
- Cancellation actor type, source screen/action, previous status, and cashier user ID when a cashier performed the cancellation.
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
- Trial, active, suspended, and cancelled subscriptions.
- Manual MRR estimate based on assigned client subscriptions and their monthly price per booth.
- Clients over booth allowance.
- Client health.
- Subscription-focused audit events.

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
- Recent booth activity with All, Sales, and Sessions filters. Covered plan sessions display as included usage, not PHP 0 sales; session-count rows show the historical used-session sequence for that completed session.
- Current booth statuses.
- Current booth statuses include the active package context. Session-count packages show the latest completed covered-session sequence, such as `2 of 5 used`; timed packages show minutes remaining and the exact expiration timestamp in Philippine time.
- Recent client audit events.

### Cashier POS View

- Assigned booth name and status.
- Current payment request card.
- Active booth offer.
- Amount due.
- Payment method, filtered by booth assignment and runtime availability.
- Countdown until expiration.
- `Approve Cash` action.
- `Cancel Transaction` action.
- `Activate Package` action when the assigned booth has a selected `TIME_UNLIMITED` or `SESSION_COUNT` package in `PENDING_PAYMENT` and no other non-terminal transaction.
- Cash-only plan activation checkout for `TIME_UNLIMITED` and `SESSION_COUNT` during MVP; approval activates the package but does not start LumaBooth.
- PayMongo QR Ph pending payments appear as backend-driven waiting states; cashier recovery actions remain permission-gated.
- Today's sales.
- Today's completed sessions.
- Assigned booth activity. Payment actions stay transaction-oriented; history uses activity wording, includes extra-print add-on transactions, and session-count rows show the historical used-session sequence.
- Extra print controls for the immediately previous booth transaction, including a 1 to 5 copy selector and computed cash total only when that transaction is eligible.
- Assigned-booth sales summary and cashier-owned audit events.
- Basic recovery action: return to welcome screen.

## Payment Requirements

Payment configuration has two levels plus the built-in cash resource:

1. Tenant-level resources define what the client account has registered or started configuring.
2. Booth-level assignments define which of those resources a booth may use when the method is runtime-enabled.

Admin Web Settings is the tenant-level payment resource surface. Cash is built in, always enabled/verified, and cannot be disabled at the tenant level. PayMongo QR Ph setup stores client-owned credentials and webhook configuration; stored secrets are encrypted and never returned after save.

Backend payment validation must check booth-level assignment, provider/resource status, and runtime feature availability. Tenant-level setup alone is never enough to expose a payment method in Booth UI or Cashier POS.

### Cash

Cash is approved manually by the cashier.

Requirements:

- Cash is always available as a tenant resource and cannot be disabled in Settings.
- Cash can be assigned per booth and remains available alongside verified provider-backed methods.
- Payment approval requires an authenticated cashier, Client Admin, or Client Owner assigned to the booth.
- Cash approval is blocked when the assigned booth's agent is offline, so staff do not collect cash for a session the agent cannot start.
- Approval must write an audit log.
- Approval must include timestamp and approving user ID.
- Cash transaction must expire if not approved in time.

### PayMongo QR Ph

`PAYMONGO_QRPH` is a client-owned dynamic QR Ph payment method backed by PayMongo Payment Intents and verified PayMongo webhooks.

Requirements:

- Client account can have one PayMongo QR Ph configuration.
- Client Owner supplies PayMongo business account name, mode (`test` or `live`), public API key, secret API key, and webhook secret.
- Public and secret key prefixes must match the selected mode: `pk_test_`/`sk_test_` for test mode and `pk_live_`/`sk_live_` for live mode.
- Secret API key and webhook secret are stored encrypted and never returned to frontend clients.
- PhotoBIZ provides a webhook URL for the client to register in PayMongo Dashboard under `Developers > Webhooks`.
- The webhook must be created in the same test/live mode selected in PhotoBIZ and subscribe to `payment.paid`, `payment.failed`, and `qrph.expired`.
- Booths can assign PayMongo QR Ph only after the client-level resource exists and is verified/usable.
- PayMongo webhooks become the source of truth for payment success, failure, and expiration.
- Payment attempts table supports auditability and retries.
- Booth UI and Cashier POS display this option only after client payment config is verified, assigned to the booth, and runtime provider integration is enabled.
- If PayMongo reports paid after a PhotoBIZ transaction was cancelled, the payment is recorded for reconciliation and must not start a booth session.

## Audit Log Requirements

Audit logs should capture:

- User login.
- User password change.
- Client account creation/update/suspension/reactivation/archive.
- Subscription creation/update/status changes.
- User creation/update/deactivation.
- Client Booth UI theme changes.
- Booth offer creation/update/deactivation.
- Booth registration/unregistration/end booth.
- Active booth offer assignment changes.
- Booth payment option assignment changes.
- Cash payment approval.
- PayMongo payment configuration changes.
- PayMongo webhook payment events.
- Transaction cancellation.
- Manual recovery actions.
- Role changes.

## Security Requirements

- Password-based login for MVP.
- New Admin Web users created through onboarding or user management use `PhotoBIZ!123` as the default initial password and must change it on first login.
- Users who must change password cannot access admin or cashier workflows until the password is updated.
- Every authenticated Admin Web role can change its own password.
- Role-based access control.
- Tenant isolation for all client-scoped data.
- Application Owner can access all clients for platform management and support.
- Client Owner and Client Admin can access only their client account.
- Client Owner and Client Admin can use Cashier POS only for their assigned booth.
- Cashiers can only access their assigned booth.
- Backend validates all payment transitions.
- Backend validates subscription status and booth allowance before activating booths or starting sessions.
- Booth UI cannot directly mark a transaction as paid.
- Agent commands require authenticated agent identity.
- Audit logs for sensitive actions.
- Cashier `return-to-welcome` recovery must resolve the active booth transaction before the booth is considered available again.

## MVP Acceptance Criteria

The MVP is considered complete when:

1. Application Owner can create a client account with first Client Owner login using the default initial password and forced password change.
2. Application Owner can define Subscriptions and assign manual subscription status and active booth allowance to clients.
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
17. PayMongo QR Ph can be configured per tenant, verified, assigned per booth, and exposed to Booth UI only when all backend gates pass.
18. Per-session transactions can accept post-session extra print add-ons.
19. Time-unlimited and session-count offers reject extra print add-ons.
20. Booth payment options are filtered by booth assignment, and cash remains available while verified PayMongo QR Ph can be runtime-enabled per booth.

## Open Decisions

- Live PayMongo validation must be completed with the client's production PayMongo account before enabling live payments at an actual booth.
