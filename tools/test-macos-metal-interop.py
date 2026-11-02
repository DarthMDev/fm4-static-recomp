#!/usr/bin/env python3
# Copyright (c) 2026, FM4 Static Recomp contributors. BSD-3-Clause.
"""Test synthetic GPU sharing against the game's exact bundled MoltenVK library."""
import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import platform
import subprocess

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--build-dir', type=Path, default=ROOT / 'out/build/mac-arm64-release')
    args = parser.parse_args()
    if platform.system() != 'Darwin' or platform.machine() != 'arm64':
        parser.error('Requires native Apple Silicon macOS')
    build = args.build_dir.resolve()
    cache = build / 'CMakeCache.txt'
    if not cache.is_file():
        parser.error('Configure the game build first')
    entries = dict(line.split('=', 1) for line in cache.read_text().splitlines()
                   if '=' in line and not line.startswith(('#', '//')))
    sdk = Path(entries['REXSDK_DIR:PATH'])
    library = build / 'rexglue-artifacts/libMoltenVK.dylib'
    if not library.is_file():
        parser.error(f'Bundled MoltenVK missing: {library}')
    output = build / 'metal-interop'
    output.mkdir(exist_ok=True)
    receipt = dict(created_utc=datetime.now(timezone.utc).isoformat(),
                   macos=platform.mac_ver()[0], machine=platform.machine(), passed=False,
                   moltenvk_sha256=hashlib.sha256(library.read_bytes()).hexdigest(),
                   moltenvk_library=str(library.resolve()),
                   formats=['RGBA8_UNORM', 'R32_SFLOAT (reconstructed depth)', 'R16G16_SFLOAT'],
                   texture_readbacks=0, diagnostic_status_bytes=4,
                   in_renderer_validated=False, metalfx_validated=False)
    try:
        with (output / 'test.log').open('w') as log:
            subprocess.run(['xcrun', 'clang++', '-std=c++20', '-fobjc-arc',
                '-mmacosx-version-min=14.0', '-arch', 'arm64',
                '-I' + str(sdk / 'thirdparty/vulkan-headers/include'),
                str(ROOT / 'tools/metal-interop/main.mm'), str(library),
                '-Wl,-rpath,' + str(library.parent), '-framework', 'Foundation',
                '-framework', 'Metal', '-o', str(output / 'fm4-metal-interop')],
                stdout=log, stderr=subprocess.STDOUT, check=True)
            subprocess.run([output / 'fm4-metal-interop'], stdout=log,
                           stderr=subprocess.STDOUT, check=True, timeout=60)
        receipt['passed'] = True
    except (OSError, subprocess.SubprocessError) as error:
        receipt['error'] = str(error)
    (output / 'result.json').write_text(json.dumps(receipt, indent=2) + '\n')
    print('PASS' if receipt['passed'] else 'FAIL', output / 'test.log')
    return 0 if receipt['passed'] else 1


if __name__ == '__main__':
    raise SystemExit(main())
