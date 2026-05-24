# Hosting And Deployment Plan

## Decision

Use a low-cost Singapore-region VPS for MVP and early production.

The first deployment runs the Angular Admin Web, Angular Booth UI, ASP.NET Core API, PostgreSQL, Redis, and reverse proxy on one VPS using Docker Compose. Booth laptops run the separate Windows Agent Control Center from a signed, self-contained Windows `.exe` installer.

Starting provider:

- DigitalOcean Droplet, Singapore region, Basic 2 GB RAM / 1 vCPU / 50 GB SSD.

Minimum production setup:

- One VPS.
- Docker Compose.
- Caddy reverse proxy.
- PostgreSQL container with persistent volume.
- Redis container with persistent volume or ephemeral cache depending on implementation.
- Cloudflare DNS in front of the domain.
- Automated VPS backups enabled.
- Off-server PostgreSQL backups copied to object storage or another backup location.

This keeps starting cost low while keeping deployment understandable and CI/CD friendly.

## Why This Hosting Model

The product needs a long-running backend API with SignalR/WebSocket connections from booth agents and browser clients. This makes a normal VPS or container host a better starting point than serverless hosting.

The MVP will have low traffic. The expensive part of the business is the physical booth, not cloud load. A single properly backed-up VPS is enough for one booth and likely enough for the first few booths.

## Estimated Starting Cost

Approximate monthly cost:

```text
DigitalOcean 2 GB Droplet:      about $12/month
Weekly Droplet backups:         about 20% of Droplet cost
Cloudflare DNS:                 free plan is enough
GitHub Actions:                 usually covered by included free minutes at this stage
Domain name:                    separate yearly cost
```

Expected MVP hosting cost: about $15/month plus domain cost.

Use a 4 GB RAM VPS when:

- Build/deploy memory becomes tight.
- PostgreSQL grows.
- More booths connect.
- The API, database, and Redis start competing for memory.

## Deployment Architecture

```mermaid
flowchart TB
  Dev["Developer pushes to GitHub"] --> GHA["GitHub Actions"]
  GHA --> Test["Build + Test"]
  Test --> Deploy["Deploy over SSH"]

  Deploy --> VPS["Singapore VPS"]

  subgraph VPS["Singapore VPS"]
    Proxy["Caddy<br/>TLS + Reverse Proxy"]
    Admin["Angular Admin Web<br/>Static Files"]
    Booth["Angular Booth UI<br/>Static Files"]
    API["ASP.NET Core API<br/>.NET 10 LTS"]
    Worker["Background Worker<br/>.NET 10 LTS Hosted Service"]
    DB[("PostgreSQL<br/>Persistent Volume")]
    Redis[("Redis<br/>Cache / Realtime Backplane / Jobs")]
  end

  Proxy --> Admin
  Proxy --> Booth
  Proxy --> API
  API --> DB
  API --> Redis
  Worker --> DB
  Worker --> Redis

  BoothLaptop["Booth Laptop<br/>Agent Control Center + kiosk Chrome"] <--> Proxy
```

## Domains

Use subdomains from the start:

```text
admin.yourdomain.com      PhotoBIZ SaaS, client admin, and cashier web app
booth.yourdomain.com      Customer-facing booth UI
api.yourdomain.com        Backend API and SignalR
```

For local booth configuration, each booth stores:

- API base URL.
- Booth ID or booth code.
- Agent pairing credential.
- Booth UI base URL.
- Chrome kiosk settings.
- LumaBooth simulator/API settings.
- Environment name: local or production.

The production booth-laptop artifact starts as a self-contained `win-x64` Windows Agent release ZIP attached to GitHub Releases. The ZIP contains the single-file `PhotoBIZ.WindowsAgent.ControlCenter.exe`, production `appsettings.json` defaults, and PowerShell install/uninstall scripts. It does not require a separate .NET runtime. The included installer script installs under `Program Files`, creates `C:\ProgramData\PhotoBIZ\Agent`, adds current-user login auto-start, and creates a Start Menu shortcut. Manual updates are enough for v1. Silent install and auto-update are intentionally out of v1 scope.

Code signing is required before client or live booth installation. Unsigned Agent builds are allowed only for local development or internal lab testing. The release workflow supports optional signing when certificate secrets are configured.

