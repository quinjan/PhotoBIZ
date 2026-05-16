# PhotoBIZ MVP Progress Tracker

Last updated: 2026-05-16

This tracker is the working handoff for agents and contributors. It summarizes the current implementation state against `docs/ARCHITECTURE.md` and `docs/PRD.md`. The architecture document remains the source of truth if this tracker conflicts with another document.

## How To Use This File

1. Read `AGENTS.md`.
2. Read `docs/ARCHITECTURE.md`, `docs/PRD.md`, and `docs/CODING_GUIDELINES.md` in that order.
3. Read this file to decide where to pick up implementation work.
4. After each meaningful vertical slice, update the relevant status, evidence, remaining work, and validation notes here.

Status values:

- `Done`: implemented and validated for the current MVP expectation.
- `Partial`: scaffolded or partly implemented, but not a complete product workflow.
- `Pending`: not substantively implemented.
- `Blocked`: waiting on an external decision, provider detail, or unavailable dependency.

## Current Phase Verdict

Current phase: `MVP vertical slice in progress`

The repo has moved beyond early Phase 1. A broad backend-authoritative slice now exists across setup, kiosk config, cash transaction state, cashier approval, expiration, agent heartbeat, command polling, simulated session completion, dedicated management views, and explicit kiosk/POS state screens. The MVP is still not complete because reporting, post-session add-ons, and real LumaBooth/Maya integrations remain.

Validated baseline:

- `dotnet test PhotoBIZ.slnx --configuration Release --verbosity minimal` passed on 2026-05-16: API 25 tests, Worker 1 test, Windows Agent 1 test. Clean builds may still emit the existing CA1848/CA1873 warnings in `PhotoBizInfrastructure.cs` for bootstrap logging.
- `npm run build` from `apps` passed on 2026-05-16 for both `admin-web` and `booth-ui`.
- `npm run test:ci` from `apps` passed on 2026-05-16: Admin Web 4 tests and Booth UI 4 tests.

## What Is Done Or Mostly Done

- Product and architecture docs exist, including current rules for agent offline state and one non-terminal booth transaction per booth.
- Monorepo structure exists for `apps`, `services/api`, `agent/windows-agent`, `infra`, and `docs`.
- Angular 21 workspace exists with `admin-web` and `booth-ui`.
- Admin Web has dedicated dashboard, setup, client/subscription/user, booth/offer/payment, and cashier POS views for the current MVP workflows.
- Booth UI can accept a kiosk token, load backend config, display active offer/theme/state, create a transaction, select cash payment, and render connect/offline/unavailable/offer/payment/waiting/approved/session/expired/error states.
- API exposes auth/session, admin setup, booth config, kiosk transaction, cashier, and agent endpoints through `MapPhotoBizApi`.
- EF Core PostgreSQL model and initial migration exist for core MVP entities.
- Worker expires pending cash transactions and marks stale idle booths offline.
- Windows Agent pairs, heartbeats, polls for start-session commands, and simulates LumaBooth session started/completed callbacks.
- Docker Compose includes PostgreSQL, Redis, API, worker, and Caddy reverse proxy scaffold.
- GitHub Actions CI runs backend restore/build/test and frontend install/build/lint/test.

## Phase Completion Matrix

| Phase | Status | Evidence | Remaining work |
| --- | --- | --- | --- |
| Phase 1: MVP Foundations | Partial | Auth/session, dev bootstrap admin, client/subscription/user/location/booth/offer creation, offer activation, booth appearance update, payment assignment, booth credential issuance, EF schema, dedicated Admin Web management views, subscription allowance enforcement, cashier assignment validation, tenant hardening, locked non-cash runtime payment tests, and backend update/deactivate flows exist. | Improve role-specific routing/permissions in UI, broaden integration coverage, and polish admin workflows. |
| Phase 2: Transaction And POS | Partial | Booth UI config, active offer display, kiosk cash transaction creation/payment selection, cashier POS approval/cancel/recovery view, expiration worker, transaction overview, state-specific kiosk screens, and one-active-transaction guard exist. | Add stricter authorization/edge-case tests, support time-unlimited/session-count plan activation, add post-session add-ons, and add transaction dashboard/report detail. |
| Phase 3: Agent And LumaBooth | Partial | Agent pairing, heartbeat, offline detection, command polling, start-session command acquisition, session started/completed/failed callbacks, and simulated Windows Agent loop exist. | Replace simulation with real LumaBooth command/event adapter once API details are known, add durable/retryable command semantics, improve local recovery, and add agent integration tests. |
| Phase 4: Reporting And Operations | Partial | Admin overview aggregates clients, subscriptions, users, locations, booths, offers, activations, payment assignments, and recent transactions. Audit logs are written for many sensitive actions. | Build role-specific dashboards/reports, audit log UI, subscription health reports, booth status reports, sales reports, and cashier/client scoped reporting. |
| Phase 5: Coming Soon Real Payments | Pending | Maya entities and design mockups exist; payment assignment supports method records, with cash as MVP runtime method. | Maya Checkout QR setup, verification, webhook handling, reconciliation, Maya Terminal ECR setup/integration, and runtime enablement. |

