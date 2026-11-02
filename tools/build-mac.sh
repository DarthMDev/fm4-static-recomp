#!/usr/bin/env bash
set -euo pipefail
export PATH="/opt/homebrew/bin:/usr/local/bin:$PATH"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# Share the launcher's pinned SDK, patches, codegen and complete build targets.
exec python3 "${ROOT}/tools/setup-macos.py" \
    --game-root "${FM4_GAME_ROOT:-${ROOT}/game/disc1}" \
    --cache-root "${ROOT}/.local/state/cache" \
    --toolchain-root "${ROOT}/.local/toolchain" "$@"