Current Agent Control Center project path:

```powershell
agent/windows-agent/src/PhotoBIZ.WindowsAgent.ControlCenter/PhotoBIZ.WindowsAgent.ControlCenter.csproj
```

Pre-installer publish command for lab validation:

```powershell
powershell -ExecutionPolicy Bypass -File agent/windows-agent/scripts/publish-control-center.ps1 -Version 0.1.0
```

This writes:

```text
artifacts/windows-agent/packages/PhotoBIZ-WindowsAgent-ControlCenter-<version>-win-x64.zip
artifacts/windows-agent/packages/PhotoBIZ-WindowsAgent-ControlCenter-<version>-win-x64.manifest.txt
```

The ZIP intentionally excludes `appsettings.Development.json`. For internal lab testing, unzip the release on the booth laptop and run:

```powershell
.\PhotoBIZ.WindowsAgent.ControlCenter.exe
```

For booth-laptop installation from the extracted ZIP:

```powershell
powershell -ExecutionPolicy Bypass -File .\Install-PhotoBIZAgent.ps1
```

The installer defaults are:

```text
Install directory: C:\Program Files\PhotoBIZ\Windows Agent
Data directory:    C:\ProgramData\PhotoBIZ\Agent
Autostart:         HKCU Run entry for the current Windows user
```

Uninstall and remove local pairing/config:

```powershell
powershell -ExecutionPolicy Bypass -File "C:\Program Files\PhotoBIZ\Windows Agent\Uninstall-PhotoBIZAgent.ps1"
```

Uninstall while preserving local pairing/config for manual troubleshooting:

```powershell
powershell -ExecutionPolicy Bypass -File "C:\Program Files\PhotoBIZ\Windows Agent\Uninstall-PhotoBIZAgent.ps1" -PreserveData
```

For a GitHub Release, run the `Windows Agent Release` workflow manually with a version such as `0.1.0`, or push a tag named:

```text
agent-v0.1.0
```

The workflow uploads the ZIP and manifest as workflow artifacts and creates a draft prerelease with those files attached.

Optional signing secrets:

```text
WINDOWS_AGENT_SIGNING_CERTIFICATE_BASE64=<base64-encoded pfx>
WINDOWS_AGENT_SIGNING_CERTIFICATE_PASSWORD=<pfx password>
```

When these secrets are present, the workflow signs `PhotoBIZ.WindowsAgent.ControlCenter.exe` before packaging. The manifest records `SignatureStatus` and SHA-256 hashes.

The installer is currently PowerShell-script based inside the release ZIP. A branded MSI/MSIX/Inno Setup style installer can still replace it later if needed, but the v1 operational requirements are represented in the release artifact.

For the full step-by-step release and booth-laptop install procedure, use [Windows Agent GitHub Release And Install Runbook](WINDOWS_AGENT_RELEASE_RUNBOOK.md).

## CI/CD Plan

Use GitHub Actions.

### Pull Request Pipeline

Runs on every pull request:

- Restore dependencies.
- Build Angular Admin Web.
- Build Angular Booth UI.
- Build ASP.NET Core API.
- Run frontend tests.
- Run backend tests.
- Run lint/format checks.
- Do not deploy.

### Production Pipeline

Runs on merge to `main`:

1. Build and test all projects.
2. Publish Angular Admin Web static build.
3. Publish Angular Booth UI static build.
4. Publish ASP.NET Core API.
5. Copy deployment artifact to VPS over SSH.
6. Run database migrations.
7. Restart Docker Compose services.
8. Run smoke checks against:
   - `https://admin.yourdomain.com`
   - `https://booth.yourdomain.com`
   - `https://api.yourdomain.com/health`

Production deployments should use GitHub Environments with manual approval once real booths are active.

## Docker Compose Services

Expected services:

```text
reverse-proxy
api
worker
postgres
redis
```

The Angular apps can be served either:

- As static files mounted into the reverse proxy container.
- Or as small static-site containers.

For MVP, serving static files through Caddy is simpler and cheaper.

## Backup Requirements

Backups are mandatory before any live booth accepts real payments.

Minimum backup setup:

