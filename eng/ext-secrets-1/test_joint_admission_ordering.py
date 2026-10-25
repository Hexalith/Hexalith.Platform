"""Disposable SQLite receiving/admission regressions; never a production cohort."""
import copy
import hashlib
import sqlite3
import time
import unittest
from contextlib import closing
from unittest.mock import patch
from cryptography.hazmat.primitives.asymmetric import ec
from joint_live_phase_runner import Blocked, PHASES, partitions, canonical, digest
from joint_authority_service import CohortHttpApplication, RetainedPhaseOwner
import test_joint_live_phase_runner as runner_fixture
from test_joint_live_phase_runner import Owner


class JointAdmissionTests(unittest.TestCase):
    def setUp(self): self.fixture=runner_fixture.JointRunnerTests();self.fixture.setUp()
    def tearDown(self): self.fixture.tearDown()

    def test_retry_recovers_prior_dispatched_state_before_readiness_or_authority_outage(self):
        f=self.fixture;f.owners[PHASES[1]].lose=True
        with self.assertRaises(Blocked): f.runner().run()
        state=f.authority.lookup_attempt(f.binding)
        f.owners[PHASES[2]].missing=['FixtureReadinessOutage']
        with self.assertRaises(Blocked) as failure:f.runner().run()
        self.assertTrue(failure.exception.effects_started);self.assertEqual(state['attemptId'],failure.exception.attempt_id)
        self.assertEqual('Unknown',failure.exception.effect_state)
        with patch.object(f.authority,'lookup_attempt',side_effect=TimeoutError()):
            with self.assertRaises(Blocked) as unknown:f.runner().run()
        self.assertTrue(unknown.exception.effects_started);self.assertEqual('LookupRequired',unknown.exception.attempt_state)
        changed=f.binding|{'configurationSha256':'f'*64}
        with self.assertRaises(Blocked) as substituted:f.runner(changed).run()
        self.assertTrue(substituted.exception.effects_started)

    def test_authenticated_current_absence_allows_zero_report_without_reserving(self):
        f=self.fixture;f.owners[PHASES[0]].missing=['FixtureMissing']
        with self.assertRaises(Blocked) as failure:f.runner().run()
        self.assertFalse(failure.exception.effects_started);self.assertEqual('NotReserved',failure.exception.attempt_state)
        with closing(sqlite3.connect(f.path)) as db:self.assertEqual(0,db.execute('SELECT count(*) FROM attempts').fetchone()[0])

    def _app(self,method,value,dispatch):
        f=self.fixture;credential='fixture-workload';now=int(time.time())
        with closing(sqlite3.connect(f.path)) as db,db:
            db.execute('INSERT OR REPLACE INTO workload_grants VALUES(?,?,?,0)',(hashlib.sha256(credential.encode()).hexdigest(),canonical([method]),now+300))
        app=CohortHttpApplication(f.path,ec.generate_private_key(ec.SECP256R1()),dispatch,'file:/fixture-channel')
        body=canonical({'protocol':'OQ-5.4-1.joint.v1','requestId':'fixture-request','method':method,'payload':value})
        with patch('qualify.read_mounted_credential',return_value='channel'):
            return app.handle(method,{'dapr-api-token':'channel','Authorization':'Bearer '+credential},body)

    def test_revocation_and_reservation_share_one_durable_ordering_boundary(self):
        f=self.fixture
        ready=[Owner().preflight(f.binding,p,partitions()[p])['readiness'] for p in range(4)]
        value={'binding':f.binding,'attemptId':'isolated-ordering','ownerReadiness':ready}
        def revoke_then_dispatch(method,value):
            with closing(sqlite3.connect(f.path)) as db,db:db.execute('UPDATE workload_grants SET revoked=1')
            return f.store.dispatch(method,value)
        with self.assertRaises(Blocked):self._app('reserve',value,revoke_then_dispatch)
        with closing(sqlite3.connect(f.path)) as db:self.assertEqual(0,db.execute('SELECT count(*) FROM attempts').fetchone()[0])
        def admit_then_revoke(method,value):
            result=f.store.dispatch(method,value)
            with closing(sqlite3.connect(f.path)) as db,db:db.execute('UPDATE workload_grants SET revoked=1')
            return result
        self.assertEqual('isolated-ordering',self._app('reserve',value,admit_then_revoke)['payload']['result']['attemptId'])
        with closing(sqlite3.connect(f.path)) as db:self.assertEqual(1,db.execute('SELECT count(*) FROM attempts').fetchone()[0])

    def test_first_owner_admission_orders_revocation_without_holding_sql_lock_during_native_work(self):
        f=self.fixture;key=digest(f.binding);attempt=f.authority.reserve(f.binding,'fixture-owner-order')['attemptId']
        request={'protocol':'OQ-5.4-1.joint.v1','attemptId':attempt,'bindingDigest':key,'phase':0,'owner':PHASES[0],
                 'previousSha256':key,'cases':partitions()[0],'requestId':digest([attempt,key,0,key])}
        f.authority.retain(request)
        class IndependentAuthority:
            def call(self,method,value):return {'exact':True}
        class Executor:
            count=0
            def execute(self,request):
                self.count+=1
                # A second connection can commit revocation while native work runs: no owner SQL lock is held.
                with closing(sqlite3.connect(f.path,timeout=1)) as db,db:db.execute('UPDATE workload_grants SET revoked=1')
                return None
            def lookup(self,request):return None
        executor=Executor();owner=RetainedPhaseOwner(f.path,0,f.binding,executor,lambda p:{'payload':p,'signature':'isolated'},IndependentAuthority())
        def revoke_first(method,value):
            with closing(sqlite3.connect(f.path)) as db,db:db.execute('UPDATE workload_grants SET revoked=1')
            return owner.dispatch(method,value)
        with self.assertRaises(Blocked):self._app('execute',request,revoke_first)
        self.assertEqual(0,executor.count)
        with closing(sqlite3.connect(f.path)) as db:self.assertEqual(0,db.execute('SELECT count(*) FROM phase_originals').fetchone()[0])
        self.assertIsNone(self._app('execute',request,owner.dispatch)['payload']['result']);self.assertEqual(1,executor.count)
        with closing(sqlite3.connect(f.path)) as db:self.assertEqual(1,db.execute('SELECT count(*) FROM phase_originals').fetchone()[0])

    def test_absence_challenges_remain_one_per_request_expire_and_are_consumed(self):
        f=self.fixture;key=digest(f.binding);attempt=f.authority.reserve(f.binding,'fixture-absence-bound')['attemptId']
        request={'protocol':'OQ-5.4-1.joint.v1','attemptId':attempt,'bindingDigest':key,'phase':0,'owner':PHASES[0],
                 'previousSha256':key,'cases':partitions()[0],'requestId':digest([attempt,key,0,key])}
        f.authority.retain(request)
        class Rpc:
            def call(_,m,v):return f.store.dispatch(m,v)
        class Executor:
            count=0
            def execute(self,r):self.count+=1;return 'Passed',r['cases'],{}
            def lookup(self,r):return None
        executor=Executor();owner=RetainedPhaseOwner(f.path,0,f.binding,executor,lambda p:{'payload':p,'signature':'isolated-signature'},Rpc())
        proof=owner.dispatch('lookup',request)
        for _ in range(300):self.assertEqual(proof,owner.dispatch('lookup',request))
        with closing(sqlite3.connect(f.path)) as db:self.assertEqual(1,db.execute('SELECT count(*) FROM phase_absences').fetchone()[0])
        with patch('time.time',return_value=proof['payload']['validUntil']):new=owner.dispatch('lookup',request)
        self.assertNotEqual(proof,new)
        with self.assertRaises(Blocked):owner.dispatch('execute-if-absent',{'request':request,'absence':proof})
        # Restore the new proof's issuing instant; two conditional deliveries still execute once.
        with patch('time.time',return_value=new['payload']['observedAt']):
            first=owner.dispatch('execute-if-absent',{'request':request,'absence':new})
            self.assertEqual(first,owner.dispatch('execute-if-absent',{'request':request,'absence':new}))
        self.assertEqual(1,executor.count)
        with closing(sqlite3.connect(f.path)) as db:self.assertEqual(0,db.execute('SELECT count(*) FROM phase_absences').fetchone()[0])
