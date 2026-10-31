from copy import deepcopy
from datetime import datetime,timezone,timedelta
import unittest
import telemetry_proof as p
from test_telemetry_postgres_adapter import fixture, snapshot_binding, canary_records, canary_bytes


class TelemetryProofTests(unittest.TestCase):
    def setUp(self):
        self.value=fixture();self.value['transfer']['key']='memories/20261031t175250z/telemetry-postgresql.dump.age'
        self.source=canary_bytes(canary_records(self.value));self.target=canary_bytes(canary_records(self.value,target=True))
        self.comparison=p.adapter.compare_canaries(self.value,self.source,self.target)
        self.manifest={'localEvidence':[],'objects':[]}
        self.records={}
        now=datetime.now(timezone.utc).replace(microsecond=0)
        self.artifact={'key':self.value['transfer']['key'],'versionId':'immutable-version','bytes':400,'sha256':'a'*64}
        retain=(now+timedelta(days=31)).isoformat()
        self.manifest['objects']=[dict(self.artifact,contentLength=400,readBackSha256='a'*64,readBy='validator',serverSideEncryption='AES256',objectLockMode='COMPLIANCE',objectLockRetainUntil=retain,retentionApi={'mode':'COMPLIANCE','retainUntil':retain})]
        self.targets={k:'fresh-'+k for k in ('namespace','statefulSet','pvc','pv')}
        for stage in p.STAGES:
            body={'kind':'telemetry-'+stage,'recoveryId':'20261031t175250z','proposalInputSha256':p.digest(p.adapter.canonical(self.value)),'sourceClusterUid':self.value['source']['cluster']['uid'],'sourceUid':self.value['source']['statefulSet']['uid'],'verificationResult':'pass'}
            if stage=='capture':
                body.update(snapshotBinding=snapshot_binding(self.value),sourceCanarySha256=p.digest(self.source),plaintextSha256='b'*64,plaintextBytes=200,artifact=self.artifact,capturedAt=now.isoformat(),expiresAt=(now+timedelta(days=1)).isoformat(),dataCutoff=(now-timedelta(seconds=2)).isoformat(),measuredRpo={'seconds':3,'withinPolicy':True,'sourceObservedAt':(now+timedelta(seconds=1)).isoformat()},checks={k:True for k in ('snapshotHolderAliveThroughDump','sameSnapshotDumpAndCanaries','remoteSessionsAbsent','allSourceWritersQuiesced','quiescedSequencesStable','artifactEncrypted','independentValidatorReadback')})
            if stage=='restore':
                body.update(plaintextSha256='b'*64,plaintextBytes=200,targetCanarySha256=p.digest(self.target),isolatedTargetUids=self.targets,checks={k:True for k in ('decryptedStdinMatchesCapture','targetToolsAndImageMatch','emptyTargetBeforeRestore','restoreCommandSucceeded','completeTargetCanaryCaptured','targetRolesRestricted')})
            if stage=='comparison':body.update(comparison=self.comparison)
            if stage in ('isolation','cleanup'):body['isolatedTargetUids']=self.targets
            if stage in ('isolation','resume','cleanup'):
                body['checks']={k:True for k in {'isolation':p.ISOLATION_CHECKS,'resume':p.RESUME_CHECKS,'cleanup':p.CLEANUP_CHECKS}[stage]}
            self.bind(stage,body)

    def bind(self,stage,body):
        raw=p.canonical(body);binding={'path':'memories/telemetry/'+stage+'.json','sha256':p.digest(raw),'raw':raw}
        self.records[stage]=binding
        self.manifest['localEvidence']=[v for v in self.manifest['localEvidence'] if v['path']!=binding['path']]
        self.manifest['localEvidence'].append({k:binding[k] for k in ('path','sha256')})

    def assemble(self):return p.assemble(self.value,self.records,self.manifest,self.source,self.target)

    def test_producer_and_validator_recompute_actual_full_canary_comparison(self):
        nested=self.assemble()
        self.assertTrue(all(nested['checks'].values()))
        self.assertEqual(p.validation_check(nested,self.value,self.records,self.manifest,self.source,self.target)['proofSha256'],p.digest(p.canonical(nested)))
        self.assertNotIn('raw',str(nested))
        self.targets['namespace']='later-mutation'
        self.assertEqual(nested['isolatedTargetUids']['namespace'],'fresh-namespace')

    def test_false_stage_cannot_pass_even_with_coherently_rebound_manifest(self):
        for stage,check in [('capture','remoteSessionsAbsent'),('restore','targetRolesRestricted'),('isolation','sourceDatabaseAccessDenied'),('resume','sourceHealthy'),('cleanup','ownedVolumePathAbsent')]:
            old=deepcopy(self.records);manifest=deepcopy(self.manifest)
            body=__import__('json').loads(self.records[stage]['raw']);body['checks'][check]=False;self.bind(stage,body)
            with self.subTest(stage=stage),self.assertRaises(ValueError):self.assemble()
            self.records,self.manifest=old,manifest

    def test_missing_or_substituted_evidence_and_ciphertext_version_are_refused(self):
        self.manifest['localEvidence'].pop()
        with self.assertRaises(ValueError):self.assemble()
        self.setUp();self.manifest['objects'][0]['versionId']='other-version'
        with self.assertRaises(ValueError):self.assemble()

    def test_comparator_and_validator_reject_changed_rows_despite_pass_booleans(self):
        records=canary_records(self.value,target=True);records['dataContents'][0]['sha256']='c'*64
        self.target=canary_bytes(records)
        with self.assertRaises(ValueError):self.assemble()
        self.setUp();nested=self.assemble();nested['checks']['completeObjectCoverage']=False
        with self.assertRaises(ValueError):p.validation_check(nested,self.value,self.records,self.manifest,self.source,self.target)

    def test_rpo_is_recomputed_from_actual_cutoff_and_durable_source_observation(self):
        body=__import__('json').loads(self.records['capture']['raw']);body['measuredRpo']['seconds']=2;self.bind('capture',body)
        with self.assertRaises(ValueError):self.assemble()

    def test_fractional_rpo_is_rounded_up_without_rewriting_event_times(self):
        body=__import__('json').loads(self.records['capture']['raw'])
        captured=datetime.fromisoformat(body['capturedAt'])
        body['dataCutoff']=(captured-timedelta(seconds=1,microseconds=200000)).isoformat()
        body['measuredRpo']['sourceObservedAt']=(captured+timedelta(microseconds=1)).isoformat()
        body['measuredRpo']['seconds']=2
        self.bind('capture',body)
        self.assertEqual(self.assemble()['measuredRpo'],body['measuredRpo'])
        body['measuredRpo']['seconds']=1;self.bind('capture',body)
        with self.assertRaises(ValueError):self.assemble()

    def test_coarse_provider_time_before_fractional_cutoff_is_refused(self):
        body=__import__('json').loads(self.records['capture']['raw'])
        captured=datetime.fromisoformat(body['capturedAt'])
        body['dataCutoff']=(captured+timedelta(microseconds=1)).isoformat()
        body['measuredRpo']['seconds']=1;self.bind('capture',body)
        with self.assertRaises(ValueError):self.assemble()

    def test_fractional_interval_above_rpo_limit_is_refused(self):
        body=__import__('json').loads(self.records['capture']['raw'])
        captured=datetime.fromisoformat(body['capturedAt'])
        body['dataCutoff']=(captured-timedelta(seconds=1800,microseconds=1)).isoformat()
        body['measuredRpo']['sourceObservedAt']=captured.isoformat()
        body['measuredRpo']['seconds']=1800;self.bind('capture',body)
        with self.assertRaises(ValueError):self.assemble()


if __name__=='__main__':unittest.main()