- VPS provider weekly backup.
- Nightly PostgreSQL dump.
- Keep at least 7 daily database backups.
- Copy database backups off-server.
- Test restore before launch.

Later backup setup:

- Managed PostgreSQL with automated backups.
- Point-in-time recovery.
- Object storage for exported reports and optional assets.

## Scaling Plan

### Stage 1: MVP / First Booth

Use one 2 GB VPS.

Run everything on the same server:

- Angular static files.
- ASP.NET Core API.
- PostgreSQL.
- Redis.
- Background worker.

### Stage 2: First Few Booths

Upgrade to one 4 GB VPS or move PostgreSQL to a managed database.

Upgrade trigger:

- More than 3-5 active booths.
- Frequent production deployments.
- Database backups/restores become operationally important.
- Memory pressure on the VPS.

### Stage 3: Multi-Location Operations

Move to managed services:

- App host for ASP.NET Core API.
- Managed PostgreSQL.
- Managed Redis or Redis-compatible service.
- Separate production infrastructure from local development.

Good candidates:

- DigitalOcean App Platform + Managed PostgreSQL.
- Azure App Service + Azure Database for PostgreSQL + Azure SignalR.
- AWS ECS/App Runner + RDS PostgreSQL + ElastiCache.

### Stage 4: SaaS Scale

Use a more managed and redundant architecture:

- Multiple API instances.
- Load balancer.
- Managed PostgreSQL with point-in-time recovery.
- Redis backplane or managed SignalR.
- Centralized logging.
- Error monitoring.
- Infrastructure as code.

## Why Not Start With Managed PaaS

Managed PaaS options are easier operationally but usually cost more once the API, database, Redis, background workers, and static sites are counted separately.

For this product, the MVP needs to preserve cash while the booth business model is validated. A VPS gives the lowest predictable monthly bill.

Move to managed services when uptime, backup guarantees, and operational simplicity become more valuable than the extra monthly cost.

## Notes On GitHub Actions Cost

GitHub Actions should be enough for MVP if the repository is small and builds are not excessive.

Use these cost controls:

- Run full deployment only on `main`.
- Run normal CI on pull requests.
- Cache NuGet and npm dependencies.
- Avoid building Docker images for every branch unless needed.
- Use manual production approval once booths are live.

## Initial Recommendation

Start with:

- DigitalOcean Basic Droplet, Singapore, 2 GB RAM.
- Docker Compose.
- Caddy reverse proxy.
- PostgreSQL and Redis on the same VPS.
- Cloudflare DNS.
- GitHub Actions deploy over SSH.
- Weekly VPS backups plus nightly PostgreSQL dumps.

This is the best balance of low cost, straightforward CI/CD, and enough control for a .NET + SignalR + Angular + Windows Agent product.

## Internal Pilot Without A Purchased Domain

For the first internal pilot, use the Droplet public IPv4 address with an IP-based DNS alias such as `sslip.io` instead of buying a domain.

If the Droplet public IP is `203.0.113.10`, set:

```text
PHOTOBIZ_PILOT_HOST=203.0.113.10.sslip.io
```

The pilot URLs are:

```text
https://admin.203.0.113.10.sslip.io
https://booth.203.0.113.10.sslip.io
https://api.203.0.113.10.sslip.io
```

Caddy requests normal public TLS certificates for these hostnames, which keeps the Admin Web cookie flow and PayMongo test webhooks on HTTPS without a purchased domain. The pilot Caddy config uses ZeroSSL because `sslip.io` is a shared registered domain and Let's Encrypt can rate-limit it globally. This is a pilot convenience only, not the long-term branded production URL.

Production deployment files:

- `docker-compose.prod.yml`
- `infra/caddy/Caddyfile.prod`
- `infra/production.env.example`
- `infra/scripts/provision-ubuntu.sh`
- `infra/scripts/backup-postgres.sh`
- `.github/workflows/deploy-pilot.yml`

### Droplet Provisioning

Create a fresh Ubuntu Droplet in the Singapore region. A 2 GB RAM Basic Droplet is enough for the pilot unless builds or database memory become tight.

Run the provisioning script as root on the Droplet:

