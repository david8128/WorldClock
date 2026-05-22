#!/usr/bin/env bash
# =============================================================================
# build-linux-deb.sh — Packages WorldClock as a Debian/Ubuntu .deb
#
# The resulting package installs a /usr/bin/worldclock launcher that:
#   • Requires WSL2 with WSLg — runs the Windows .exe directly via binfmt interop.
#
# Prerequisites:
#   • .NET SDK 8 (https://dot.net)
#   • dpkg-deb   (sudo apt-get install dpkg-dev)
#   • imagemagick (for icon conversion, optional)
#
# Usage:
#   bash scripts/build-linux-deb.sh [--version 1.2.3] [--skip-publish]
#   bash scripts/build-linux-deb.sh [--version 1.2.3] --upload --repo owner/repo
#
#   GITHUB_TOKEN must be set as an environment variable before running --upload.
#   Never pass a token as a command-line argument.
# =============================================================================

set -euo pipefail

# ── Defaults ──────────────────────────────────────────────────────────────────
VERSION="1.0.0"
SKIP_PUBLISH=false
ARCH="amd64"
UPLOAD=false
GITHUB_REPO="${GITHUB_REPO:-}"   # owner/repo  (can also be set via env)
GITHUB_TAG=""                     # defaults to v$VERSION

# ── Parse arguments ───────────────────────────────────────────────────────────
while [[ $# -gt 0 ]]; do
    case "$1" in
        --version)      VERSION="${2//$'\r'/}"; shift 2 ;;
        --skip-publish) SKIP_PUBLISH=true; shift ;;
        --upload)       UPLOAD=true; shift ;;
        --repo)         GITHUB_REPO="${2}"; shift 2 ;;
        --tag)          GITHUB_TAG="${2}"; shift 2 ;;
        --help)
            echo "Usage: build-linux-deb.sh [--version X.Y.Z] [--skip-publish]"
            echo "                          [--upload [--repo OWNER/REPO] [--tag vX.Y.Z]]"
            echo ""
            echo "Upload flags (requires GITHUB_TOKEN env var):"
            echo "  --upload      Push the .deb to a GitHub Release as a release asset"
            echo "  --repo        GitHub repo as OWNER/REPO (or set \$GITHUB_REPO env var)"
            echo "  --tag         Release tag to target (defaults to v\$VERSION)"
            exit 0
            ;;
        *) echo "Unknown option: $1"; exit 1 ;;
    esac
done

# ── Paths ─────────────────────────────────────────────────────────────────────
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
PROJECT_FILE="$REPO_ROOT/WorldClock/WorldClock.csproj"
PUBLISH_DIR="$REPO_ROOT/publish/win-x64"
DEB_ROOT="$REPO_ROOT/publish/deb-pkg"
PKG_DIR="$DEB_ROOT/worldclock_${VERSION}_${ARCH}"
DEB_FILE="$DEB_ROOT/worldclock_${VERSION}_${ARCH}.deb"

step() { echo -e "\n\033[36m==> $*\033[0m"; }

# ── Step 1: dotnet publish ─────────────────────────────────────────────────────
if [ "$SKIP_PUBLISH" = false ]; then
    step "Publishing WorldClock (self-contained, win-x64, single-file) ..."
    dotnet publish "$PROJECT_FILE" \
        -c Release \
        -r win-x64 \
        --self-contained true \
        "-p:VersionPrefix=$VERSION" \
        -o "$PUBLISH_DIR" \
        --nologo
else
    step "Skipping publish (using existing $PUBLISH_DIR)"
    [ -f "$PUBLISH_DIR/WorldClock.exe" ] || \
        { echo "ERROR: WorldClock.exe not found in $PUBLISH_DIR"; exit 1; }
fi

# ── Step 2: Build .deb directory tree ─────────────────────────────────────────
step "Creating .deb directory structure ..."
rm -rf "$PKG_DIR"

OPT_DIR="$PKG_DIR/opt/worldclock"
BIN_DIR="$PKG_DIR/usr/bin"
DESKTOP_DIR="$PKG_DIR/usr/share/applications"
ICONS_BASE="$PKG_DIR/usr/share/icons/hicolor"
DOC_DIR="$PKG_DIR/usr/share/doc/worldclock"
DEBIAN_DIR="$PKG_DIR/DEBIAN"

