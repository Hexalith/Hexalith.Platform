import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('custody_prepare_image', Path(__file__).with_name('prepare-image.py'))
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class PrepareImageTests(unittest.TestCase):
    def test_hash_bound_complete_output_and_exclusive_context(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory); publish = root / 'publish'; publish.mkdir()
            dll = publish / 'Hexalith.Platform.Custody.Dapr.dll'; dll.write_bytes(b'disposable-test-output')
            inventory = root / 'inventory.json'; runtime = 'synthetic.invalid/runtime@sha256:' + 'a' * 64
            inventory.write_text(json.dumps({'runtimeImage': runtime, 'files': {dll.name: hashlib.sha256(dll.read_bytes()).hexdigest()}}))
            digest = hashlib.sha256(inventory.read_bytes()).hexdigest()
            output = root / 'context'
            result = module.prepare(publish, inventory, digest, output, runtime)
            self.assertEqual('OfflineContextPrepared', result['status'])
            self.assertFalse(result['imageBuilt'])
            self.assertEqual('FROM ' + runtime, (output / 'Dockerfile').read_text().splitlines()[1])
            self.assertNotIn('ARG ', (output / 'Dockerfile').read_text())
            self.assertEqual(dll.read_bytes(), (output / 'reviewed-publish' / dll.name).read_bytes())
            with self.assertRaises(FileExistsError): module.prepare(publish, inventory, digest, output, runtime)
            dll.write_bytes(b'substituted-output')
            with self.assertRaises(ValueError): module.prepare(publish, inventory, digest, root / 'tampered', runtime)
            self.assertFalse((root / 'tampered').exists())
            with self.assertRaises(ValueError): module.prepare(publish, inventory, '0' * 64, root / 'wrong-inventory', runtime)

    def test_runtime_pin_and_duplicate_inventory_fields_fail_before_context(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory); publish = root / 'publish'; publish.mkdir()
            dll = publish / 'Hexalith.Platform.Custody.Dapr.dll'; dll.write_bytes(b'fixture')
            runtime = 'synthetic.invalid/runtime@sha256:' + 'a' * 64
            inventory = root / 'inventory.json'; inventory.write_text(json.dumps({'runtimeImage': runtime, 'files': {dll.name: hashlib.sha256(dll.read_bytes()).hexdigest()}}))
            digest = hashlib.sha256(inventory.read_bytes()).hexdigest()
            for invalid in [None, 'runtime:latest', runtime[:-1] + 'b', '${MUTABLE_BASE}@sha256:' + 'a' * 64]:
                with self.assertRaises(ValueError): module.prepare(publish, inventory, digest, root / 'invalid', invalid)
                self.assertFalse((root / 'invalid').exists())
            for raw in ['{"runtimeImage":"one","runtimeImage":"two","files":{}}', '{"runtimeImage":"' + runtime + '","files":{"same":"one","same":"two"}}']:
                inventory.write_text(raw); digest = hashlib.sha256(inventory.read_bytes()).hexdigest()
                with self.assertRaises(ValueError): module.prepare(publish, inventory, digest, root / 'duplicate', runtime)