```bash
DEPLOY_USER=photobiz SSH_PUBLIC_KEY="<deploy public ssh key>" bash infra/scripts/provision-ubuntu.sh
```

Then create the production environment file:

```bash
cd /opt/photobiz
cp infra/production.env.example .env
nano .env
```

Required `.env` values:

```text
PHOTOBIZ_PILOT_HOST=<droplet-public-ip>.sslip.io
POSTGRES_DB=photobiz
POSTGRES_USER=photobiz
POSTGRES_PASSWORD=<long random password>
CADDY_ACME_EMAIL=<your real email address>
```

Set `CADDY_ACME_EMAIL` to your real email address. Caddy uses it when registering the ZeroSSL ACME account for these temporary HTTPS certificates.

Do not expose PostgreSQL or Redis ports publicly. The production Compose file keeps them on the internal Docker network.

### GitHub Actions Deployment

Configure the repository `production` environment with manual approval before a live booth is connected.

Required GitHub secrets:

```text
PHOTOBIZ_DEPLOY_HOST=<droplet public ip>
PHOTOBIZ_DEPLOY_USER=photobiz
PHOTOBIZ_DEPLOY_SSH_KEY=<private key for the deploy user>
```

Optional GitHub environment variable:

```text
PHOTOBIZ_DEPLOY_PATH=/opt/photobiz
```

The deploy workflow validates backend tests, frontend builds/tests, and `docker compose --env-file infra/production.env.example -f docker-compose.prod.yml config`. It then copies the repository and built Angular artifacts to the Droplet, runs `docker compose --env-file .env -f docker-compose.prod.yml up -d --build`, and smoke-checks Admin, Booth UI, and API HTTPS endpoints.

### Backups

Enable DigitalOcean Droplet backups before any real booth pilot.

Install nightly PostgreSQL dumps:

```bash
sudo APP_DIR=/opt/photobiz bash /opt/photobiz/infra/scripts/install-backup-cron.sh
```

Backups are written under:

```text
/opt/photobiz/backups/postgres
```

Keep at least 7 daily dumps. Before real payments or client-facing launch, copy backups off-server and perform one restore rehearsal.

### PayMongo Pilot Mode

Use PayMongo test mode only for this no-domain pilot.

Register the tenant webhook in PayMongo Dashboard using the generated PhotoBIZ webhook URL, which should resolve to:

```text
https://api.<droplet-public-ip>.sslip.io/api/payments/paymongo/webhooks/{resourceId}
```

Do not enable live PayMongo payments until HTTPS smoke checks, webhook callbacks, booth Agent pairing, and real booth hardware validation have all passed.

## Step By Step DigitalOcean Pilot Deployment

Use this runbook for the first internal pilot deployment on a fresh DigitalOcean Droplet. The commands assume Windows PowerShell on your local machine and Ubuntu on the Droplet.

Replace these placeholders as you go:

```text
<droplet-ip>        The public IPv4 shown in DigitalOcean after the Droplet is created
<pilot-host>        <droplet-ip>.sslip.io
<repo-root>         C:\Quinjan\Repos\PhotoBIZ
```

### 1. Create An SSH Key For The Pilot

From Windows PowerShell:

```powershell
ssh-keygen -t ed25519 -C "photobiz-pilot" -f "$env:USERPROFILE\.ssh\photobiz_pilot_ed25519"
Get-Content "$env:USERPROFILE\.ssh\photobiz_pilot_ed25519.pub"
```

Copy the printed public key. You will paste it into DigitalOcean when creating the Droplet.

Keep the private key file private:

```text
C:\Users\<you>\.ssh\photobiz_pilot_ed25519
```

This private key is also the value that will later go into the GitHub secret `PHOTOBIZ_DEPLOY_SSH_KEY`.

For GitHub Actions, use a dedicated deploy key with no passphrase. When `ssh-keygen` asks:

```text
Enter passphrase (empty for no passphrase):
Enter same passphrase again:
```

Press `Enter` for both prompts. This should be a deploy-only key, not your personal everyday SSH key. Your personal key can and should keep its passphrase.

If you already created the Droplet with a passphrase-protected personal key, create a second deploy key now:

```powershell
ssh-keygen -t ed25519 -C "photobiz-github-actions-deploy" -f "$env:USERPROFILE\.ssh\photobiz_github_actions_ed25519"
Get-Content "$env:USERPROFILE\.ssh\photobiz_github_actions_ed25519.pub"
```

Press `Enter` twice for no passphrase. Then add the new public key to the Droplet:

```powershell
$deployPublicKey = Get-Content "$env:USERPROFILE\.ssh\photobiz_github_actions_ed25519.pub" -Raw
ssh -i "$env:USERPROFILE\.ssh\photobiz_pilot_ed25519" root@<droplet-ip> "mkdir -p /home/photobiz/.ssh && echo '$deployPublicKey' >> /home/photobiz/.ssh/authorized_keys && chown -R photobiz:photobiz /home/photobiz/.ssh && chmod 700 /home/photobiz/.ssh && chmod 600 /home/photobiz/.ssh/authorized_keys"
```

Use the new private key for `PHOTOBIZ_DEPLOY_SSH_KEY`:

```powershell
Get-Content "$env:USERPROFILE\.ssh\photobiz_github_actions_ed25519" -Raw
```

### 2. Create The DigitalOcean Droplet

In DigitalOcean:

1. Go to `Droplets`.
2. Click `Create Droplet`.
3. Choose region `Singapore`.
4. Choose image `Ubuntu LTS`.
5. Choose the Basic plan. Use at least `2 GB RAM / 1 vCPU / 50 GB SSD` for the pilot.
6. Under authentication, choose `SSH Key`.
7. Add/pick the public key from step 1.
8. Enable DigitalOcean Droplet backups if available for the plan.
9. Name it something clear, for example `photobiz-pilot-sgp1`.
10. Create the Droplet.

After it finishes, copy the public IPv4 address. In the rest of this guide, that is `<droplet-ip>`.

Quick connection check:

```powershell
ssh -i "$env:USERPROFILE\.ssh\photobiz_pilot_ed25519" root@<droplet-ip>
```

If the SSH prompt opens, exit back to PowerShell:

```bash
exit
```

### 3. Provision The Droplet

Copy the provisioning script and public key to the Droplet:

```powershell
Set-Location C:\Quinjan\Repos\PhotoBIZ
scp -i "$env:USERPROFILE\.ssh\photobiz_pilot_ed25519" infra/scripts/provision-ubuntu.sh root@<droplet-ip>:/tmp/provision-ubuntu.sh
scp -i "$env:USERPROFILE\.ssh\photobiz_pilot_ed25519" "$env:USERPROFILE\.ssh\photobiz_pilot_ed25519.pub" root@<droplet-ip>:/tmp/photobiz_pilot_ed25519.pub
```

Run provisioning as root:

```powershell
ssh -i "$env:USERPROFILE\.ssh\photobiz_pilot_ed25519" root@<droplet-ip> 'SSH_PUBLIC_KEY="$(cat /tmp/photobiz_pilot_ed25519.pub)" DEPLOY_USER=photobiz bash /tmp/provision-ubuntu.sh'
```

This installs Docker, Docker Compose, firewall rules, fail2ban, creates the `photobiz` deploy user, and creates `/opt/photobiz`.

Verify the deploy user works:

```powershell
ssh -i "$env:USERPROFILE\.ssh\photobiz_pilot_ed25519" photobiz@<droplet-ip> "docker --version && docker compose version"
```

### 4. Create The Pilot Environment File

Generate a long PostgreSQL password locally:

```powershell
-join ((48..57) + (65..90) + (97..122) | Get-Random -Count 40 | ForEach-Object {[char]$_})
```

SSH into the Droplet as the deploy user:

```powershell
ssh -i "$env:USERPROFILE\.ssh\photobiz_pilot_ed25519" photobiz@<droplet-ip>
```

Create `/opt/photobiz/.env`:

```bash
cat >/opt/photobiz/.env <<'EOF'
PHOTOBIZ_PILOT_HOST=<droplet-ip>.sslip.io
POSTGRES_DB=photobiz
POSTGRES_USER=photobiz
POSTGRES_PASSWORD=<paste-long-random-password-here>
CADDY_ACME_EMAIL=owner@your-email.example

# Temporary first-login Application Owner for the internal pilot.
# Remove these two lines after the first owner account exists.
BootstrapAdmin__Email=owner@your-email.example
BootstrapAdmin__Password=<strong temporary password>
EOF

chmod 600 /opt/photobiz/.env
exit
```

