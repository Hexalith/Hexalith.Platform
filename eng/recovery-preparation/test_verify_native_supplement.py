#!/usr/bin/env python3
"""Synthetic filesystem/parser failures for the offline supplement verifier."""

import contextlib
from datetime import datetime, timezone
import importlib.util
import io
import json
import marshal
import os
from pathlib import Path
import stat
import struct
import tempfile
import types
import unittest
from unittest.mock import patch


def load_source(path, name):
    """Compile bounded source directly; never consume an eligible pyc cache."""
    directory = os.open('/', os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW)
    try:
        for part in path.parts[1:-1]:
            following = os.open(part, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW, dir_fd=directory)
            os.close(directory)
            directory = following
            info = os.fstat(directory)
            sticky = info.st_uid == 0 and info.st_mode & stat.S_ISVTX
            if info.st_uid not in (0, os.getuid()) or stat.S_IMODE(info.st_mode) & 0o022 and not sticky:
                raise ValueError('unsafe-source-custody')
        fd = os.open(path.name, os.O_RDONLY | os.O_NOFOLLOW | os.O_NONBLOCK, dir_fd=directory)
        with os.fdopen(fd, 'rb') as stream:
            before = os.fstat(stream.fileno())
            if not stat.S_ISREG(before.st_mode) or before.st_nlink != 1 or before.st_size > 1048576:
                raise ValueError('unsafe-source-file')
            raw = stream.read(1048577)
            after = os.fstat(stream.fileno())
            if len(raw) > 1048576 or (before.st_size, before.st_mtime_ns, before.st_ctime_ns) != (after.st_size, after.st_mtime_ns, after.st_ctime_ns):
                raise ValueError('source-changed')
    finally:
        os.close(directory)
    module = types.ModuleType(name)
    module.__file__ = str(path)
    exec(compile(raw, str(path), 'exec'), module.__dict__)
    return module


v = load_source(Path(__file__).with_name('verify_native_supplement.py'), 'supplement_verifier')


