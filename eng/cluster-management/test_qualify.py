"""Evidence disclosure, coverage and fail-closed qualification boundaries."""
import copy
import json
import os
from pathlib import Path
import stat
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch

from evidence import Attempt, now
from qualify import Capture, classify, compatible, project_resource, validate_authority, validate_native_endpoint, validate_ownership, validate_pins


class ProjectionTests(unittest.TestCase):
    def test_secret_and_configuration_values_never_enter_projection(self):
        for kind in ('Secret', 'ConfigMap', 'ClusterConfiguration', 'ExtensionVersion', 'User'):
            raw = {'apiVersion': 'v1', 'kind': kind, 'metadata': {'name': 'example', 'uid': 'uid-1', 'resourceVersion': '12',
                    'annotations': {'private': 'DO-NOT-PUBLISH-SECRET'}, 'labels': {'private': 'DO-NOT-PUBLISH-SECRET'}},
                   'data': {'password': 'DO-NOT-PUBLISH-SECRET'}, 'stringData': {'key': 'DO-NOT-PUBLISH-SECRET'},
                   'spec': {'config': 'DO-NOT-PUBLISH-SECRET'}, 'status': {'token': 'DO-NOT-PUBLISH-SECRET'}}
            projection = json.dumps(project_resource(raw))
            self.assertNotIn('DO-NOT-PUBLISH-SECRET', projection)
            self.assertNotIn('password', projection)

    def test_ownership_storage_and_installed_catalog_distinction(self):
        base = {'apiVersion': 'extensions.kubesphere.io/v1alpha1', 'metadata': {'name': 'sample', 'uid': 'uid-1', 'resourceVersion': '2'}}
        self.assertEqual(project_resource({**base, 'kind': 'Extension'})['extensionEvidenceClass'], 'catalog-or-configuration')
        self.assertEqual(project_resource({**base, 'kind': 'InstallPlan'})['extensionEvidenceClass'], 'installation-plan')
        claim = project_resource({'apiVersion': 'v1', 'kind': 'PersistentVolumeClaim', 'metadata': {'name': 'data', 'uid': 'claim-1'},
                                  'spec': {'volumeName': 'pv-1', 'storageClassName': 'local'}, 'status': {'phase': 'Bound'}})
        self.assertEqual(claim['binding']['volumeName'], 'pv-1')
        self.assertEqual(claim['uid'], 'claim-1')

    def test_unknown_owner_consumer_and_deletion_effects_block_retirement(self):
        item = project_resource({'apiVersion': 'v1', 'kind': 'ConfigMap', 'metadata': {'name': 'manager', 'namespace': 'kubesphere-system',
                                 'uid': 'child-1', 'ownerReferences': [{'uid': 'unknown-1', 'kind': 'Workspace', 'name': 'unknown'}]}})
        errors = validate_ownership(classify([item]))
        self.assertIn('unresolved-owner-or-consumer', errors)
        self.assertIn('unverified-propagation', errors)
        self.assertIn('unresolved-disposition', errors)


class CensusTests(unittest.TestCase):
    def test_endpoint_userinfo_query_and_fragment_cannot_disclose_credentials(self):
        validate_native_endpoint('https://192.168.1.30:6443')
        for endpoint in ('https://user:secret@api.example:6443', 'https://api.example:6443?token=secret',
                         'https://api.example:6443#secret', 'https://api.example/k8s/clusters/source'):
            with self.assertRaises(ValueError):validate_native_endpoint(endpoint)

    def test_supported_skew_and_prerelease_fail(self):
        self.assertTrue(compatible('v1.34.12', 'v1.34.9'))
        self.assertTrue(compatible('v1.35.9', 'v1.34.9'))
        self.assertFalse(compatible('v1.36.1', 'v1.34.9'))
        self.assertFalse(compatible('v1.35.9-rc.1', 'v1.34.9'))

    def test_failed_discovery_is_not_an_empty_accepted_census(self):
        capture = Capture(SimpleNamespace(kubeconfig=Path(__file__)), None)
        config = {'clusters': [{'cluster': {'server': 'https://127.0.0.1:6443',
                  'certificate-authority-data': __import__('base64').b64encode(b'invalid-cert').decode()}}]}
        with patch.object(capture, 'kube', side_effect=[{'clientVersion': {'gitVersion': 'v1.34.12'},
                         'serverVersion': {'gitVersion': 'v1.34.9'}}, config]), patch('qualify.ssl.create_default_context'), \
                patch('qualify.urllib.request.urlopen', side_effect=OSError), patch.object(capture, 'raw', return_value=None):
            with self.assertRaisesRegex(ValueError, 'api-discovery-failed'):
                capture.collect()
        self.assertEqual(capture.inventory, [])

    def test_proxy_credentials_are_refused_before_discovery(self):
        capture = Capture(SimpleNamespace(), None)
        proxy = {'clusters': [{'cluster': {'server': 'https://rancher.example/k8s/clusters/cluster-id',
                                          'certificate-authority-data': 'irrelevant'}}]}
        with patch.object(capture, 'kube', side_effect=[{'clientVersion': {'gitVersion': 'v1.34.12'},
                         'serverVersion': {'gitVersion': 'v1.34.9'}}, proxy]):
            with self.assertRaisesRegex(ValueError, 'direct-verified-tls'):
                capture.collect()