Those `BootstrapAdmin__...` settings are environment variables read by the API container on startup. They create the first Application Owner only when no Application Owner exists yet. Set them only in `/opt/photobiz/.env` on the Droplet. `BootstrapAdmin__Name` is optional and omitted to avoid shell quoting issues; the API defaults it to `PhotoBIZ Owner`.

The production Compose file explicitly passes those bootstrap values into the API container. If `grep '^BootstrapAdmin__' /opt/photobiz/.env` shows values but the database has no `APPLICATION_OWNER` row, redeploy or restart the API with the current production Compose file.

Example with a fake IP and fake email:

```text
PHOTOBIZ_PILOT_HOST=203.0.113.10.sslip.io
POSTGRES_DB=photobiz
POSTGRES_USER=photobiz
POSTGRES_PASSWORD=replace-this-with-a-long-random-password
CADDY_ACME_EMAIL=owner@example.com
BootstrapAdmin__Email=owner@example.com
BootstrapAdmin__Password=ReplaceThisWithAStrongTemporaryPassword123!
```

Important: `PHOTOBIZ_PILOT_HOST` must not include `admin.`, `booth.`, `api.`, `https://`, or a trailing slash. It should look like:

```text
203.0.113.10.sslip.io
```

### 5. Configure GitHub Deployment Secrets

In GitHub:

1. Open the PhotoBIZ repository.
2. Go to `Settings` > `Environments`.
3. Create or open the environment named `production`.
4. Enable required reviewers/manual approval if this repository supports it.
5. Add these environment secrets:

```text
PHOTOBIZ_DEPLOY_HOST=<droplet-ip>
PHOTOBIZ_DEPLOY_USER=photobiz
PHOTOBIZ_DEPLOY_SSH_KEY=<contents of C:\Users\<you>\.ssh\photobiz_pilot_ed25519>
```

To print the private key for copying:

```powershell
Get-Content "$env:USERPROFILE\.ssh\photobiz_pilot_ed25519" -Raw
```

The `PHOTOBIZ_DEPLOY_SSH_KEY` secret must contain the whole private key file, including the first and last lines. Use your real generated key, not this fake example:

```text
-----BEGIN OPENSSH PRIVATE KEY-----
b3BlbnNzaC1rZXktdjEAAAAABGZha2UAAAAIZmFrZS1vbmx5LWZvci1kb2NzAAAA
ZmFrZS1leGFtcGxlLWRvLW5vdC11c2UtdGhpcy1rZXktZm9yLXJlYWwtZGVwbG95
bWVudHMAAAAgZmFrZS1waG90b2Jpei1waWxvdC1kZXBsb3kta2V5LWV4YW1wbGU=
-----END OPENSSH PRIVATE KEY-----
```

For this workflow, `PHOTOBIZ_DEPLOY_SSH_KEY` must be a deploy-only private key without a passphrase. If your existing key has a passphrase, do not put that personal key into GitHub. Create the separate deploy key described in step 1 and use that instead.

Add this optional environment variable if the path differs from the default:

```text
PHOTOBIZ_DEPLOY_PATH=/opt/photobiz
```

### 6. Run The First GitHub Deploy

In GitHub:

1. Go to `Actions`.
2. Open `Deploy Pilot`.
3. Click `Run workflow`.
4. Select the branch to deploy.
5. Approve the `production` environment if prompted.

The workflow will:

1. Run backend tests.
2. Install/build/test Angular apps.
3. Validate the production Compose file.
4. Build the production API and worker Docker images.
5. Sync the repository and built Angular artifacts to `/opt/photobiz`.
6. Run `docker compose --env-file .env -f docker-compose.prod.yml up -d --build`.
7. Smoke-check the HTTPS endpoints.

If the first TLS attempt fails, wait 1-2 minutes and rerun the smoke step or the workflow. Caddy sometimes needs a short moment for the first certificate issue.

### 7. Verify The Pilot URLs

From your browser, open:

