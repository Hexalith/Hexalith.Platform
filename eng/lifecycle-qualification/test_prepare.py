"""Resource-free cache poisoning, fixed-input and source-gate controls; no live credit."""
from copy import deepcopy
from contextlib import redirect_stderr, redirect_stdout
import hashlib
import importlib.util
import io
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

SPEC = importlib.util.spec_from_file_location('lifecycle_prepare', Path(__file__).with_name('prepare.py'))
P = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(P)


class PreparationTests(unittest.TestCase):
    def inputs(self):
        return {'schema': P.SCHEMA, 'policyId': P.POLICY,
                'sources': {'caller': '1' * 40, 'builds': '2' * 40, 'platform': '3' * 40},
                'minimumPlatformRevision': '4' * 40,
                'tool': {'version': '5.0.0', 'sourceRevision': '2' * 40,
                         'framework': 'net11.0', 'downloadId': 'module-tool'},
                'catalogSha256': '5' * 64,
                'toolchain': {'dotnetSdk': '11.0.100', 'aspire': '13.6.5',
                              'daprCli': '1.19.1', 'daprRuntime': '1.19.1'},
                'downloads': [{'id': 'module-tool', 'fileName': 'tool.5.0.0.nupkg',
                               'sha256': hashlib.sha256(b'verified package bytes').hexdigest()}]}

    def fixture(self, root):
        inputs = self.inputs()
        downloads = P.private(root / 'downloads')
        (downloads / inputs['downloads'][0]['fileName']).write_bytes(b'verified package bytes')
        return inputs, downloads, root / 'cache'

    def test_every_relevant_source_catalog_package_and_toolchain_change_separates_cache(self):
        original = self.inputs()
        for group, field, changed in (('sources', 'caller', 'a' * 40), ('sources', 'builds', 'b' * 40),
                ('sources', 'platform', 'c' * 40), ('tool', 'version', '5.0.1'),
                ('tool', 'sourceRevision', 'd' * 40), ('tool', 'framework', 'net10.0'),
                ('toolchain', 'dotnetSdk', '11.0.101'), ('toolchain', 'aspire', '13.6.6'),
                ('toolchain', 'daprCli', '1.19.2'), ('toolchain', 'daprRuntime', '1.19.2')):
            with self.subTest(group=group, field=field):
                value = deepcopy(original)
                value[group][field] = changed
                self.assertNotEqual(P.key(value), P.key(original))
        for field in ('catalogSha256', 'minimumPlatformRevision'):
            value = deepcopy(original)
            value[field] = 'e' * len(value[field])
            self.assertNotEqual(P.key(value), P.key(original))
        value = deepcopy(original)
        value['downloads'][0]['sha256'] = 'f' * 64
        self.assertNotEqual(P.key(value), P.key(original))

    def test_unsafe_names_duplicate_inputs_unknown_fields_and_results_are_refused(self):
        for filename in ('../tool.nupkg', '/tool.nupkg', 'folder/tool.zip', 'test-result.json', 'journal.json', 'state.txt'):
            value = self.inputs()
            value['downloads'][0]['fileName'] = filename
            with self.subTest(filename=filename), self.assertRaises(P.Refusal):
                P.validate(value)
        value = self.inputs()
        value['downloads'].append(value['downloads'][0].copy())
        with self.assertRaises(P.Refusal):
            P.validate(value)
        with self.assertRaises(P.Refusal):
            P.validate({**self.inputs(), 'reusedResult': 'PRIVATE_PAYLOAD'})
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / 'inputs.json'
            path.write_text('{"schema":"one","schema":"two"}')
            with self.assertRaises(P.Refusal):
                P.read_inputs(path)

    def test_verified_cache_hit_revalidates_and_each_restore_has_independent_private_bytes(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            inputs, downloads, cache = self.fixture(root)
            entry, hit = P.populate(cache, inputs, downloads)
            self.assertFalse(hit)
            downloads.rename(root / 'absent-downloads')
            self.assertEqual(P.populate(cache, inputs, downloads), (entry, True))
            first = P.restore(cache, inputs, root / 'one')
            second = P.restore(cache, inputs, root / 'two')
            filename = inputs['downloads'][0]['fileName']
            self.assertNotEqual((entry / filename).stat().st_ino, (first / filename).stat().st_ino)
            self.assertNotEqual((first / filename).stat().st_ino, (second / filename).stat().st_ino)
            self.assertEqual(first.stat().st_mode & 0o077, 0)
            self.assertEqual((first / filename).stat().st_mode & 0o077, 0)
            (first / filename).write_bytes(b'job-local change')
            self.assertEqual((second / filename).read_bytes(), b'verified package bytes')
            P.verify_entry(entry, inputs)

    def test_poisoned_hit_and_restore_fail_without_repair_or_published_partial_state(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            inputs, downloads, cache = self.fixture(root)
            entry, _ = P.populate(cache, inputs, downloads)
            filename = inputs['downloads'][0]['fileName']
            (entry / filename).write_bytes(b'poisoned cache')
            for operation in (lambda: P.populate(cache, inputs, downloads),
                              lambda: P.restore(cache, inputs, root / 'run')):
                with self.assertRaises(P.Refusal) as caught:
                    operation()
                self.assertEqual(caught.exception.code, 'HXLQ303')
            self.assertFalse((root / 'run').exists())
            self.assertEqual((entry / filename).read_bytes(), b'poisoned cache')

    def test_missing_cache_entry_is_not_created_by_restore(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            inputs, _, cache = self.fixture(root)
            with self.assertRaises(FileNotFoundError):
                P.restore(cache, inputs, root / 'run')
            self.assertFalse((cache / 'entries' / P.key(inputs)).exists())
            self.assertFalse((root / 'run').exists())

    def test_bad_download_cannot_publish_a_cache_entry_and_other_entries_survive(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            inputs, downloads, cache = self.fixture(root)
            good, _ = P.populate(cache, inputs, downloads)
            changed = deepcopy(inputs)
            changed['toolchain']['aspire'] = '13.6.6'
            (downloads / inputs['downloads'][0]['fileName']).write_bytes(b'PRIVATE_BAD_DOWNLOAD')
            with self.assertRaises(P.Refusal):
                P.populate(cache, changed, downloads)
            self.assertEqual(list((cache / 'entries').iterdir()), [good])
            P.verify_entry(good, inputs)

    def test_symlink_and_hardlink_downloads_or_cache_files_are_never_consumed(self):
        for linked in ('symlink', 'hardlink'):
            with self.subTest(linked=linked), tempfile.TemporaryDirectory() as temporary:
                root = Path(temporary)
                inputs, downloads, cache = self.fixture(root)
                entry, _ = P.populate(cache, inputs, downloads)
                path = entry / inputs['downloads'][0]['fileName']
                foreign = root / 'foreign.nupkg'
                foreign.write_bytes(path.read_bytes())
                path.unlink()
                path.symlink_to(foreign) if linked == 'symlink' else os.link(foreign, path)
                with self.assertRaises(P.Refusal) as caught:
                    P.restore(cache, inputs, root / 'run')
                self.assertEqual(caught.exception.code, 'HXLQ302')
                self.assertFalse((root / 'run').exists())
                self.assertEqual(foreign.read_bytes(), b'verified package bytes')

    def test_stale_metadata_extra_state_and_parent_links_fail_before_restore(self):
        for fault in ('metadata', 'old-run-state', 'parent-link'):
            with self.subTest(fault=fault), tempfile.TemporaryDirectory() as temporary:
                root = Path(temporary)
                inputs, downloads, cache = self.fixture(root)
                entry, _ = P.populate(cache, inputs, downloads)
                if fault == 'metadata':
                    (entry / 'inputs.json').write_bytes(b'{}')
                elif fault == 'old-run-state':
                    (entry / 'journal.json').write_bytes(b'{}')
                else:
                    (root / 'linked').symlink_to(cache, target_is_directory=True)
                    cache = root / 'linked'
                with self.assertRaises(P.Refusal):
                    P.restore(cache, inputs, root / 'run')
                self.assertFalse((root / 'run').exists())

    def test_nonfresh_destination_or_cache_nested_state_is_refused_and_preserved(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            inputs, downloads, cache = self.fixture(root)
            P.populate(cache, inputs, downloads)
            destination = P.private(root / 'existing')
            (destination / 'journal.json').write_text('PRIVATE_OLD_STATE')
            for path in (destination, cache / 'run', cache / 'locks' / 'run'):
                with self.subTest(path=path), self.assertRaises(P.Refusal):
                    P.restore(cache, inputs, path)
            self.assertEqual((destination / 'journal.json').read_text(), 'PRIVATE_OLD_STATE')

    def test_destination_created_during_restore_is_never_replaced(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            inputs, downloads, cache = self.fixture(root)
            P.populate(cache, inputs, downloads)
            publish = P.publish_directory
            destination = root / 'run'

            def competing_creator(source, target):
                target.mkdir(mode=0o700)
                publish(source, target)

            with patch.object(P, 'publish_directory', side_effect=competing_creator), self.assertRaises(P.Refusal):
                P.restore(cache, inputs, destination)
            self.assertTrue(destination.is_dir())
            self.assertEqual(list(destination.iterdir()), [])
            self.assertFalse(any(root.glob('.lifecycle-downloads-*')))

    def test_cli_redacts_paths_decoder_errors_and_unexpected_exceptions(self):
        with tempfile.TemporaryDirectory(prefix='PRIVATE_PAYLOAD_') as temporary:
            path = Path(temporary) / 'inputs.json'
            path.write_text('PRIVATE_JSON_PAYLOAD')
            stdout, stderr = io.StringIO(), io.StringIO()
            with redirect_stdout(stdout), redirect_stderr(stderr):
                code = P.main(['key', '--inputs', str(path)])
            self.assertEqual(code, 1)
            self.assertEqual(stdout.getvalue(), '')
            result = json.loads(stderr.getvalue())
            self.assertEqual(result, {'failureCode': 'HXLQ305', 'redacted': True,
                                      'candidateAcceptance': False, 'toolAccepted': False})
            self.assertNotIn('PRIVATE', stderr.getvalue())

    def test_source_audit_detects_missing_real_port_binding_and_pin_drift_without_changes(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)

            def git(directory, *args):
                return subprocess.run(['git', '-c', 'commit.gpgsign=false', '-c', 'user.name=Control',
                    '-c', 'user.email=control@example.invalid', *args], cwd=directory,
                    check=True, capture_output=True, text=True).stdout.strip()

            def commit(directory):
                git(directory, 'add', '.')
                git(directory, 'commit', '-qm', 'test: create isolated preparation fixture')
                return git(directory, 'rev-parse', 'HEAD')

            platform = P.private(root / 'platform')
            git(platform, 'init', '-q')
            (platform / 'model.txt').write_text('original endpoint')
            old = commit(platform)
            (platform / 'model.txt').write_text('bound endpoint')
            bound = commit(platform)
            caller = P.private(root / 'caller')
            git(caller, 'init', '-q')
            builds = P.private(caller / 'references/Hexalith.Builds')
            git(builds, 'init', '-q')
            (builds / 'Props').mkdir()
            catalog = (b'<Project><HexalithAspireAppHostSdkVersion>13.6.5</HexalithAspireAppHostSdkVersion>'
                       b'<HexalithDaprCliVersion>1.19.1</HexalithDaprCliVersion>'
                       b'<HexalithDaprRuntimeVersion>1.19.1</HexalithDaprRuntimeVersion></Project>\n')
            (builds / 'Props/Directory.Packages.props').write_bytes(catalog)
            builds_sha = commit(builds)
            (caller / '.config').mkdir()
            (caller / '.config/dotnet-tools.json').write_text(json.dumps({'tools': {
                'hexalith.builds.module.cli': {'version': '5.0.0', 'rollForward': False}}}))
            (caller / 'global.json').write_text('{"sdk":{"version":"11.0.100"}}')
            (caller / 'references/Hexalith.Platform').mkdir()
            (caller / '.gitmodules').write_text('\n'.join(
                '[submodule "' + name + '"]\npath = references/Hexalith.' + name + '\nurl = https://example.invalid/' + name
                for name in ('Builds', 'Platform')))
            git(caller, 'add', '.config', '.gitmodules', 'global.json')
            for name, revision in (('Builds', builds_sha), ('Platform', old)):
                git(caller, 'update-index', '--add', '--cacheinfo', '160000,' + revision + ',references/Hexalith.' + name)
            git(caller, 'commit', '-qm', 'test: bind isolated preparation sources')
            inputs = self.inputs()
            inputs['sources'].update(caller=git(caller, 'rev-parse', 'HEAD'), builds=builds_sha, platform=old)
            inputs['catalogSha256'] = hashlib.sha256(catalog).hexdigest()
            inputs['minimumPlatformRevision'] = bound
            before = git(caller, 'status', '--porcelain')
            result = P.source_audit(caller, platform, inputs)
            self.assertEqual(result['reasons'], ['platform-live-port-binding-unavailable'])
            self.assertFalse(result['sourceBindingsMatch'])
            self.assertFalse(result['publicationVerified'])
            self.assertFalse(result['candidateAcceptance'])
            self.assertEqual(git(caller, 'status', '--porcelain'), before)
            # A final publication check is independently required even when every selected source agrees.
            inputs['minimumPlatformRevision'] = old
            self.assertTrue(P.source_audit(caller, platform, inputs)['sourceBindingsMatch'])
            inputs['tool']['version'] = '5.0.1'
            self.assertEqual(P.source_audit(caller, platform, inputs)['reasons'], ['caller-tool-mismatch'])
            inputs['tool']['version'] = '5.0.0'
            inputs['toolchain']['daprRuntime'] = '1.19.2'
            self.assertEqual(P.source_audit(caller, platform, inputs)['reasons'], ['toolchain-daprRuntime-mismatch'])


if __name__ == '__main__':
    unittest.main()
