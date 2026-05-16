# PhotoBIZ MVP Progress Tracker

Last updated: 2026-05-17

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

Current phase: `Non-Maya MVP software slice implemented`

The repo has moved beyond early Phase 1. A broad backend-authoritative slice now exists across setup, kiosk config, cash transaction state, cashier approval, expiration, agent heartbeat, command polling, LumaBooth API/trigger/print-copy integration boundary, dedicated management views, explicit kiosk/POS state screens, cashier-led post-session extra print add-ons, scoped reports, and scoped audit views. The remaining non-Maya blocker is real booth hardware validation with LumaBooth/dslrBooth Professional, camera, printer, and URL triggers enabled. Maya remains intentionally out of scope.

Validated baseline:

- `dotnet test services/api/tests/PhotoBIZ.Api.Tests/PhotoBIZ.Api.Tests.csproj --configuration Release --verbosity minimal` passed on 2026-05-17: 38 API/workflow tests. Clean builds may still emit the existing CA1848/CA1873 warnings in `PhotoBizInfrastructure.cs` for bootstrap logging.
- `dotnet test agent/windows-agent/tests/PhotoBIZ.WindowsAgent.Tests/PhotoBIZ.WindowsAgent.Tests.csproj --configuration Release --verbosity minimal` passed on 2026-05-17: 7 Windows Agent tests.
- `dotnet test PhotoBIZ.slnx --configuration Release --verbosity minimal` passed on 2026-05-17: API 38 tests, Worker 1 test, Windows Agent 7 tests.
- `npm run build` from `apps` passed on 2026-05-17 for both `admin-web` and `booth-ui`.
- `npm run test:ci` from `apps` passed on 2026-05-17: Admin Web 6 tests and Booth UI 5 tests.

## What Is Done Or Mostly Done

- Product and architecture docs exist, including current rules for agent offline state and one non-terminal booth transaction per booth.
- Monorepo structure exists for `apps`, `services/api`, `agent/windows-agent`, `infra`, and `docs`.
- Angular 21 workspace exists with `admin-web` and `booth-ui`.
- Admin Web has dedicated dashboard, setup, client/subscription/user, booth/offer/payment, cashier POS, reports, and audit views for the current MVP workflows.
- Booth UI can accept a kiosk token, load backend config, display active offer/theme/state, create a transaction, select cash payment, and render connect/offline/unavailable/offer/payment/waiting/approved/session/expired/error states.
- API exposes auth/session, admin setup, booth config, kiosk transaction, cashier, and agent endpoints through `MapPhotoBizApi`.
- EF Core PostgreSQL model and initial migration exist for core MVP entities.
- Worker expires pending cash transactions, resets completed booths to welcome after the 15-second extra-print prompt, and marks stale idle booths offline.
- Windows Agent pairs, heartbeats, polls for start-session and print-copy commands, supports simulator and local dslrBooth/LumaBooth API modes, stores active session context locally, listens for LumaBooth URL trigger events, maps terminal events to backend callbacks, calls `/api/print?count={count}` for extra copies, and handles Booth UI/LumaBooth focus handoff on a best-effort basis.
- Docker Compose includes PostgreSQL, Redis, API, worker, and Caddy reverse proxy scaffold.
- GitHub Actions CI runs backend restore/build/test and frontend install/build/lint/test.

## Phase Completion Matrix

