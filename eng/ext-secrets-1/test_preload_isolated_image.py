"""Conditional cleanup of the original transient provisioning objects."""
import json
import types
import unittest
from unittest.mock import Mock, patch

import preload_isolated_image as image


class PreloadOwnershipTests(unittest.TestCase):
    def run_preload(self, fail_pod_delete=False, missing_secret_identity=False):
        instance = Mock()
        instance.create.side_effect = [None if missing_secret_identity else {'uid': 'original-secret-uid', 'resourceVersion': '17'},
            {'uid': 'original-pod-uid', 'resourceVersion': '21'}]
        deletions = []
        def kube(*args, **kwargs):
            if args[0] == 'get':
                raise AssertionError('Cleanup must use the original create response, never a later GET identity.')
            if args[0] == 'delete':
                deletions.append((args[2], json.loads(kwargs['data'])))
                if fail_pod_delete and '/pods/' in args[2]:
                    raise RuntimeError('conditional-deletion-denied')
            return types.SimpleNamespace(stdout=b'')
        pod = {'metadata': {'uid': 'original-pod-uid'}, 'spec': {'nodeName': 'isolated-node'}, 'status': {'containerStatuses': [{'imageID': 'native-exact-digest'}]}}
        with patch.object(image, 'distribution_auth', return_value={'auth': 'transient-operator-credential'}), patch.object(image, 'kube', side_effect=kube), patch.object(image, 'kjson', return_value=pod):
            if fail_pod_delete or missing_secret_identity:
                with self.assertRaisesRegex(RuntimeError, 'preload-owned-resource-cleanup-incomplete'):
                    image.preload(instance, 'registry.hexalith.com/ext-secrets-1-hmac-authority@sha256:' + 'b' * 64)
            else:
                image.preload(instance, 'registry.hexalith.com/ext-secrets-1-hmac-authority@sha256:' + 'b' * 64)
        return deletions, instance

    def test_deletes_only_original_pod_and_original_secret_version(self):
        deletions, instance = self.run_preload()
        self.assertEqual(deletions[0][1]['preconditions'], {'uid': 'original-pod-uid'})
        self.assertEqual(deletions[1][1]['preconditions'], {'uid': 'original-secret-uid', 'resourceVersion': '17'})
        evidence = instance.write.call_args.args[1]
        self.assertFalse(evidence['provisioningCredentialRetained'])
        self.assertNotIn('transient-operator-credential', json.dumps(evidence))

    def test_failed_pod_cleanup_still_attempts_secret_cleanup_and_cannot_report_success(self):
        deletions, _ = self.run_preload(fail_pod_delete=True)
        self.assertEqual(len(deletions), 2)

    def test_unknown_secret_identity_is_preserved_for_original_effect_recovery(self):
        deletions, _ = self.run_preload(missing_secret_identity=True)
        self.assertEqual(deletions, [])


if __name__ == '__main__':
    unittest.main()
