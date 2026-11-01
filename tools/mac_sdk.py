# Copyright (c) 2026, FM4 Static Recomp contributors. BSD-3-Clause.
"""Prepare the pinned macOS SDK without changing Windows' release toolchain."""
import hashlib
import json
from pathlib import Path
import subprocess

from sdk_patches import apply_sdk_patches


def git(source, *arguments, capture=False):
    command = ['git', '-c', 'core.symlinks=true', '-C', str(source), *map(str, arguments)]
    if not capture:
        print('+ ' + ' '.join(command), flush=True)
    result = subprocess.run(command, check=True, text=True, capture_output=capture)
    return result.stdout.strip() if capture else None


def prepare_sdk(root, external=None):
    root = Path(root).resolve()
    config = json.loads((root / 'config/macos-toolchain.json').read_text())['sdk']
    revision = config['revision']
    if len(revision) != 40 or any(c not in '0123456789abcdef' for c in revision):
        raise ValueError('The macOS SDK revision must be a full Git commit hash')
    patches = []
    patched_paths = set()
    identities = {}
    for entry in config['patches']:
        patch = (root / entry['path']).resolve()
        if not patch.is_relative_to(root):
            raise ValueError('SDK patches must be inside the source payload')
        digest = hashlib.sha256(patch.read_bytes()).hexdigest()
        if digest != entry['sha256']:
            raise ValueError(f'SDK patch checksum mismatch: {entry["path"]}')
        patches.append(patch)
        identities[entry['path']] = digest
        stats = subprocess.check_output(['git', 'apply', '--numstat', str(patch)], text=True)
        patched_paths.update(line.split('\t', 2)[2] for line in stats.splitlines())
    source = Path(external).expanduser().resolve() if external else root / '.local/shiftglue-sdk-macos' / revision
    if not external:
        source.mkdir(parents=True, exist_ok=True)
        if not (source / '.git').exists():
            # Never adopt or overwrite an unrelated directory left at this path.
            if any(source.iterdir()):
                raise ValueError(f'Managed SDK folder is not empty and is not a Git checkout: {source}')
            git(source, 'init')
            git(source, 'remote', 'add', 'origin', config['repository'])
        if git(source, 'remote', 'get-url', 'origin', capture=True) != config['repository']:
            raise ValueError(f'Managed SDK origin differs from the macOS pin: {source}')
        head = subprocess.run(['git', '-C', str(source), 'rev-parse', '--verify', 'HEAD'],
                              text=True, capture_output=True)
        if head.returncode:
            git(source, 'fetch', '--depth=1', 'origin', revision)
            git(source, 'checkout', '--detach', 'FETCH_HEAD')
        actual = git(source, 'rev-parse', 'HEAD', capture=True)
        if actual != revision:
            raise ValueError(f'Managed SDK revision mismatch: expected {revision}, found {actual}')
        git(source, 'submodule', 'update', '--init', '--recursive', '--depth=1')
    if not (source / 'CMakeLists.txt').is_file():
        raise ValueError(f'ShiftGlue source is missing at {source}')
    actual = git(source, 'rev-parse', 'HEAD', capture=True)
    if actual != revision:
        raise ValueError(f'SDK revision mismatch: expected {revision}, found {actual}. '
                         'Use the pinned checkout or remove the development SDK override.')
    submodules = git(source, 'submodule', 'status', '--recursive', capture=True)
    if any(line.startswith(('-', '+', 'U')) for line in submodules.splitlines()):
        raise ValueError(f'SDK submodules are missing or at different revisions: {source}')
    if not external:
        changed = set(git(source, 'diff', '--name-only', '--ignore-submodules=dirty', 'HEAD', capture=True).splitlines())
        if changed - patched_paths:
            raise ValueError('Managed SDK has unrelated local changes; preserve them and use a fresh checkout: '
                             + ', '.join(sorted(changed - patched_paths)))
    apply_sdk_patches(source, patches)
    diff = git(source, 'diff', '--binary', '--ignore-submodules=dirty', 'HEAD', capture=True)
    identity = dict(repository=config['repository'], revision=actual, external=bool(external),
                    patches=identities, submodules=submodules,
                    working_diff_sha256=hashlib.sha256(diff.encode()).hexdigest())
    return source, identity
