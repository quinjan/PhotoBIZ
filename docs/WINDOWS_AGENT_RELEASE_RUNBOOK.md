# Windows Agent GitHub Release And Install Runbook

Use this runbook to publish the PhotoBIZ Windows Agent Control Center as a GitHub Release artifact and install it on a booth laptop.

## What The Release Contains

The Windows Agent release ZIP contains:

```text
PhotoBIZ.WindowsAgent.ControlCenter.exe
appsettings.json
Install-PhotoBIZAgent.ps1
Uninstall-PhotoBIZAgent.ps1
```

The release ZIP intentionally excludes `appsettings.Development.json`, so it does not carry local development booth codes or Agent credentials.

## 1. Pick A Version

Use semantic versions for Agent releases:

```text
0.1.0
0.1.1
0.2.0-preview.1
```

Git tags for Agent releases must use this format:

```text
agent-v0.1.0
```

## 2. Optional: Configure Code Signing

Unsigned builds are acceptable only for internal lab testing. Before client/live booth installation, configure signing.

In GitHub repository secrets or the protected release environment, add:

```text
WINDOWS_AGENT_SIGNING_CERTIFICATE_BASE64=<base64-encoded .pfx>
WINDOWS_AGENT_SIGNING_CERTIFICATE_PASSWORD=<pfx password>
```

To convert a local `.pfx` to base64 from PowerShell:

```powershell
[Convert]::ToBase64String([IO.File]::ReadAllBytes("C:\path\to\certificate.pfx")) | Set-Clipboard
```

The release manifest will show `SignatureStatus: Signed:<thumbprint>` when signing succeeds.

## 3. Create The GitHub Release Artifact

### Option A: Manual GitHub Actions Run

Use this for normal internal releases.

1. Open the GitHub repository.
2. Go to `Actions`.
3. Select `Windows Agent Release`.
4. Click `Run workflow`.
5. Enter the version, for example:

```text
0.1.0
```

6. Run the workflow.
7. Wait for the workflow to complete.
8. Open `Releases`.
9. Confirm a draft prerelease exists named like:

```text
PhotoBIZ Windows Agent 0.1.0
```

10. Confirm these assets are attached:

```text
PhotoBIZ-WindowsAgent-ControlCenter-0.1.0-win-x64.zip
PhotoBIZ-WindowsAgent-ControlCenter-0.1.0-win-x64.manifest.txt
```

11. Review the manifest.
12. If this is ready for the intended audience, publish the draft release.

### Option B: Tag-Based Release

Use this when you want releases tied to Git tags.

From repo root:

```powershell
git tag agent-v0.1.0
git push origin agent-v0.1.0
```

Then follow steps 7-12 from Option A.

## 4. Local Release Dry Run

Before relying on GitHub Actions, you can produce the same ZIP locally:

```powershell
powershell -ExecutionPolicy Bypass -File agent/windows-agent/scripts/publish-control-center.ps1 -Version 0.1.0-test -SkipTests
```

Output:

```text
artifacts/windows-agent/packages/PhotoBIZ-WindowsAgent-ControlCenter-0.1.0-test-win-x64.zip
artifacts/windows-agent/packages/PhotoBIZ-WindowsAgent-ControlCenter-0.1.0-test-win-x64.manifest.txt
```

## 5. Download The Release On The Booth Laptop

1. Sign in to Windows using the booth operator Windows user.
2. Open the GitHub Release page.
3. Download:

```text
PhotoBIZ-WindowsAgent-ControlCenter-<version>-win-x64.zip
PhotoBIZ-WindowsAgent-ControlCenter-<version>-win-x64.manifest.txt
```

4. Create a temporary folder, for example:

```text
C:\Temp\PhotoBIZ-Agent
```

5. Extract the ZIP into that folder.

If Windows blocks the downloaded files, run this from PowerShell:

```powershell
Get-ChildItem "C:\Temp\PhotoBIZ-Agent" -Recurse | Unblock-File
```

## 6. Verify The Download

From PowerShell:

```powershell
Get-FileHash "C:\Temp\PhotoBIZ-Agent\PhotoBIZ.WindowsAgent.ControlCenter.exe" -Algorithm SHA256
```

Compare the result with `ExeSHA256` in the manifest.

