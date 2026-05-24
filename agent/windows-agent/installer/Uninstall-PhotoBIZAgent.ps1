param(
    [string] $InstallDirectory = "$env:ProgramFiles\PhotoBIZ\Windows Agent",
    [string] $DataDirectory = "$env:ProgramData\PhotoBIZ\Agent",
    [switch] $PreserveData
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$exeName = "PhotoBIZ.WindowsAgent.ControlCenter.exe"
$targetExe = Join-Path $InstallDirectory $exeName
$runKeyPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
$runValueName = "PhotoBIZ Agent Control Center"

Get-Process -Name "PhotoBIZ.WindowsAgent.ControlCenter" -ErrorAction SilentlyContinue | Where-Object {
    try {
        $_.Path -eq $targetExe
    } catch {
        $false
    }
} | Stop-Process -Force

if (Test-Path $runKeyPath) {
    Remove-ItemProperty -Path $runKeyPath -Name $runValueName -ErrorAction SilentlyContinue
}

$shortcutPath = Join-Path $env:ProgramData "Microsoft\Windows\Start Menu\Programs\PhotoBIZ\PhotoBIZ Agent Control Center.lnk"
if (Test-Path $shortcutPath) {
    Remove-Item -LiteralPath $shortcutPath -Force
}

if (Test-Path $InstallDirectory) {
    Remove-Item -LiteralPath $InstallDirectory -Recurse -Force
}

if (-not $PreserveData -and (Test-Path $DataDirectory)) {
    Remove-Item -LiteralPath $DataDirectory -Recurse -Force
}

Write-Host "PhotoBIZ Agent uninstalled."
if ($PreserveData) {
    Write-Host "Local pairing/config data was preserved at: $DataDirectory"
} else {
    Write-Host "Local pairing/config data was removed from: $DataDirectory"
}
