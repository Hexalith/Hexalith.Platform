"""Offline ownership/pinning/source controls; never starts a container or invokes native tests."""
import hashlib
import importlib.util
from pathlib import Path
import subprocess
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

    def test_shared_container_creation_is_offline_tmpfs_and_create_only(self):
        arguments = probe.base.create_args(probe.base.IMAGE, 'a' * 32, 'owned-native-test')
        self.assertIn('--pull=never', arguments)
        self.assertIn('--network=none', arguments)
        self.assertIn('--read-only', arguments)
        self.assertIn('--log-driver=none', arguments)
        self.assertNotIn('--mount', arguments)
        self.assertEqual(3, arguments.count('--tmpfs'))
        self.assertIn('max_entry_size = 67108864', probe.ERC_CONFIG)
        self.assertIn('max_request_size = 67108864', probe.ERC_CONFIG)
        self.assertIn('max_request_json_strings = 1000000', probe.ERC_CONFIG)
        self.assertIn('max_request_json_memory = 134217728', probe.ERC_CONFIG)


if __name__ == '__main__':
    unittest.main()
