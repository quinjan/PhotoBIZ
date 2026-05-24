param(
    [string] $Version = "0.0.0-local",
    [string] $Configuration = "Release",
    [string] $RuntimeIdentifier = "win-x64",
    [string] $OutputRoot = "",
    [string] $CodeSigningCertificatePath = "",
    [string] $CodeSigningCertificatePassword = "",
    [switch] $SkipTests
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$agentRoot = Resolve-Path (Join-Path $scriptRoot "..")
$repoRoot = Resolve-Path (Join-Path $agentRoot "..\..")
$projectPath = Join-Path $agentRoot "src\PhotoBIZ.WindowsAgent.ControlCenter\PhotoBIZ.WindowsAgent.ControlCenter.csproj"
$testProjectPath = Join-Path $agentRoot "tests\PhotoBIZ.WindowsAgent.Tests\PhotoBIZ.WindowsAgent.Tests.csproj"
$installerDirectory = Join-Path $agentRoot "installer"

if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repoRoot "artifacts\windows-agent"
}

$resolvedOutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
$artifactName = "PhotoBIZ-WindowsAgent-ControlCenter-$Version-$RuntimeIdentifier"
$publishDir = Join-Path $resolvedOutputRoot "publish\$artifactName"
$packageDir = Join-Path $resolvedOutputRoot "packages"
$zipPath = Join-Path $packageDir "$artifactName.zip"
$manifestPath = Join-Path $packageDir "$artifactName.manifest.txt"
$exePath = Join-Path $publishDir "PhotoBIZ.WindowsAgent.ControlCenter.exe"
$numericVersion = if ($Version -match '^(\d+)\.(\d+)\.(\d+)') {
    "$($Matches[1]).$($Matches[2]).$($Matches[3]).0"
} else {
    "0.0.0.0"
}
$signatureStatus = "NotSigned"

function Invoke-CheckedNative {
    param(
        [string] $FilePath,
        [string[]] $Arguments
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$FilePath failed with exit code $LASTEXITCODE"
    }
}

Write-Host "Publishing PhotoBIZ Windows Agent Control Center"
Write-Host "Version: $Version"
Write-Host "Runtime: $RuntimeIdentifier"
Write-Host "Output: $resolvedOutputRoot"

if (-not $SkipTests) {
    Invoke-CheckedNative "dotnet" @(
        "test",
        $testProjectPath,
        "--configuration",
        $Configuration,
        "--no-restore"
    )
}

if (Test-Path $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $publishDir, $packageDir | Out-Null

Invoke-CheckedNative "dotnet" @(
    "publish",
    $projectPath,
    "--configuration",
    $Configuration,
    "--runtime",
    $RuntimeIdentifier,
    "--self-contained",
    "true",
    "--output",
    $publishDir,
    "-p:Version=$Version",
    "-p:AssemblyVersion=$numericVersion",
    "-p:FileVersion=$numericVersion",
    "-p:InformationalVersion=$Version",
    "-p:PublishSingleFile=true",
    "-p:EnableCompressionInSingleFile=true",
    "-p:IncludeNativeLibrariesForSelfExtract=true",
    "-p:DebugType=none",
    "-p:DebugSymbols=false"
)

$developmentConfig = Join-Path $publishDir "appsettings.Development.json"
if (Test-Path $developmentConfig) {
    throw "Development configuration was included in the publish output: $developmentConfig"
}

$devHostRuntimeConfig = Join-Path $publishDir "PhotoBIZ.WindowsAgent.runtimeconfig.json"
if (Test-Path $devHostRuntimeConfig) {
    Remove-Item -LiteralPath $devHostRuntimeConfig -Force
}

$devHostExe = Join-Path $publishDir "PhotoBIZ.WindowsAgent.exe"
if (Test-Path $devHostExe) {
    Remove-Item -LiteralPath $devHostExe -Force
}

Copy-Item -LiteralPath (Join-Path $installerDirectory "Install-PhotoBIZAgent.ps1") -Destination $publishDir -Force
Copy-Item -LiteralPath (Join-Path $installerDirectory "Uninstall-PhotoBIZAgent.ps1") -Destination $publishDir -Force

if (-not [string]::IsNullOrWhiteSpace($CodeSigningCertificatePath)) {
    if (-not (Test-Path $CodeSigningCertificatePath)) {
        throw "Code signing certificate was not found: $CodeSigningCertificatePath"
    }

    if (-not (Test-Path $exePath)) {
        throw "Published executable was not found: $exePath"
    }

    $securePassword = ConvertTo-SecureString $CodeSigningCertificatePassword -AsPlainText -Force
    $certificate = Import-PfxCertificate `
        -FilePath $CodeSigningCertificatePath `
        -CertStoreLocation Cert:\CurrentUser\My `
        -Password $securePassword `
        -Exportable

    try {
        $signature = Set-AuthenticodeSignature `
            -FilePath $exePath `
            -Certificate $certificate `
            -TimestampServer "http://timestamp.digicert.com" `
            -HashAlgorithm SHA256

        if ($signature.Status -ne "Valid") {
            throw "Code signing failed with status $($signature.Status): $($signature.StatusMessage)"
        }

        $signatureStatus = "Signed:$($certificate.Thumbprint)"
    }
    finally {
        Remove-Item -LiteralPath "Cert:\CurrentUser\My\$($certificate.Thumbprint)" -ErrorAction SilentlyContinue
    }
}

if (Test-Path $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -CompressionLevel Optimal

$sha256 = (Get-FileHash -Algorithm SHA256 -Path $zipPath).Hash
$exePath = Join-Path $publishDir "PhotoBIZ.WindowsAgent.ControlCenter.exe"
$manifest = @(
    "PhotoBIZ Windows Agent Control Center",
    "Version: $Version",
    "Runtime: $RuntimeIdentifier",
    "Configuration: $Configuration",
    "Artifact: $(Split-Path -Leaf $zipPath)",
    "SHA256: $sha256",
    "EntryPoint: PhotoBIZ.WindowsAgent.ControlCenter.exe",
    "Installer: Install-PhotoBIZAgent.ps1",
    "Uninstaller: Uninstall-PhotoBIZAgent.ps1",
    "SelfContained: true",
    "SingleFile: true",
    "SignatureStatus: $signatureStatus",
    "DevelopmentConfigIncluded: false",
    "GeneratedUtc: $((Get-Date).ToUniversalTime().ToString("O"))"
)

if (Test-Path $exePath) {
    $manifest += "ExeSHA256: $((Get-FileHash -Algorithm SHA256 -Path $exePath).Hash)"
}

$manifest | Set-Content -Path $manifestPath -Encoding utf8

Write-Host "Published: $publishDir"
Write-Host "Package: $zipPath"
Write-Host "Manifest: $manifestPath"
