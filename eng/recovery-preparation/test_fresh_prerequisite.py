"""Synthetic offline successor tests; no credentials or operational tools."""

from datetime import datetime, timedelta, timezone
from contextlib import redirect_stderr, redirect_stdout
import importlib.util
import io
import json
from pathlib import Path
import os
import tempfile
import sys
import unittest
from unittest.mock import patch


SPEC = importlib.util.spec_from_file_location('fresh_prerequisite', Path(__file__).with_name('fresh_prerequisite.py'))
guard = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(guard)
NOW = datetime(2026, 10, 17, 14, 30, tzinfo=timezone.utc)


class FreshPrerequisiteTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        self.policy = self.root / 'old-policy.json'
        self.refusal = self.root / 'refusal.json'
        self.owner = self.root / 'owner.json'
        self.record = self.root / 'actual-public-observation.json'
        self.output = self.root / 'output'
        self.output.mkdir(mode=0o700)
        self.write(self.policy, {
            'kind': 'hexalith.unsigned-recovery-policy-candidate',
            'recoveryId': guard.HISTORICAL_RID, 'preparedAt': guard.HISTORICAL_PREPARED,
            'policyExpiresAt': guard.HISTORICAL_CEILING, 'maximumPolicyAgeSeconds': 86400,
            'signed': False, 'operationalGate': 'closed', 'capturedAt': None, 'acceptedAt': None,
            'retainedDecisions': {'destination': {'bucket': 'synthetic-existing-destination'},
                                  'signing': {'administratorOnly': True}},
            'targetProposals': {'historical': True}, 'freshMeasurementsRequired': True,
        })
        self.write(self.refusal, {
            'kind': 'offline-intake-exact-verifier-result', 'exitCode': 1,
            'operationalGate': 'closed',
            'command': ['/never/execute/old-verifier', '--sha256', guard.HISTORICAL_SEALED_SHA256],
            'stdout': json.dumps({'status': 'refused', 'operationalGate': 'closed',
                                  'reason': 'offline-revision-integrity-or-custody-refusal'}),
        })
        self.write(self.owner, {
            'kind': 'sole-owner-documentary-policy-direction',
            'declaredOwner': {'name': 'Synthetic owner'},
            'trustModel': {'independentHumanReviewerRequired': False,
                           'scope': 'Owner documents only; actual facts require observations.'},
        })
        self.write(self.record, {'observedAt': '2026-10-17T14:29:00Z', 'outcome': 'synthetic'})
        for name, path in (('ORIGINAL_POLICY_SHA256', self.policy), ('ORIGINAL_REFUSAL_SHA256', self.refusal)):
            anchor = patch.object(guard, name, guard.sha(path.read_bytes()))
            anchor.start()
            self.addCleanup(anchor.stop)

    def write(self, path, value):
        path.write_bytes(guard.canonical(value))
        path.chmod(0o600)

    def prepare(self, **kwargs):
        return guard.prepare(self.output, self.policy, self.refusal, self.owner,
                             [('public-observation', self.record)], now=kwargs.get('now', NOW))

    def replace_and_reanchor(self, receipt, name, change):
        root = Path(receipt['path'])
        value = guard.parse((root / name).read_bytes())
        change(value)
        self.write(root / name, value)
        manifest = guard.parse((root / 'packet.json').read_bytes())
        raw = (root / name).read_bytes()
        manifest['files'][name] = {'bytes': len(raw), 'sha256': guard.sha(raw)}
        self.write(root / 'packet.json', manifest)
        return guard.sha((root / 'packet.json').read_bytes())

    def testFreshPreparationPreservesOriginalBytesAndTimes(self):
        originals = {path: (path.read_bytes(), path.stat().st_mtime_ns) for path in (self.policy, self.refusal, self.owner)}
        receipt = self.prepare()
        result = guard.verify(Path(receipt['path']), receipt['packetSha256'], now=NOW)
        self.assertFalse(result['candidateExpired'])
        self.assertFalse(result['recoveryCurrent'])
        self.assertEqual(result['missingEvidenceCount'], len(guard.GAPS))
        root = Path(receipt['path'])
        self.assertEqual((root / 'prior-policy.original.json').read_bytes(), originals[self.policy][0])
        self.assertEqual((root / 'original-verifier-refusal.json').read_bytes(), originals[self.refusal][0])
        fresh = guard.parse((root / 'policy.candidate.json').read_bytes())
        self.assertEqual(fresh['preparedAt'], '2026-10-17T14:30:00Z')
        self.assertEqual(fresh['policyExpiresAt'], '2026-10-18T14:30:00Z')
        self.assertIn(receipt['rid'], fresh['targetProposals']['keycloakNamespace'])
        for path, before in originals.items():
            self.assertEqual((path.read_bytes(), path.stat().st_mtime_ns), before)

    def testCollisionRetainsFirstPacket(self):
        receipt = self.prepare()
        original = (Path(receipt['path']) / 'packet.json').read_bytes()
        with self.assertRaises(FileExistsError):
            self.prepare()
        self.assertEqual((Path(receipt['path']) / 'packet.json').read_bytes(), original)

    def testExpiryNeverBecomesCurrentRecovery(self):
        receipt = self.prepare()
        result = guard.verify(Path(receipt['path']), receipt['packetSha256'], now=NOW + timedelta(days=1))
        self.assertTrue(result['candidateExpired'])
        self.assertEqual(result['operationalGate'], 'closed')
        self.assertFalse(result['approvalGranted'])

    def testFuturePreparationRefused(self):
        receipt = self.prepare()
        with self.assertRaisesRegex(guard.Refusal, 'future-preparation'):
            guard.verify(Path(receipt['path']), receipt['packetSha256'], now=NOW - timedelta(seconds=1))

    def testPayloadAndExternalRecordTamperRefused(self):
        receipt = self.prepare()
        self.record.write_bytes(b'{"changed":true}\n')
        with self.assertRaisesRegex(guard.Refusal, 'bound-local-record-changed'):
            guard.verify(Path(receipt['path']), receipt['packetSha256'], now=NOW)
        self.write(self.record, {'observedAt': '2026-10-17T14:29:00Z', 'outcome': 'synthetic'})
        (Path(receipt['path']) / 'prerequisite.json').write_bytes(b'{}\n')
        with self.assertRaisesRegex(guard.Refusal, 'payload-digest-mismatch'):
            guard.verify(Path(receipt['path']), receipt['packetSha256'], now=NOW)

    def testInventedAuthorityAndHistoricalRefreshRefusedEvenReanchored(self):
        receipt = self.prepare()
        anchor = self.replace_and_reanchor(receipt, 'prerequisite.json', lambda value: value.update(authorityAccepted=True))
        with self.assertRaisesRegex(guard.Refusal, 'closed-prerequisite-required'):
            guard.verify(Path(receipt['path']), anchor, now=NOW)
        anchor = self.replace_and_reanchor(receipt, 'prerequisite.json', lambda value: value.update(authorityAccepted=False))
        anchor = self.replace_and_reanchor(receipt, 'prerequisite.json',
                                          lambda value: value['historicalCandidate'].update(ceilingExclusive='2026-10-18T14:30:00Z'))
        with self.assertRaisesRegex(guard.Refusal, 'historical-or-documentary-claims-mismatch'):
            guard.verify(Path(receipt['path']), anchor, now=NOW)

    def testOldTargetAndAlteredRetainedDecisionRefused(self):
        receipt = self.prepare()
        anchor = self.replace_and_reanchor(receipt, 'policy.candidate.json',
                                          lambda value: value['retainedDecisions'].update(signing={'administratorOnly': False}))
        with self.assertRaisesRegex(guard.Refusal, 'retained-policy-or-fresh-targets-mismatch'):
            guard.verify(Path(receipt['path']), anchor, now=NOW)

    def testSymlinkInputAndUnsafeCustodyRefused(self):
        link = self.root / 'alias.json'
        link.symlink_to(self.policy)
        with self.assertRaisesRegex(guard.Refusal, 'symlink-refused'):
            guard.prepare(self.output, link, self.refusal, self.owner, now=NOW)
        self.record.chmod(0o666)
        with self.assertRaisesRegex(guard.Refusal, 'unsafe-writable-custody'):
            self.prepare()

    def testForgedHistoricalSuccessRefused(self):
        value = guard.parse(self.refusal.read_bytes())
        value['exitCode'] = 0
        self.write(self.refusal, value)
        with self.assertRaisesRegex(guard.Refusal, 'historical-original-digest-mismatch'):
            self.prepare()

    def testDuplicateEvidenceRoleAndDuplicateJsonKeyRefused(self):
        with self.assertRaisesRegex(guard.Refusal, 'duplicate-or-excess-evidence-role'):
            guard.prepare(self.output, self.policy, self.refusal, self.owner,
                          [('same-role', self.record), ('same-role', self.record)], now=NOW)
        with self.assertRaisesRegex(guard.Refusal, 'duplicate-json-key'):
            guard.parse(b'{"status":"missing","status":"verified"}')

    def testRacingAncestorCannotRedirectWrites(self):
        trap = self.root / 'unrelated'
        trap.mkdir(mode=0o700)
        retained = self.root / 'retained-output'
        real_mkdir = os.mkdir

        def replace_parent(name, mode=0o777, *, dir_fd=None):
            result = real_mkdir(name, mode, dir_fd=dir_fd)
            if dir_fd is not None:
                self.output.rename(retained)
                self.output.symlink_to(trap, target_is_directory=True)
            return result

        with patch.object(guard.os, 'mkdir', side_effect=replace_parent):
            with self.assertRaises((guard.Refusal, OSError)):
                self.prepare()
        self.assertEqual(list(trap.iterdir()), [])
        self.assertTrue((retained / NOW.strftime('%Y%m%dt%H%M%Sz') / 'packet.json').is_file())

    def assertBoundedCliRefusal(self, arguments):
        stdout, stderr = io.StringIO(), io.StringIO()
        with patch.object(sys, 'argv', ['fresh_prerequisite.py', *arguments]), \
                redirect_stdout(stdout), redirect_stderr(stderr):
            self.assertEqual(guard.main(), 1)
        self.assertEqual(json.loads(stdout.getvalue()), {
            'status': 'refused', 'operationalGate': 'closed',
            'reason': 'offline-prerequisite-input-or-custody-refusal'})
        self.assertEqual(stderr.getvalue(), '')

    def testMalformedManifestRootsAndFilesHaveBoundedCliRefusal(self):
        receipt = self.prepare()
        root = Path(receipt['path'])
        original = guard.parse((root / 'packet.json').read_bytes())
        for malformed in ([], None, 'not-an-object', 7, {**original, 'files': []},
                          {**original, 'files': {'unhashable-ignored': []}}):
            with self.subTest(value=malformed):
                self.write(root / 'packet.json', malformed)
                anchor = guard.sha((root / 'packet.json').read_bytes())
                self.assertBoundedCliRefusal(['verify', '--packet', str(root), '--sha256', anchor])

    def testMalformedPayloadRootAndNestedObjectsHaveBoundedCliRefusal(self):
        receipt = self.prepare()
        root = Path(receipt['path'])
        manifest = (root / 'packet.json').read_bytes()
        packet = (root / 'prerequisite.json').read_bytes()
        owner = (root / 'owner-documentary-policy.original.json').read_bytes()
        cases = (
            ('prerequisite.json', []),
            ('prerequisite.json', {**guard.parse(packet), 'localRecords': [None]}),
            ('prerequisite.json', {**guard.parse(packet), 'localRecords': [[]]}),
            ('prerequisite.json', {**guard.parse(packet), 'localRecords': [{'role': [], 'path': '/never/read'}]}),
            ('owner-documentary-policy.original.json', []),
            ('owner-documentary-policy.original.json', {**guard.parse(owner), 'trustModel': []}),
            ('owner-documentary-policy.original.json', {**guard.parse(owner), 'declaredOwner': []}),
        )
        for name, value in cases:
            with self.subTest(name=name, value=value):
                (root / 'packet.json').write_bytes(manifest)
                (root / 'prerequisite.json').write_bytes(packet)
                (root / 'owner-documentary-policy.original.json').write_bytes(owner)
                self.write(root / name, value)
                changed = guard.parse(manifest)
                raw = (root / name).read_bytes()
                changed['files'][name] = {'bytes': len(raw), 'sha256': guard.sha(raw)}
                self.write(root / 'packet.json', changed)
                anchor = guard.sha((root / 'packet.json').read_bytes())
                self.assertBoundedCliRefusal(['verify', '--packet', str(root), '--sha256', anchor])

    def testMalformedOwnerPreparationHasBoundedCliRefusal(self):
        original = guard.parse(self.owner.read_bytes())
        for malformed in ([], {**original, 'trustModel': []}, {**original, 'declaredOwner': []}):
            with self.subTest(value=malformed):
                self.write(self.owner, malformed)
                self.assertBoundedCliRefusal([
                    'prepare', '--output-parent', str(self.output), '--prior-policy', str(self.policy),
                    '--original-refusal', str(self.refusal), '--owner-policy', str(self.owner)])
        self.assertEqual(list(self.output.iterdir()), [])


if __name__ == '__main__':
    unittest.main()
