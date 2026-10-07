"""Failure-mode checks for local runner relocation preparation; no operational proof."""
import copy
from datetime import datetime, timedelta, timezone
import json
import os
from pathlib import Path
import shutil
import stat
import subprocess
import tempfile
import unittest

import prepare


class SourceCheckpointTests(unittest.TestCase):
    def setUp(self):
        self.clock = datetime(2026, 10, 7, 16, 0, tzinfo=timezone.utc)
        self.source = {'clusterUid': 'fixture-cluster',
            'namespace': {'name': 'forgejo-runner', 'uid': 'fixture-namespace',
                          'resourceVersion': '10', 'configSha256': 'a' * 64},
            'deployment': {'name': 'forgejo-runner', 'uid': 'fixture-deployment',
                           'resourceVersion': '20', 'configSha256': 'b' * 64,
                           'podTemplateSha256': 'c' * 64},
            'registeredRunnerId': 'fixture-runner', 'runnerConfigSha256': 'd' * 64}
        common = {'story': '4.3', 'attemptId': 'fixture-attempt', 'expiresAt': self.time(120)}
        self.baseline = {**common, 'capturedAt': self.time(-180), 'source': self.source}
        self.decisions = {**common, 'capturedAt': self.time(-90), 'accepted': True,
            'sourceBaselineSha256': 'e' * 64, 'approvedPhases': list(prepare.PHASES),
            'operatorIds': ['fixture-operator'], 'checkpointMaxAgeSeconds': 60,
            'cutoverWindow': {'startsAt': self.time(-300), 'endsAt': self.time(300)}}
        self.current = {**common, 'capturedAt': self.time(-5), 'collectionStartedAt': self.time(-10),
            'phase': 'revoke-credentials', 'operator': 'fixture-operator',
            'source': copy.deepcopy(self.source), 'activeJobsReadable': True,
            'activeJobsObservedAt': self.time(-7), 'activeJobIds': [], 'deploymentPresent': True}

    def time(self, seconds):
        return (self.clock + timedelta(seconds=seconds)).isoformat()

    def check(self, phase='revoke-credentials', *, baseline_hash='e' * 64):
        return prepare.check_source(self.baseline, self.decisions, self.current,
                                    baseline_hash, phase, self.clock)

    def test_valid_checkpoint_still_does_not_authorize_or_accept(self):
        result = self.check()
        self.assertTrue(result['sourceCheckpointConsistent'])
        self.assertFalse(result['mutationAuthorized'])
        self.assertFalse(result['operationalAcceptance'])
        self.assertFalse(result['complete'])
        self.assertNotIn('signatureVerification', result)  # Pure check does not verify signatures.

    def test_every_source_identity_or_digest_change_blocks(self):
        fields = [('clusterUid',), ('registeredRunnerId',), ('runnerConfigSha256',)]
        fields += [(kind, field) for kind in ('namespace', 'deployment')
                   for field in ('uid', 'resourceVersion', 'configSha256')]
        fields += [('deployment', 'podTemplateSha256')]
        for fields_path in fields:
            with self.subTest(fields_path=fields_path):
                self.current['source'] = copy.deepcopy(self.source)
                obj = self.current['source']
                for field in fields_path[:-1]:
                    obj = obj[field]
                obj[fields_path[-1]] = 'f' * 64
                with self.assertRaisesRegex(ValueError, 'source-identity-or-config-drift'):
                    self.check()

    def test_missing_runner_identity_or_effective_config_blocks(self):
        for field in ('registeredRunnerId', 'runnerConfigSha256'):
            with self.subTest(field=field):
                self.current['source'] = copy.deepcopy(self.source)
                self.current['source'][field] = None
                with self.assertRaisesRegex(ValueError, 'ambiguous-registered-runner'):
                    self.check()

    def test_wrong_baseline_bytes_operator_phase_or_attempt_blocks(self):
        with self.assertRaisesRegex(ValueError, 'unapproved-baseline-bytes'):
            self.check(baseline_hash='f' * 64)
        for field, value in (('operator', 'unapproved'), ('phase', 'delete-resources'),
                             ('attemptId', 'other-attempt')):
            current = copy.deepcopy(self.current)
            self.current[field] = value
            with self.subTest(field=field), self.assertRaises(ValueError):
                self.check()
            self.current = current

    def test_active_jobs_allowed_only_when_disabling_new_scheduling(self):
        self.current['activeJobIds'] = ['fixture-active-job']
        self.current['phase'] = 'disable-scheduling'
        self.check('disable-scheduling')
        for phase in prepare.PHASES[1:]:
            self.current['phase'] = phase
            with self.subTest(phase=phase), self.assertRaisesRegex(ValueError, 'active-jobs-block'):
                self.check(phase)

    def test_unreadable_duplicate_or_stale_job_census_blocks(self):
        for field, value in (('activeJobsReadable', False),
                             ('activeJobIds', ['job', 'job']),
                             ('activeJobsObservedAt', self.time(-11))):
            current = copy.deepcopy(self.current)
            self.current[field] = value
            with self.subTest(field=field), self.assertRaises(ValueError):
                self.check()
            self.current = current

    def test_expired_future_old_or_preapproval_checkpoint_blocks(self):
        for field, value in (('expiresAt', self.time(-1)), ('capturedAt', self.time(1)),
                             ('collectionStartedAt', self.time(-61)),
                             ('collectionStartedAt', self.time(-100))):
            current = copy.deepcopy(self.current)
            self.current[field] = value
            with self.subTest(field=field), self.assertRaises(ValueError):
                self.check()
            self.current = current
        self.decisions['capturedAt'] = self.time(-4)
        with self.assertRaisesRegex(ValueError, 'checkpoint-not-immediately-after-approval'):
            self.check()

    def test_missing_approval_or_closed_window_blocks(self):
        for field, value in (('accepted', False), ('approvedPhases', []), ('operatorIds', []),
                             ('checkpointMaxAgeSeconds', 61), ('checkpointMaxAgeSeconds', True),
                             ('cutoverWindow', {'startsAt': self.time(1), 'endsAt': self.time(300)})):
            decisions = copy.deepcopy(self.decisions)
            self.decisions[field] = value
            with self.subTest(field=field), self.assertRaises(ValueError):
                self.check()
            self.decisions = decisions

    def test_absent_deployment_needs_exact_namespace_deletion_lineage(self):
        self.current['deploymentPresent'] = False
        self.current['phase'] = 'delete-namespace'
        with self.assertRaisesRegex(ValueError, 'missing-exact-source-deletion-lineage'):
            self.check('delete-namespace')
        self.current['sourceDeletionReceipt'] = {'source': copy.deepcopy(self.source),
            'evidenceSha256': 'f' * 64, 'absenceVerified': True, 'deletedAt': self.time(-20)}
        self.check('delete-namespace')
        self.current['sourceDeletionReceipt']['source']['deployment']['uid'] = 'replacement'
        with self.assertRaisesRegex(ValueError, 'missing-exact-source-deletion-lineage'):
            self.check('delete-namespace')

    def test_absent_source_cannot_pass_an_earlier_phase(self):
        self.current['deploymentPresent'] = False
        with self.assertRaisesRegex(ValueError, 'source-deployment-not-present'):
            self.check()

    def test_sensitive_fields_are_rejected_without_echoing_values(self):
        for field in ('registrationToken', 'kubeconfig', 'registry_credentials', 'stringData'):
            current = copy.deepcopy(self.current)
            self.current[field] = 'fixture-sensitive-value'
            with self.subTest(field=field), self.assertRaisesRegex(ValueError, '^prohibited-evidence-field$'):
                self.check()
            self.current = current


class CustodyTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix='runner-relocation-test-')
        self.root = Path(self.temp.name)
        self.addCleanup(self.temp.cleanup)

    def test_pending_attempt_is_private_immutable_and_never_operational(self):
        old = os.umask(0)
        try:
            directory = prepare.prepare(prepare.PROJECT, self.root / 'custody', 'fixture-attempt', 'fixture-operator')
        finally:
            os.umask(old)
        for path in (directory, *(p for p in directory.parents if self.root in p.parents)):
            self.assertEqual(0o700, stat.S_IMODE(path.stat().st_mode))
        for path in directory.iterdir():
            self.assertEqual(0o600, stat.S_IMODE(path.stat().st_mode))
            if path.suffix == '.json':
                record = json.loads(path.read_bytes())
                self.assertFalse(record['accepted'])
                prepare.no_sensitive_fields(record)
        attempt = json.loads((directory / 'attempt.json').read_bytes())
        self.assertIsNone(attempt['source']['registeredRunnerId'])
        self.assertIsNone(attempt['targetHostId'])
        self.assertFalse(attempt['complete'])
        before = (directory / 'SHA256SUMS').read_bytes()
        with self.assertRaisesRegex(ValueError, 'attempt-already-exists'):
            prepare.prepare(prepare.PROJECT, self.root / 'custody', 'fixture-attempt', 'fixture-operator')
        self.assertEqual(before, (directory / 'SHA256SUMS').read_bytes())

    def test_shared_symlink_repository_and_gitfile_roots_are_rejected(self):
        shared = self.root / 'shared'
        shared.mkdir(mode=0o755)
        shared.chmod(0o755)
        with self.assertRaisesRegex(ValueError, 'custody-must-be-owner-only'):
            prepare.prepare(prepare.PROJECT, shared, 'fixture', 'operator')
        link = self.root / 'link'
        link.symlink_to(self.root, target_is_directory=True)
        with self.assertRaisesRegex(ValueError, 'symlink-in-private-path'):
            prepare.prepare(prepare.PROJECT, link / 'child', 'fixture', 'operator')
        with self.assertRaisesRegex(ValueError, 'evidence-must-be-outside-repository'):
            prepare.prepare(prepare.PROJECT, prepare.PROJECT / 'custody', 'fixture', 'operator')
        other = self.root / 'other-checkout'
        other.mkdir(mode=0o700)
        (other / '.git').write_text('gitdir: /fixture/private-git\n')
        with self.assertRaisesRegex(ValueError, 'evidence-must-be-outside-git-worktree'):
            prepare.prepare(prepare.PROJECT, other / 'custody', 'fixture', 'operator')

    def test_path_traversal_attempt_is_rejected(self):
        with self.assertRaisesRegex(ValueError, 'invalid-attempt-id'):
            prepare.prepare(prepare.PROJECT, self.root / 'custody', '../escape', 'operator')

    @unittest.skipUnless(shutil.which('ssh-keygen'), 'SSH signing verifier unavailable')
    def test_actual_signatures_reject_tampering_wrong_principal_and_json_ambiguity(self):
        key = self.root / 'fixture-key'
        tool = shutil.which('ssh-keygen')
        subprocess.run([tool, '-q', '-t', 'ed25519', '-N', '', '-f', str(key)],
                       check=True, capture_output=True)
        trust = self.root / 'allowed_signers'
        prepare.write_new(trust, ('fixture-admin ' + Path(str(key) + '.pub').read_text()).encode())
        record = self.root / 'fixture.json'
        prepare.write_new(record, b'{"story":"4.3"}\n')

        def sign():
            signature = Path(str(record) + '.sig')
            if signature.exists():
                signature.unlink()
            subprocess.run([tool, '-Y', 'sign', '-f', str(key), '-n', prepare.NAMESPACE, str(record)],
                           check=True, capture_output=True)
            signature.chmod(0o600)

        sign()
        result, hashed = prepare.signed_input(record, prepare.PROJECT, trust, 'fixture-admin', tool)
        self.assertEqual('4.3', result['story'])
        self.assertEqual(prepare.digest(record.read_bytes()), hashed)
        with self.assertRaisesRegex(ValueError, 'unverified-evidence-signature'):
            prepare.signed_input(record, prepare.PROJECT, trust, 'wrong-principal', tool)
        record.write_bytes(b'{"story":"tampered"}\n')
        with self.assertRaisesRegex(ValueError, 'unverified-evidence-signature'):
            prepare.signed_input(record, prepare.PROJECT, trust, 'fixture-admin', tool)
        for body, error in ((b'{"story":"4.3","story":"other"}', 'duplicate-json-key'),
                            (b'{"value":NaN}', 'nonfinite-json'),
                            (b'{"registrationToken":"fixture"}', 'prohibited-evidence-field')):
            record.write_bytes(body)
            sign()
            with self.subTest(body=body), self.assertRaisesRegex(ValueError, error):
                prepare.signed_input(record, prepare.PROJECT, trust, 'fixture-admin', tool)


if __name__ == '__main__':
    unittest.main()