| Phase | Status | Evidence | Remaining work |
| --- | --- | --- | --- |
| Phase 1: MVP Foundations | Done | Auth/session, dev bootstrap admin, client/subscription/user/location/booth/offer creation, offer activation, booth appearance update, payment assignment, booth credential issuance, EF schema, dedicated Admin Web management views, subscription allowance enforcement, cashier assignment validation, tenant hardening, locked non-cash runtime payment tests, backend update/deactivate flows, and role-aware Admin Web navigation exist. | Production hardening/polish can continue outside the MVP acceptance baseline. |
| Phase 2: Transaction And POS | Done | Booth UI config, active offer display, kiosk cash transaction creation/payment selection, cashier POS approval/cancel/recovery view, cashier-led post-session extra print add-on creation for latest completed per-session transactions, explicit timed/session-count extra-print rejection tests, return-to-welcome recovery that cancels the active booth transaction, expiration/completed-prompt worker behavior, transaction overview, state-specific kiosk screens, and one-active-transaction guard exist. | Time-unlimited/session-count plan activation UX can be deepened after MVP baseline. |
| Phase 3: Agent And LumaBooth | Blocked | Agent pairing, heartbeat, offline detection, command polling, extended start-session/print-copy command metadata, session started/completed/failed callbacks with optional LumaBooth refs, print-completed/print-failed callbacks, simulator mode, local dslrBooth/LumaBooth API start and print-copy client, local trigger listener, active session context store, focus handoff service, and focused API/Agent tests exist. | Requires real LumaBooth/dslrBooth Professional hardware smoke validation before marking complete. |
| Phase 4: Reporting And Operations | Done | Admin overview aggregates clients, subscriptions, users, locations, booths, offers, activations, payment assignments, recent transactions, backend-computed report summaries, and scoped recent audit events. Admin Web exposes dashboard, reports, and audit views. API tests cover tenant/cashier scoping. | Deeper export/report formats can be added after MVP baseline. |
| Phase 5: Coming Soon Real Payments | Pending | Maya entities and design mockups exist; payment assignment supports method records, with cash as MVP runtime method. | Maya Checkout QR setup, verification, webhook handling, reconciliation, Maya Terminal ECR setup/integration, and runtime enablement. |

## Phase 1 Detailed Checklist

| Item | Status | Evidence | Remaining work |
| --- | --- | --- | --- |
| Monorepo setup | Done | Top-level runtime/docs folders, solution, Angular workspace, CI, Docker, and infra scaffold exist. | Keep structure aligned with architecture. |
| Database schema | Done | EF entities and initial migration cover core MVP tables used by setup, transactions, agent sessions, add-ons, reports, and audit logs. | Keep migrations focused as post-MVP features evolve. |
| Auth and roles | Done | Cookie auth, login/logout/session endpoints, ASP.NET password hashing, role claims, dev bootstrap admin, scoped query helpers, role-aware Admin Web navigation, and client/cashier management route hardening tests exist. | Production-safe first-owner onboarding can be hardened after MVP baseline. |
| Client account management | Done | Admin create/update client APIs and overview list exist, including suspend/reactivate/archive statuses, audit coverage, dedicated Admin Web client controls, and scoped audit views. | Broader UI polish can continue post-MVP. |
| Manual subscription management | Done | Create/update subscription APIs exist; latest `TRIAL`/`ACTIVE` subscription and active booth allowance are enforced before new booth creation or offer activation; Admin Web exposes allowance/status controls and reports subscription health. | None for non-Maya MVP baseline. |
| Client user management | Done | Create/update user APIs exist with role validation, deactivate/reactivate status, cashier same-tenant one-booth assignment validation, Admin Web user management controls, and cashier overview scoping tests. | Password reset/change can follow post-MVP. |
| Location and booth management | Done | Create/update location/booth APIs exist; booth creation issues kiosk token and agent credential, creates default appearance and cash assignment; cross-tenant location/booth references return validation/forbid responses; booth deactivation clears active offer activations; Admin Web exposes location/booth controls. | Credential rotation/revocation can follow post-MVP. |
| Booth offer management | Done | Create/update offer and activate offer APIs exist; active-offer uniqueness exists in schema; activation validates scoped booth/offer references plus subscription eligibility; offer deactivation clears active activations; Admin Web exposes offer controls; explicit extra-print rejection tests cover timed/session-count snapshots. | Activation scheduling/limits can deepen later. |
| Minimal tenant Booth UI theme management | Done | Booth appearance update API exists with constrained hex color validation; Booth UI consumes theme config. | Richer theme UI can follow post-MVP. |
| Booth-level cash payment assignment | Done | Booth creation creates cash assignment; payment assignment API exists; runtime cash validation exists; non-cash assignment attempts are stored locked with `RuntimeEnabled=false`, disabled assignments are excluded from Booth UI runtime config, and lifecycle tests cover disable behavior. | None for non-Maya MVP baseline. |
| Client-level draft Maya QR and ECR setup records | Partial | Data model and design mockups exist. | Add coming-soon setup APIs/UI with locked runtime behavior and tests. |

