"""Offline regression controls for the current Dapr qualifier."""
import json
from pathlib import Path
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch

import dapr_candidate


class DaprCandidateTests(unittest.TestCase):
    def candidate(self):
        scratch = tempfile.TemporaryDirectory()
        self.addCleanup(scratch.cleanup)
        candidate = dapr_candidate.DaprCandidate(Path(scratch.name) / 'evidence')
        self.addCleanup(candidate.close)
        self.addCleanup(lambda: candidate.temp.exists() and candidate.cleanup())
        return candidate

    def test_invalid_lock_leaves_failed_evidence_and_removes_temporary_files(self):
        candidate = self.candidate()
        malformed = candidate.temp / 'invalid-lock.json'
        malformed.write_text('{"schema":"unknown"}')
        with patch.object(dapr_candidate, 'LOCK', malformed), self.assertRaisesRegex(ValueError, 'unsupported.*lock'):
            candidate.execute()
        record = json.loads((candidate.evidence / 'qualification.json').read_text())
        self.assertEqual(record['status'], 'failed')
        self.assertEqual(record['candidate']['file'], 'invalid-lock.json')
        self.assertTrue(record['cleanup']['temporaryDirectoryRemoved'])

    def test_missing_scheduler_replica_or_container_status_is_refused(self):
        candidate = self.candidate()
        image = 'ghcr.io/dapr/scheduler@sha256:' + 'a' * 64
        config = 'sha256:' + 'b' * 64
        candidate.image_config_digests = {image: config}
        candidate.record['checks']['daprLiveImagePins'] = {'replicasByImage': {image: 3}}
        pods = [
            {'metadata': {'name': f'dapr-scheduler-server-{i}'},
             'spec': {'containers': [{'name': 'scheduler', 'image': image}]},
             'status': {'containerStatuses': [{'name': 'scheduler', 'ready': True, 'image': config, 'imageID': image}]}}
            for i in range(3)
        ]
        candidate.check_dapr_control_plane(pods)
        with self.assertRaisesRegex(AssertionError, 'replica count'):
            candidate.check_dapr_control_plane(pods[:2])
        pods[2]['status']['containerStatuses'] = []
        with self.assertRaisesRegex(AssertionError, 'missing declared container status'):
            candidate.check_dapr_control_plane(pods)

    def test_wrong_digest_is_refused_even_for_compliant_injected_sidecar(self):
        candidate = self.candidate()
        candidate.tools = json.loads(dapr_candidate.LOCK.read_text())
        expected = candidate.tools['dapr']['images']['daprd']['linuxAmd64']
        container = {
            'name': 'daprd', 'image': expected,
            'securityContext': {'allowPrivilegeEscalation': False, 'capabilities': {'drop': ['ALL']},
                                'runAsNonRoot': True, 'runAsUser': 1654, 'readOnlyRootFilesystem': True,
                                'seccompProfile': {'type': 'RuntimeDefault'}},
        }
        pod = {'spec': {'containers': [container]}}
        self.assertEqual(candidate.check_admitted_sidecar(pod)[0]['image'], expected)
        container['image'] = 'ghcr.io/dapr/daprd@sha256:' + 'a' * 64
        with self.assertRaisesRegex(AssertionError, 'unexpected daprd image'):
            candidate.check_admitted_sidecar(pod)

    def test_application_dapr_inventory_retained_after_removal_and_absent_after_uninstall(self):
        candidate = self.candidate()
        objects = [{'kind': 'Configuration', 'metadata': {'name': 'reference-config'}},
                   {'kind': 'Subscription', 'metadata': {'name': 'reference-subscription'}}]
        candidate.application_dapr_names = candidate.application_dapr_inventory(objects)
        candidate.record['checks']['applicationDaprInventory'] = {'expected': candidate.application_dapr_names}
        candidate.component_baseline = {}
        candidate.record['checks']['componentOwnership'] = {}
        with patch.object(candidate, 'component_snapshot', return_value={}), patch.object(candidate, 'kube') as kube:
            kube.return_value = SimpleNamespace(stdout=json.dumps({'items': objects}))
            candidate.check_components('removeWorkloads')
            with self.assertRaisesRegex(AssertionError, 'differs during uninstall'):
                candidate.check_components('uninstall')
            kube.return_value = SimpleNamespace(stdout='{"items":[]}')
            candidate.check_components('uninstall')
            with self.assertRaisesRegex(AssertionError, 'differs during removeWorkloads'):
                candidate.check_components('removeWorkloads')
