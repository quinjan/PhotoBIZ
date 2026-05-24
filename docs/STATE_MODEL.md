# PhotoBIZ State Model

This document defines the PhotoBIZ lifecycle statuses, runtime states, parent-child availability rules, and known implementation gaps. It is a companion to the PRD. `docs/ARCHITECTURE.md` remains the source of truth if any document conflicts with this one.

The main rule: `ACTIVE` and `INACTIVE` are local to the entity that owns them. They are not one global state. A client account, user, location, booth, package, payment assignment, transaction, and booth session can each be active or inactive for different reasons.

## Status Vocabularies

### Client Account

`ClientAccount.Status` controls tenant account access.

| Status | Meaning | Expected effect |
| --- | --- | --- |
| `ACTIVE` | The tenant account is live. | Client users may use normal tenant workflows when their own user and subscription gates also pass. |
| `SUSPENDED` | The tenant account is paused by the Application Owner. | Client users may sign in for read-only account/history/report access. New setup mutations, POS runtime actions, kiosk transactions, and new agent commands are blocked. |
| `ARCHIVED` | The tenant account is retired. | Client users cannot sign in or use runtime surfaces. Application Owner can still view/manage historical records. |

Client account status is separate from subscription status. A client can be `ACTIVE` while its latest subscription is `SUSPENDED` or `CANCELLED`.

### User

`ApplicationUser.Status` controls individual identity access.

| Status | Meaning | Expected effect |
| --- | --- | --- |
| `ACTIVE` | The user may authenticate if all parent account gates allow it. | Login may succeed. Role, tenant scope, password-change state, and assigned booth still constrain access. |
| `INACTIVE` | The user account is disabled. | Login and existing authenticated access are blocked. |

`MustChangePassword` is not a lifecycle status. It allows authentication but blocks admin and cashier workflows until the password is changed.

Client Owner rules:

- Each client account has exactly one Client Owner.
- Client Owner/Admin user management cannot create another owner, change the current user's own role, or deactivate the current user's own account.
- Application Owner owner transfer is the only path that changes the Client Owner.

### Subscription Plan

`SubscriptionPlan.Active` controls catalog availability only.

| Value | Meaning | Expected effect |
| --- | --- | --- |
| `true` | The plan is selectable for new client subscription assignments. | Existing assignments can reference it. |
| `false` | The plan is hidden/retired from new assignments. | Existing client subscription records remain historical data and should not be rewritten. |

This is not the same as a client's subscription status.

### Client Subscription

`ClientSubscription.Status` controls commercial eligibility and booth allowance for the tenant. The latest subscription for a client is the one used for runtime and allowance decisions.

| Status | Meaning | Runtime effect |
| --- | --- | --- |
| `TRIAL` | Trial subscription is valid. | Booth activation and runtime sessions are allowed within active booth allowance. |
| `ACTIVE` | Paid/manual subscription is valid. | Booth activation and runtime sessions are allowed within active booth allowance. |
| `SUSPENDED` | Subscription is suspended. | New booth activation and new booth sessions are blocked. |
| `CANCELLED` | Subscription has ended. | New booth activation and new booth sessions are blocked. Historical records remain. |

The active booth allowance counts `Booth.Status == ACTIVE`, not agent online state.

### Location

`Location.Status` controls a physical/site-level availability gate.

| Status | Meaning | Expected effect |
| --- | --- | --- |
| `ACTIVE` | The site can host active booths. | Booths under the location may be available if their own gates pass. |
| `INACTIVE` | The site is paused or closed. | Booths under the location remain stored but become effectively unavailable for kiosk transactions, POS runtime actions, and new agent commands. |

Inactive location rule:

- Inactivating a location does not delete booths.
- Inactivating a location does not automatically set child booths to `INACTIVE`.
- Historical transactions and reports for the location remain visible.
- Reactivating the location can restore booth availability only if the client account, subscription, booth, offer activation, payment assignment, and agent heartbeat gates also pass.

### Booth

PhotoBIZ uses two booth concepts that must not be merged:

- `Booth.Status`: inventory lifecycle.
- `Booth.CurrentState`: live runtime state.

`Booth.Status` values:

