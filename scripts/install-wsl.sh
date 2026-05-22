#!/usr/bin/env bash
# =============================================================================
# install-wsl.sh — Build, install, and/or launch WorldClock inside WSL2
#
# Usage (from a WSL2 Bash prompt):
#   bash scripts/install-wsl.sh [--build] [--install] [--run]
#
# Options:
#   --build    Build the .deb package (requires .NET SDK 8 + dpkg-dev)
#   --install  Install the .deb (requires sudo; installs Wine if needed)
#   --run      Launch WorldClock after install
#
# One-liner (full pipeline):
#   bash scripts/install-wsl.sh --build --install --run
# =============================================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
VERSION="1.0.0"
DEB_FILE="$REPO_ROOT/publish/deb-pkg/worldclock_${VERSION}_amd64.deb"

# ── Colour helpers ─────────────────────────────────────────────────────────────
RED='\033[31m'; GREEN='\033[32m'; CYAN='\033[36m'; YELLOW='\033[33m'; RESET='\033[0m'
info()    { echo -e "${CYAN}==> $*${RESET}"; }
success() { echo -e "${GREEN}[OK] $*${RESET}"; }
warn()    { echo -e "${YELLOW}[WARN] $*${RESET}"; }
err()     { echo -e "${RED}[ERROR] $*${RESET}" >&2; exit 1; }

show_help() {
    cat <<'EOF'
install-wsl.sh — WorldClock WSL2 installer helper

Usage:
  bash scripts/install-wsl.sh [options]

Options:
  --build       Build the .deb package from source
  --install     Install the .deb (needs sudo; auto-installs Wine via apt)
  --run         Launch WorldClock immediately
  --version X   Package version (default: 1.0.0)
  --help        Show this help

Examples:
  # Full pipeline: build → install → run
  bash scripts/install-wsl.sh --build --install --run

  # Install a pre-built .deb then run
  bash scripts/install-wsl.sh --install --run

  # Just launch (already installed)
  bash scripts/install-wsl.sh --run
EOF
}

# ── Parse arguments ───────────────────────────────────────────────────────────
DO_BUILD=false
DO_INSTALL=false
DO_RUN=false

[[ $# -eq 0 ]] && { show_help; exit 0; }

while [[ $# -gt 0 ]]; do
    case "$1" in
        --build)   DO_BUILD=true;  shift ;;
        --install) DO_INSTALL=true; shift ;;
        --run)     DO_RUN=true;    shift ;;
        --version) VERSION="$2";   DEB_FILE="$REPO_ROOT/publish/deb-pkg/worldclock_${VERSION}_amd64.deb"; shift 2 ;;
        --help)    show_help; exit 0 ;;
        *) err "Unknown option: $1.  Use --help for usage." ;;
    esac
done

# ── WSL2 check ────────────────────────────────────────────────────────────────
if ! grep -qi microsoft /proc/version 2>/dev/null; then
    warn "Not running inside WSL2 — continuing anyway (bare Linux mode)."
fi

# ── Step 1: Build ─────────────────────────────────────────────────────────────
if $DO_BUILD; then
    info "Building .deb package (version $VERSION) ..."

    # Ensure dpkg-deb is available
    if ! command -v dpkg-deb &>/dev/null; then
        info "Installing dpkg-dev ..."
        sudo apt-get update -qq
        sudo apt-get install -y --no-install-recommends dpkg-dev
    fi

    bash "$SCRIPT_DIR/build-linux-deb.sh" --version "$VERSION"
    success "Package built: $DEB_FILE"
fi

# ── Step 2: Install ───────────────────────────────────────────────────────────
if $DO_INSTALL; then
    [ -f "$DEB_FILE" ] || err ".deb not found: $DEB_FILE\nRun with --build first."

    info "Installing WorldClock from $DEB_FILE ..."
    sudo dpkg -i "$DEB_FILE" || true

    info "Resolving dependencies (Wine) ..."
    sudo apt-get install -f -y

    # Verify
    command -v worldclock &>/dev/null || err "Installation failed — worldclock not in PATH."
    success "WorldClock installed at $(command -v worldclock)"

    # ── First-time Wine initialisation ────────────────────────────────────────
    if ! grep -qi microsoft /proc/version 2>/dev/null; then
        # Only needed on bare Linux (Wine); WSL2 uses native Windows interop
        if command -v wineboot &>/dev/null; then
            info "Initialising Wine prefix (first-time setup — may take a moment) ..."
            WINEDEBUG=-all wineboot --init 2>/dev/null || true
            success "Wine prefix ready."
        fi
    fi
fi

# ── Step 3: Run ───────────────────────────────────────────────────────────────
if $DO_RUN; then
    command -v worldclock &>/dev/null || \
        err "worldclock not found in PATH.\nInstall with --install first."

    info "Launching WorldClock ..."

    # WSL2: ensure DISPLAY is set for WSLg
    if grep -qi microsoft /proc/version 2>/dev/null; then
        # WSLg sets DISPLAY automatically on Windows 11 / updated WSL2.
        # Fall back to :0 for older setups with an explicit VcXsrv/X410 server.
        export DISPLAY="${DISPLAY:-:0}"
        info "WSL2 display: $DISPLAY"
    fi

    worldclock &
    success "WorldClock launched (PID $!)."
fi
