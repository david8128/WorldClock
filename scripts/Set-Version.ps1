<#
.SYNOPSIS
    Tags the current git commit with the version read from the VERSION file.

.DESCRIPTION
    1. Reads the version string from <repo-root>\VERSION.
    2. Validates it is a semantic version (X.Y.Z or X.Y.Z-prerelease).
    3. Creates an annotated git tag "v<version>" on HEAD.
    4. Optionally pushes the tag to a remote.

    Run this script whenever you change the VERSION file and are ready to
    mark the commit as a release.  The build script (Build-WindowsInstaller.ps1)
    automatically reads the same VERSION file, so the installer will carry the
    matching version without any extra steps.

.PARAMETER Push
    Push the new tag to the remote after creating it.

.PARAMETER Remote
    Git remote to push to. Default: origin.

.PARAMETER Force
    Overwrite an existing tag with the same name (git tag -f).
    Use with caution on already-pushed tags.

.EXAMPLE
    # Tag HEAD with the version in VERSION (no push)
    .\scripts\Set-Version.ps1

.EXAMPLE
    # Tag and push to origin
    .\scripts\Set-Version.ps1 -Push

.EXAMPLE
    # Re-tag (overwrite) and push
    .\scripts\Set-Version.ps1 -Force -Push -Remote upstream
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [switch] $Push,
    [string] $Remote = 'origin',
    [switch] $Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-Step([string]$Message) {
    Write-Host "`n==> $Message" -ForegroundColor Cyan
}

# ── Resolve repo root ─────────────────────────────────────────────────────────
$RepoRoot    = (Resolve-Path "$PSScriptRoot\..").Path
$VersionFile = Join-Path $RepoRoot 'VERSION'

# ── Read VERSION ──────────────────────────────────────────────────────────────
Write-Step "Reading version from VERSION file ..."

if (-not (Test-Path $VersionFile)) {
    Write-Error "VERSION file not found: $VersionFile"
}

$Version = (Get-Content $VersionFile -Raw).Trim()

if ([string]::IsNullOrWhiteSpace($Version)) {
    Write-Error "VERSION file is empty."
}

# Basic semver guard: X.Y.Z or X.Y.Z-pre.release+build
if ($Version -notmatch '^\d+\.\d+\.\d+(-[\w.]+)?(\+[\w.]+)?$') {
    Write-Error "VERSION '$Version' is not a valid semantic version.`nExpected format: X.Y.Z  or  X.Y.Z-prerelease"
}

$TagName = "v$Version"
Write-Host "    Version : $Version" -ForegroundColor Gray
Write-Host "    Tag     : $TagName"  -ForegroundColor Gray

# ── Require git ───────────────────────────────────────────────────────────────
if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    Write-Error "git is not in PATH. Install Git from https://git-scm.com"
}

# ── Check working directory ───────────────────────────────────────────────────
Push-Location $RepoRoot
try {
    # Confirm we are inside a git repository
    $null = git rev-parse --git-dir 2>&1
    if ($LASTEXITCODE -ne 0) { Write-Error "Not a git repository: $RepoRoot" }

    $HeadCommit = git rev-parse --short HEAD
    Write-Host "    Commit  : $HeadCommit" -ForegroundColor Gray

    # ── Check for existing tag ────────────────────────────────────────────────
    $existing = git tag --list $TagName
    if ($existing -eq $TagName) {
        if ($Force) {
            Write-Warning "Tag '$TagName' already exists — overwriting (-Force)."
        } else {
            Write-Error (
                "Tag '$TagName' already exists on commit $(git rev-list -n1 $TagName --abbrev-commit).`n" +
                "Use -Force to overwrite, or bump the version in VERSION first."
            )
        }
    }

    # ── Create annotated tag ──────────────────────────────────────────────────
    Write-Step "Creating annotated git tag '$TagName' on $HeadCommit ..."

    $forceArg = if ($Force) { '-f' } else { $null }
    $tagArgs  = @('-a', $TagName, '-m', "Release $Version")
    if ($forceArg) { $tagArgs += $forceArg }

    if ($PSCmdlet.ShouldProcess("HEAD ($HeadCommit)", "git tag $TagName")) {
        git tag @tagArgs
        if ($LASTEXITCODE -ne 0) { Write-Error "git tag failed (exit $LASTEXITCODE)." }
        Write-Host "[OK] Tag created: $TagName -> $HeadCommit" -ForegroundColor Green
    }

    # ── Optionally push ───────────────────────────────────────────────────────
    if ($Push) {
        Write-Step "Pushing '$TagName' to remote '$Remote' ..."
        $pushArgs = @('push', $Remote, $TagName)
        if ($forceArg) { $pushArgs += $forceArg }

        if ($PSCmdlet.ShouldProcess($Remote, "git push $TagName")) {
            git @pushArgs
            if ($LASTEXITCODE -ne 0) { Write-Error "git push failed (exit $LASTEXITCODE)." }
            Write-Host "[OK] Pushed $TagName to $Remote." -ForegroundColor Green
        }
    } else {
        Write-Host ''
        Write-Host "    To push the tag later, run:" -ForegroundColor Gray
        Write-Host "      git push origin $TagName"   -ForegroundColor Gray
        Write-Host "    Or re-run with -Push:"        -ForegroundColor Gray
        Write-Host "      .\scripts\Set-Version.ps1 -Push" -ForegroundColor Gray
    }

} finally {
    Pop-Location
}

Write-Host ''
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  Build the installer:  .\scripts\Build-WindowsInstaller.ps1 -IsccPath `"<path>\ISCC.exe`"" -ForegroundColor Gray
Write-Host "  (Version $Version will be read automatically from the VERSION file.)" -ForegroundColor Gray