```text
https://admin.<droplet-ip>.sslip.io
https://booth.<droplet-ip>.sslip.io
https://api.<droplet-ip>.sslip.io/health
https://api.<droplet-ip>.sslip.io/api/platform/status
```

Expected API status:

```json
{
  "service": "PhotoBIZ.Api",
  "status": "ok",
  "runtime": "net10.0"
}
```

On the Droplet, check container status:

```powershell
ssh -i "$env:USERPROFILE\.ssh\photobiz_pilot_ed25519" photobiz@<droplet-ip> "cd /opt/photobiz && docker compose --env-file .env -f docker-compose.prod.yml ps"
```

View logs if something is down:

```powershell
ssh -i "$env:USERPROFILE\.ssh\photobiz_pilot_ed25519" photobiz@<droplet-ip> "cd /opt/photobiz && docker compose --env-file .env -f docker-compose.prod.yml logs --tail=100 api reverse-proxy"
```

### 8. Install Nightly Database Backups

After the first deploy succeeds, install the backup cron:

```powershell
ssh -i "$env:USERPROFILE\.ssh\photobiz_pilot_ed25519" root@<droplet-ip> "APP_DIR=/opt/photobiz bash /opt/photobiz/infra/scripts/install-backup-cron.sh"
```

Run one manual backup:

```powershell
ssh -i "$env:USERPROFILE\.ssh\photobiz_pilot_ed25519" photobiz@<droplet-ip> "APP_DIR=/opt/photobiz bash /opt/photobiz/infra/scripts/backup-postgres.sh"
```

Confirm a `.dump.gz` file exists:

```powershell
ssh -i "$env:USERPROFILE\.ssh\photobiz_pilot_ed25519" photobiz@<droplet-ip> "ls -lh /opt/photobiz/backups/postgres"
```

Before real-money use, copy backups off the Droplet and do a restore rehearsal.

### 9. First Application Setup

Open:

```text
https://admin.<droplet-ip>.sslip.io
```

Sign in with the temporary bootstrap Application Owner from `/opt/photobiz/.env`.

After confirming the first owner account exists, remove the `BootstrapAdmin__...` lines from `/opt/photobiz/.env` and restart the API:

```powershell
ssh -i "$env:USERPROFILE\.ssh\photobiz_pilot_ed25519" photobiz@<droplet-ip> "cd /opt/photobiz && sed -i '/^BootstrapAdmin__/d' .env && docker compose --env-file .env -f docker-compose.prod.yml up -d api"
```

Then verify:

1. Application Owner login works.
2. Client onboarding works.
3. Client Owner forced password change works.
4. Location, booth, package, payment assignment, and cashier setup work.
5. Booth UI opens with a kiosk token.

### 10. PayMongo Test Mode

Keep PayMongo in test mode for this pilot.

In PhotoBIZ Admin:

1. Open the tenant payment settings.
2. Enter PayMongo test keys.
3. Save step 1 to generate the webhook URL.
4. Copy the generated webhook URL.

In PayMongo Dashboard test mode:

1. Go to `Developers` > `Webhooks`.
2. Create a webhook with the copied URL.
3. Subscribe to:
   - `payment.paid`
   - `payment.failed`
   - `qrph.expired`
4. Copy the webhook signing secret back into PhotoBIZ.
5. Verify the setup from PhotoBIZ.

The webhook URL should look like:

```text
https://api.<droplet-ip>.sslip.io/api/payments/paymongo/webhooks/{resourceId}
```

### 11. Agent Pilot Settings

On the booth laptop, configure the Agent Control Center with:

```text
API base URL:      https://api.<droplet-ip>.sslip.io
Booth UI base URL: https://booth.<droplet-ip>.sslip.io
```

Use simulator mode first. Only move to LumaBooth API mode after the full cash/session flow works against the pilot API.

### 12. Common Troubleshooting

If HTTPS does not work:

- Confirm ports 80 and 443 are open in DigitalOcean Cloud Firewall, if one is attached.
- Confirm Ubuntu firewall allows 80 and 443:

```bash
sudo ufw status
```

- Confirm the hostname resolves:

```powershell
nslookup api.<droplet-ip>.sslip.io
```

