"""Input-admission regressions only; no deployed/native service is contacted."""
import copy
import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

ROOT = Path(__file__).parent
spec = importlib.util.spec_from_file_location("custody_preflight", ROOT / "preflight.py")
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class PreflightTests(unittest.TestCase):
    def check_inputs(self, mutate):
        with tempfile.TemporaryDirectory() as directory:
            artifact = Path(directory) / 'retained-proof'; artifact.write_bytes(b'presence-only-unverified')
            data = json.loads((ROOT / 'inputs.uninstalled.json').read_text())
            for key in data['images']: data['images'][key] = 'reviewed/image@sha256:' + 'a' * 64
            for key in data['artifacts']: data['artifacts'][key] = dict(path=str(artifact), sha256=hashlib.sha256(artifact.read_bytes()).hexdigest())
            for key in data['credentialReferences']: data['credentialReferences'][key] = 'secretref:' + key
            for section in ['placement', 'logicalRoles']:
                for key in data[section]: data[section][key] = key
            for key in ['kubernetesNativeSidecars', 'swapDisabled', 'coreDumpsDisabled', 'memoryCaptureDisabled']: data['profiles'][key] = True
            for key in data['kubernetes']: data['kubernetes'][key] = key
            data['kubernetes'].update(appPort=8080, appProtocol='http', internalGrpcPort=50002, workerGroupId=2001, sentryTokenExpirationSeconds=600)
            for key in data['referenceCredentials']: data['referenceCredentials'][key] = key
            data['referenceCredentials']['mountedFile'] = '/credentials/credentials'
            data['g1Qualified'] = True
            mutate(data)
            path = Path(directory) / 'inputs.json'; path.write_text(json.dumps(data))
            return module.inspect(path)

    def test_empty_sections_name_every_required_field(self):
        for section in module.REQUIRED:
            result = self.check_inputs(lambda data: data.__setitem__(section, {}))
            self.assertEqual('Blocked', result['status'])
            fields = {entry['input'] for entry in result['missing']}
            for field in module.REQUIRED[section]: self.assertIn(section + '.' + field, fields)

    def test_extra_wrong_type_and_unpinned_inputs_deny(self):
        mutations = [lambda d: d['images'].__setitem__('extra', 'image:latest'),
                     lambda d: d.__setitem__('profiles', []),
                     lambda d: d['profiles'].__setitem__('swapDisabled', 1),
                     lambda d: d['credentialReferences'].__setitem__('component', 'plaintext-value'),
                     lambda d: d['images'].__setitem__('dapr', 'daprio/daprd:latest'),
                     lambda d: d['artifacts'].__setitem__('g1Evidence', {'path': 7, 'sha256': 9}),
                     lambda d: d['placement'].__setitem__('authorityFailureDomain', d['placement']['applicationFailureDomain'])]
        for mutate in mutations: self.assertEqual('Blocked', self.check_inputs(mutate)['status'])

    def test_bare_g1_boolean_never_closes_proof_input(self):
        result = self.check_inputs(lambda d: d['artifacts'].pop('g1Evidence'))
        self.assertEqual('Blocked', result['status'])
        self.assertIn('artifacts.g1Evidence', {entry['input'] for entry in result['missing']})

    def test_duplicate_fields_rejected_recursively(self):
        for text in ['{"protocol":"one","protocol":"two"}', '{"artifacts":{"g1Evidence":{"path":"one","path":"two"}}}']:
            with tempfile.TemporaryDirectory() as directory:
                path = Path(directory) / 'duplicate.json'; path.write_text(text)
                with self.assertRaises(ValueError): module.inspect(path)

    def test_presence_never_claims_installed_qualified_available(self):
        result = self.check_inputs(lambda d: None)
        self.assertEqual('InputsPresentUnverified', result['status'])
        for field in ['installedProvider', 'physicalQualification', 'dependencyAvailable', 'runnableInstallationGenerated']:
            self.assertIs(result[field], False)


if __name__ == '__main__': unittest.main()
