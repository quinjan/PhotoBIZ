param(
    [string] $InstallDirectory = "$env:ProgramFiles\PhotoBIZ\Windows Agent",
    [string] $DataDirectory = "$env:ProgramData\PhotoBIZ\Agent",
    [switch] $NoAutoStart
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$sourceDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$exeName = "PhotoBIZ.WindowsAgent.ControlCenter.exe"
$sourceExe = Join-Path $sourceDirectory $exeName
$targetExe = Join-Path $InstallDirectory $exeName
$runKeyPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
$runValueName = "PhotoBIZ Agent Control Center"

if (-not (Test-Path $sourceExe)) {
    throw "Expected $exeName next to this install script. Run the script from the extracted PhotoBIZ Agent release ZIP."
}

New-Item -ItemType Directory -Force -Path $InstallDirectory, $DataDirectory | Out-Null

Get-ChildItem -LiteralPath $sourceDirectory -File | Where-Object {
    $_.Name -in @(
        "PhotoBIZ.WindowsAgent.ControlCenter.exe",
        "appsettings.json",
        "Uninstall-PhotoBIZAgent.ps1"
    )
} | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $InstallDirectory $_.Name) -Force
}

if (-not $NoAutoStart) {
    New-Item -Path $runKeyPath -Force | Out-Null
    New-ItemProperty `
        -Path $runKeyPath `
        -Name $runValueName `
        -Value "`"$targetExe`"" `
        -PropertyType String `
        -Force | Out-Null
}

$startMenuDirectory = Join-Path $env:ProgramData "Microsoft\Windows\Start Menu\Programs\PhotoBIZ"
New-Item -ItemType Directory -Force -Path $startMenuDirectory | Out-Null
$shortcutPath = Join-Path $startMenuDirectory "PhotoBIZ Agent Control Center.lnk"
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $targetExe
$shortcut.WorkingDirectory = $InstallDirectory
$shortcut.Description = "PhotoBIZ Agent Control Center"
$shortcut.Save()

Write-Host "PhotoBIZ Agent installed."
Write-Host "Install directory: $InstallDirectory"
Write-Host "Data directory: $DataDirectory"
if ($NoAutoStart) {
    Write-Host "Login auto-start: disabled by installer argument"
} else {
    Write-Host "Login auto-start: enabled for the current Windows user"
}
Write-Host "Run: $targetExe"
