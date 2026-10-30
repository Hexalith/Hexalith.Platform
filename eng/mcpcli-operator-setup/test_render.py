"""Successful real Helm render with synthetic inputs; registry reads are mocked."""
from datetime import datetime, timedelta, timezone
import hashlib
import json
import os
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch
import yaml
import prepare
from test_seed import plan, TENANT
sys.path.insert(0, str(prepare.ROOT / 'eng/helm-qualification'))
import test_chart


class RenderTests(unittest.TestCase):
    def test_complete_synthetic_packet_renders_eight_workloads_and_no_public_route(self):
        helm = os.environ.get('HELM_BIN')
        self.assertTrue(helm, 'HELM_BIN must point at the pinned real Helm binary')
        with tempfile.TemporaryDirectory() as directory:
            directory = Path(directory)
            private_seed = directory / 'seed.json'; private_seed.write_text(json.dumps(plan())); private_seed.chmod(0o600)
            profile = prepare.read(prepare.HERE / 'profile.template.json')
            manifest = b'{"schemaVersion":2}'
            image_digest = hashlib.sha256(manifest).hexdigest()
            with patch.object(test_chart, 'CHART', prepare.HERE / 'chart'):
                values = test_chart.fixture_values()
            values['images'] = {k: 'registry.internal/' + k.replace('/', '-') + '@sha256:' + image_digest for k in values['images']}
            profile.update(sourceRevision=prepare.subprocess_git_revision(prepare.ROOT), sourceHashes=prepare.source_inputs(prepare.ROOT),
                           canonicalGraphSha256=prepare.sha(prepare.HERE / 'chart/reference-graph.json'),
                           expiresAt=(datetime.now(timezone.utc) + timedelta(hours=1)).isoformat(), tenant=TENANT,
                           operatorSubject='operator-user', values=values, privateSeedFile=str(private_seed))
            profile['target'].update(namespace=TENANT, release=TENANT)
            # Private files stand in for task-owned Secret creation; no provider is required.
            secret = directory / 'private-token'; secret.write_text('synthetic-test-only'); secret.chmod(0o600)
            refs = {'fixture-credential': {'token'}, 'fixture-registry': {'.dockerconfigjson'}}
            for w in prepare.read(prepare.HERE / 'chart/reference-graph.json')['workloads']:
                refs['fixture-' + w['resourceId'] + '-app'] = {'token'}
                refs['fixture-' + w['resourceId'] + '-api'] = {'token'}
            profile['privateSecretFiles'] = {name: {key: str(secret) for key in keys} for name, keys in refs.items()}
            profile['prerequisites'] = {key: 'synthetic-test-binding' for key in profile['prerequisites']}
            profile['environmentManifests'] = [
                {'apiVersion': 'v1', 'kind': 'ConfigMap', 'metadata': {'name': values['environment']['componentConfigMap'], 'namespace': TENANT,
                    'labels': {'hexalith.com/operator-task': TENANT}}, 'data': {}},
                {'apiVersion': 'v1', 'kind': 'PersistentVolumeClaim', 'metadata': {'name': 'task-data', 'namespace': TENANT,
                    'labels': {'hexalith.com/operator-task': TENANT}}, 'spec': {'resources': {'requests': {'storage': '1Gi'}}}}]
            path = directory / 'profile.json'; path.write_text(json.dumps(profile))
            real_run = prepare.run
            commands = []
            def read_only_run(argv):
                commands.append(argv)
                if argv[0] == 'skopeo': return manifest
                return real_run(argv)
            output = directory / 'review'
            with patch.object(prepare, 'run', side_effect=read_only_run):
                packet = prepare.prepare(path, prepare.HERE / 'chart/reference-graph.json', output, helm, 'skopeo')
            documents = list(yaml.safe_load_all((output / 'application.yaml').read_text()))
            self.assertEqual(8, sum(d['kind'] == 'Deployment' for d in documents))
            self.assertFalse(any(d['kind'] == 'HTTPRoute' for d in documents))
            self.assertEqual('review-required', packet['status']); self.assertFalse(packet['applyAuthorized'])
            self.assertEqual(8, len(packet['imagesPulled']))
            self.assertTrue(all(command[1:3] in (['version', '--short'], ['template', 'mcpcli-417'], ['inspect', '--raw']) for command in commands))
            self.assertNotIn('operator-token', (output / 'review.json').read_text())
            self.assertEqual(9, len(packet['seed']['requests']))