class PinAuthorityTests(unittest.TestCase):
    def pins(self):
        return {'checkedAt': now(), **{k: {'version': v, 'artifactSha256': 'a' * 64, 'prerelease': False} for k, v in
                [('rancher', 'v2.14.6'), ('managementK3s', 'v1.35.8+k3s1'), ('workloadKubernetes', 'v1.35.9')]},
                'chart': {'sha256': 'b' * 64}, 'images': [{'identity': 'rancher/rancher@sha256:' + 'c' * 64}],
                **{k: {'verified': True, 'evidenceSha256': 'd' * 64} for k in
                   ('hostingMatrix', 'importMatrix', 'securityReview', 'licenses', 'managementTools')}}

    def test_unqualified_tag_hosting_security_and_tools_fail_closed(self):
        pins = self.pins()
        pins['hostingMatrix']['distro'] = 'k3s'
        self.assertEqual(validate_pins(pins), [])
        for field in ('securityReview', 'licenses', 'managementTools', 'importMatrix'):
            failed = copy.deepcopy(pins);failed[field]['verified'] = False
            self.assertIn('unqualified-' + field, validate_pins(failed))
        pins['hostingMatrix']['distro'] = 'generic-import'
        pins['images'][0]['identity'] = 'rancher/rancher:latest'
        self.assertIn('generic-import-does-not-qualify-hosting', validate_pins(pins))
        self.assertIn('unqualified-image-identity', validate_pins(pins))

    def test_github_prerelease_and_stale_review_rejected(self):
        pins = self.pins();pins['hostingMatrix']['distro'] = 'k3s'
        pins['managementK3s']['prerelease'] = True
        self.assertIn('prerelease-managementK3s', validate_pins(pins))
        pins['checkedAt'] = '2025-01-01T00:00:00Z'
        self.assertIn('stale-or-future-pin-review', validate_pins(pins))
        self.assertTrue(validate_pins({}))

    def authority(self):
        event = {'sequence': 1, 'previousSha256': None, 'signatureVerified': True, 'sha256': 'a' * 64,
                 'approvedBy': 'Administrator', 'action': 'grant', 'principal': 'deputy', 'scope': 'scoped-native-recovery'}
        return {'nativeAccess': 'verified', 'nativePath': 'direct',
                **{k: True for k in ('mfaVerified', 'unauthorizedDenied', 'publicDenied', 'independentCustodyReadback',
                   'administratorSignatureVerified', 'independentOffsiteLineageReadback', 'restoredRevocationDenied')},
                'events': [event], 'currentHeadSha256': event['sha256'], 'restoredScopes': {'deputy': 'scoped-native-recovery'}}

    def test_missing_gapped_conflicting_or_expanded_authority_fails_closed(self):
        self.assertTrue(validate_authority({}))
        a = self.authority();self.assertEqual(validate_authority(a), [])
        a['events'][0]['sequence'] = 2
        self.assertIn('gapped-or-unverified-authority-lineage', validate_authority(a))
        a = self.authority();a['events'][0]['scope'] = 'global-admin'
        self.assertIn('deputy-authority-expanded', validate_authority(a))
        a = self.authority();a['nativePath'] = 'rancher-proxy'
        self.assertIn('native-access-unverified-or-proxy-only', validate_authority(a))

    def test_post_cut_revocation_survives_source_loss(self):
        a = self.authority()
        a['events'].append({'sequence': 2, 'previousSha256': 'a' * 64, 'signatureVerified': True, 'sha256': 'b' * 64,
                            'approvedBy': 'Administrator', 'action': 'revoke', 'principal': 'deputy'})
        a['currentHeadSha256'] = 'b' * 64
        self.assertIn('missing-or-conflicting-current-authority', validate_authority(a))
        a['restoredScopes'] = {}
        self.assertEqual(validate_authority(a), [])
        a['independentOffsiteLineageReadback'] = False
        self.assertIn('missing-independentOffsiteLineageReadback', validate_authority(a))


class CustodyTests(unittest.TestCase):
    def test_private_attempts_immutable_and_no_recovery_overwrite(self):
        with tempfile.TemporaryDirectory() as temp:
            base = Path(temp);project = base / 'repo';project.mkdir()
            root = base / 'private'
            attempt = Attempt(project, root, 'first')
            self.assertEqual(stat.S_IMODE(attempt.directory.stat().st_mode), 0o700)
            attempt.record('safe.json', {'accepted': False})
            self.assertEqual(stat.S_IMODE((attempt.directory/'safe.json').stat().st_mode), 0o600)
            with self.assertRaises(FileExistsError):attempt.record('safe.json', {})
            with self.assertRaises(FileExistsError):Attempt(project, root, 'first')
            with self.assertRaises(ValueError):Attempt(project, project / 'private', 'second')
            with self.assertRaises(ValueError):Attempt(project, Path.home() / 'hexalith-recovery-evidence' / 'new', 'second')

    def test_symlink_evidence_custody_refused(self):
        with tempfile.TemporaryDirectory() as temp:
            base = Path(temp);project = base / 'repo';project.mkdir();private = base / 'private';private.mkdir(mode=0o700)
            link = base / 'link';link.symlink_to(private)
            with self.assertRaisesRegex(ValueError, 'symlink'):Attempt(project, link, 'first')


if __name__ == '__main__':
    unittest.main()