mkdir -p "$OPT_DIR" "$BIN_DIR" "$DESKTOP_DIR" "$DOC_DIR" "$DEBIAN_DIR"
HICOLOR_SIZES="16 22 24 32 36 48 64 72 96 128 192 256 512"
for _sz in $HICOLOR_SIZES; do
    mkdir -p "$ICONS_BASE/${_sz}x${_sz}/apps"
done

# Copy published app
cp -r "$PUBLISH_DIR/." "$OPT_DIR/"


# ── Step 3: DEBIAN/control ────────────────────────────────────────────────────
cat > "$DEBIAN_DIR/control" <<EOF
Package: worldclock
Version: ${VERSION}
Architecture: ${ARCH}
Maintainer: WorldClock Team <worldclock@example.com>
Description: WorldClock — multi-timezone clock and time visualizer
 A desktop clock app showing multiple timezones with a time visualizer,
 city search, and Microsoft Teams meeting integration.
 .
 Requires WSL2 with WSLg — the Windows executable runs directly via binfmt interop.
Homepage: https://github.com/worldclock
Section: utils
Priority: optional
EOF

# ── Step 4: /usr/bin/worldclock launcher ──────────────────────────────────────
cat > "$BIN_DIR/worldclock" <<'LAUNCHER'
#!/usr/bin/env bash
# WorldClock launcher — requires WSL2 with WSLg

set -euo pipefail

INSTALL_DIR="/opt/worldclock"
EXE="$INSTALL_DIR/WorldClock.exe"

if [ ! -f "$EXE" ]; then
    echo "ERROR: WorldClock.exe not found at $EXE" >&2
    exit 1
fi

# ── WSL2 check ───────────────────────────────────────────────────────────────
if ! grep -qi microsoft /proc/version 2>/dev/null; then
    echo "ERROR: WorldClock requires WSL2 with WSLg." >&2
    echo "  See: https://learn.microsoft.com/windows/wsl/tutorials/gui-apps" >&2
    exit 1
fi

# WSLg provides an X/Wayland display — run the Windows .exe via binfmt interop.
exec "$EXE" "$@"
LAUNCHER

chmod 755 "$BIN_DIR/worldclock"

# ── Step 5: .desktop entry ────────────────────────────────────────────────────
cat > "$DESKTOP_DIR/worldclock.desktop" <<EOF
[Desktop Entry]
Version=1.0
Type=Application
Name=WorldClock
GenericName=World Clock
Comment=Multi-timezone clock and time visualizer
Exec=worldclock
Icon=worldclock
Categories=Utility;Clock;
Keywords=clock;timezone;time;world;
StartupWMClass=WorldClock
Terminal=false
EOF

# ── Step 6: Copyright ─────────────────────────────────────────────────────────
cat > "$DOC_DIR/copyright" <<EOF
WorldClock ${VERSION}
Copyright (C) $(date +%Y) WorldClock Team. All rights reserved.

This software is provided "as is" without warranty of any kind.
EOF

# ── Step 7: DEBIAN/postinst (update icon cache + desktop DB) ──────────────────
cat > "$DEBIAN_DIR/postinst" <<'POSTINST'
#!/bin/sh
set -e
if command -v update-icon-caches >/dev/null 2>&1; then
    update-icon-caches /usr/share/icons/hicolor || true
fi
if command -v update-desktop-database >/dev/null 2>&1; then
    update-desktop-database /usr/share/applications || true
fi
POSTINST
chmod 755 "$DEBIAN_DIR/postinst"

# ── Step 8: Fix permissions ───────────────────────────────────────────────────
find "$PKG_DIR" -type d -exec chmod 755 {} \;
find "$PKG_DIR" -type f ! -name "worldclock" ! -name "postinst" \
    -exec chmod 644 {} \;
# Ensure the Windows .exe is executable for binfmt interop
[ -f "$OPT_DIR/WorldClock.exe" ] && chmod 755 "$OPT_DIR/WorldClock.exe"

# ── Step 9: Build the .deb ────────────────────────────────────────────────────
step "Building .deb package ..."
dpkg-deb --build --root-owner-group "$PKG_DIR" "$DEB_FILE"

echo ""
echo -e "\033[32m[OK] Package ready: $DEB_FILE\033[0m"
echo ""
echo "Install on WSL2:"
echo "  sudo dpkg -i \"$DEB_FILE\""
echo ""
echo "Then run:  worldclock"

