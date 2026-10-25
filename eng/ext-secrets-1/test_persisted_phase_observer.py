"""Isolated signed-original reader tests; fixture receipts are not installed evidence."""
import base64
import hashlib
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import ec

from joint_authority_service import PersistedPhaseObserver
from joint_live_phase_runner import Blocked, canonical, digest, partitions, validate_source_checkout, run_installed_cohort
from test_qualify import Fixture


class PersistedObserverTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="fixture-persisted-observer-")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.key = ec.generate_private_key(ec.SECP256R1())
        anchor = self.root / 'fixture-owner.pem'
        anchor.write_bytes(self.key.public_key().public_bytes(serialization.Encoding.PEM, serialization.PublicFormat.SubjectPublicKeyInfo))
        self.binding = {'fixtureOnly': True}
        self.request = {'requestId': digest('fixture-original'), 'bindingDigest': digest(self.binding), 'phase': 0, 'cases': partitions()[0]}
        self.evidence_root = self.root / self.request['requestId']
        self.evidence_root.mkdir()
        self.current = True
        self.admitted = True
        fixture = self
        class Authority:
            def call(self, method, value):
                return {'current': fixture.current} if method == 'current' else {'exact': fixture.admitted}
        self.descriptor = {'ownerOriginalAnchorFile': str(anchor), 'ownerOriginalAnchorSha256': hashlib.sha256(anchor.read_bytes()).hexdigest(),
                           'evidenceDirectory': str(self.root), 'installedArtifacts': [{'path': str(anchor), 'sha256': hashlib.sha256(anchor.read_bytes()).hexdigest()}]}
        self.observer = PersistedPhaseObserver(self.descriptor, self.binding, Authority())
        case = self.request['cases'][0]
        self.evidence = self.evidence_root / (hashlib.sha256(case.encode()).hexdigest() + '.json')
        self.evidence.write_bytes(canonical({'sourceKind': 'PersistedOwnerRead', 'requestSha256': digest(self.request), 'caseId': case,
            'sourceReceipts': [{'sourceRevision': 1, 'eventId': 'fixture-event', 'sourceSha256': digest('fixture-native-bytes')}], 'outcome': 'Verified'}))
        self.payload = {'requestSha256': digest(self.request), 'bindingDigest': digest(self.binding),
                        'observations': [{'caseId': case, 'evidenceSha256': hashlib.sha256(self.evidence.read_bytes()).hexdigest()}]}
        self.write_original()

    def write_original(self):
        (self.evidence_root / 'owner-original.json').write_bytes(canonical({'payload': self.payload,
            'signature': base64.b64encode(self.key.sign(canonical(self.payload), ec.ECDSA(hashes.SHA256()))).decode()}))

    def lookup(self):
        return self.observer.dispatch('lookup-phase-original', {'binding': self.binding, 'request': self.request})

    def test_reads_complete_signed_original_and_exact_persisted_case_bytes(self):
        result = self.lookup()
        self.assertEqual('Passed', result['state'])
        self.assertEqual(self.request['cases'], result['passedCases'])
        self.evidence.write_text('substituted')
        with self.assertRaises(Blocked): self.lookup()

    def test_unavailable_or_changed_current_admission_and_incomplete_partition_deny(self):
        self.current = False
        with self.assertRaises(Blocked): self.lookup()
        self.current, self.admitted = True, False
        with self.assertRaises(Blocked): self.lookup()
        self.admitted = True
        self.payload['observations'] = []
        self.write_original()
        with self.assertRaises(Blocked): self.lookup()

    def test_missing_original_stays_unknown_and_changed_installed_input_blocks_preflight(self):
        (self.evidence_root / 'owner-original.json').unlink()
        self.assertEqual('Unknown', self.lookup()['state'])
        Path(self.descriptor['installedArtifacts'][0]['path']).write_text('changed')
        result = self.observer.dispatch('preflight-phase', {'binding': self.binding, 'phase': 0, 'cases': partitions()[0]})
        self.assertEqual(['ExactInstalledObserverInput'], result['missingInputs'])


class SourceCheckoutTests(unittest.TestCase):
    def test_real_isolated_checkout_must_match_commit_and_be_clean(self):
        with tempfile.TemporaryDirectory(prefix='fixture-executing-checkout-') as directory:
            fixture = Fixture(directory)
            validate_source_checkout({'platformCommit': fixture.target}, fixture.root)
            with self.assertRaises(Blocked): validate_source_checkout({'platformCommit': '0' * 40}, fixture.root)
            (fixture.root / 'dirty').write_text('fixture')
            with self.assertRaises(Blocked): validate_source_checkout({'platformCommit': fixture.target}, fixture.root)

    def test_checkout_refusal_precedes_any_network_or_reservation(self):
        with tempfile.TemporaryDirectory() as directory:
            config = Path(directory) / 'fixture-joint.json'
            config.write_bytes(canonical({'binding': {'platformCommit': 'a' * 40}}))
            with patch('qualify.git', return_value='b' * 40), patch('joint_live_phase_runner.DaprCohortRpc') as rpc:
                with self.assertRaises(Blocked): run_installed_cohort(config, Path(directory) / 'missing-register')
                rpc.assert_not_called()


if __name__ == '__main__':
    unittest.main()