## Phase 1 Detailed Checklist

| Item | Status | Evidence | Remaining work |
| --- | --- | --- | --- |
| Monorepo setup | Done | Top-level runtime/docs folders, solution, Angular workspace, CI, Docker, and infra scaffold exist. | Keep structure aligned with architecture. |
| Database schema | Partial | EF entities and initial migration cover core MVP tables. | Validate schema through end-to-end flows, add missing constraints from real workflows, and keep migrations focused. |
| Auth and roles | Partial | Cookie auth, login/logout/session endpoints, ASP.NET password hashing, role claims, dev bootstrap admin, scoped query helpers, and client/cashier management route hardening tests exist. | Add production-safe first-owner onboarding, broader auth integration tests, role-specific frontend routing, and policy hardening. |
| Client account management | Partial | Admin create/update client APIs and overview list exist, including suspend/reactivate/archive statuses, audit coverage, and dedicated Admin Web client controls. | Add broader tests and audit views. |
| Manual subscription management | Partial | Create/update subscription APIs exist; latest `TRIAL`/`ACTIVE` subscription and active booth allowance are enforced before new booth creation or offer activation; Admin Web exposes allowance/status controls. | Add audit views and more lifecycle tests. |
| Client user management | Partial | Create/update user APIs exist with role validation, deactivate/reactivate status, cashier same-tenant one-booth assignment validation, and Admin Web user management controls. | Add password reset/change, role-specific routing, and broader tests. |
| Location and booth management | Partial | Create/update location/booth APIs exist; booth creation issues kiosk token and agent credential, creates default appearance and cash assignment; cross-tenant location/booth references return validation/forbid responses; booth deactivation clears active offer activations; Admin Web exposes location/booth controls. | Add credential rotation/revocation, richer UI polish, and broader tests. |
| Booth offer management | Partial | Create/update offer and activate offer APIs exist; active-offer uniqueness exists in schema; activation validates scoped booth/offer references plus subscription eligibility; offer deactivation clears active activations; Admin Web exposes offer controls. | Add deeper offer-type validation for timed/session-count rules, activation scheduling/limits, and tests. |
| Minimal tenant Booth UI theme management | Partial | Booth appearance update API exists with constrained hex color validation; Booth UI consumes theme config. | Add client-level branding fields if needed, validate image URLs, improve UI, audit coverage, and tests. |
| Booth-level cash payment assignment | Partial | Booth creation creates cash assignment; payment assignment API exists; runtime cash validation exists; non-cash assignment attempts are stored locked with `RuntimeEnabled=false`, disabled assignments are excluded from Booth UI runtime config, and lifecycle tests cover disable behavior. | Add client-level payment setup flows and UI polish. |
| Client-level draft Maya QR and ECR setup records | Partial | Data model and design mockups exist. | Add coming-soon setup APIs/UI with locked runtime behavior and tests. |

## MVP Acceptance Checklist

