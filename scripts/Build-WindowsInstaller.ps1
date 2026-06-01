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

.PARAMETER Upload
    Upload the finished installer to a GitHub Release as a versioned asset.
    Requires the GITHUB_TOKEN environment variable to be set.

.PARAMETER GitHubRepo
    GitHub repository as OWNER/REPO (e.g. myorg/worldclock).
    Can also be provided via the GITHUB_REPO environment variable.

.PARAMETER GitHubTag
    Release tag to upload to (e.g. v1.2.3).
    Defaults to v<Version> when omitted.

.PARAMETER GenerateIcons
    Regenerate Logo.ico and hicolor PNGs by calling generate-icons.sh via WSL
    before the build. Requires WSL with ImageMagick installed.

.EXAMPLE
    # Build and upload to GitHub Releases
    $env:GITHUB_TOKEN = 'ghp_...'
    .\scripts\Build-WindowsInstaller.ps1 -Upload -GitHubRepo myorg/worldclock

.EXAMPLE
    # Re-upload only (skip publish + ISCC), targeting an explicit tag
    $env:GITHUB_TOKEN = 'ghp_...'
    .\scripts\Build-WindowsInstaller.ps1 -SkipPublish -Upload -GitHubRepo myorg/worldclock -GitHubTag v1.1.0
#>
[CmdletBinding()]
param(
    [string] $Configuration = 'Release',
    [string] $Version       = '',       # empty = read from VERSION file
    [string] $IsccPath      = '',
    [switch] $SkipPublish,
    [switch] $Upload,                   # push installer to a GitHub Release
    [string] $GitHubRepo    = '',       # OWNER/REPO  (or $env:GITHUB_REPO)
    [string] $GitHubTag     = '',       # defaults to v$Version
    [switch] $GenerateIcons             # call generate-icons.sh via WSL first
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

# ── Step 0: Regenerate icons via generate-icons.sh (optional) ─────────────────────
if ($GenerateIcons) {
    Write-Step 'Regenerating icons via generate-icons.sh (WSL) ...'
    $IconScript  = Join-Path $RepoRoot 'scripts\generate-icons.sh'
    $WslScriptPath = & wsl wslpath -u $IconScript 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($WslScriptPath)) {
        Write-Error 'WSL is required for -GenerateIcons. Enable WSL and install ImageMagick inside it.'
    }
    & wsl bash $WslScriptPath.Trim()
    if ($LASTEXITCODE -ne 0) { Write-Error "generate-icons.sh failed (exit $LASTEXITCODE)." }
}

# Verify Logo.ico exists (required by installer\WorldClock.iss SetupIconFile)
$IcoPath = Join-Path $RepoRoot 'WorldClock\Images\Logo.ico'
if (-not (Test-Path $IcoPath)) {
    Write-Error "Logo.ico not found at $IcoPath.`n  Run:  bash scripts/generate-icons.sh"
}

# ── Step 1: Publish ───────────────────────────────────────────────────────────
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