| Status | Meaning | Expected effect |
| --- | --- | --- |
| `ACTIVE` | The booth is part of the client's active booth inventory and counts against allowance. | Runtime may be available if all parent gates pass. |
| `INACTIVE` | The booth is disabled/ended/unavailable. | Runtime is unavailable. Active offer activations are deactivated. Historical records remain. |

`Booth.CurrentState` values:

| State | Meaning |
| --- | --- |
| `OFFLINE` | Effective booth state when the agent has no fresh heartbeat, or a disabled booth is forced offline. |
| `WELCOME` | Booth is available for the next customer flow. |
| `OFFER_CONFIRMED` | Customer confirmed the active offer and a transaction has been created. |
| `PAYMENT_PENDING` | Payment is waiting for cashier/provider approval. |
| `PAID` | Payment is accepted and the transaction is waiting for agent command pickup. |
| `STARTING_LUMABOOTH` | Agent command has been acquired and LumaBooth start is in progress. |
| `IN_LUMABOOTH_SESSION` | LumaBooth session is in progress. |
| `PRINTING_OR_SHARING` | Extra print or post-session output workflow is in progress. |
| `COMPLETED` | Session completed and the Booth UI completion prompt is active. |
| `ERROR` | Booth/session/print workflow failed and needs recovery. |

Agent availability is derived from `LastHeartbeatAt`. A booth with stale or missing heartbeat is effectively `OFFLINE` even if persisted `CurrentState` is `WELCOME`.

Payment method selection does not have its own booth runtime state. Once a customer selects a payment method, the booth advances directly from `OFFER_CONFIRMED` to `PAYMENT_PENDING`.

### Booth Offer

`BoothOffer.Active` controls whether a package can be selected for booths.

| Value | Meaning | Expected effect |
| --- | --- | --- |
| `true` | Package is available for assignment. | Client can select it for a booth if subscription and booth gates pass. |
| `false` | Package is retired. | Existing active activations should be deactivated. Historical transactions keep their offer snapshot. |

Offer types:

- `PER_SESSION`
- `TIME_UNLIMITED`
- `SESSION_COUNT`

### Booth Offer Activation

`BoothOfferActivation.Status` is the booth-level selected package state.

| Status | Meaning | Runtime effect |
| --- | --- | --- |
| `ACTIVE` | Package is selected and usable. | Booth UI can create per-session transactions or covered plan sessions as appropriate. |
| `PENDING_PAYMENT` | Time/session-count package is selected but not paid/activated by cashier yet. | Booth UI can display the package but must not expose customer runtime payment options. Cashier POS can create a `PLAN_ACTIVATION` transaction. |
| `INACTIVE` | Package was deactivated/replaced. | Not usable for new sessions. |
| `COMPLETED` | Timed package expired or session-count allowance was consumed. | Not usable for new sessions. |
| `CANCELLED` | Pending activation was cancelled/replaced before becoming active. | Not usable for new sessions. |

A booth can have only one `ACTIVE` activation. It may also have one current `PENDING_PAYMENT` package selection for non-per-session plans.

### Payment Resources And Assignments

Tenant-level payment resources register built-in or provider setup. Booth-level assignments decide runtime availability.

Cash is a special built-in tenant resource. It is always enabled/verified for every client account and cannot be disabled at the tenant level. Provider-backed resources such as PayMongo QR Ph require verified tenant setup before they can be assigned and runtime-enabled for a booth; disabling a provider resource moves it to `DISABLED`.

Client payment resource statuses:

| Status | Meaning |
| --- | --- |
| `NOT_CONFIGURED` | Provider/resource is absent. |
| `DRAFT` | Provider/resource setup is saved or enabled but not runtime-ready. |
| `VERIFIED` | Provider/resource is verified for assignment where supported. |
| `DISABLED` | Provider/resource is disabled. |

Booth payment assignment statuses:

| Status | Meaning | Runtime effect |
| --- | --- | --- |
| `ASSIGNED` | Payment method is assigned and may be runtime-enabled. | Can appear on Booth UI only when `RuntimeEnabled == true` and provider feature is live. |
| `LOCKED` | Payment method is assigned/configured but not runtime-enabled. | Visible as setup/coming-soon where appropriate, not usable by customers. |
| `DISABLED` | Payment method was removed from runtime assignment. | Not usable. |

