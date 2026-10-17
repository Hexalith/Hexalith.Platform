"""Offline safety boundaries for the operational key installation."""
import base64
import json
import os
import subprocess
from pathlib import Path
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch

import provision_operational_hmac as module


class OperationalHmacProvisioningTests(unittest.TestCase):
    def test_plan_uses_only_real_tenants_and_permitted_tenant_purposes(self):
        plan = module.plan_keys()
        self.assertEqual(len(plan), 4)
        self.assertEqual({item['tenantId'] for item in plan}, set(module.TENANTS))
        self.assertEqual({item['purpose'] for item in plan}, {'ContentDigest', 'TrustedEnvelope'})
        self.assertEqual(len({item['opaqueVersion'] for item in plan}), 4)
        self.assertTrue(all(not item['secretReference'].startswith(module.MOUNT + '/') for item in plan))

    def test_data_policy_grants_exact_data_reads_and_no_metadata(self):
        plan = module.plan_keys()
        policy = module.policy_for(plan)
        self.assertNotIn('/metadata/', policy)
        self.assertNotIn('*', policy)
        self.assertNotIn('"create"', policy)
        self.assertNotIn('"delete"', policy)
        for item in plan:
            self.assertIn('path "' + module.MOUNT + '/data/' + item['secretReference'] + '" { capabilities = ["read"] }', policy)

    def test_provisioner_has_no_key_read_or_overwrite_capability(self):
        policy = module.policy_for(module.plan_keys(), True)
        self.assertNotIn('"read",', policy)
        for line in policy.splitlines():
            if '/data/' in line:
                self.assertTrue(line.endswith('capabilities = ["create"] }'))

    def test_generation_streams_cas_zero_and_exposes_only_write_metadata(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            material = base64.b64encode(bytes(range(32))).decode()
            fixture = root / 'bao'
            fixture.write_text("#!/usr/bin/env python3\nimport json,sys\nfrom pathlib import Path\n" +
                "args=sys.argv[1:]\nif args==['write','-field=random_bytes','sys/tools/random/32','format=base64']:\n print(" + repr(material) + ")\n" +
                "else:\n assert args==['write','-format=json','ext-secrets-1-isolated/data/tenant/purpose/key','-']\n" +
                " body=json.load(sys.stdin)\n Path(" + repr(str(root / 'payload.json')) + ").write_text(json.dumps(body))\n" +
                " print(json.dumps({'data':{'version':1,'created_time':'2026-10-17T10:00:00Z','destroyed':False,'deletion_time':''}}))\n")
            fixture.chmod(0o700)
            def execute(*args, data, **kwargs):
                script = args[args.index('-c') + 1]
                result = subprocess.run(['sh', '-c', script, *args[args.index('-c') + 2:]], input=data, capture_output=True,
                                        env={**os.environ, 'PATH': directory + os.pathsep + os.environ['PATH']}, check=True)
                self.assertNotIn(material.encode(), result.stdout + result.stderr)
                self.assertNotIn(b'offline-provisioner', result.stdout + result.stderr)
                return result
            with patch.object(module, 'kube', side_effect=execute):
                self.assertEqual(module.generate_inside_provider(b'offline-provisioner', 'tenant/purpose/key')['version'], 1)
            payload = json.loads((root / 'payload.json').read_text())
            self.assertEqual(payload, {'options': {'cas': 0}, 'data': {'hmacKey': material}})
            self.assertEqual(len(base64.b64decode(payload['data']['hmacKey'], validate=True)), 32)

    def test_component_is_scoped_to_custody_and_preserves_tls_and_relative_names(self):
        component = module.component_manifest()
        self.assertEqual(component['scopes'], ['ext-secrets-1-custody'])
        metadata = {item['name']: item['value'] for item in component['spec']['metadata']}
        self.assertEqual(metadata['enginePath'], module.MOUNT)
        self.assertEqual(metadata['vaultKVUsePrefix'], 'false')
        self.assertEqual(metadata['vaultValueType'], 'map')
        self.assertEqual(metadata['skipVerify'], 'false')
        self.assertEqual(metadata['tlsServerName'], module.BAO_NAME)
        self.assertNotIn('vaultToken', metadata)

    def test_malformed_or_changed_native_version_denies(self):
        for metadata in [{'current_version': True}, {'current_version': 0},
                         {'current_version': 1, 'versions': {'1': {'destroyed': False, 'created_time': 'utc'}}},
                         {'current_version': 1, 'versions': {'1': {'destroyed': True, 'created_time': 'utc', 'deletion_time': ''}}}]:
            with self.assertRaises(RuntimeError):
                module.validate_version({}, metadata)

    def test_existing_key_is_never_replaced(self):
        self._reject_creation('Prepared', {'current_version': 1}, 'new-key-reference-already-exists')

    def test_uncertain_absent_key_requires_exact_recovery(self):
        self._reject_creation('CreationStarted', None, 'uncertain-key-creation-requires-exact-metadata-recovery')

    def test_uncertain_present_key_is_never_adopted(self):
        self._reject_creation('CreationStarted', {'current_version': 1}, 'uncertain-key-creation-requires-exact-metadata-recovery')

    def test_plan_tampering_denies_before_credential_or_policy_creation(self):
        for field, value in [('tenantId', 'foreign'), ('purpose', 'SecurityObservation'), ('secretReference', '../foreign'),
                             ('secretField', 'wrong'), ('phase', 'Unrecognized'), ('opaqueVersion', 'invalid')]:
            plan = module.plan_keys()
            plan[0][field] = value
            with self.assertRaisesRegex(RuntimeError, 'invalid-operational-key-plan'):
                module.validate_plan(plan)
        plan = module.plan_keys()
        plan[1] = plan[0].copy()
        with self.assertRaises(RuntimeError):
            module.validate_plan(plan)

    def _reject_creation(self, phase, metadata, reason):
        with tempfile.TemporaryDirectory() as directory:
            installer = SimpleNamespace(evidence=Path(directory), bao=lambda *args, **kwargs: SimpleNamespace(stdout=b'[]'))
            plan = module.plan_keys()
            item = plan[0]
            item['phase'] = phase
            reader = SimpleNamespace(read=lambda reference: metadata)
            token = bytearray(b'offline-provisioner')
            with patch.object(module, 'issue', return_value=(token, {'accessor': 'offline-accessor'})), \
                    patch.object(module, 'application_request') as revoke, \
                    patch.object(module, 'generate_inside_provider') as generate:
                with self.assertRaisesRegex(RuntimeError, reason):
                    module.provision_keys(installer, reader, plan)
                generate.assert_not_called()
                revoke.assert_called_once()
                self.assertTrue(all(value == 0 for value in token))
                self.assertEqual(json.loads((Path(directory) / 'key-provisioning-credential.json').read_text())['phase'], 'Revoked')


if __name__ == '__main__':
    unittest.main()
