#!/usr/bin/env bash
# =============================================================================
# build-linux-deb.sh — Packages WorldClock as a Debian/Ubuntu .deb
#
# The resulting package installs a /usr/bin/worldclock launcher that:
#   • On WSL2  – runs the Windows .exe directly via WSLg / binfmt interop.
#   • On Linux – runs the Windows .exe via Wine.
#
# Prerequisites:
#   • .NET SDK 8 (https://dot.net)
#   • dpkg-deb   (sudo apt-get install dpkg-dev)
#   • imagemagick (for icon conversion, optional)
#
# Usage:
#   bash scripts/build-linux-deb.sh [--version 1.2.3] [--skip-publish]
# =============================================================================

set -euo pipefail

# ── Defaults ──────────────────────────────────────────────────────────────────
VERSION="1.0.0"
SKIP_PUBLISH=false
ARCH="amd64"

# ── Parse arguments ───────────────────────────────────────────────────────────
while [[ $# -gt 0 ]]; do
    case "$1" in
        --version)      VERSION="$2"; shift 2 ;;
        --skip-publish) SKIP_PUBLISH=true; shift ;;
        --help)
            echo "Usage: build-linux-deb.sh [--version X.Y.Z] [--skip-publish]"
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
        -p:PublishSingleFile=true \
        -p:IncludeNativeLibrariesForSelfExtract=true \
        "-p:Version=$VERSION" \
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
ICON_DIR_256="$PKG_DIR/usr/share/icons/hicolor/256x256/apps"
ICON_DIR_48="$PKG_DIR/usr/share/icons/hicolor/48x48/apps"
DOC_DIR="$PKG_DIR/usr/share/doc/worldclock"
DEBIAN_DIR="$PKG_DIR/DEBIAN"

mkdir -p "$OPT_DIR" "$BIN_DIR" "$DESKTOP_DIR" \
         "$ICON_DIR_256" "$ICON_DIR_48" "$DOC_DIR" "$DEBIAN_DIR"

# Copy published app
cp -r "$PUBLISH_DIR/." "$OPT_DIR/"

# Copy icons
cp "$REPO_ROOT/WorldClock/Images/Logo.png" "$ICON_DIR_256/worldclock.png"

# Downscale to 48x48 if ImageMagick is available; otherwise copy as-is
if command -v convert &>/dev/null; then
    convert "$ICON_DIR_256/worldclock.png" -resize 48x48 "$ICON_DIR_48/worldclock.png"
else
    cp "$ICON_DIR_256/worldclock.png" "$ICON_DIR_48/worldclock.png"
fi

# ── Step 3: DEBIAN/control ────────────────────────────────────────────────────
cat > "$DEBIAN_DIR/control" <<EOF
Package: worldclock
Version: ${VERSION}
Architecture: ${ARCH}
Maintainer: WorldClock Team <worldclock@example.com>
Depends: wine (>= 6.0) | wine64 (>= 6.0)
Recommends: winetricks
Description: WorldClock — multi-timezone clock and time visualizer
 A desktop clock app showing multiple timezones with a time visualizer,
 city search, and Microsoft Teams meeting integration.
 .
 On WSL2 the Windows executable runs directly via WSLg.
 On bare Linux it runs via Wine.
Homepage: https://github.com/worldclock
Section: utils
Priority: optional
EOF

# ── Step 4: /usr/bin/worldclock launcher ──────────────────────────────────────
cat > "$BIN_DIR/worldclock" <<'LAUNCHER'
#!/usr/bin/env bash
# WorldClock launcher
# - WSL2 with WSLg: runs the Windows .exe via binfmt interop (display via WSLg)
# - Bare Linux:     runs the Windows .exe via Wine

set -euo pipefail

INSTALL_DIR="/opt/worldclock"
EXE="$INSTALL_DIR/WorldClock.exe"

if [ ! -f "$EXE" ]; then
    echo "ERROR: WorldClock.exe not found at $EXE" >&2
    exit 1
fi

# ── WSL2 detection ────────────────────────────────────────────────────────────
if grep -qi microsoft /proc/version 2>/dev/null; then
    # WSLg provides an X/Wayland display automatically.
    exec "$EXE" "$@"
fi

# ── Bare Linux via Wine ───────────────────────────────────────────────────────
WINE_BIN=""
for candidate in wine64 wine; do
    if command -v "$candidate" &>/dev/null; then
        WINE_BIN="$candidate"
        break
    fi
done

if [ -z "$WINE_BIN" ]; then
    echo "WorldClock requires Wine on non-WSL Linux." >&2
    echo "Install with:  sudo apt-get install wine" >&2
    exit 1
fi

# Silence Wine debug noise unless WINEDEBUG is already set
export WINEDEBUG="${WINEDEBUG:-fixme-all}"
exec "$WINE_BIN" "$EXE" "$@"
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
# Ensure the Windows .exe is executable (Wine needs it)
[ -f "$OPT_DIR/WorldClock.exe" ] && chmod 755 "$OPT_DIR/WorldClock.exe"

# ── Step 9: Build the .deb ────────────────────────────────────────────────────
step "Building .deb package ..."
dpkg-deb --build --root-owner-group "$PKG_DIR" "$DEB_FILE"

echo ""
echo -e "\033[32m[OK] Package ready: $DEB_FILE\033[0m"
echo ""
echo "Install on Debian/Ubuntu/WSL2:"
echo "  sudo dpkg -i \"$DEB_FILE\""
echo "  sudo apt-get install -f          # installs Wine if missing"
echo ""
echo "Then run:  worldclock"