MVP runtime payment is cash only. Tenant-level setup alone must never expose a payment method to Booth UI or POS.

### Transaction

Transaction types:

- `SESSION_PURCHASE`
- `PLAN_ACTIVATION`
- `COVERED_PLAN_SESSION`
- `EXTRA_PRINT_ADD_ON`

Transaction statuses:

| Status | Meaning |
| --- | --- |
| `CREATED` | Kiosk session purchase transaction exists before payment method selection. |
| `PENDING_CASH` | Cash approval is waiting at POS. |
| `PAID` | Payment was approved and agent work is pending. |
| `STARTING_SESSION` | Agent has acquired the command and is starting capture or print work. |
| `IN_SESSION` | Capture session is in progress. |
| `COMPLETED` | Transaction completed successfully. |
| `EXPIRED` | Pending payment expired. |
| `CANCELLED` | Cashier/user/system recovery cancelled the transaction. |
| `PAYMENT_FAILED` | Provider payment failed. |
| `SESSION_FAILED` | Agent/LumaBooth/print workflow failed. |

Terminal statuses for MVP runtime availability are `COMPLETED`, `EXPIRED`, and `CANCELLED`. `PAYMENT_FAILED` and `SESSION_FAILED` are terminal-like for customer messaging but `SESSION_FAILED` may still require manual recovery.

### Payment Attempt

Payment attempt status is provider/payment-flow specific. Cash transactions carry the authoritative cash status on the transaction. PayMongo QR Ph uses payment attempts for provider references, webhook outcomes, retries, and auditability.

### Booth Session

`BoothSession.Status` tracks the physical LumaBooth session attached to a transaction.

| Status | Meaning |
| --- | --- |
| `STARTING` | Backend issued an agent command and created a session record. |
| `IN_SESSION` | Agent reported session start. |
| `COMPLETED` | Agent reported session end. |
| `FAILED` | Agent/session recovery marked the session failed. |

### Print Entitlement

Print entitlements should not have tenant-facing active/inactive lifecycle states. The intended product state is derived:

| Derived state | Meaning |
| --- | --- |
| `In Use` | One or more packages reference the entitlement name. |
| `Not Used` | No package references the entitlement name and it may be deleted. |

Print entitlements are created or deleted. They do not store active/inactive lifecycle state.

## Effective Availability

Runtime availability is an AND of parent gates. A booth is available for a new customer session only when every applicable gate passes:

| Gate | Required condition |
| --- | --- |
| Client account | `ClientAccount.Status == ACTIVE` |
| User | Authenticated user is `ACTIVE` for cashier/admin actions |
| Subscription | Latest subscription allows runtime; `TRIAL` and `ACTIVE` are eligible by default |
| Active booth allowance | Active booth count is within the latest subscription allowance for activation flows |
| Location | `Location.Status == ACTIVE` |
| Booth inventory | `Booth.Status == ACTIVE` |
| Agent | Fresh heartbeat within the configured offline threshold for session-starting flows |
| Booth runtime | Effective booth state is `WELCOME` for a new session transaction |
| Offer activation | Booth has an `ACTIVE` package, or a `PENDING_PAYMENT` non-per-session package for cashier activation only |
| Payment assignment | Required payment method is assigned to the booth and runtime-enabled; provider-backed methods also require a usable client-level resource |

If any gate fails, Booth UI should show an unavailable state and transaction creation should be rejected by the backend. Admin Web may still show records for setup, history, reporting, and recovery where policy allows.

## Access Outcomes

### Client Owner Login

| User status | Client status | Expected outcome |
| --- | --- | --- |
| `ACTIVE` | `ACTIVE` | Login succeeds. Normal tenant access depends on role and password-change state. |
| `ACTIVE` | `SUSPENDED` | Login succeeds in read-only mode. New setup mutations, POS runtime actions, kiosk transactions, and new agent commands are blocked. |
| `ACTIVE` | `ARCHIVED` | Login is blocked for client users. |
| `INACTIVE` | Any | Login is blocked. |

Application Owner access remains separate. The Application Owner can manage client lifecycle and subscription lifecycle regardless of the client account's status.

### Suspended Clients

Suspended clients retain historical data. Client users under a suspended client can view account/history/reporting data, but cannot start new operational work. In particular:

