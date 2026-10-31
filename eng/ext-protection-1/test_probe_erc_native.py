"""Offline ownership/pinning/source controls; never starts a container or invokes native tests."""
import hashlib
import importlib.util
from pathlib import Path
import subprocess
import tempfile
import json
import unittest

SPEC = importlib.util.spec_from_file_location('erc_probe_test', Path(__file__).with_name('probe_erc_native.py'))
probe = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(probe)


class NativeHarnessControls(unittest.TestCase):
    def test_invalid_assembly_pin_refuses_before_docker(self):
        calls = []
        def runner(arguments, data=None):
            calls.append(arguments)
            raise AssertionError('Unexpected native command')
        value = probe.ErcProbe('/missing/owned.dll', '0' * 64, runner)
        result, code = value.run()
        self.assertEqual(1, code)
        self.assertFalse(calls)
        self.assertEqual('BlockedUnknown', result['outcome'])
        self.assertFalse(result['productionQualified'])
        self.assertFalse(result['liveExecuted'])

    def test_missing_local_image_cannot_create_or_pull(self):
        assembly = probe.ROOT / 'tests/Hexalith.Platform.Protection.Tests/bin/Debug/net10.0/Hexalith.Platform.Protection.Tests.dll'
        if not assembly.is_file():
            self.skipTest('Build the owned Debug test assembly first')
        calls = []
        def runner(arguments, data=None):
            calls.append(arguments)
            return subprocess.CompletedProcess(arguments, 1, b'', b'local image absent')
        value = probe.ErcProbe(assembly, hashlib.sha256(assembly.read_bytes()).hexdigest(), runner)
        result, code = value.run()
        self.assertEqual(1, code)
        self.assertEqual([['image', 'inspect', probe.base.IMAGE]], calls)
        self.assertFalse(value.creation_attempted)
        self.assertTrue(result['cleanup']['complete'])

    def test_source_has_only_the_narrow_five_native_cases(self):
        self.assertEqual(5, len(probe.CASES))
        self.assertEqual(5, len(set(probe.CASES)))
        source = Path(probe.__file__).read_text()
        self.assertIn("'-class', 'Hexalith.Platform.Protection.Tests.OpenBaoErasureRootIntegrationTests'", source)
        self.assertNotIn('ext_protection_1.py', source)
        self.assertNotIn("'snapshot'", source)

    def test_runtime_policy_evidence_requires_both_exact_rendered_tenant_policies(self):
        script = probe.ROOT / 'deploy/erasure-root/bootstrap.py'
        specification = importlib.util.spec_from_file_location('erc_native_policy_control', script)
        shipped = importlib.util.module_from_spec(specification)
        specification.loader.exec_module(shipped)
        enrollment = {'transitMount': 'transit-owned', 'headMount': 'heads-owned', 'nativeKeys': {'tenant-a': 'epoch-a', 'tenant-b': 'epoch-b'}}
        fragment_hash = hashlib.sha256(script.with_name('custody-policy.hcl').read_bytes()).hexdigest()
        for mutation in (None, 'fragment-only', 'missing-tenant', 'foreign-scope', 'shared-policy', 'duplicate-tenant'):
            with self.subTest(mutation=mutation), tempfile.TemporaryDirectory() as directory:
                for tenant in ('tenant-a', 'tenant-b'):
                    if mutation == 'missing-tenant' and tenant == 'tenant-b':
                        continue
                    scope = tenant + ':custody:key-lifecycle-v1|custody-key-lifecycle-candidate-v1'
                    value = {'shippedPolicySha256': fragment_hash, 'renderedPolicySha256': hashlib.sha256(shipped.render_policy(enrollment, tenant, scope).encode()).hexdigest(),
                             'transitMount': enrollment['transitMount'], 'headMount': enrollment['headMount'], 'runtimePolicyName': shipped.policy_name(tenant, scope),
                             'tenantId': tenant, 'scope': scope, 'rootUsedOnlyForBootstrap': True}
                    if mutation == 'fragment-only':
                        value['renderedPolicySha256'] = fragment_hash
                    elif mutation == 'foreign-scope':
                        value['scope'] = 'tenant-foreign:custody:key-lifecycle-v1|custody-key-lifecycle-candidate-v1'
                    elif mutation == 'shared-policy':
                        value['runtimePolicyName'] = 'erc-shared-runtime'
                    (Path(directory) / ('policy-' + tenant + '.json')).write_text(json.dumps(value))
                    if mutation == 'duplicate-tenant' and tenant == 'tenant-a':
                        (Path(directory) / 'policy-duplicate.json').write_text(json.dumps(value))
                if mutation is None:
                    self.assertEqual([value['tenantId'] for value in probe.read_runtime_policies(directory)], ['tenant-a', 'tenant-b'])
                else:
                    with self.assertRaises(probe.base.Blocked):
                        probe.read_runtime_policies(directory)

    def test_frozen_runtime_tamper_or_manifest_hash_mismatch_refuses_before_docker(self):
        for tamper in ('file', 'manifest-hash'):
            with self.subTest(tamper=tamper), tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                assembly = root / 'Hexalith.Platform.Protection.Tests.dll'
                assembly.write_bytes(b'pinned-offline-test-runtime')
                assembly_hash = hashlib.sha256(assembly.read_bytes()).hexdigest()
                manifest = root / 'runtime-manifest.json'
                manifest.write_text(json.dumps({'files': [{'path': assembly.name, 'sha256': assembly_hash}]}))
                manifest_hash = hashlib.sha256(manifest.read_bytes()).hexdigest()
                if tamper == 'file':
                    assembly.write_bytes(b'changed-runtime')
                else:
                    manifest_hash = '0' * 64
                calls = []
                def runner(arguments, data=None):
                    calls.append(arguments)
                    raise AssertionError('Unexpected native command')
                result, code = probe.ErcProbe(assembly, assembly_hash, runner=runner, runtime_manifest=manifest, runtime_manifest_sha256=manifest_hash).run()
                self.assertEqual(code, 1)
                self.assertFalse(calls)
                self.assertFalse(result['cleanup'].get('complete') is False)
                self.assertEqual(result['outcome'], 'BlockedUnknown')

    def test_unlisted_runtime_assembly_refuses_before_image_inspect_without_recording_the_manifest_hash(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            assembly = root / 'Hexalith.Platform.Protection.Tests.dll'
            assembly.write_bytes(b'pinned-offline-test-runtime')
            extra = root / 'Hexalith.Platform.Protection.dll'
            extra.write_bytes(b'mutated-unlisted-application')
            assembly_hash = hashlib.sha256(assembly.read_bytes()).hexdigest()
            manifest = root / 'runtime-manifest.json'
            manifest.write_text(json.dumps({'files': [{'path': assembly.name, 'sha256': assembly_hash}]}))
            manifest_hash = hashlib.sha256(manifest.read_bytes()).hexdigest()
            calls = []
            def runner(arguments, data=None):
                calls.append(arguments)
                raise AssertionError('Unexpected native command')
            result, code = probe.ErcProbe(assembly, assembly_hash, runner=runner, runtime_manifest=manifest, runtime_manifest_sha256=manifest_hash).run()
            self.assertEqual(code, 1)
            self.assertFalse(calls)
            self.assertNotIn('frozenRuntimeManifestSha256', result)
            self.assertEqual(result['outcome'], 'BlockedUnknown')

    def test_shared_container_creation_is_offline_tmpfs_and_create_only(self):
        arguments = probe.base.create_args(probe.base.IMAGE, 'a' * 32, 'owned-native-test')
        self.assertIn('--pull=never', arguments)
        self.assertIn('--network=none', arguments)
        self.assertIn('--read-only', arguments)
        self.assertIn('--log-driver=none', arguments)
        self.assertNotIn('--mount', arguments)
        self.assertEqual(3, arguments.count('--tmpfs'))
        self.assertIn('max_entry_size = 67108864', probe.ERC_CONFIG)
        self.assertIn('max_transaction_size = 67108864', probe.ERC_CONFIG)
        prepared = (probe.ROOT / 'deploy/erasure-root/statefulset.yaml').read_text()
        for limit in ('max_entry_size = 67108864', 'max_transaction_size = 67108864', 'max_request_size = 67108864'):
            self.assertIn(limit, prepared)
        self.assertIn('max_request_size = 67108864', probe.ERC_CONFIG)
        self.assertIn('max_request_json_strings = 1000000', probe.ERC_CONFIG)
        self.assertIn('max_request_json_memory = 134217728', probe.ERC_CONFIG)


if __name__ == '__main__':
    unittest.main()