class SupplementTests(unittest.TestCase):
    def test_loader_ignores_eligible_unbound_bytecode(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / 'synthetic.py'
            source = b'identity = "bound-source"\n'
            path.write_bytes(source)
            info = path.stat()
            cached = Path(importlib.util.cache_from_source(str(path)))
            cached.parent.mkdir(mode=0o700)
            cached.write_bytes(importlib.util.MAGIC_NUMBER + struct.pack('<III', 0, int(info.st_mtime), len(source)) +
                               marshal.dumps(compile('identity = "unbound-cache"', str(path), 'exec')))
            self.assertEqual(load_source(path, 'synthetic').identity, 'bound-source')

    def test_escape_and_alias_paths_refused(self):
        for name in ('../secret', '/etc/shadow', 'a/../b', 'a//b', './a', 'a\\b', ''):
            with self.subTest(name=name), self.assertRaises(v.Refusal):
                v.safe_relative(name)

    def test_duplicate_nonfinite_and_nonobject_json_refused(self):
        for raw in (b'{"a":1,"a":2}', b'{"a":NaN}', b'{"a":Infinity}', b'{"a":1e999}', b'[]'):
            with self.subTest(raw=raw), self.assertRaises(v.Refusal):
                v.strict_json(raw)

    def test_unapproved_preservation_inputs(self):
        for name in ('/home/administrator/.kube/hexalith-production', '/home/administrator/.ssh/id_ed25519', '/etc/shadow'):
            self.assertFalse(v.permitted_preservation(Path(name)))

    def test_unapproved_runtime_inputs(self):
        for name in ('/home/administrator/.kube/hexalith-production', '/etc/ssl/private/key', '/usr/lib/x86_64-linux-gnu/not-a-library'):
            self.assertFalse(v.permitted_runtime(Path(name)))

    def test_bound_synthetic_file(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / 'synthetic'
            path.write_bytes(b'synthetic')
            os.chmod(path, 0o600)
            raw = v.check_record(path, {'bytes': 9, 'sha256': v.sha(b'synthetic')})
            self.assertEqual(raw, b'synthetic')

    def test_changed_bytes_and_custody(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / 'synthetic'
            path.write_bytes(b'changed')
            os.chmod(path, 0o600)
            with self.assertRaises(v.Refusal):
                v.check_record(path, {'bytes': 7, 'sha256': v.sha(b'initial')})
            with self.assertRaises(v.Refusal):
                v.check_record(path, {'bytes': 7, 'sha256': v.sha(b'changed'), 'mode': 0o644})

    def test_symlink_and_hardlink(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / 'synthetic'
            path.write_bytes(b'only synthetic')
            link = Path(tmp) / 'link'
            link.symlink_to(path)
            with self.assertRaises(v.Refusal):
                v.read_file(link)
            link.unlink()
            os.link(path, link)
            with self.assertRaises(v.Refusal):
                v.read_file(path)

    def test_writable_ancestor(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / 'synthetic'
            path.write_bytes(b'synthetic')
            os.chmod(tmp, 0o777)
            try:
                with self.assertRaises(v.Refusal):
                    v.read_file(path)
            finally:
                os.chmod(tmp, 0o700)

    def test_oversize_rejected_before_read(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / 'synthetic'
            path.write_bytes(b'12345')
            with patch.object(v, 'MAX_FILE', 4), self.assertRaises(v.Refusal):
                v.read_file(path)

    def test_wrong_anchor_before_payload(self):
        with patch.object(v, 'read_file', return_value=b'{}') as read:
            with self.assertRaises(v.Refusal):
                v.verify('0' * 64)
            self.assertEqual(read.call_count, 1)

    def test_bad_anchor_before_file_access(self):
        with patch.object(v, 'read_file') as read:
            with self.assertRaises(v.Refusal):
                v.verify('SECRET-not-a-hash')
            read.assert_not_called()

    def test_cli_suppresses_supplied_secret_and_unknown_commands(self):
        for args in (['verify', '--sha256', 'SECRET'], ['verify', '--command', 'SECRET'], ['verify', '--sha', 'SECRET']):
            output = io.StringIO()
            with patch('sys.argv', args), contextlib.redirect_stdout(output), contextlib.redirect_stderr(output):
                result = v.main()
            self.assertEqual(result, 1)
            self.assertNotIn('SECRET', output.getvalue())
            self.assertEqual(json.loads(output.getvalue())['operationalGate'], 'closed')

    def test_manifest_layout_extra_file_refused(self):
        manifest = {'kind': 'hexalith.native-measurement-supplement', 'operationalGate': 'closed',
                    'v4Sha256': v.V4_SHA, 'policyCeilingEnds': v.CEILING, 'files': {'synthetic.json': {}}}
        raw = json.dumps(manifest).encode()
        with patch.object(v, 'read_file', return_value=raw), patch.object(v, 'inventory_paths', return_value={'packet-v1.json', 'synthetic.json', 'extra'}), patch.object(v, 'check_record') as check:
            with self.assertRaises(v.Refusal):
                v.verify(v.sha(raw))
            check.assert_not_called()

    def test_empty_and_incomplete_source_inventory_refused(self):
        for files in ({}, {str(v.SOURCE_ROOT / 'native_measurement_adapter.py'): {}}):
            with self.subTest(files=files), patch.object(v, 'check_record') as check:
                with self.assertRaises(v.Refusal):
                    v.validate_sources(files)
                check.assert_not_called()

    def test_empty_and_incomplete_runtime_inventory_refused(self):
        for libraries in ([], ['/usr/lib/x86_64-linux-gnu/libc.so.6']):
            runtime = {'files': {}, 'dynamicDependencies': libraries,
                       'interpreterFlags': ['-I', '-S', '-B'], 'executionQualified': False}
            with self.subTest(libraries=libraries), patch.object(v, 'check_record') as check:
                with self.assertRaises(v.Refusal):
                    v.validate_runtime(runtime)
                check.assert_not_called()

    def test_runtime_omission_before_read(self):
        runtime = {'files': {}, 'dynamicDependencies': sorted(v.REQUIRED_LIBRARIES),
                   'interpreterFlags': ['-I', '-S', '-B'], 'executionQualified': False}
        with patch.object(v, 'stdlib_files', return_value={'/usr/lib/python3.14/json/__init__.py'}), patch.object(v, 'check_record') as check:
            with self.assertRaises(v.Refusal):
                v.validate_runtime(runtime)
            check.assert_not_called()

    def test_final_external_recheck_detects_changed_input(self):
        path = v.V4 / 'packet-v4.json'
        before = {str(path): {'bytes': 0, 'sha256': '0' * 64}}
        with patch.object(v, 'check_record', side_effect=v.Refusal('byte-binding-mismatch')) as check:
            with self.assertRaises(v.Refusal):
                v.recheck_external(before, {}, {})
            self.assertEqual(check.call_count, 1)

    def test_final_layout_recheck_detects_addition(self):
        path = v.V4 / 'packet-v4.json'
        before = {str(path): {'bytes': 0, 'sha256': '0' * 64}}
        with patch.object(v, 'check_record'), patch.object(v, 'inventory_paths', return_value={'packet-v4.json', 'extra'}):
            with self.assertRaises(v.Refusal):
                v.recheck_external(before, {}, {})

    def test_parent_replacement_after_open_refused(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            parent = root / 'parent'
            parent.mkdir(mode=0o700)
            (parent / 'input').write_bytes(b'expected')
            other = root / 'other'
            other.mkdir(mode=0o700)
            (other / 'input').write_bytes(b'changed!')
            original = os.fdopen
            def replace(fd, *args, **kwargs):
                parent.rename(root / 'original')
                parent.symlink_to(other, target_is_directory=True)
                return original(fd, *args, **kwargs)
            with patch.object(v.os, 'fdopen', side_effect=replace), self.assertRaises(v.Refusal):
                v.check_record(parent / 'input', {'bytes': 8, 'sha256': v.sha(b'expected')})

    def test_original_v4_payload_hash_is_independent(self):
        manifest = {'files': {'synthetic-payload.json': {'bytes': 1, 'sha256': '0' * 64}}}
        with patch.object(v, 'check_record', side_effect=v.Refusal('byte-binding-mismatch')) as check:
            with self.assertRaises(v.Refusal):
                v.expected_preservation(manifest)
            check.assert_called_once_with(v.V4 / 'synthetic-payload.json', manifest['files']['synthetic-payload.json'], True)

    def test_symlink_replacement_before_open_never_reads_other_input(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            parent = root / 'allowed'
            other = root / 'outside'
            parent.mkdir(mode=0o700)
            other.mkdir(mode=0o700)
            (parent / 'input').write_bytes(b'expected')
            (other / 'input').write_bytes(b'excluded-synthetic-sentinel')
            original = v.open_bound
            def replace(path, ancestry):
                parent.rename(root / 'retained')
                parent.symlink_to(other, target_is_directory=True)
                return original(path, ancestry)
            with patch.object(v, 'open_bound', side_effect=replace), patch.object(v.os, 'fdopen') as read:
                with self.assertRaises((v.Refusal, OSError)):
                    v.read_file(parent / 'input')
                read.assert_not_called()

    def test_extra_empty_directory_is_visible(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / 'extra').mkdir(mode=0o700)
            with self.assertRaises(v.Refusal):
                v.check_directories(root, {'payload.json'})


class CompleteSyntheticVerification(unittest.TestCase):
    """Exercise verify's full success/final-refusal path using local fake bytes."""

    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.base = Path(self.temp.name)
        self.root, self.v4, self.history = (self.base / name for name in ('supplement', 'v4', 'history'))
        self.receipts, self.sources, self.runtime = (self.base / name for name in ('receipts', 'sources', 'runtime'))
        for path in (self.root, self.v4, self.history, self.receipts, self.sources, self.runtime):
            path.mkdir(mode=0o700)
        self.stdlib = self.runtime / 'stdlib'
        self.stdlib.mkdir(mode=0o700)
        self.python, self.library = self.runtime / 'python', self.runtime / 'lib.so.1'
        for path in (self.stdlib / 'synthetic.py', self.python, self.library):
            self.write(path, b'synthetic-runtime')
        for name in v.SOURCE_NAMES:
            self.write(self.sources / name, b'# synthetic-source\n')
        self.statuses = {self.base / 'platform-status', self.base / 'timesheets-status'}
        for path in self.statuses:
            self.write(path, b'in-progress')
        for name in ('README.md', 'validation.json', 'user-supplied-receipt.json', 'SHA256SUMS', 'packet-v4-offline-recheck.json'):
            self.write(self.receipts / name, b'synthetic-receipt')
        for index in range(348):
            self.write(self.history / f'historical-{index}', b'synthetic-historical')
        self.changed_history = self.history / 'historical-0'
        history = {str(self.history): {p.name: {'kind': 'file', **self.record(p)} for p in self.history.iterdir()}}
        self.write_json(self.v4 / 'preservation-inventory.json', {'roots': history})
        for index in range(138):
            self.write(self.v4 / f'payload-{index}', b'synthetic-v4-payload')
        v4_files = {p.name: self.record(p) for p in self.v4.iterdir()}
        self.write_json(self.v4 / 'packet-v4.json', {'files': v4_files})
        self.v4_sha = v.sha((self.v4 / 'packet-v4.json').read_bytes())
        before = {}
        for root in (self.v4, self.history, self.receipts):
            before.update({str(p): self.record(p) for p in root.iterdir()})
        before.update({str(p): self.record(p) for p in self.statuses})
        self.assertEqual(len(before), 495)
        self.write_json(self.root / 'preservation-before.json', {'files': before})
        self.before_sha = v.sha((self.root / 'preservation-before.json').read_bytes())
        runtime_files = {str(p): self.record(p) for p in (self.python, self.library, self.stdlib / 'synthetic.py')}
        self.write_json(self.root / 'runtime-inventory.json', {
            'files': runtime_files, 'dynamicDependencies': [str(self.library)], 'stdlibSymlinks': {},
            'interpreterFlags': ['-I', '-S', '-B'], 'executionQualified': False})
        self.write_json(self.root / 'source-inventory.json', {'files': {
            str(p): self.record(p) for p in self.sources.iterdir()}})
        self.write_json(self.root / 'packet-v1.json', {
            'kind': 'hexalith.native-measurement-supplement', 'operationalGate': 'closed',
            'v4Sha256': self.v4_sha, 'policyCeilingEnds': v.CEILING,
            'files': {p.name: self.record(p) for p in self.root.iterdir()}})
        self.anchor = v.sha((self.root / 'packet-v1.json').read_bytes())
        values = {'ROOT': self.root, 'V4': self.v4, 'V4_SHA': self.v4_sha,
                  'PRESERVATION_SHA': self.before_sha, 'HISTORY': (self.history,),
                  'RECEIPT_ROOT': self.receipts, 'STATUS_FILES': self.statuses,
                  'SOURCE_ROOT': self.sources, 'STDLIB': self.stdlib, 'PYTHON': self.python,
                  'REQUIRED_LIBRARIES': {str(self.library)}}
        self.stack = contextlib.ExitStack()
        self.addCleanup(self.stack.close)
        self.stack.enter_context(patch.multiple(v, **values))
        self.stack.enter_context(patch.object(v, 'permitted_runtime', side_effect=lambda path: path.is_relative_to(self.runtime)))

    def write(self, path, raw):
        path.write_bytes(raw)
        path.chmod(0o600)

    def write_json(self, path, value):
        self.write(path, (json.dumps(value, sort_keys=True) + '\n').encode())

    def record(self, path):
        raw = path.read_bytes()
        return {'bytes': len(raw), 'sha256': v.sha(raw)}

    def test_complete_synthetic_packet_passes(self):
        result = v.verify(self.anchor)
        self.assertEqual(result['preservationFilesUnchanged'], 495)
        self.assertEqual(result['runtimeFiles'], 3)
        self.assertEqual(result['sourceFiles'], 6)
        self.assertEqual(result['operationalGate'], 'closed')

    def test_verify_detects_history_source_and_runtime_change_before_final_acceptance(self):
        for path in (self.changed_history, self.sources / 'native_measurement_adapter.py', self.python):
            with self.subTest(path=path.name):
                raw = path.read_bytes()
                original = v.read_file
                count = 0
                def mutate_after_initial_validation(candidate, private=False):
                    nonlocal count
                    result = original(candidate, private)
                    if candidate == self.root / 'packet-v1.json':
                        count += 1
                        if count == 2:
                            path.write_bytes(b'changed-synthetic-external-input')
                    return result
                try:
                    with patch.object(v, 'read_file', side_effect=mutate_after_initial_validation):
                        with self.assertRaises(v.Refusal):
                            v.verify(self.anchor)
                finally:
                    path.write_bytes(raw)

    def test_expiry_uses_clock_after_final_checks(self):
        class Clock:
            @staticmethod
            def now(tz):
                return datetime(2026, 10, 16, 16, 27, 56, tzinfo=timezone.utc)
            fromisoformat = staticmethod(datetime.fromisoformat)
        original = v.recheck_external
        clock_after_check = []
        def check(*args):
            value = original(*args)
            clock_after_check.append(True)
            return value
        class AssertClock(Clock):
            @staticmethod
            def now(tz):
                if not clock_after_check:
                    raise AssertionError('clock sampled before final checks')
                return Clock.now(tz)
        with patch.object(v, 'recheck_external', side_effect=check), patch.object(v, 'datetime', AssertClock):
            self.assertTrue(v.verify(self.anchor)['candidateExpired'])


if __name__ == '__main__':
    unittest.main(verbosity=2)