- No new kiosk transactions.
- No cashier payment approvals or plan activations.
- No new booth offer activations.
- No booth registration or booth reactivation.
- No new agent commands.

### Archived Clients

Archived clients are retired tenants. Client users cannot sign in. The Application Owner can retain platform visibility for audit, reporting, support, or restoration decisions.

## Parent-Child Relationship Rules

### Client Account To Children

Client account status is a parent gate for users, locations, booths, packages, payment resources, and transactions.

- `SUSPENDED` does not delete child records.
- `ARCHIVED` does not delete child records.
- Historical transactions, reports, and audit logs remain tenant-scoped and visible to the Application Owner.
- Runtime actions require the client account to be `ACTIVE`.

### Subscription To Booths

Subscription status and allowance protect booth activation and runtime.

- `TRIAL` and `ACTIVE` are eligible for active booth operation.
- `SUSPENDED` and `CANCELLED` block new booth activation and new sessions.
- Active booth allowance is counted from `Booth.Status == ACTIVE`.

### Location To Booths

Location status is a parent availability gate for booths.

- Inactive location keeps child booths and historical data.
- Active booths under an inactive location should be counted carefully: they remain `Booth.Status == ACTIVE` unless explicitly deactivated, but are not effectively runtime-available.
- New booth registration or moving a booth into an inactive location should be rejected.
- Existing booths under an inactive location should not receive new kiosk transactions, cashier runtime actions, or agent commands.

### Booth To Offer Activation

Booth lifecycle controls active package usability.

- Deactivating a booth should set its active offer activations to `INACTIVE`.
- Reactivating a booth should not silently reactivate old package activations. The client should intentionally select/activate a package again.
- Runtime requires the booth lifecycle to be `ACTIVE` and effective current state to be `WELCOME`.

### Offer To Activation

Package lifecycle controls whether it can be newly selected.

- Inactive packages cannot be newly assigned to booths.
- Deactivating a package should deactivate active activations that reference it.
- Historical transactions keep snapshots and must not change when package fields change.

### Payment Resource To Assignment

Runtime payment requires both levels:

- Tenant-level payment resource exists. Cash is built in and always usable; provider-backed methods require a verified/usable client-level resource.
- Booth-level payment assignment is `ASSIGNED` and runtime-enabled.

Cash is always runtime-eligible when assigned. PayMongo QR Ph can be runtime-enabled only when the tenant resource is verified and the booth assignment is enabled; resources in `DRAFT`, `DISABLED`, or `NOT_CONFIGURED` are not runtime-usable.

### Transaction To Booth Current State

The backend owns transaction and booth runtime transitions. Booth UI and Admin Web must not locally fake state changes.

Typical MVP mapping:

| Transaction/runtime event | Booth current state |
| --- | --- |
| Agent heartbeat missing/stale | Effective `OFFLINE` |
| Ready for customer | `WELCOME` |
| Transaction created | `OFFER_CONFIRMED` |
| Cash pending or plan activation pending | `PAYMENT_PENDING` |
| Cash approved | `PAID` |
| Agent acquired start command | `STARTING_LUMABOOTH` |
| Agent reports session started | `IN_LUMABOOTH_SESSION` |
| Agent reports session completed | `COMPLETED` |
| Extra print command in progress | `PRINTING_OR_SHARING` |
| Agent/session/print failure | `ERROR` |
| Expiration, cancellation, print completion, print timeout, or recovery | `WELCOME` |

## Current Implementation Alignment

The backend now enforces the parent gates in this document across the main runtime paths.

- Login rejects inactive users and archived client accounts. Suspended client accounts may authenticate for read-only access.
- Authenticated cookie sessions are rejected when the user becomes inactive or the client account is archived.
- Suspended client accounts can read Admin Web overview data but cannot perform client-scoped setup mutations, kiosk transactions, POS runtime actions, or agent runtime actions.
- Inactive locations block new booth registration, booth movement/reactivation into that location, Booth UI runtime config availability, kiosk transactions, POS runtime actions, and new agent commands.
- Inactive booths are effectively offline and block kiosk transactions and new agent commands.
- Booth UI transaction creation and payment selection revalidate client account, latest subscription, location, booth lifecycle, and agent availability gates before changing transaction state.
