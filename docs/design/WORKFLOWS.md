# UI Workflow Mockups

This document maps the PhotoBIZ PRD functionality to UI mockup screens.

The source mockups live in `ui-mockups.html`. Screenshots in `screenshots/` may be stale unless explicitly regenerated; do not generate screenshots unless requested.

## Central Web App Coverage

| Functionality | Mockup section id |
| --- | --- |
| Login | `central-login` |
| Application Owner platform dashboard | `platform-dashboard` |
| Client accounts list | `client-accounts` |
| Client account detail | `client-account-detail` |
| Manual subscription editor | `subscription-editor` |
| Client Owner dashboard | `central-dashboard` |
| Client user management | `users-list` |
| Add/edit client user | `user-form` |
| Locations and booth inventory | `locations-booths` |
| Register booth | `booth-register` |
| Booth detail, health, recovery, and end booth | `booth-detail` |
| Booth offer catalog | `packages-management` |
| Booth offer editor and booth activation | `package-editor` |
| Client branding plus booth-level theme/session/payment setup | `booth-customization` |
| Transactions, reports, and audit activity | `transactions-reporting` |
| Client payment resource and session settings | `settings-payments` |
| PayMongo QR Ph registration | `paymongo-qrph-registration` |
| Cashier POS | `cashier-pos` |

## Booth UI Coverage

| Functionality | Mockup section id |
| --- | --- |
| Client A welcome, vintage theme | `client-a-booth-welcome` |
| Client A active offer review, vintage theme | `client-a-booth-packages` |
| Client A payment preview, vintage theme | `client-a-booth-payment` |
| Client B welcome, modern pop theme | `client-b-booth-welcome` |
| Client B covered plan handoff, modern pop theme | `client-b-booth-packages` |
| Client B covered plan return to welcome, modern pop theme | `client-b-booth-payment` |
| Cash waiting screen | `booth-cash-waiting` |
| Payment approved / starting LumaBooth | `booth-starting-session` |
| LumaBooth session in progress / handoff | `booth-session-progress` |
| Session complete | `booth-session-complete` |
| Error and recovery | `booth-error-recovery` |

## End-To-End SaaS Workflow

```mermaid
flowchart TD
  A["Application Owner signs in"] --> B["Create client account"]
  B --> C["Assign manual per-booth subscription"]
  C --> D["Create/invite Client Owner"]
  D --> E["Client Owner configures client brand defaults"]
  E --> F["Client Owner creates location"]
  F --> G["Client Owner registers booth"]
  G --> H["Backend checks subscription booth allowance"]
  H --> I["Generate agent credential and kiosk token"]
  I --> J["Windows Agent pairs"]
  J --> K["Booth UI loads /booth-ui/config"]
  K --> L["Client creates booth offers"]
  L --> M["Client configures client payment resources"]
  M --> N["Client activates one offer, one theme, and booth payment options"]
  N --> O["Customer confirms per-session offer and chooses cash payment"]
  O --> P["Cashier approves cash"]
  P --> Q["Backend commands Windows Agent"]
  Q --> R["LumaBooth captures, prints, and handles Fotoshare"]
  R --> S["Agent reports session complete"]
  S --> T["Booth UI returns to welcome"]
```

## Client Subscription Setup Checklist

1. Application Owner creates a client account.
2. Application Owner creates or assigns a subscription plan.
3. Application Owner sets subscription status and active booth allowance.
4. Application Owner creates or invites the first Client Owner.
5. Client Owner signs in and configures client branding.
6. Client Owner creates locations, users, booth offers, and booths.

## Booth Registration To Live Checklist

1. Client Owner or Client Admin creates or confirms the location.
2. Client Owner or Client Admin registers the booth with name, code, location, assigned cashier, camera, and printer.
3. Backend validates client subscription status and active booth allowance.
4. System creates agent pairing credential and Booth UI kiosk token.
5. Technician installs and pairs the Windows Agent on the booth laptop.
6. Booth UI opens using the booth-scoped kiosk token.
7. Client activates one booth offer for the booth.
8. Client selects a PhotoBIZ-managed theme at booth level, while keeping client-level branding and session text.
9. Client assigns payment options for the booth. Cash is the only runtime-enabled MVP option.
10. Booth status becomes online and ready.

## Customer Session Checklist

1. Booth UI loads booth-level theme plus client branding and active session config.
2. Booth UI shows the welcome screen.
3. Customer taps start.
4. Customer reviews and confirms the booth's active offer.
5. Customer chooses Cash payment for a payable per-session flow.
6. Backend creates a pending transaction.
7. Cashier POS receives the pending payment.
8. Cashier approves cash.
9. Backend marks transaction as paid.
10. Backend commands Windows Agent to start LumaBooth.
11. LumaBooth completes photo capture, print, and Fotoshare.
12. Agent reports completion.
13. Backend marks transaction completed.
14. Booth UI returns to welcome.

## Payment Resource And Booth Assignment Checklist

These mockups document client-level payment resources and booth-level assignment. PayMongo QR Ph is runtime-available only after verified tenant setup, booth assignment, and runtime enablement.

PayMongo QR Ph setup:

1. Client Owner opens `settings-payments`.
2. Client selects PayMongo test or live mode and reviews the same-page setup instructions.
3. Client copies matching API keys from PayMongo `Settings > Developers` and enters business account name, public key, secret key, and webhook secret.
4. Client creates a same-mode PayMongo webhook in `Developers > Webhooks` using the PhotoBIZ webhook URL and subscribes to `payment.paid`, `payment.failed`, and `qrph.expired`.
5. Client saves the provider resource, then uses Verify PayMongo Setup.
6. Verified `PAYMONGO_QRPH` can be assigned per booth and runtime-enabled alongside cash.

## Ending A Booth

Ending a booth means taking a booth out of service while preserving historical transactions.

Flow:

1. Client Owner or Client Admin opens Booth Detail.
2. User confirms no active customer session is in progress.
3. User disables new sessions.
4. User clicks `End Booth`.
5. System revokes agent and Booth UI kiosk credentials.
6. Booth status changes to inactive/unregistered.
7. Active booth usage is reduced for subscription allowance.
8. Audit log records the action.
9. Historical transactions remain visible in reports.
