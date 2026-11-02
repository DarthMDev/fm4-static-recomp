"""Functional tests for pinned SDK preparation; use local repos, never the network."""
import hashlib
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
import zipfile

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from mac_sdk import prepare_sdk


def git(directory, *args):
    result = subprocess.run(['git', '-C', str(directory), *args], text=True,
                            capture_output=True, check=True)
    return result.stdout.strip()


class MacSdkTest(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix='fm4-mac-sdk-test-')
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name) / 'payload with spaces'
        self.origin = Path(self.temp.name) / 'origin'
        self.root.mkdir()
        self.origin.mkdir()
        git(self.origin, 'init')
        (self.origin / 'CMakeLists.txt').write_text('cmake_minimum_required(VERSION 3.25)\n')
        (self.origin / 'compiler.cpp').write_text('before\n')
        git(self.origin, 'add', '.')
        git(self.origin, '-c', 'user.name=SDK Test', '-c', 'user.email=test@example.invalid', 'commit', '-m', 'fixture')
        revision = git(self.origin, 'rev-parse', 'HEAD')
        (self.origin / 'compiler.cpp').write_text('after\n')
        patch = git(self.origin, 'diff') + '\n'
        git(self.origin, 'restore', 'compiler.cpp')
        (self.root / 'patches').mkdir()
        (self.root / 'patches/compiler.patch').write_text(patch)
        self.config = {'sdk': {'repository': self.origin.as_uri(), 'revision': revision,
            'patches': [{'path': 'patches/compiler.patch', 'sha256': hashlib.sha256(patch.encode()).hexdigest()}]}}
        (self.root / 'config').mkdir()
        self.write_config()

    def write_config(self):
        (self.root / 'config/macos-toolchain.json').write_text(json.dumps(self.config))

    def test_managed_checkout_is_pinned_patched_and_resumable(self):
        source, receipt = prepare_sdk(self.root)
        self.assertEqual(git(source, 'rev-parse', 'HEAD'), self.config['sdk']['revision'])
        self.assertEqual((source / 'compiler.cpp').read_text(), 'after\n')
        self.assertFalse(receipt['external'])
        again, repeated = prepare_sdk(self.root)
        self.assertEqual(source, again)
        self.assertEqual(receipt, repeated)
        self.assertEqual((self.origin / 'compiler.cpp').read_text(), 'before\n')

    def test_external_revision_mismatch_is_rejected_without_changes(self):
        self.config['sdk']['revision'] = 'a' * 40
        self.write_config()
        with self.assertRaisesRegex(ValueError, 'revision mismatch'):
            prepare_sdk(self.root, self.origin)
        self.assertEqual(git(self.origin, 'status', '--porcelain'), '')

    def test_patch_checksum_mismatch_is_rejected_before_checkout(self):
        (self.root / 'patches/compiler.patch').write_text('corrupt')
        with self.assertRaisesRegex(ValueError, 'checksum mismatch'):
            prepare_sdk(self.root)
        self.assertFalse((self.root / '.local').exists())

    def test_conflicting_local_edit_is_preserved(self):
        (self.origin / 'compiler.cpp').write_text('my own edit\n')
        with self.assertRaisesRegex(ValueError, 'conflicting'):
            prepare_sdk(self.root, self.origin)
        self.assertEqual((self.origin / 'compiler.cpp').read_text(), 'my own edit\n')

    def test_non_repository_managed_directory_is_not_adopted(self):
        managed = self.root / '.local/shiftglue-sdk-macos' / self.config['sdk']['revision']
        managed.mkdir(parents=True)
        (managed / 'mine.txt').write_text('keep')
        with self.assertRaisesRegex(ValueError, 'not empty'):
            prepare_sdk(self.root)
        self.assertEqual((managed / 'mine.txt').read_text(), 'keep')

    def test_managed_unrelated_edit_is_rejected_without_resetting_it(self):
        source, _ = prepare_sdk(self.root)
        (source / 'CMakeLists.txt').write_text('my own build change\n')
        with self.assertRaisesRegex(ValueError, 'unrelated local changes'):
            prepare_sdk(self.root)
        self.assertEqual((source / 'CMakeLists.txt').read_text(), 'my own build change\n')


class MacPayloadTest(unittest.TestCase):
    def test_source_payload_contains_complete_mac_bootstrap_and_pinned_patches(self):
        path = Path(__file__).resolve().parents[1] / 'package-launcher-macos.py'
        spec = importlib.util.spec_from_file_location('package_mac', path)
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        with tempfile.TemporaryDirectory() as directory:
            archive = Path(directory) / 'source.zip'
            module.source_payload(archive)
            with zipfile.ZipFile(archive) as source:
                for name in ('tools/mac_sdk.py', 'tools/sdk_patches.py', 'tools/setup-macos.py'):
                    self.assertIn(name, source.namelist())
                release = json.loads(source.read('config/release.json'))
                expected = json.loads((module.ROOT / 'config/macos-release.json').read_text())
                self.assertEqual(release, expected)
                self.assertIn('docs/MACOS.md', source.namelist())
                self.assertIn('src/ui/guest_frame_stats.h', source.namelist())
                config = json.loads(source.read('config/macos-toolchain.json'))
                for patch in config['sdk']['patches']:
                    self.assertEqual(hashlib.sha256(source.read(patch['path'])).hexdigest(), patch['sha256'])
                self.assertFalse(any(n.startswith(('game/', 'user/', 'out/', '.local/', 'thirdparty/')) for n in source.namelist()))


if __name__ == '__main__':
    unittest.main()
