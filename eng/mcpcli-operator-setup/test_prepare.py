"""Refusal and chart parity checks; no cluster or domain calls."""
import copy
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import prepare
import seed


class PreparationTests(unittest.TestCase):
    def test_unfilled_template_refuses_before_any_external_tool_or_output(self):
        with tempfile.TemporaryDirectory() as directory, patch.object(prepare, 'run') as run:
            output = Path(directory) / 'packet'
            with self.assertRaisesRegex(ValueError, 'source-revision-required'):
                prepare.prepare(prepare.HERE / 'profile.template.json', prepare.HERE / 'chart/reference-graph.json', output, 'helm', 'skopeo')
            self.assertFalse(output.exists()); run.assert_not_called()

    def test_task_chart_keeps_generic_templates_and_complete_closure(self):
        chart = prepare.ROOT / 'eng/helm-qualification/chart'
        for original in chart.rglob('*'):
            if original.is_file() and original.name != 'reference-graph.json':
                self.assertEqual(original.read_bytes(), (prepare.HERE / 'chart' / original.relative_to(chart)).read_bytes())
        graph = prepare.read(prepare.HERE / 'chart/reference-graph.json')
        self.assertEqual(8, len(graph['workloads']))
        self.assertEqual(prepare.IDENTITIES, {w['identity'] for w in graph['workloads']})

    def test_mutating_subprocesses_refused_before_execution(self):
        for command in (['kubectl', 'apply', '-f', 'anything'], ['skopeo', 'copy', 'a', 'b'], ['helm', 'upgrade', 'anything']):
            with self.subTest(command=command), patch.object(prepare.subprocess, 'run') as run:
                with self.assertRaisesRegex(ValueError, 'external-command-not-read-only'):
                    prepare.run(command)
                run.assert_not_called()

    def test_missing_and_conflicting_source_bindings_refused(self):
        profile = prepare.read(prepare.HERE / 'profile.template.json')
        profile['sourceRevision'] = 'a' * 40
        with patch.object(prepare, 'subprocess_git_revision', return_value='a' * 40):
            with self.assertRaisesRegex(ValueError, 'source-inputs-drift-or-unbound'):
                prepare.validate_profile(profile, prepare.ROOT, prepare.HERE / 'chart/reference-graph.json')

    def test_environment_rejects_external_and_cluster_resources(self):
        for resource in ({'kind': 'ClusterRole', 'metadata': {'name': 'admin'}},
                         {'kind': 'Service', 'metadata': {'name': 'wrong', 'namespace': 'another-app'}}):
            profile = {'environmentManifests': [resource], 'target': {'namespace': 'mcpcli-417-test'}}
            with self.subTest(resource=resource), self.assertRaisesRegex(ValueError, 'external-or-cluster-resource-refused'):
                prepare.environment_documents(profile)

    def test_seed_external_write_refused_before_network(self):
        plan = {'namespace': 'mcpcli-417-test', 'tenant': 'mcpcli-417-test', 'operatorRole': 'TenantContributor',
                'operatorSubject': 'operator', 'forwardedPorts': [18170, 18171],
                'steps': [{'name': name, 'url': 'https://192.168.1.30/api/v1/commands'} for name in seed.STEPS]}
        with patch.object(seed, 'build_opener') as network:
            with self.assertRaisesRegex(ValueError, 'external-seed-write-refused'):
                seed.validate(plan, 'mcpcli-417-test', 'mcpcli-417-test')
            network.assert_not_called()

    def test_uncertain_seed_journal_refuses_reexecution_before_network(self):
        with tempfile.TemporaryDirectory() as journal, patch.object(seed, 'build_opener') as network:
            with self.assertRaisesRegex(ValueError, 'seed-journal-exists-do-not-retry'):
                seed.execute({}, {}, journal)
            network.assert_not_called()


if __name__ == '__main__':
    unittest.main()