# ── Step 3: Upload to GitHub Releases ─────────────────────────────────────────────
if ($Upload) {
    Write-Step 'Uploading installer to GitHub Releases ...'

    # ── Validate prerequisites ───────────────────────────────────────────────
    $Token = $env:GITHUB_TOKEN
    if ([string]::IsNullOrWhiteSpace($Token)) {
        Write-Error "GITHUB_TOKEN environment variable is not set.`n  Set it with: `$env:GITHUB_TOKEN = 'ghp_...'"
    }
    if ([string]::IsNullOrWhiteSpace($GitHubRepo)) {
        $GitHubRepo = $env:GITHUB_REPO
        if ([string]::IsNullOrWhiteSpace($GitHubRepo)) {
            Write-Error 'Specify -GitHubRepo OWNER/REPO or set the GITHUB_REPO environment variable.'
        }
    }
    if ([string]::IsNullOrWhiteSpace($GitHubTag)) { $GitHubTag = "v$Version" }

    $InstallerPath = Join-Path $OutputDir 'WorldClockSetup.exe'
    if (-not (Test-Path $InstallerPath)) {
        Write-Error "Installer not found at $InstallerPath — ensure the build succeeded before uploading."
    }

    $ApiBase   = "https://api.github.com/repos/$GitHubRepo"
    $AssetName = "WorldClockSetup-$Version.exe"
    $Headers   = @{
        Authorization          = "Bearer $Token"
        Accept                 = 'application/vnd.github+json'
        'X-GitHub-Api-Version' = '2022-11-28'
    }

    Write-Host "    Tag:  $GitHubTag" -ForegroundColor Gray
    Write-Host "    Repo: $GitHubRepo" -ForegroundColor Gray
    Write-Host "    File: $InstallerPath" -ForegroundColor Gray

    # ── Look up (or create) the release ───────────────────────────────────
    Write-Host "    Looking up release '$GitHubTag' ..." -ForegroundColor Gray
    $Release = $null
    try {
        $Release = Invoke-RestMethod -Uri "$ApiBase/releases/tags/$GitHubTag" `
                       -Headers $Headers -Method Get -ErrorAction Stop
    } catch {
        # 404 = release does not exist yet; any other error is fatal
        if ($_.Exception.Message -notmatch '404|Not Found') { throw }
    }

    if ($null -eq $Release) {
        Write-Host "    Release '$GitHubTag' not found — creating it ..." -ForegroundColor Gray
        $Body    = [ordered]@{
            tag_name   = $GitHubTag
            name       = "WorldClock $Version"
            body       = "WorldClock $Version"
            draft      = $false
            prerelease = $false
        } | ConvertTo-Json
        $Release = Invoke-RestMethod -Uri "$ApiBase/releases" `
                       -Headers $Headers -Method Post `
                       -ContentType 'application/json' -Body $Body
    }

    Write-Host "    Release ID: $($Release.id)" -ForegroundColor Gray

    # ── Delete existing asset with the same name (makes re-runs idempotent) ───
    $Existing = $Release.assets | Where-Object { $_.name -eq $AssetName } | Select-Object -First 1
    if ($Existing) {
        Write-Host "    Removing existing asset '$AssetName' (id=$($Existing.id)) ..." -ForegroundColor Gray
        try {
            Invoke-RestMethod -Uri "$ApiBase/releases/assets/$($Existing.id)" `
                -Headers $Headers -Method Delete -ErrorAction Stop | Out-Null
        } catch {
            # GitHub may return 404 here if the asset was already removed or token visibility is limited.
            # Do not hard-fail yet; upload logic below will retry conflict handling if needed.
            if ($_.Exception.Message -match '404|Not Found') {
                Write-Warning "Asset delete returned 404 for id=$($Existing.id). Continuing with upload attempt."
            } else {
                throw
            }
        }
    }

    # ── Upload the installer ─────────────────────────────────────────────────
    $UploadUri = if ($Release.upload_url) {
        ($Release.upload_url -replace '\{\?name,label\}$', '')
    } else {
        "https://uploads.github.com/repos/$GitHubRepo/releases/$($Release.id)/assets"
    }
    $EncodedAssetName = [System.Uri]::EscapeDataString($AssetName)
    $UploadUriWithQuery = "${UploadUri}?name=$EncodedAssetName&label=$EncodedAssetName"
    $FileSizeMB = (Get-Item $InstallerPath).Length / 1MB
    Write-Host ("    Uploading $AssetName ({0:N1} MB) ..." -f $FileSizeMB) -ForegroundColor Gray

    $Asset = $null
    try {
        $Asset = Invoke-RestMethod -Uri $UploadUriWithQuery `
                     -Headers $Headers -Method Post `
                     -ContentType 'application/octet-stream' `
                     -InFile $InstallerPath -ErrorAction Stop
    } catch {
        # Common rerun case: old asset still exists and upload returns HTTP 422 already_exists.
        if ($_.Exception.Message -match '422|already_exists|already exists') {
            Write-Warning "Upload conflict for '$AssetName'. Refreshing assets and retrying once."
            $Assets = Invoke-RestMethod -Uri "$ApiBase/releases/$($Release.id)/assets" `
                        -Headers $Headers -Method Get -ErrorAction Stop
            $SameName = $Assets | Where-Object { $_.name -eq $AssetName } | Select-Object -First 1
            if ($SameName) {
                Write-Host "    Removing conflicting asset '$AssetName' (id=$($SameName.id)) ..." -ForegroundColor Gray
                Invoke-RestMethod -Uri "$ApiBase/releases/assets/$($SameName.id)" `
                    -Headers $Headers -Method Delete -ErrorAction Stop | Out-Null
            }

            $Asset = Invoke-RestMethod -Uri $UploadUriWithQuery `
                         -Headers $Headers -Method Post `
                         -ContentType 'application/octet-stream' `
                         -InFile $InstallerPath -ErrorAction Stop
        } elseif ($_.Exception.Message -match '404|Not Found') {
            throw "GitHub upload endpoint returned 404. Release lookup worked, so this is usually token permission scope on asset upload/delete. Use a token with repository Contents write access for $GitHubRepo and retry. Upload URI: $UploadUri"
        } else {
            throw
        }
    }

    Write-Host ''
    Write-Host "[OK] Asset published: $($Asset.browser_download_url)" -ForegroundColor Green
}
