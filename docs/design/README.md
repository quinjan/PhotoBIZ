# UI Mockups

This folder contains sample UI designs for the PhotoBIZ Central Web App and Booth UI.

For the full PRD-to-screen workflow map, see [UI Workflow Mockups](WORKFLOWS.md).

## Source

- `ui-mockups.html`: source mockup file.
- `screenshots/`: previously exported PNGs. These are not regenerated unless explicitly requested.

## Central Web App Direction

The Central Web App is dense, operational, and role-aware:

- Application Owner screens focus on SaaS clients, subscriptions, active booths, and platform health.
- Client Owner/Admin screens focus on one client account: users, locations, booths, packages, branding, sessions, and reports.
- Cashier screens focus on one assigned booth and fast payment approval.
- Payment setup screens show cash-only MVP behavior plus coming soon Maya Checkout QR and Maya Terminal ECR registration flows.

## Booth UI Customization Model

MVP Booth UI customization is intentionally minimal and safe. Client branding is stored once per client account, while an active session can override welcome copy and assigned packages.

Client-level fields:

- Brand/display name.
- Logo.
- Background image.
- Theme preset: `VINTAGE_FILM` or `MODERN_POP`.
- Primary color.
- Accent color.
- Default welcome headline.
- Default welcome subtitle.

Session-level overrides:

- Session label.
- Welcome headline.
- Welcome subtitle.
- Assigned packages.

Implementation rule:

- Booth UI renders from backend config returned by `GET /booth-ui/config`.
- Angular maps theme values to CSS custom properties such as `--booth-primary`, `--booth-accent`, `--booth-background-image`, and `--booth-font-mode`.
- Clients cannot upload arbitrary CSS, scripts, or custom layouts in MVP.

## Sample Client Designs

The mockup source includes two sample tenant Booth UI designs:

- **The Memory Box**: vintage film style with warm cream, rust, gold, muted teal, serif display type, and background-image treatment.
- **Pixel Pop Studio**: bright modern mall style with clean white/black base, saturated cyan/magenta accents, larger touch targets, and bold card layout.

## Payment Mockup Notes

MVP payments are cash only. `MAYA_CHECKOUT_QR` and `MAYA_TERMINAL_ECR` appear as coming soon planning screens so the future client registration workflow is visible without implying live cashless payment support.
