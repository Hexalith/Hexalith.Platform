"""Conditional cleanup and observation-only recovery; no installed operations."""
import json
from pathlib import Path
import tempfile
import types
import unittest
from unittest.mock import Mock, patch

import preload_isolated_image as image
from native_journal import durable_write

IMAGE = 'registry.hexalith.com/ext-secrets-1-hmac-authority@sha256:' + 'b' * 64
SUFFIX = image.sha(IMAGE.encode())[:12]


class PreloadOwnershipTests(unittest.TestCase):
    def setUp(self):
        directory = tempfile.TemporaryDirectory()
        self.addCleanup(directory.cleanup)
        self.evidence = Path(directory.name)
        self.instance = types.SimpleNamespace(evidence=self.evidence, resources=[], create=Mock())
        self.instance.create.side_effect = [{'uid': 'original-secret-uid', 'resourceVersion': '17'},
                                            {'uid': 'original-pod-uid', 'resourceVersion': '21'}]

    def run_preload(self, fail_pod_delete=False, missing_secret_identity=False):
        if missing_secret_identity:
            self.instance.create.side_effect = [None]
        deletions = []
        def kube(*args, **kwargs):
            if args[0] == 'delete':
                journal = json.loads((self.evidence / ('preload-journal-' + SUFFIX + '.json')).read_text())
                self.assertEqual(journal['podCleanupPhase' if '/pods/' in args[2] else 'secretCleanupPhase'], 'Started')
                deletions.append((args[2], json.loads(kwargs['data'])))
                if fail_pod_delete and '/pods/' in args[2]:
                    raise RuntimeError('conditional-deletion-denied')
            return types.SimpleNamespace(stdout=b'')
        pod = {'metadata': {'uid': 'original-pod-uid'}, 'spec': {'nodeName': 'isolated-node', 'containers': [{'image': IMAGE}]},
               'status': {'phase': 'Succeeded', 'containerStatuses': [{'imageID': IMAGE}]}}
        with patch.object(image, 'distribution_auth', return_value={'auth': 'transient-operator-credential'}), patch.object(image, 'kube', side_effect=kube), patch.object(image, 'kjson', return_value=pod):
            if fail_pod_delete or missing_secret_identity:
                with self.assertRaisesRegex(RuntimeError, 'preload-owned-resource-cleanup-incomplete'):
                    image.preload(self.instance, IMAGE)
            else:
                image.preload(self.instance, IMAGE)
        return deletions

    def retained(self):
        identities = {'secret': {'uid': 'original-secret-uid', 'resourceVersion': '17'},
                      'pod': {'uid': 'original-pod-uid', 'resourceVersion': '21'}}
        evidence = {'image': IMAGE, 'node': 'original-node', 'nativeImageId': IMAGE,
                    'provisioningCredentialRetained': False, 'credentialMountedInProcess': False}
        durable_write(self.evidence / ('preload-creation-identities-' + SUFFIX + '.json'), identities)
        durable_write(self.evidence / ('preloaded-image-' + SUFFIX + '.json'), evidence)
        return identities, evidence

    def test_deletes_only_original_pod_and_original_secret_version(self):
        deletions = self.run_preload()
        self.assertEqual(deletions[0][1]['preconditions'], {'uid': 'original-pod-uid'})
        self.assertEqual(deletions[1][1]['preconditions'], {'uid': 'original-secret-uid', 'resourceVersion': '17'})
        evidence = json.loads((self.evidence / ('preload-completion-' + SUFFIX + '.json')).read_text())
        self.assertFalse(evidence['provisioningCredentialRetained'])
        for path in self.evidence.iterdir():
            self.assertNotIn('transient-operator-credential', path.read_text())

    def test_failed_pod_cleanup_still_attempts_secret_cleanup_and_cannot_report_success(self):
        self.assertEqual(len(self.run_preload(fail_pod_delete=True)), 2)
        self.assertFalse((self.evidence / ('preload-completion-' + SUFFIX + '.json')).exists())

    def test_unknown_secret_identity_is_preserved_for_original_effect_recovery(self):
        self.assertEqual(self.run_preload(missing_secret_identity=True), [])

    def test_confirmed_absence_reconciles_original_completion_without_create_pull_or_delete(self):
        self.retained()
        before = {path.name: path.read_bytes() for path in self.evidence.iterdir()}
        with patch.object(image, 'kube', return_value=types.SimpleNamespace(stdout=b'')) as native, patch.object(image, 'distribution_auth') as auth:
            image.preload(self.instance, IMAGE)
            image.preload(self.instance, IMAGE)
        self.instance.create.assert_not_called()
        auth.assert_not_called()
        self.assertTrue(all(call.args[0] == 'get' for call in native.call_args_list))
        self.assertTrue(all((self.evidence / name).read_bytes() == content for name, content in before.items()))

    def test_missing_legacy_creation_identities_cannot_be_synthesized_from_resource_journal(self):
        self.retained()
        (self.evidence / ('preload-creation-identities-' + SUFFIX + '.json')).unlink()
        self.instance.resources = [{'name': 'ext-secrets-1-preload-' + SUFFIX, 'uid': 'legacy-observed-uid', 'resourceVersion': '17'}]
        with patch.object(image, 'kube') as native, patch.object(image, 'distribution_auth') as auth:
            with self.assertRaisesRegex(RuntimeError, 'original-create-identities-and-pull-evidence-required'):
                image.preload(self.instance, IMAGE)
        native.assert_not_called()
        auth.assert_not_called()
        self.instance.create.assert_not_called()

    def test_replacement_or_still_present_original_blocks_without_cleanup_replay(self):
        self.retained()
        for output, reason in [(b'foreign-uid 99', 'replaced'), (b'original-secret-uid 17', 'cleanup-not-confirmed')]:
            with self.subTest(output=output), patch.object(image, 'kube', return_value=types.SimpleNamespace(stdout=output)) as native:
                with self.assertRaisesRegex(RuntimeError, reason):
                    image.preload(self.instance, IMAGE)
                self.assertTrue(all(call.args[0] == 'get' for call in native.call_args_list))
        self.instance.create.assert_not_called()

    def test_missing_node_or_wrong_native_image_blocks_before_resource_reads(self):
        _, retained = self.retained()
        for changes in ({'node': ''}, {'nativeImageId': 'sha256:' + 'c' * 64}, {'nativeImageId': IMAGE + 'a'}, {'image': IMAGE + '-foreign'}):
            durable_write(self.evidence / ('preloaded-image-' + SUFFIX + '.json'), dict(retained, **changes))
            with self.subTest(changes=changes), patch.object(image, 'kube') as native:
                with self.assertRaisesRegex(RuntimeError, 'exact-image-node-native-image-evidence-required'):
                    image.preload(self.instance, IMAGE)
                native.assert_not_called()


if __name__ == '__main__':
    unittest.main()
