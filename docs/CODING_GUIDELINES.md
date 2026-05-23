# PhotoBIZ Coding Guidelines

These guidelines are written for AI agents and human contributors building PhotoBIZ. They turn the product architecture into everyday coding rules so implementation stays consistent across the Angular apps, ASP.NET Core API, and Windows Booth Agent.

The practical standard is strict by default and flexible by exception. Follow the rules, use automated checks where available, and document any exception that is needed to preserve MVP speed or avoid needless complexity.

## Source Of Truth

Read these documents before implementing:

1. `docs/ARCHITECTURE.md`
2. `docs/PRD.md`
3. This file

Use these official style baselines:

- [.NET C# coding conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- [ASP.NET Core guidance](https://learn.microsoft.com/en-us/aspnet/core/performance/overview)
- [EF Core efficient querying](https://learn.microsoft.com/en-us/ef/core/performance/efficient-querying)
- [Angular style guide](https://angular.dev/style-guide)
- [Angular signals guide](https://angular.dev/guide/signals/)

If guidance conflicts, prefer the most local project rule, then `docs/ARCHITECTURE.md`, then official framework guidance.

## Agent Workflow

- Understand the requested behavior before editing. Search the relevant docs and code first.
- Keep diffs focused. Do not refactor unrelated code while implementing a feature.
- Prefer vertical slices by workflow, such as client management, booth pairing, cash payment approval, or booth session state.
- Build the smallest complete behavior that fits the current phase.
- Add tests where behavior, authorization, state transitions, tenant isolation, payments, or agent commands change.
- Keep secrets, credentials, tokens, and provider keys out of logs, screenshots, tests, fixtures, and frontend responses.
- Update docs when implementation changes architecture, public behavior, state machines, deployment, or operational assumptions.
- Record any deliberate exception in the PR summary.

## Repository Structure

The repository is a monorepo:

```text
apps/
  admin-web/
  booth-ui/
services/
  api/
agent/
  windows-agent/
docs/
```

- `apps/admin-web` contains the Central Web App for Application Owner, client users, and cashiers.
- `apps/booth-ui` contains the customer-facing kiosk UI.
- `services/api` contains the ASP.NET Core backend API and background worker code unless a later architecture update splits them.
- `agent/windows-agent` contains the .NET Windows Service that integrates with LumaBooth.
- Shared frontend DTOs, API clients, validation helpers, constants, and UI primitives should live in Angular workspace libraries once the workspace exists.

Do not add another top-level runtime surface without updating `docs/ARCHITECTURE.md`.

## .NET And C# Standards

- Target .NET 10 LTS unless `docs/ARCHITECTURE.md` is updated.
- Enable nullable reference types for all new C# projects.
- Use file-scoped namespaces.
- Use four-space indentation and Allman braces.
- Use `async` and `await` for I/O-bound work.
- Pass `CancellationToken` through request handlers, database calls, provider calls, SignalR paths, and background jobs when available.
- Use `string`, `int`, and other C# keywords instead of runtime type names in normal code.
- Use `var` only when the type is obvious from the right side.
- Prefer immutable DTOs and records for request/response shapes when practical.
- Use clear names over abbreviations. Domain terms should match the PRD and architecture.
- Catch only exceptions the code can handle meaningfully. Let unexpected failures flow to centralized error handling.
- Log structured events with enough context to debug, but never log secrets or full payment/provider payloads that contain sensitive data.

## Backend API Standards

- Organize backend code by vertical slice or product workflow rather than broad technical folders when the project shape allows it.
- Keep controllers or endpoint handlers thin. Put validation, authorization decisions, state transitions, and persistence orchestration in the slice handler or application service.
- Use explicit request and response DTOs. Do not expose EF entities directly from API endpoints.
- Backend owns truth for tenant isolation, subscription enforcement, transaction state, payment state, booth state, and agent commands.
- Use policy-based authorization for role and tenant-sensitive behavior.
- Return consistent validation and problem responses. Do not leak internal exception details to clients.
- Keep API routes resource and workflow oriented. Use verbs in route names only for true commands such as payment approval, booth pairing, or session recovery.
- All sensitive state changes must be auditable, including subscription changes, user status changes, booth credential changes, payment approvals, Maya configuration changes, and manual recovery actions.

## EF Core And Data Rules

- Every client-scoped query must filter by `client_account_id` or equivalent tenant scope before returning or mutating data.
- Prefer projections into DTOs for read endpoints instead of loading full entity graphs.
- Use `AsNoTracking` for read-only queries.
- Keep transactions explicit around multi-step state changes that must commit atomically.
- Model transaction, payment, booth, and session state transitions in code so invalid transitions cannot slip through simple property updates.
- Booth offer details used by a transaction must be snapshotted into the transaction record.
- Use raw SQL only when EF Core cannot produce the needed query or performance requires it. Keep raw SQL parameterized.
- Do not rely on frontend state, hidden form fields, or client-provided tenant IDs as authority.

### EF Core Migration Workflow

- Prefer `dotnet ef migrations add <Name> --project services/api/src/PhotoBIZ.Api/PhotoBIZ.Api.csproj --startup-project services/api/src/PhotoBIZ.Api/PhotoBIZ.Api.csproj` for schema changes instead of hand-authoring migration files.
- A valid migration must include the migration `.cs` file, its `.Designer.cs` metadata file when generated by EF, and the updated `PhotoBizDbContextModelSnapshot.cs`. Do not leave snapshot or designer changes out of a PR.
- If a migration is hand-authored or repaired, confirm it has both `[DbContext(typeof(PhotoBizDbContext))]` and `[Migration("<MigrationId>_<Name>")]` metadata so EF Core can discover it. Missing metadata can make code and model snapshots look correct while `dotnet ef` silently ignores the migration.
- After adding or editing a migration, run `dotnet ef migrations list --project services/api/src/PhotoBIZ.Api/PhotoBIZ.Api.csproj --startup-project services/api/src/PhotoBIZ.Api/PhotoBIZ.Api.csproj` and verify the new migration appears. If it should still be unapplied locally, it must show as `(Pending)`.
- Apply migrations to the local development database with `dotnet ef database update --project services/api/src/PhotoBIZ.Api/PhotoBIZ.Api.csproj --startup-project services/api/src/PhotoBIZ.Api/PhotoBIZ.Api.csproj` before validating workflows that depend on the schema.
- For destructive changes such as dropping columns, indexes, or enum-like status values, include explicit data cleanup or remapping SQL in `Up` before the drop, and a reasonable `Down` path for local rollback.
- When removing a property from an EF entity, also remove related indexes, DTO fields, seed/default insert values, tests, and documentation references in the same change.
- Validate schema-sensitive workflows against the local database after applying the migration. A green build is not enough if the bug depends on real PostgreSQL constraints.

## Security And Tenant Isolation

- Treat all browser clients, kiosk clients, and booth agents as untrusted input sources.
- Admin users authenticate with secure HttpOnly cookie sessions.
- Booth UI authenticates with a booth-scoped kiosk token.
- Windows Agent authenticates with a separate booth agent credential.
- Cashiers can approve only transactions for their assigned booth.
- Application Owner can manage platform clients and subscriptions, but normal booth transaction approvals remain client/cashier scoped.
- Validate subscription status and active booth allowance before starting sessions or activating booths.
- Validate theme colors, image URLs, booth offer inputs, and all provider configuration.
- Never allow tenant-provided CSS, scripts, SQL, HTML, command strings, or arbitrary local paths.
- Store provider secrets encrypted and never return secret values to frontend clients after save.

## Angular And TypeScript Standards

- Use Angular 21 in one Angular workspace with separate `admin-web` and `booth-ui` applications.
- Use strict TypeScript and strict Angular templates.
- Prefer standalone components for new Angular code.
- Organize by feature area, not by generic type folders such as `components`, `services`, or `models`.
- Keep related component TypeScript, template, style, and test files together.
- Use hyphenated file names.
- Keep components focused on presentation and user interaction. Move validation, API mapping, and data transforms into focused helpers or services.
- Use signals for local component state and derived UI state.
- Use RxJS for API calls, realtime streams, cross-component async flows, and cancellation-aware streams.
- Prefer `protected` for members used only by templates.
- Mark Angular inputs, outputs, queries, injected services, and constants as `readonly` when they should not be reassigned.
- Prefer native `class` and `style` bindings over `ngClass` and `ngStyle`.
- Keep templates simple. Move complex conditions, formatting, and derived values into TypeScript.
- Do not duplicate backend authorization or state-machine authority in the frontend. The frontend can guide users, but the backend must enforce.

## UI And Design Standards

- Admin and cashier screens should feel like modern operational SaaS: calm, dense enough for repeated work, responsive, accessible, and easy to scan.
- Booth UI should be touch-first, readable from a distance, kiosk-safe, and resilient to refreshes or temporary realtime disconnects.
- Use Angular Material for admin surfaces unless a feature needs a custom UI primitive.
- Use AG Grid Community for dense Admin Web operational tables; do not add Bootstrap or ng-bootstrap alongside Material.
- Use constrained tenant theming through backend-provided configuration and CSS variables.
- Validate color contrast for tenant themes where practical.
- Prefer clear icons, status labels, tables, filters, tabs, dialogs, and forms for admin workflows.
- Avoid decorative layouts that reduce scanability in operational screens.
- Ensure all buttons, cards, form controls, and kiosk touch targets remain usable on target booth and desktop viewports.
- Do not use arbitrary tenant CSS, scripts, HTML templates, or layout definitions.

## Realtime, Jobs, And Agent Rules

- Use SignalR for booth state, cashier notifications, dashboards, and agent command updates that must arrive quickly.
- Realtime events should reflect backend state after persistence, not optimistic client state.
- Use Hangfire for background jobs such as transaction expiration and scheduled recovery tasks.
- Use Redis for realtime backplane, cache, and distributed locks according to the architecture.
- Agent commands must be idempotent or safely retryable where possible.
- The Windows Agent owns local Windows and LumaBooth integration. The API owns command authorization and durable state.
- Booth UI must not start LumaBooth directly.

## Testing Expectations

- Backend unit tests should cover domain rules, state transitions, authorization helpers, validators, and provider mapping logic.
- Backend integration tests should cover API auth, tenant isolation, EF Core persistence, transaction workflows, and error responses.
- Frontend tests should cover important user workflows, component state, form validation, API services, and realtime handling.
- Booth UI tests should cover active offer display, payment method display, cash waiting state, expiration/error handling, and reset to welcome.
- Add regression tests for every bug fix unless the test would be more brittle than useful. Document the reason if no test is added.
- Guideline-only documentation changes do not require application tests.

## Enforcement

Add these enforcement pieces as the codebase is scaffolded:

- Root `.editorconfig` for C# and TypeScript formatting.
- .NET analyzers with nullable reference types enabled.
- Angular ESLint and Prettier.
- Strict TypeScript and Angular template checking.
- CI checks for backend build/test, frontend build/test/lint, and formatting where practical.
- PR checklist items for tenant isolation, backend authority, tests, and documentation updates.

Use warnings or documented exceptions during early scaffolding when strict enforcement would slow necessary setup. Move toward failing CI for meaningful correctness and security issues as soon as the project has stable build pipelines.

## Git And PR Standards

- Keep commits focused and descriptive.
- Do not mix unrelated feature work, refactors, generated output, and formatting churn.
- PR descriptions should include summary, impact, validation, and documented exceptions.
- Mention changed public interfaces, routes, DTOs, state transitions, environment variables, migrations, and deployment behavior.
- Do not commit secrets, local credentials, production data, or machine-specific files.

## Do Not Do This

- Do not bypass tenant filters for convenience.
- Do not let Booth UI or Admin Web mark transactions as paid without backend authorization.
- Do not start LumaBooth from browser code.
- Do not expose agent credentials, kiosk tokens, password hashes, Maya secrets, or raw sensitive provider payloads.
- Do not introduce arbitrary tenant CSS, scripts, SQL, command strings, or local file paths.
- Do not return EF entities directly from API endpoints.
- Do not hide failed checks or test gaps.
- Do not create a new architectural pattern because a single feature feels awkward. Adjust the feature first, then update architecture only when the pattern is broadly useful.