# ── Step 10: Upload to GitHub Releases ────────────────────────────────────────
if [ "$UPLOAD" = true ]; then
    step "Uploading .deb to GitHub Releases ..."

    # ── Validate prerequisites ────────────────────────────────────────────────
    if [ -z "${GITHUB_TOKEN:-}" ]; then
        echo "ERROR: GITHUB_TOKEN environment variable is not set." >&2
        echo "  Set it with:  export GITHUB_TOKEN=ghp_..." >&2
        exit 1
    fi
    if [ -z "$GITHUB_REPO" ]; then
        echo "ERROR: Specify --repo OWNER/REPO or set the GITHUB_REPO environment variable." >&2
        exit 1
    fi

    [ -z "$GITHUB_TAG" ] && GITHUB_TAG="v${VERSION}"

    API="https://api.github.com/repos/${GITHUB_REPO}"
    DEB_FILENAME="$(basename "$DEB_FILE")"
    GH_HEADERS=(
        -H "Authorization: Bearer ${GITHUB_TOKEN}"
        -H "Accept: application/vnd.github+json"
        -H "X-GitHub-Api-Version: 2022-11-28"
    )

    echo "  Tag: ${GITHUB_TAG}  |  Repo: ${GITHUB_REPO}"

    # ── Look up release by tag (create if absent) ─────────────────────────────
    RELEASE_JSON=$(curl -sf "${GH_HEADERS[@]}" \
        "${API}/releases/tags/${GITHUB_TAG}" || true)

    if [ -z "$RELEASE_JSON" ] || echo "$RELEASE_JSON" | grep -q '"Not Found"'; then
        echo "  Release '${GITHUB_TAG}' not found — creating it ..."
        RELEASE_JSON=$(curl -sSf -X POST "${GH_HEADERS[@]}" \
            -H "Content-Type: application/json" \
            -d "{\"tag_name\":\"${GITHUB_TAG}\",\"name\":\"WorldClock ${VERSION}\",\"body\":\"WorldClock ${VERSION}\",\"draft\":false,\"prerelease\":false}" \
            "${API}/releases")
    fi

    RELEASE_ID=$(echo "$RELEASE_JSON" | python3 -c \
        "import sys,json; print(json.load(sys.stdin)['id'])" 2>/dev/null || true)
    if [ -z "$RELEASE_ID" ]; then
        echo "ERROR: Could not determine release ID from GitHub response." >&2
        echo "$RELEASE_JSON" >&2
        exit 1
    fi
    echo "  Release ID: ${RELEASE_ID}"

    # ── Delete existing asset with the same name (makes re-runs idempotent) ───
    EXISTING_ID=$(echo "$RELEASE_JSON" | python3 -c "
import sys, json
data = json.load(sys.stdin)
for a in data.get('assets', []):
    if a['name'] == '${DEB_FILENAME}':
        print(a['id'])
        break
" 2>/dev/null || true)

    if [ -n "$EXISTING_ID" ]; then
        echo "  Removing existing asset '${DEB_FILENAME}' (id=${EXISTING_ID}) ..."
        curl -sSf -X DELETE "${GH_HEADERS[@]}" \
            "${API}/releases/assets/${EXISTING_ID}"
    fi

    # ── Upload the .deb ───────────────────────────────────────────────────────
    UPLOAD_URL="https://uploads.github.com/repos/${GITHUB_REPO}/releases/${RELEASE_ID}/assets"
    echo "  Uploading ${DEB_FILENAME} ($(du -h "$DEB_FILE" | cut -f1)) ..."
    ASSET_JSON=$(curl -sSf -X POST "${GH_HEADERS[@]}" \
        -H "Content-Type: application/vnd.debian.binary-package" \
        --data-binary "@${DEB_FILE}" \
        "${UPLOAD_URL}?name=${DEB_FILENAME}&label=${DEB_FILENAME}")

    ASSET_URL=$(echo "$ASSET_JSON" | python3 -c \
        "import sys,json; print(json.load(sys.stdin)['browser_download_url'])" 2>/dev/null || true)

    if [ -n "$ASSET_URL" ]; then
        echo ""
        echo -e "\033[32m[OK] Asset published: ${ASSET_URL}\033[0m"
    else
        echo "ERROR: Upload failed. Response:" >&2
        echo "$ASSET_JSON" >&2
        exit 1
    fi
fi
