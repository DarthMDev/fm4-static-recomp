#!/usr/bin/env bash
set -euo pipefail

# Ensure homebrew tools are in PATH
export PATH="/opt/homebrew/bin:/usr/local/bin:$PATH"

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BUILD_DIR="${FM4_BUILD_DIR:-${ROOT}/out/build/mac-arm64-release}"
EXECUTABLE="${BUILD_DIR}/fm4_static_recomp"
GAME_ROOT="${ROOT}/game/disc1"
STATE_ROOT="${ROOT}/.local/state"
USER_ROOT="${ROOT}/user"

if [[ ! -x "${EXECUTABLE}" ]]; then
    echo "Executable missing at ${EXECUTABLE}. Please run tools/build-mac.sh first."
    exit 1
fi

if [[ "${1:-}" == "--help" || "${1:-}" == "-h" ]]; then
    echo "Usage: ./tools/run-mac.sh [options...]"
    echo ""
    echo "Defaults configured:"
    echo "  --game_data_root=${GAME_ROOT}"
    echo "  --user_data_root=${USER_ROOT}"
    echo "  --gpu_backend=vulkan"
    echo ""
    echo "Useful game options:"
    echo "  --user_xuid=<16-hex>     Sign in with specific profile XUID"
    echo "  --fm4_level_of_detail=pc    PC high level of detail"
    echo "  --fm4_full_rate_scenes=true Unlock full rate in menus/scenes"
    echo "  --anisotropic_override=16   Force 16x anisotropic filtering"
    exit 0
fi

if [[ ! -d "${GAME_ROOT}" ]]; then
    echo "Game data root missing at ${GAME_ROOT}."
    exit 1
fi

mkdir -p "${STATE_ROOT}/cache" "${STATE_ROOT}/logs" "${STATE_ROOT}/update"

DEFAULT_ARGS=(
    "--game_data_root=${GAME_ROOT}"
    "--user_data_root=${USER_ROOT}"
    "--update_data_root=${STATE_ROOT}/update"
    "--cache_root=${STATE_ROOT}/cache"
    "--log_file=${STATE_ROOT}/logs/runtime.log"
    "--gpu_backend=vulkan"
)

echo "Launching FM4 Static Recomp on macOS (Apple Silicon Vulkan / MoltenVK)..."
cd "${BUILD_DIR}"
exec "${EXECUTABLE}" "${DEFAULT_ARGS[@]}" "$@"