| # | Acceptance criterion | Status | Evidence | Remaining work |
| --- | --- | --- | --- | --- |
| 1 | Application Owner can create a client account. | Partial | Dev bootstrap admin plus `/api/admin/clients`, `/api/admin/clients/{clientId}`, and Admin Web setup/client views exist. | Production-safe bootstrap/onboarding and broader integration tests. |
| 2 | Application Owner can assign manual subscription status and active booth allowance. | Partial | `/api/admin/subscription-plans`, `/api/admin/subscriptions`, `/api/admin/subscriptions/{subscriptionId}`, Admin Web subscription controls, and allowance enforcement on booth creation/offer activation exist. | Add audit views and broader lifecycle tests. |
| 3 | Client Owner can create a location, booth, user, and booth offer inside their client account. | Partial | Admin APIs support scoped create/update operations; Client Owner requests are forced to their own tenant and covered by API tests; Admin Web has setup and management controls. | Add role-specific UI routing, more validation, and broader tests. |
| 4 | Client Owner or Client Admin can activate exactly one booth offer for a booth. | Partial | Activation API exists, schema enforces one active activation per booth, and API validates scoped booth/offer references plus subscription eligibility; offer/booth deactivation clears active activations; Admin Web exposes activation controls. | Add broader replacement behavior tests. |
| 5 | Cashier can be assigned to exactly one booth. | Partial | User model has one assigned booth; create/update user APIs reject cashier records without a same-tenant booth; Admin Web exposes assigned-booth user controls. | Add broader tests and role-specific routing. |
| 6 | Booth UI displays only the booth's active offer. | Partial | `/api/booth-ui/config` and Booth UI active offer rendering/state screens exist. | Add deeper UI tests for offer changes and recovery states. |
| 7 | Booth UI displays client branding and active session text. | Partial | Booth config returns client/theme/session values and Booth UI applies colors/text. | Add logo/background/image validation and richer theme tests. |
| 8 | Customer can confirm the active offer and choose cash payment when payment is required. | Partial | Booth UI has offer confirmation, payment selection, waiting/approved/session/expired/error states, and selects `CASH`. | Add broader UI tests and add-on handling. |
| 9 | Cashier can approve cash payment. | Partial | Cashier approval endpoint and dedicated Admin Web POS view exist; approval blocks offline agent. | Add role-specific tests and realtime/polling polish. |
| 10 | Pending cash payments expire after the configured timeout. | Partial | Worker expires `PENDING_CASH`, returns booth to welcome, and Booth UI has an expired state. | Add operational tests around worker loop and expired recovery. |
| 11 | Paid transactions command the booth agent to start a LumaBooth session. | Partial | Agent command polling turns `PAID` into `STARTING_SESSION` and returns `START_SESSION`. | Durable/retryable command semantics and real LumaBooth adapter. |
| 12 | Agent can report session completion. | Partial | Agent session started/completed/failed endpoints and simulated Windows Agent callbacks exist. | Real LumaBooth event mapping and integration tests. |
| 13 | Completed transactions appear in tenant-scoped reports. | Partial | Admin overview includes recent scoped transactions. | Dedicated tenant-scoped reports and report tests. |
| 14 | Application Owner dashboard shows client and subscription health. | Partial | Admin overview includes clients/subscriptions. | Dedicated dashboard metrics and UI polish. |
| 15 | Client Owner dashboard shows client sales and booth status. | Partial | Overview scope helpers support client scoping; Admin Web shows booth/transaction counts. | Dedicated Client Owner dashboard and aggregate sales reports. |
| 16 | Cashier dashboard shows only the assigned booth. | Partial | Scoped query helpers, cashier endpoints, and Admin Web POS booth filtering exist. | Add tests proving assigned-booth isolation. |
| 17 | Maya Checkout QR and Maya Terminal ECR are visible only as coming soon setup flows and locked booth assignment options. | Pending | Data model and mockups exist. | Implement locked setup/assignment UI and APIs without runtime enablement. |
| 18 | Per-session transactions can accept post-session extra print add-ons. | Pending | Parent transaction/add-on fields exist only. | Add add-on workflow, validation, payment, and agent print-copy boundary. |
| 19 | Time-unlimited and session-count offers reject extra print add-ons. | Pending | Offer type constants exist. | Add domain validation and tests. |
| 20 | Booth payment options are filtered by booth assignment, and cash is the only runtime-enabled MVP payment option. | Partial | Booth config filters assigned runtime-enabled cash options, workflow only accepts `CASH`, non-cash assignment attempts are locked, and disabled cash assignments are excluded. | Add client-level payment setup UI/tests. |

## Next Recommended Vertical Slices

1. Add post-session extra print add-on workflow and rejection tests for timed/session-count offers.
2. Replace simulated agent behavior with a LumaBooth adapter boundary once command/event details are decided.
3. Add role-specific dashboards, reports, and audit log views.
4. Add coming-soon Maya setup/locked assignment flows without enabling runtime Maya payments.

## Open Decisions And Blockers

- Exact LumaBooth API command format, session mode mapping, print-copy command behavior, and event trigger payload mapping remain open.
- Exact Maya production verification steps with the client's Maya account manager remain open and belong to Phase 5.
- Real Maya payment runtime support is intentionally out of scope until Phase 5.

## Update Rules

- Update this file in the same PR as any meaningful MVP vertical slice.
- Keep statuses conservative. A scaffold or single-path happy flow is usually `Partial`, not `Done`.
- Record evidence as concrete code, route, test, or UI behavior.
- Record validation commands and whether they passed.
- Do not mark an acceptance criterion `Done` until the workflow is implemented end to end enough for the MVP expectation.