## MVP Acceptance Checklist

| # | Acceptance criterion | Status | Evidence | Remaining work |
| --- | --- | --- | --- | --- |
| 1 | Application Owner can create a client account. | Done | Dev bootstrap admin plus `/api/admin/clients`, `/api/admin/clients/{clientId}`, and Admin Web setup/client views exist. | None for non-Maya MVP baseline. |
| 2 | Application Owner can assign manual subscription status and active booth allowance. | Done | `/api/admin/subscription-plans`, `/api/admin/subscriptions`, `/api/admin/subscriptions/{subscriptionId}`, Admin Web subscription controls, allowance enforcement on booth creation/offer activation, and audit/report views exist. | None for non-Maya MVP baseline. |
| 3 | Client Owner can create a location, booth, user, and booth offer inside their client account. | Done | Admin APIs support scoped create/update operations; Client Owner requests are forced to their own tenant and covered by API tests; Admin Web has setup and management controls. | None for non-Maya MVP baseline. |
| 4 | Client Owner or Client Admin can activate exactly one booth offer for a booth. | Done | Activation API exists, schema enforces one active activation per booth, and API validates scoped booth/offer references plus subscription eligibility; offer/booth deactivation clears active activations; Admin Web exposes activation controls. | None for non-Maya MVP baseline. |
| 5 | Cashier can be assigned to exactly one booth. | Done | User model has one assigned booth; create/update user APIs reject cashier records without a same-tenant booth; Admin Web exposes assigned-booth user controls; tests cover invalid assignment. | None for non-Maya MVP baseline. |
| 6 | Booth UI displays only the booth's active offer. | Done | `/api/booth-ui/config` and Booth UI active offer rendering/state screens exist. | None for non-Maya MVP baseline. |
| 7 | Booth UI displays client branding and active session text. | Done | Booth config returns client/theme/session values and Booth UI applies colors/text. | None for non-Maya MVP baseline. |
| 8 | Customer can confirm the active offer and choose cash payment when payment is required. | Done | Booth UI has offer confirmation, payment selection, waiting/approved/session/completed-prompt/expired/error states, shows extra print unit price when present, and selects `CASH`. | None for non-Maya MVP baseline. |
| 9 | Cashier can approve cash payment. | Done | Cashier approval endpoint and dedicated Admin Web POS view exist; approval blocks offline agent; cashier `return-to-welcome` recovery cancels the booth's active non-terminal transaction, marks the latest booth session failed when present, and returns the booth to `WELCOME`. | None for non-Maya MVP baseline. |
| 10 | Pending cash payments expire after the configured timeout. | Done | Worker expires `PENDING_CASH`, returns booth to welcome, resets `COMPLETED` booths after the 15-second post-session prompt, and Booth UI has expired/completed states. | None for non-Maya MVP baseline. |
| 11 | Paid transactions command the booth agent to start a LumaBooth session. | Blocked | Agent command polling turns `PAID` into `STARTING_SESSION` and returns `START_SESSION` with validated `lumaboothSessionMode`, offer, transaction, and reserved print metadata; Windows Agent can call the local LumaBooth API in `Api` mode or preserve simulator mode. | Requires real booth hardware/LumaBooth smoke validation before marking complete. |
| 12 | Agent can report session completion. | Blocked | Agent session started/completed/failed endpoints accept optional LumaBooth refs/events; local trigger listener maps `session_start` and `session_end` to backend callbacks; API and Agent tests cover metadata persistence and trigger handling. | Requires real booth hardware/LumaBooth trigger validation before marking complete. |
| 13 | Completed transactions appear in tenant-scoped reports. | Done | Admin overview includes scoped transactions and backend-computed booth/location/offer sales summaries; API tests cover client scoping. | None for non-Maya MVP baseline. |
| 14 | Application Owner dashboard shows client and subscription health. | Done | Admin overview includes active clients, active/offline booths, subscription status counts, manual MRR estimate, and clients over allowance; Admin Web renders dashboard/report metrics. | None for non-Maya MVP baseline. |
| 15 | Client Owner dashboard shows client sales and booth status. | Done | Overview scope helpers support client scoping; Admin Web dashboard/reports show sales, booth status, pending cash, failures, locations, offers, and transactions. | None for non-Maya MVP baseline. |
| 16 | Cashier dashboard shows only the assigned booth. | Done | Cashier endpoints, overview booth scoping, POS filtering, reports, and tests prove assigned-booth isolation. | None for non-Maya MVP baseline. |
| 17 | Maya Checkout QR and Maya Terminal ECR are visible only as coming soon setup flows and locked booth assignment options. | Pending | Data model and mockups exist. | Implement locked setup/assignment UI and APIs without runtime enablement. |
| 18 | Per-session transactions can accept post-session extra print add-ons. | Blocked | Cashier endpoint `POST /api/cashier/transactions/{parentTransactionId}/extra-prints` creates cash-only add-ons for the latest completed per-session transaction; Admin Web POS exposes 1-5 copy controls; backend command polling emits `PRINT_COPIES`; Agent API mode calls LumaBooth `/api/print?count={count}`; print-completed/failed callbacks update transaction and booth state; API, Agent, and frontend tests cover the main path. | Requires real booth hardware/LumaBooth print-copy smoke validation before marking complete. |
| 19 | Time-unlimited and session-count offers reject extra print add-ons. | Done | Extra print creation validates the parent offer snapshot is `PER_SESSION`; explicit endpoint tests cover `TIME_UNLIMITED` and `SESSION_COUNT` rejection. | None for non-Maya MVP baseline. |
| 20 | Booth payment options are filtered by booth assignment, and cash is the only runtime-enabled MVP payment option. | Done | Booth config filters assigned runtime-enabled cash options, workflow only accepts `CASH`, non-cash assignment attempts are locked, and disabled cash assignments are excluded. | None for non-Maya MVP baseline. |

## Next Recommended Vertical Slices

1. Run a real booth laptop smoke test with LumaBooth/dslrBooth Professional API, URL triggers, and `/api/print?count={count}` enabled, then tighten Agent recovery around any observed ambiguous start/end/print states.
2. When Maya work is intentionally resumed, add coming-soon Maya setup/locked assignment flows without enabling runtime Maya payments.

## Open Decisions And Blockers

- LumaBooth session start, terminal trigger mapping, and extra print copy command are selected for MVP. Real booth validation remains required and cannot be completed without the booth laptop, LumaBooth/dslrBooth Professional, camera, printer, and URL triggers available.
- Exact Maya production verification steps with the client's Maya account manager remain open and belong to Phase 5.
- Real Maya payment runtime support is intentionally out of scope until Phase 5.

## Update Rules

- Update this file in the same PR as any meaningful MVP vertical slice.
- Keep statuses conservative. A scaffold or single-path happy flow is usually `Partial`, not `Done`.
- Record evidence as concrete code, route, test, or UI behavior.
- Record validation commands and whether they passed.
- Do not mark an acceptance criterion `Done` until the workflow is implemented end to end enough for the MVP expectation.