For signed releases, verify the signature:

```powershell
Get-AuthenticodeSignature "C:\Temp\PhotoBIZ-Agent\PhotoBIZ.WindowsAgent.ControlCenter.exe"
```

Expected signed release status:

```text
Status: Valid
```

## 7. Install On The Booth Laptop

Open PowerShell as Administrator.

Run:

```powershell
Set-Location "C:\Temp\PhotoBIZ-Agent"
powershell -ExecutionPolicy Bypass -File .\Install-PhotoBIZAgent.ps1
```

Default install result:

```text
App files:   C:\Program Files\PhotoBIZ\Windows Agent
Local data:  C:\ProgramData\PhotoBIZ\Agent
Autostart:   HKCU Run entry for the current Windows user
Shortcut:    Start Menu > PhotoBIZ > PhotoBIZ Agent Control Center
```

The autostart entry is current-user based. Install while signed in as the Windows user who will operate the booth.

## 8. First Launch And Pairing

Start the app:

```powershell
& "C:\Program Files\PhotoBIZ\Windows Agent\PhotoBIZ.WindowsAgent.ControlCenter.exe"
```

Then in the Control Center:

1. Enable `Technician` mode.
2. Open `Pair / Re-pair`.
3. Enter the production API URL:

```text
https://api.<your-domain>
```

4. Enter the booth code from Admin Web.
5. Paste the Agent credential from Admin Web.
6. Click `Pair`.
7. Open `Kiosk / Display`.
8. Set Booth UI URL:

```text
https://booth.<your-domain>
```

9. Click `Detect` for Chrome, or enter the Chrome path manually.
10. Enable `Launch Booth UI`.
11. Enable `Kiosk Mode` for live booth use.
12. Save kiosk settings.
13. Open `LumaBooth`.
14. Keep `Simulator` for first API/backend validation, or switch to `Api` on the real booth laptop.
15. Save LumaBooth settings.
16. Click `Start Booth`.

Expected result:

- Chrome opens to the Booth UI token route.
- Agent status becomes online.
- Backend receives heartbeat.
- Booth UI no longer shows Agent offline.

## 9. Reboot Validation

After installation:

1. Reboot the booth laptop.
2. Sign in as the booth Windows user.
3. Confirm PhotoBIZ Agent Control Center opens automatically.
4. Confirm the booth remains offline until `Start Booth` is clicked.
5. Click `Start Booth`.
6. Confirm kiosk Chrome opens and heartbeat starts.

## 10. Updating To A New Release

1. Download the new release ZIP.
2. Extract it to a temporary folder.
3. Close PhotoBIZ Agent Control Center.
4. Run PowerShell as Administrator.
5. Run:

```powershell
Set-Location "C:\Temp\PhotoBIZ-Agent-New"
powershell -ExecutionPolicy Bypass -File .\Install-PhotoBIZAgent.ps1
```

The install script overwrites app files and preserves local pairing/config in:

```text
C:\ProgramData\PhotoBIZ\Agent
```

## 11. Uninstall

Run PowerShell as Administrator.

To remove the app and local pairing/config:

```powershell
powershell -ExecutionPolicy Bypass -File "C:\Program Files\PhotoBIZ\Windows Agent\Uninstall-PhotoBIZAgent.ps1"
```

To remove the app while preserving local pairing/config:

```powershell
powershell -ExecutionPolicy Bypass -File "C:\Program Files\PhotoBIZ\Windows Agent\Uninstall-PhotoBIZAgent.ps1" -PreserveData
```

## 12. Live Booth Final Checks

Before using with a client/live booth:

1. Release workflow succeeds in GitHub.
2. Release manifest shows the expected version and hashes.
3. Authenticode signature is valid.
4. Clean Windows install succeeds.
5. Reboot autostart works.
6. Pair/Re-pair works with Admin Web-issued credentials.
7. Start Booth launches kiosk Chrome.
8. Stop Booth closes only PhotoBIZ-launched Chrome.
9. LumaBooth API mode starts a real session.
10. LumaBooth `session_start` and `session_end` URL triggers reach the Agent.
11. Focus handoff works between Booth UI and LumaBooth.
12. Extra print command works with the real printer/LumaBooth setup.
