"""Exercise real isolated Git histories for the exact 412-case historical source lane."""
import copy
import json
from pathlib import Path
import subprocess
import tempfile
import unittest

import pre_extension_tuple_source as history


class PreExtensionSourceTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.git('init', '-q')
        self.git('config', 'user.name', 'Pre-extension source fixture')
        self.git('config', 'user.email', 'fixture@invalid')
        root = Path(__file__).parents[1]
        self.write('eng/tuple-ci-suites.json', (root / 'eng/tuple-ci-suites.json').read_bytes())
        self.provenance = json.loads((root / history.PROVENANCE).read_bytes())
        # Explicit immutable SDK4.1/T705/M25 fixture, independent from a future default adoption.
        self.provenance['eventstore'].update(version='4.1.0', commit='dbc6e7e08d605ba6c6c790e3e08a15dc8bbdec8a')
        self.write(history.PROVENANCE, (json.dumps(self.provenance, indent=2) + '\n').encode())
        fixture_root = root / 'tests/Hexalith.Platform.Composition.Tests/Fixtures'
        self.write('.config/dotnet-tools.json', (fixture_root / 'pre-extension-dotnet-tools.json').read_bytes())
        self.write('eng/builds-tuple.json', (fixture_root / 'pre-extension-builds-tuple.json').read_bytes())
        self.write('hexalith-platform.json', (fixture_root / 'pre-extension-platform-identity.json').read_bytes())
        self.write('src/Owner.cs', b'unchanged production source\n')
        self.write('tests/Control.cs', b'unchanged controls\n')
        self.gitlink('references/Hexalith.Builds', history.BUILDS)
        self.gitlink('references/Hexalith.EventStore', history.EVENTSTORE)
        self.descriptor = dict(schema='hexalith.platform.pre-extension-tuple-source.v1', purpose='same-code-sdk41-tuple-regression',
                               sourceRevision=None, buildsSource=history.BUILDS, eventStoreSource=history.EVENTSTORE,
                               toolVersion='4.45.0', profileSha256=history.PROFILE_HASH, requiredCount=412, qualificationCredit=False)
        self.write_descriptor()
        self.old = self.commit()
        self.descriptor['sourceRevision'] = self.old
        self.write_descriptor()
        self.sdk42 = copy.deepcopy(self.provenance)
        self.sdk42['eventstore'].update(version='4.2.0', commit=history.SDK42_SOURCE)
        self.write(history.PROVENANCE, json.dumps(self.sdk42).encode())
        self.gitlink('references/Hexalith.EventStore', history.SDK42_SOURCE)
        self.current = self.commit()

    def git(self, *args):
        return subprocess.check_output(['git', *args], cwd=self.root, stderr=subprocess.PIPE).decode().strip()

    def gitlink(self, path, source):
        (self.root / path).mkdir(parents=True, exist_ok=True)
        self.git('update-index', '--add', '--cacheinfo', '160000,' + source + ',' + path)

    def write(self, path, raw):
        target = self.root / path
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(raw)

    def write_descriptor(self):
        self.write(history.DESCRIPTOR, json.dumps(self.descriptor).encode())

    def commit(self):
        self.git('add', '.')
        self.git('commit', '-q', '-m', 'test(tuple): record private historical source fixture')
        return self.git('rev-parse', 'HEAD')

    def test_exact_same_code_and_sdk42_metadata_preserve_all_412_cases(self):
        self.assertEqual(history.resolve(self.root, self.current), self.old)
        self.git('checkout', '-q', self.old)
        before = self.git('status', '--porcelain')
        result = history.verify(self.root, self.current, self.old)
        self.assertEqual(result['requiredCount'], 412)
        self.assertEqual(result['sourceEquivalence'], 'verified')
        self.assertFalse(result['qualificationCredit'])
        self.assertFalse(result['binaryEquivalenceClaimed'])
        self.assertEqual(self.git('status', '--porcelain'), before)

    def test_real_checkout_and_committed_dispatch_binding_are_mandatory(self):
        with self.assertRaises(ValueError):
            history.verify(self.root, self.current, self.old)
        with self.assertRaises(ValueError):
            history.resolve(self.root, self.current, self.current)
        for value in [None, 'pending', 'a' * 39, 'A' * 40]:
            with self.subTest(value=value), self.assertRaises(ValueError):
                history.resolve(self.root, value)

    def test_changed_production_controls_or_helpers_refuse(self):
        for name in ['src/Owner.cs', 'tests/Control.cs', 'eng/helper.py', 'Directory.Build.props', '.github/workflows/tuple-ci.yml']:
            self.git('checkout', '-q', self.current)
            self.write(name, b'changed\n')
            changed = self.commit()
            self.git('checkout', '-q', self.old)
            with self.subTest(path=name), self.assertRaisesRegex(ValueError, 'Production, controls'):
                history.verify(self.root, changed, self.old)

    def test_crossed_source_and_provenance_fields_never_substitute(self):
        for field, value in [('commit', 'f' * 40), ('version', '4.2.1'), ('sha256', '0' * 64), ('source', 'foreign.json'), ('repository', 'Foreign.Owner')]:
            self.git('checkout', '-q', self.current)
            selected = copy.deepcopy(self.sdk42)
            selected['eventstore'][field] = value
            self.write(history.PROVENANCE, json.dumps(selected).encode())
            changed = self.commit()
            self.git('checkout', '-q', self.old)
            with self.subTest(field=field), self.assertRaises(ValueError):
                history.verify(self.root, changed, self.old)
        self.git('checkout', '-q', self.current)
        self.gitlink('references/Hexalith.EventStore', 'e' * 40)
        changed = self.commit()
        self.git('checkout', '-q', self.old)
        with self.assertRaisesRegex(ValueError, 'exact published source'):
            history.verify(self.root, changed, self.old)

    def test_each_allowed_json_surface_refuses_unrelated_changes(self):
        mutations = [
            ('.config/dotnet-tools.json', lambda d: d.update(isRoot=False)),
            ('.config/dotnet-tools.json', lambda d: d['tools']['hexalith.builds.module.cli'].update(commands=['foreign'])),
            ('.config/dotnet-tools.json', lambda d: d['tools']['hexalith.builds.module.cli'].update(rollForward=True)),
            ('.config/dotnet-tools.json', lambda d: d['tools'].update(foreign={'version': '1.0.0'})),
            ('hexalith-platform.json', lambda d: d.update(schemaVersion=2)),
            ('hexalith-platform.json', lambda d: d.update(unknown='ignored')),
            ('hexalith-platform.json', lambda d: d.update(toolVersionRange='[1.0.0, 6.0.0)')),
            ('eng/builds-tuple.json', lambda d: d.update(unknown='ignored')),
            ('eng/builds-tuple.json', lambda d: d['producer'].update(repository='https://github.com/Foreign/Other.git')),
            ('eng/builds-tuple.json', lambda d: d['producer']['catalog'].update(path='other.props')),
            ('eng/builds-tuple.json', lambda d: d['publication'].update(inventoryUrl='https://example.invalid/inventory.json')),
            ('eng/builds-tuple.json', lambda d: d['publication'].update(sourceRevision='f' * 40)),
            ('eng/builds-tuple.json', lambda d: d['publication'].update(archiveSha256='invalid')),
        ]
        for path, mutate in mutations:
            self.git('checkout', '-q', self.current)
            document = json.loads((self.root / path).read_bytes())
            mutate(document)
            self.write(path, json.dumps(document).encode())
            changed = self.commit()
            self.git('checkout', '-q', self.old)
            with self.subTest(path=path, document=document), self.assertRaises(ValueError):
                history.verify(self.root, changed, self.old)

    def test_parties_source_has_no_unused_exemption(self):
        self.git('checkout', '-q', self.current)
        self.gitlink('references/Hexalith.Parties', 'd' * 40)
        changed = self.commit()
        self.git('checkout', '-q', self.old)
        with self.assertRaisesRegex(ValueError, 'unapproved metadata'):
            history.verify(self.root, changed, self.old)

    def test_private_tuple_identity_transformation_changes_only_bound_version_fields(self):
        # These deliberately synthetic private Git/package identities prove only the metadata comparison.
        self.git('checkout', '-q', self.current)
        document = json.loads((self.root / 'eng/builds-tuple.json').read_bytes())
        document['producer']['sourceRevision'] = 'e' * 40
        document['publication'].update(version='4.999.0', sourceRevision='e' * 40, releaseTag='v4.999.0',
            inventoryUrl='https://github.com/Hexalith/Hexalith.Builds/releases/download/v4.999.0/g4-tool-package-inventory.json')
        self.write('eng/builds-tuple.json', json.dumps(document).encode())
        self.gitlink('references/Hexalith.Builds', 'e' * 40)
        tools = json.loads((self.root / '.config/dotnet-tools.json').read_bytes())
        tools['tools']['hexalith.builds.module.cli']['version'] = '4.999.0'
        self.write('.config/dotnet-tools.json', json.dumps(tools).encode())
        identity = json.loads((self.root / 'hexalith-platform.json').read_bytes())
        identity['toolVersionRange'] = '[4.999.0, 5.0.0-0)'
        self.write('hexalith-platform.json', json.dumps(identity).encode())
        changed = self.commit()
        self.git('checkout', '-q', self.old)
        result = history.verify(self.root, changed, self.old)
        self.assertFalse(result['qualificationCredit'])
        self.assertEqual(result['sourceEquivalence'], 'verified')

    def test_positive_counts_and_exact_old_tool_are_immutable(self):
        for field, value in [('requiredCount', 411), ('toolVersion', '4.45.1'), ('profileSha256', '0' * 64), ('qualificationCredit', True)]:
            self.git('checkout', '-q', self.current)
            selected = copy.deepcopy(self.descriptor)
            selected[field] = value
            self.write(history.DESCRIPTOR, json.dumps(selected).encode())
            changed = self.commit()
            with self.subTest(field=field), self.assertRaisesRegex(ValueError, 'historical obligations'):
                history.resolve(self.root, changed)


if __name__ == '__main__':
    unittest.main()