If Caddy logs show `too many certificates ... already issued for "sslip.io"` from Let's Encrypt, the Droplet and DNS are probably fine. The shared `sslip.io` domain has hit a Let's Encrypt global rate limit. Make sure the deployed `infra/caddy/Caddyfile.prod` contains the ZeroSSL ACME setting and that `/opt/photobiz/.env` has a real `CADDY_ACME_EMAIL` value:

```bash
cd /opt/photobiz
grep -E 'CADDY_ACME_EMAIL|acme_ca' .env infra/caddy/Caddyfile.prod
docker compose --env-file .env -f docker-compose.prod.yml up -d reverse-proxy
docker compose --env-file .env -f docker-compose.prod.yml logs --tail=120 reverse-proxy
```

If GitHub deploy cannot SSH:

- Confirm `PHOTOBIZ_DEPLOY_HOST` is only the IP address.
- Confirm `PHOTOBIZ_DEPLOY_USER` is `photobiz`.
- Confirm `PHOTOBIZ_DEPLOY_SSH_KEY` contains the full private key, including the `BEGIN OPENSSH PRIVATE KEY` and `END OPENSSH PRIVATE KEY` lines.
- Confirm the deploy user can SSH manually from your machine.

The exact GitHub error usually looks like:

```text
Permission denied (publickey).
rsync: connection unexpectedly closed
```

Fix it with these checks.

First confirm the `photobiz` Linux user exists:

```powershell
ssh -i "$env:USERPROFILE\.ssh\photobiz_pilot_ed25519" root@<droplet-ip> "id photobiz"
```

If that says the user does not exist, the provisioning script did not finish. Run the provisioning step again.

Next test the same deploy key that GitHub Actions uses:

```powershell
ssh -i "$env:USERPROFILE\.ssh\photobiz_github_actions_ed25519" photobiz@<droplet-ip> "whoami"
```

Expected output:

```text
photobiz
```

If this fails, add the deploy public key to the `photobiz` user's authorized keys. This command uses your root-capable key to install the GitHub deploy key:

```powershell
scp -i "$env:USERPROFILE\.ssh\photobiz_pilot_ed25519" "$env:USERPROFILE\.ssh\photobiz_github_actions_ed25519.pub" root@<droplet-ip>:/tmp/photobiz_github_actions_ed25519.pub
ssh -i "$env:USERPROFILE\.ssh\photobiz_pilot_ed25519" root@<droplet-ip> "mkdir -p /home/photobiz/.ssh && cat /tmp/photobiz_github_actions_ed25519.pub >> /home/photobiz/.ssh/authorized_keys && chown -R photobiz:photobiz /home/photobiz/.ssh && chmod 700 /home/photobiz/.ssh && chmod 600 /home/photobiz/.ssh/authorized_keys"
```

Retest:

```powershell
ssh -i "$env:USERPROFILE\.ssh\photobiz_github_actions_ed25519" photobiz@<droplet-ip> "whoami"
```

Only rerun GitHub Actions after the local deploy-key test succeeds.

If rsync can SSH but fails with permission denied deleting deployed frontend folders:

```text
rsync: [generator] delete_file: rmdir(deploy/booth-ui) failed: Permission denied (13)
rsync: [generator] delete_file: rmdir(deploy/admin-web) failed: Permission denied (13)
```

The deploy folders are owned by `root` or another user from an earlier manual command. Fix ownership once:

```powershell
ssh -i "$env:USERPROFILE\.ssh\photobiz_pilot_ed25519" root@<droplet-ip> "chown -R photobiz:photobiz /opt/photobiz"
```

Then rerun `Deploy Pilot`.

If containers fail:

```bash
cd /opt/photobiz
docker compose --env-file .env -f docker-compose.prod.yml ps
docker compose --env-file .env -f docker-compose.prod.yml logs --tail=200
```

If PayMongo webhook URLs are wrong:

- Confirm the public Admin Web is being accessed through `https://admin.<droplet-ip>.sslip.io`.
- Confirm API is behind `https://api.<droplet-ip>.sslip.io`.
- Confirm `ForwardedHeaders__Enabled=true` is present in the API container environment through `docker compose --env-file .env -f docker-compose.prod.yml config`.
