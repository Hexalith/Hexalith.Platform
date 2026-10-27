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
        self.source_key = ec.generate_private_key(ec.SECP256R1())
        source_anchor = self.root / 'independent-source.pem'
        source_anchor.write_bytes(self.source_key.public_key().public_bytes(serialization.Encoding.PEM,serialization.PublicFormat.SubjectPublicKeyInfo))
        facts = {name:1 for name in ('G1','G2','G3','G4','G5','G6','G7')}
        self.source = {'requestSha256':digest(self.request),'bindingDigest':digest(self.binding),'caseId':case,
                       'records':[{'sourceRevision':1,'eventId':'fixture-native-event','payload':facts}]}
        self.source_file = self.root / 'source' / self.request['requestId'] / self.evidence.name
        self.source_file.parent.mkdir(parents=True)
        self.source_file.write_bytes(canonical({'payload':self.source,'signature':base64.b64encode(self.source_key.sign(canonical(self.source),ec.ECDSA(hashes.SHA256()))).decode()}))
        self.descriptor['sourceEvidence'] = {case:{'anchorFile':str(source_anchor),'anchorSha256':hashlib.sha256(source_anchor.read_bytes()).hexdigest(),
            'directory':str(self.root/'source'),'invariants':{name:{'operation':'equals','path':[0,'payload',name],'expected':1} for name in facts}}}
        self.evidence.write_bytes(canonical({'requestSha256':digest(self.request),'caseId':case,'sourceRecordsSha256':digest(self.source['records'])}))
        self.payload = {'requestSha256': digest(self.request), 'bindingDigest': digest(self.binding),
                        'observations': [{'caseId': case, 'evidenceSha256': hashlib.sha256(self.evidence.read_bytes()).hexdigest()}]}
        self.write_original()

    def write_original(self):
        (self.evidence_root / 'owner-original.json').write_bytes(canonical({'payload': self.payload,
            'signature': base64.b64encode(self.key.sign(canonical(self.payload), ec.ECDSA(hashes.SHA256()))).decode()}))

    def lookup(self):
        return self.observer.dispatch('lookup-phase-original', {'binding': self.binding, 'request': self.request})

    def test_reads_complete_signed_original_and_exact_persisted_case_bytes(self):
        with self.assertRaises(Blocked) as failure:
            self.lookup()
        self.assertIn('ClosedNativePhaseSemanticsUnavailable', failure.exception.missing)
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
        self.assertIn('ExactInstalledObserverInput', result['missingInputs'])
        self.assertTrue(any('CanonicalProtectionOwner' in item for item in result['missingInputs']))

    def test_owner_receipt_shape_cannot_replace_actual_independent_source_read(self):
        self.source_file.unlink()
        self.evidence.write_bytes(canonical({'sourceKind':'PersistedOwnerRead','requestSha256':digest(self.request),'caseId':self.request['cases'][0],
            'sourceReceipts':[{'sourceRevision':1,'eventId':'invented','sourceSha256':digest('invented')}],'outcome':'Verified'}))
        self.payload['observations'][0]['evidenceSha256'] = hashlib.sha256(self.evidence.read_bytes()).hexdigest()
        self.write_original()
        self.assertEqual('Unknown',self.lookup()['state'])

    def test_independent_source_invariant_failure_denies_even_signed_owner_success(self):
        self.source['records'][0]['payload']['G1'] = 0
        self.source_file.write_bytes(canonical({'payload':self.source,'signature':base64.b64encode(self.source_key.sign(canonical(self.source),ec.ECDSA(hashes.SHA256()))).decode()}))
        self.evidence.write_bytes(canonical({'requestSha256':digest(self.request),'caseId':self.request['cases'][0],'sourceRecordsSha256':digest(self.source['records'])}))
        self.payload['observations'][0]['evidenceSha256'] = hashlib.sha256(self.evidence.read_bytes()).hexdigest(); self.write_original()
        with self.assertRaises(Blocked): self.lookup()

    def test_long_phase_and_later_recovery_verify_each_bound_immutable_case_proof_at_its_own_time(self):
        import time
        import qualify as q
        (self.root/'secrets-fixture').mkdir()
        fixture=Fixture(self.root/'secrets-fixture')
        case=next(case for case in q.plan() if case.id=='S1.reject.missing-config')
        base={'protocol':q.PROTOCOL,'runId':'fixture-retained-attempt','targetCommit':fixture.target,
              'isolatedTargetId':fixture.config['qualification']['isolatedTargetId'],'planSha256':q.plan_digest()}
        admission=base|{'operation':'enroll'};admission['requestId']=q.digest(admission)
        enrolled=fixture.call('enrollment','enroll',admission)
        original=base|{'caseId':case.id,'caseTargetId':enrolled['payload']['caseTargetIds'][case.id],
            'enrolledScope':enrolled['payload']['caseScopes'][case.id],
            'scenario':{'outcomes':list(case.outcomes),'expected':q.GLOBAL|case.expected,'boundary':case.boundary,
                        'persistedStages':['AfterEffect','AfterRestart','AfterRestore','AfterLookup']}}
        original['requestId']=q.digest(original)
        then=int(time.time())-3600
        def historical(role,proof):
            return fixture.sign(role,proof['payload']|{'observedAt':then,'validUntil':then+25})
        execution=historical('executor',fixture.call('executor','execute',original))
        observation=historical('observer',fixture.call('observer','observe',original))
        enrollment=historical('enrollment',enrolled)
        self.binding.update(platformCommit=fixture.target,configurationSha256=q.digest(fixture.config))
        self.request={'requestId':digest('long-fixture-phase'),'bindingDigest':digest(self.binding),'phase':1,
                      'attemptId':base['runId'],'cases':['secrets:'+case.id]}
        self.evidence_root=self.root/self.request['requestId'];self.evidence_root.mkdir()
        evidence={'request':original,'execution':execution,'observation':observation,'enrollment':enrollment,'cohortRequestId':self.request['requestId']}
        evidence_file=self.evidence_root/(hashlib.sha256(self.request['cases'][0].encode()).hexdigest()+'.json')
        evidence_file.write_bytes(canonical(evidence))
        self.descriptor.update(secretsRuntimeConfiguration=str(fixture.path),secretsRuntimeBinding=fixture.binding)
        self.payload={'requestSha256':digest(self.request),'bindingDigest':digest(self.binding),'completedAt':int(time.time()),
            'observations':[{'caseId':self.request['cases'][0],'evidenceSha256':hashlib.sha256(evidence_file.read_bytes()).hexdigest()}]}
        self.write_original()
        self.assertEqual('Passed',self.lookup()['state'])
        evidence['execution']['payload']['observedAt']+=1;evidence_file.write_bytes(canonical(evidence))
        self.payload['observations'][0]['evidenceSha256']=hashlib.sha256(evidence_file.read_bytes()).hexdigest();self.write_original()
        with self.assertRaises(q.QualificationFailed): self.lookup()

    def test_tautological_profile_cannot_certify_any_required_native_invariant(self):
        profile=self.descriptor['sourceEvidence'][self.request['cases'][0]]
        profile['invariants']={name:{'operation':'equals','path':[0,'sourceRevision'],'expected':1} for name in profile['invariants']}
        with self.assertRaises(Blocked) as failure:
            self.lookup()
        self.assertIn('ClosedNativePhaseSemanticsUnavailable',failure.exception.missing)


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
