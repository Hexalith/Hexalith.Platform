"""Evidence gates reject successful exits with incomplete or substituted execution."""
import hashlib
import io
import json
from pathlib import Path
import tempfile
import tarfile
import unittest
from unittest.mock import patch

from verify_works_host import ROWS, PHASES, RESTORE_PHASE, bind_persisted_outcomes, require_passed_tests, require_published_runtime_source, sha256, verify_host_records


class WorksEvidenceGateTests(unittest.TestCase):
    def result(self, attributes='', test='<test method="Required" result="Pass" />'):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        path = Path(temporary.name) / 'result.xml'
        path.write_text(f'<assemblies><assembly total="1" {attributes}>{test}</assembly></assemblies>')
        return path

    def test_skipped_failed_error_and_not_run_counts_are_refused(self):
        for key in ['skipped', 'failed', 'errors', 'not-run']:
            with self.subTest(key=key), self.assertRaises(ValueError):
                require_passed_tests(self.result(f'{key}="1"'))

    def test_empty_filtered_or_nonpassing_execution_is_refused(self):
        for test in ['', '<test method="Required" result="Skip" />', '<test method="Required" result="Fail" />']:
            with self.subTest(test=test), self.assertRaises(ValueError):
                require_passed_tests(self.result(test=test))
        with self.assertRaises(ValueError):
            require_passed_tests(self.result(), ['Missing'])

    def test_passed_required_method_has_exact_result_hash(self):
        path = self.result()
        self.assertEqual(require_passed_tests(path, ['Required']), {'total': 1, 'passed': 1, 'skipped': 0, 'sha256': sha256(path)})

    def test_required_supporting_classes_must_execute(self):
        with self.assertRaises(ValueError):
            require_passed_tests(self.result(), required_types=["RequiredClass"])
        path = self.result(test='<test type="RequiredClass" method="Required" result="Pass" />')
        self.assertEqual(require_passed_tests(path, required_types=["RequiredClass"])["total"], 1)

    def test_runtime_source_must_match_public_archive_producer(self):
        content = b'public producer source'
        archive = io.BytesIO()
        with tarfile.open(fileobj=archive, mode='w') as output:
            member = tarfile.TarInfo('src/Producer.cs')
            member.size = len(content)
            output.addfile(member, io.BytesIO(content))
        snapshot = {'root': '/fixture/sdk', 'commit': 'metadata-only-new-head',
                    'sourceFiles': {'src/Producer.cs': hashlib.sha256(content).hexdigest()}, 'inventorySha256': 'bound-inventory'}
        producers = {'Published.SDK': {'repositoryCommit': 'public-producer'}}
        with patch('verify_works_host.subprocess.check_output', return_value=archive.getvalue()):
            result = require_published_runtime_source(snapshot, producers)
            self.assertTrue(result['matchesPublishedProducer'])
            self.assertEqual(result['repositoryCommit'], 'public-producer')
            snapshot['sourceFiles']['src/Producer.cs'] = 'unpublished-patch'
            with self.assertRaises(ValueError):
                require_published_runtime_source(snapshot, producers)

    def test_another_host_or_scheduler_cannot_qualify_platform(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            assembly = root / 'apphost.dll'
            assembly.write_bytes(b'bound fixture')
            records = root / 'starts.jsonl'
            record = {'assembly': str(assembly), 'sha256': sha256(assembly), 'directory': str(root), 'schedulerVolume': 'works-sdk416-owned', 'role': 'platform-target', 'actorType': 'WorksSdkReminderActor', 'sdkAssemblies': [{'name': 'Hexalith.EventStore.Aspire', 'version': '3.123.0.0', 'path': str(assembly), 'sha256': sha256(assembly)}]}
            records.write_text(json.dumps(record) + '\n')
            self.assertEqual(len(verify_host_records(records, assembly, root)), 1)
            for key, value in [('sha256', 'changed'), ('assembly', str(root / 'old-host.dll')), ('directory', str(root / 'works')), ('schedulerVolume', 'hexalith-works-dapr-scheduler'), ('role', 'unknown'), ('actorType', 'DateReminderActor'), ('sdkAssemblies', []), ('sdkAssemblies', [{'name': 'Hexalith.EventStore.Aspire', 'version': '3.123.1.0', 'path': str(assembly), 'sha256': sha256(assembly)}])]:
                with self.subTest(key=key), self.assertRaises(ValueError):
                    records.write_text(json.dumps(record | {key: value}) + '\n')
                    verify_host_records(records, assembly, root)

    def test_incomplete_phases_or_missing_retained_host_cannot_accept_rows(self):
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / 'states.jsonl'
            phases = {phase for values in PHASES.values() for phase in values}
            phases.update(['complete-platform-fleet-snapshot', RESTORE_PHASE,
                           'platform-historical-restart-replay-and-family', 'platform-two-host-ha-lost-firing-stale-callback-recovery'])
            path.write_text(''.join(json.dumps({'phase': phase, 'state': {}}) + '\n' for phase in phases))
            with self.assertRaises(ValueError):
                bind_persisted_outcomes(path, {}, [{'role': 'platform-target'}])
            phases.remove(RESTORE_PHASE)
            path.write_text(''.join(json.dumps({'phase': phase, 'state': {}}) + '\n' for phase in phases))
            with self.assertRaises(ValueError):
                bind_persisted_outcomes(path, {}, [{'role': 'retained-rollback'}])

    def test_contract_requires_every_row(self):
        self.assertEqual(set(ROWS), {f'R{index}' for index in range(1, 12)})


if __name__ == '__main__':
    unittest.main()
