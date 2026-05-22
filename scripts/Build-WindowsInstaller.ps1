<#
.SYNOPSIS
    Publishes WorldClock and builds the Windows installer via Inno Setup.

.DESCRIPTION
    1. Runs `dotnet publish` for win-x64 (self-contained, single-file).
    2. Runs Inno Setup Compiler (ISCC.exe) on installer\WorldClock.iss.
    3. Outputs the ready-to-distribute WorldClockSetup.exe under installer\Output\.

.PARAMETER Configuration
    Build configuration.  Default: Release.

.PARAMETER Version
    Application version to embed in the installer.
    When omitted the version is read from the VERSION file at the repo root.

.PARAMETER IsccPath
    Full path to ISCC.exe.  Auto-detected from standard Inno Setup 6 locations
    when omitted.

.PARAMETER SkipPublish
    Skip the dotnet publish step (use an existing publish\win-x64 output).

.EXAMPLE
    # Full build
    .\scripts\Build-WindowsInstaller.ps1

.EXAMPLE
    # Skip publish, re-run Inno Setup only
    .\scripts\Build-WindowsInstaller.ps1 -SkipPublish

.EXAMPLE
    # Custom ISCC path
    .\scripts\Build-WindowsInstaller.ps1 -IsccPath "D:\Tools\InnoSetup6\ISCC.exe"
#>
[CmdletBinding()]
param(
    [string] $Configuration = 'Release',
    [string] $Version       = '',       # empty = read from VERSION file
    [string] $IsccPath      = '',
    [switch] $SkipPublish
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ── Paths ──────────────────────────────────────────────────────────────────────
$RepoRoot    = (Resolve-Path "$PSScriptRoot\..").Path
$ProjectFile = Join-Path $RepoRoot 'WorldClock\WorldClock.csproj'
$IssFile     = Join-Path $RepoRoot 'installer\WorldClock.iss'
$PublishDir  = Join-Path $RepoRoot 'publish\win-x64'
$OutputDir   = Join-Path $RepoRoot 'installer\Output'

# ── Resolve version (VERSION file or -Version parameter) ───────────────────
if ([string]::IsNullOrWhiteSpace($Version)) {
    $VersionFile = Join-Path $RepoRoot 'VERSION'
    if (-not (Test-Path $VersionFile)) {
        Write-Error "No -Version supplied and VERSION file not found: $VersionFile"
    }
    $Version = (Get-Content $VersionFile -Raw).Trim()
    if ([string]::IsNullOrWhiteSpace($Version)) {
        Write-Error "VERSION file is empty. Add a version string such as: 1.0.0"
    }
    Write-Host "    Version (from VERSION file): $Version" -ForegroundColor Gray
} else {
    Write-Host "    Version (from -Version parameter): $Version" -ForegroundColor Gray
}

# ── Locate dotnet ──────────────────────────────────────────────────────────────
$DotnetExe = if ($env:DOTNET_ROOT) {
    Join-Path $env:DOTNET_ROOT 'dotnet.exe'
} else {
    'dotnet'
}

# ── Locate ISCC ───────────────────────────────────────────────────────────────
if (-not $IsccPath) {
    $IsccCandidates = @(
        'C:\Program Files (x86)\Inno Setup 6\ISCC.exe',
        'C:\Program Files\Inno Setup 6\ISCC.exe',
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    )
    $IsccPath = $IsccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}

if (-not $IsccPath -or -not (Test-Path $IsccPath)) {
    Write-Error @'
Inno Setup 6 (ISCC.exe) was not found.
  Download from:  https://jrsoftware.org/isdl.php
  Then pass -IsccPath "C:\path\to\ISCC.exe"  or install to the default location.
'@
}

function Write-Step([string]$Message) {
    Write-Host "`n==> $Message" -ForegroundColor Cyan
}

# ── Step 1: Publish ────────────────────────────────────────────────────────────
if (-not $SkipPublish) {
    Write-Step "Publishing WorldClock (self-contained, win-x64, single-file) ..."

    $publishArgs = @(
        'publish', $ProjectFile
        '-c', $Configuration
        '-r', 'win-x64'
        '--self-contained', 'true'
        '-p:PublishSingleFile=true'
        '-p:IncludeNativeLibrariesForSelfExtract=true'
        "-p:Version=$Version"
        '-o', $PublishDir
        '--nologo'
    )

    & $DotnetExe @publishArgs
    if ($LASTEXITCODE -ne 0) { Write-Error "dotnet publish failed (exit $LASTEXITCODE)." }

    Write-Host "    Published to: $PublishDir" -ForegroundColor Gray
} else {
    Write-Host "`n==> Skipping publish (using existing output in $PublishDir)" -ForegroundColor Yellow
    if (-not (Test-Path (Join-Path $PublishDir 'WorldClock.exe'))) {
        Write-Error "WorldClock.exe not found in $PublishDir.  Run without -SkipPublish first."
    }
}

# ── Step 2: Run Inno Setup ────────────────────────────────────────────────────
Write-Step "Running Inno Setup Compiler ..."
Write-Host "    ISCC: $IsccPath" -ForegroundColor Gray
Write-Host "    .iss: $IssFile"  -ForegroundColor Gray

# Normalise the publish path — prevents Inno Setup from emitting \\?\  
# (extended-length path prefix) in its compression log.
$PublishDirNorm = [System.IO.Path]::GetFullPath($PublishDir)

& $IsccPath `
    "/DAppVersion=$Version" `
    "/DPublishDir=$PublishDirNorm" `
    $IssFile

if ($LASTEXITCODE -ne 0) { Write-Error "Inno Setup compilation failed (exit $LASTEXITCODE)." }

# ── Done ──────────────────────────────────────────────────────────────────────
$InstallerFile = Join-Path $OutputDir 'WorldClockSetup.exe'
Write-Host ''
if (Test-Path $InstallerFile) {
    $size = (Get-Item $InstallerFile).Length / 1MB
    Write-Host ("[OK] Installer ready: $InstallerFile  ({0:N1} MB)" -f $size) -ForegroundColor Green
} else {
    Write-Host '[OK] Inno Setup finished. Check installer\Output\ for the .exe.' -ForegroundColor Green
}
