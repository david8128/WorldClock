#!/usr/bin/env bash
# =============================================================================
# generate-icons.sh — Uses ImageMagick to generate all standard freedesktop
#                     hicolor PNG sizes + a multi-resolution ICO from a single
#                     source image (default: WorldClock/Images/Logo.png).
#
# Prerequisites:
#   sudo apt-get install imagemagick
#
# Usage:
#   bash scripts/generate-icons.sh [OPTIONS]
#
# Options:
#   --src  FILE    Source image (default: WorldClock/Images/Logo.png)
#   --out  DIR     PNG output root; icons go in <DIR>/<sz>x<sz>/worldclock.png
#                  (default: WorldClock/Images/hicolor)
#   --ico  FILE    Path for the generated multi-resolution ICO
#                  (default: WorldClock/Images/Logo.ico)
#   --no-ico       Skip ICO generation
#   --help
# =============================================================================

set -euo pipefail

# ── Defaults ──────────────────────────────────────────────────────────────────
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
SRC_IMAGE="$REPO_ROOT/WorldClock/Images/Logo.png"
OUT_DIR="$REPO_ROOT/WorldClock/Images/hicolor"
ICO_OUT="$REPO_ROOT/WorldClock/Images/Logo.ico"
SKIP_ICO=false

# ── Parse arguments ───────────────────────────────────────────────────────────
while [[ $# -gt 0 ]]; do
    case "$1" in
        --src)    SRC_IMAGE="$2"; shift 2 ;;
        --out)    OUT_DIR="$2"; shift 2 ;;
        --ico)    ICO_OUT="$2"; shift 2 ;;
        --no-ico) SKIP_ICO=true; shift ;;
        --help)
            echo "Usage: generate-icons.sh [--src FILE] [--out DIR] [--ico FILE] [--no-ico]"
            exit 0
            ;;
        *) echo "Unknown option: $1"; exit 1 ;;
    esac
done

step() { echo -e "\n\033[36m==> $*\033[0m"; }

# ── Prerequisites ─────────────────────────────────────────────────────────────
if ! command -v convert &>/dev/null; then
    echo "ERROR: ImageMagick is required but not found." >&2
    echo "  Install with:  sudo apt-get install imagemagick" >&2
    exit 1
fi

if [ ! -f "$SRC_IMAGE" ]; then
    echo "ERROR: Source image not found: $SRC_IMAGE" >&2
    exit 1
fi

echo "  Source : $SRC_IMAGE"
echo "  PNG out: $OUT_DIR"
[ "$SKIP_ICO" = false ] && echo "  ICO out: $ICO_OUT"

# ── Step 1: PNG — all standard freedesktop hicolor sizes ─────────────────────
HICOLOR_SIZES="16 22 24 32 36 48 64 72 96 128 192 256 512"

step "Generating hicolor PNGs ..."
for _sz in $HICOLOR_SIZES; do
    TARGET_DIR="$OUT_DIR/${_sz}x${_sz}"
    mkdir -p "$TARGET_DIR"
    convert "$SRC_IMAGE" \
        -resize "${_sz}x${_sz}" \
        -background none \
        -gravity center \
        -extent "${_sz}x${_sz}" \
        "$TARGET_DIR/worldclock.png"
    echo "  [OK] ${_sz}x${_sz}/worldclock.png"
done

# ── Step 2: ICO — multi-resolution (16 32 48 64 128 256) ─────────────────────
if [ "$SKIP_ICO" = false ]; then
    ICO_SIZES="16 32 48 64 128 256"

    step "Generating multi-resolution ICO (sizes: $ICO_SIZES) ..."

    TMPDIR_ICO="$(mktemp -d)"
    trap 'rm -rf "$TMPDIR_ICO"' EXIT

    ICO_INPUTS=()
    for _sz in $ICO_SIZES; do
        TMP_PNG="$TMPDIR_ICO/${_sz}.png"
        convert "$SRC_IMAGE" \
            -resize "${_sz}x${_sz}" \
            -background none \
            -gravity center \
            -extent "${_sz}x${_sz}" \
            "$TMP_PNG"
        ICO_INPUTS+=("$TMP_PNG")
    done

    mkdir -p "$(dirname "$ICO_OUT")"
    convert "${ICO_INPUTS[@]}" "$ICO_OUT"
    echo "  [OK] $ICO_OUT"
fi

# ── Done ──────────────────────────────────────────────────────────────────────
echo ""
echo -e "\033[32m[OK] All icons generated.\033[0m"
echo "  PNG icons : $OUT_DIR/<sz>x<sz>/worldclock.png"
[ "$SKIP_ICO" = false ] && echo "  ICO       : $ICO_OUT"
