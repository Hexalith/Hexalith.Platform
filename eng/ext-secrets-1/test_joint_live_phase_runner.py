import copy
import uuid
from sqlite_storage_fixture import enrolled_storage,completion_pool
import json
from pathlib import Path
import sqlite3
from contextlib import closing
import tempfile
import unittest
from unittest.mock import patch

def patch_time(value): return patch("time.time",return_value=value)

from joint_live_phase_runner import JointLivePhaseRunner, Blocked, PHASES, partitions, full_plan, digest, canonical, calendar_date
from joint_authority_service import CohortAuthorityStore


class Authority:
    def __init__(self, store): self.store = store
    def preflight(self, binding): return self.store.dispatch("preflight", {"binding": binding})["missingInputs"]
    def current(self, binding): return self.store.dispatch("current", {"binding": binding})["current"]
    def lookup_attempt(self, binding): return self.store.dispatch("lookup-attempt", {"binding":binding})
    def reserve(self, binding, attempt, readiness=None):
        if readiness is None: readiness = [Owner().preflight(binding, phase, partitions()[phase])["readiness"] for phase in range(4)]
        attempt=str(uuid.uuid5(uuid.NAMESPACE_URL,attempt)) if isinstance(attempt,str) and len(attempt)!=36 else attempt
        return self.store.dispatch("reserve", {"binding": binding, "attemptId": attempt, "ownerReadiness": readiness})
    def retain(self, request): return self.store.dispatch("retain", {"request": request})["first"]
    def publish(self, request, result):
        if not self.store.dispatch("publish", {"request": request, "result": result})["exact"]: raise Blocked("changed")
    def verify(self, request, result): return result["signature"] == "isolated-signature"
    def verify_completion(self, binding, result): return self.store.dispatch('completion', {'binding':binding,'result':result})['exact']


class Owner:
    def __init__(self): self.executions = []; self.lookups = []; self.originals = {}; self.lose = False; self.fail = False; self.missing = []
    def preflight(self, binding, phase, cases):
        import time
        now = int(time.time())
        return {"missingInputs": self.missing, "readiness": {"payload": {"bindingDigest": digest(binding), "phase": phase, "cases": cases,
            "profileSha256": binding["phaseExecutionProfileSha256"][str(phase)], "observedAt": now, "validUntil": now + 30}, "signature": "isolated-signature"}}

    def execute(self, request):
        self.executions.append(copy.deepcopy(request))
        result = {"payload": request | {"state": "Failed" if self.fail else "Passed", "passedCases": request["cases"]}, "signature": "isolated-signature"}
        self.originals[request["requestId"]] = result
        if self.lose: raise TimeoutError()
        return result
    def lookup(self, request):
        self.lookups.append(copy.deepcopy(request)); return self.originals.get(request["requestId"])


class JointRunnerTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.path = Path(self.directory.name) / 'authority.sqlite'
        self.binding = {"platformCommit": "a" * 40, "secretsCommit": "a" * 40, "hostCommit": "a" * 40,
                        "platformIntegrationDate": "2026-11-03", "protectionIntegrationDate": "2026-11-02",
                        "planSha256": digest(full_plan()), "host": "192.168.1.30", "partiesBranch": "B",
                        "timing": {"L": 300, "S": 30, "O": 600, "H": 86400, "R": 604800}}
        for name in ("configurationSha256", "profileSha256", "protectionProfileSha256", "executorAnchorSha256", "observerAnchorSha256", "enrollmentAnchorSha256", "currentTrustSha256", "installedImageSha256"):
            self.binding[name] = digest(name)
        self.binding["phaseExecutionProfileSha256"] = {str(phase): digest(["isolated-profile", phase]) for phase in range(4)}
        with closing(sqlite3.connect(self.path)) as db, db:
            db.executescript(Path(__file__).with_name('joint-authority-schema.sql').read_text())
            db.execute('INSERT INTO accepted_sets VALUES(?,?,1)', (digest(self.binding), canonical(self.binding)))
            db.execute('INSERT INTO current_set VALUES(1,?)', (digest(self.binding),))
        self.storage=enrolled_storage(self.path)
        self.completion_capacity=completion_pool(self.path)
        self.owners = {name: Owner() for name in PHASES}
        self.store = CohortAuthorityStore(self.path, lambda phase, result: result.get('signature') == 'isolated-signature',storage=self.storage)
        self.authority = Authority(self.store)

    def tearDown(self): self.directory.cleanup()
    def runner(self, binding=None): return JointLivePhaseRunner(binding or self.binding, self.authority, self.owners)

    def test_preflight_reserves_nothing_and_exact_coverage(self):
        self.runner().preflight()
        with closing(sqlite3.connect(self.path)) as db, db: self.assertEqual(0, db.execute('SELECT COUNT(*) FROM attempts').fetchone()[0])
        all_cases = [case for phase in partitions().values() for case in phase]
        self.assertEqual(len(all_cases), len(set(all_cases)))
        self.assertEqual(234, sum(case.startswith('secrets:') for case in all_cases))
        self.assertIn('secrets:Recovery.replay.lost-result', partitions()[1])
        self.assertIn('secrets:Recovery.dispatch.lookup-outage', partitions()[3])

    def test_complete_pair_reuses_attempt_and_never_reexecutes(self):
        first = self.runner().run()
        second = self.runner().run()
        self.assertEqual(first, second)
        self.assertTrue(first['cohortPassed'])
        self.assertFalse(first['dependencyAvailable'])
        for owner in self.owners.values(): self.assertEqual(1, len(owner.executions)); self.assertEqual(1, len(owner.lookups))
        self.assertTrue(self.store.dispatch('completion', {'binding': self.binding, 'result': first})['exact'])

    def test_completed_attempt_recovers_when_execution_readiness_inputs_have_been_consumed(self):
        first = self.runner().run_complete()
        def expired(*args): raise AssertionError("execution readiness was reacquired during exact recovery")
        self.authority.reserve = expired
        for owner in self.owners.values(): owner.preflight = expired
        self.assertEqual(first, self.runner().run_complete())
        self.assertEqual(4, sum(len(owner.executions) for owner in self.owners.values()))
        self.assertEqual(4, sum(len(owner.lookups) for owner in self.owners.values()))

    def test_retained_phase_admission_rejects_unretained_substituted_and_wrong_predecessor(self):
        attempt = self.authority.reserve(self.binding,"fixture-exact-phase")["attemptId"]
        request={"protocol":"OQ-5.4-1.joint.v1","attemptId":attempt,"bindingDigest":digest(self.binding),"phase":0,"owner":PHASES[0],"previousSha256":digest(self.binding),"cases":partitions()[0]}
        request["requestId"]=digest([attempt,request["bindingDigest"],0,request["previousSha256"]])
        self.assertFalse(self.store.dispatch("phase-admission",{"request":request})["exact"])
        self.authority.retain(request)
        self.assertTrue(self.store.dispatch("phase-admission",{"request":request})["exact"])
        for field,value in (("attemptId","foreign"),("previousSha256","f"*64),("owner",PHASES[1]),("cases",partitions()[1])):
            with self.assertRaises(Blocked): self.store.dispatch("phase-admission",{"request":request|{field:value}})

    def test_unknown_effect_after_restart_uses_exact_lookup(self):
        self.owners['secrets'].lose = True
        with self.assertRaises(Blocked) as caught: self.runner().run()
        self.assertTrue(caught.exception.effects_started)
        self.assertEqual('Reserved',caught.exception.attempt_state)
        self.store = CohortAuthorityStore(self.path, lambda phase, result: result.get('signature') == 'isolated-signature',storage=self.storage)
        self.authority = Authority(self.store)
        recovered = self.runner().run()
        self.assertTrue(recovered['cohortPassed'])
        owner = self.owners['secrets']
        self.assertEqual(owner.executions[0], owner.lookups[0])
        self.assertEqual(1, len(owner.executions))

    def test_failed_phase_keeps_both_unavailable_and_stops_order(self):
        self.owners['secrets'].fail = True
        for _ in range(2):
            with self.assertRaises(Blocked): self.runner().run()
        self.assertFalse(self.owners['host-refusing-consumers'].executions)
        self.assertEqual(1, len(self.owners['secrets'].executions))

    def test_current_authority_substitution_and_bad_date_deny_before_reservation(self):
        for date in ('2026-02-30', '2026-1-03', '2026-11-03T00:00:00'):
            changed = self.binding | {'platformIntegrationDate': date}
            with self.assertRaises(Blocked): self.runner(changed).run()
        changed = self.binding | {'configurationSha256': 'b' * 64}
        with self.assertRaises(Blocked): self.runner(changed).run()
        with closing(sqlite3.connect(self.path)) as db, db: self.assertEqual(0, db.execute('SELECT COUNT(*) FROM attempts').fetchone()[0])

    def test_out_of_order_phase_cannot_be_retained(self):
        reserved = self.authority.reserve(self.binding, 'test-attempt')
        request = {'protocol': 'OQ-5.4-1.joint.v1', 'attemptId': reserved['attemptId'], 'bindingDigest': digest(self.binding),
                   'phase': 3, 'owner': PHASES[3], 'previousSha256': digest(self.binding), 'cases': partitions()[3], 'requestId': 'changed'}
        with self.assertRaises(Blocked): self.authority.retain(request)

    def test_changed_installed_phase_pins_refuse_before_attempt_reservation(self):
        self.owners[PHASES[2]].missing = ["ExactInstalledPhaseExecutable"]
        with self.assertRaises(Blocked): self.runner().run()
        with closing(sqlite3.connect(self.path)) as db, db:
            self.assertEqual(0, db.execute('SELECT COUNT(*) FROM attempts').fetchone()[0])

    def test_final_completion_refusal_or_transport_loss_preserves_all_dispatched_effect_state(self):
        for raised in (False, True):
            def completion(binding, result):
                if raised: raise TimeoutError('fixture final completion acknowledgement lost')
                return False
            self.authority.verify_completion = completion
            with self.assertRaises(Blocked) as failure: self.runner().run_complete()
            self.assertTrue(failure.exception.effects_started)
            self.assertEqual('Reserved',failure.exception.attempt_state)
            self.assertIsNotNone(failure.exception.attempt_id)
            self.assertIsNotNone(failure.exception.phase_request_id)
            self.assertIn(failure.exception.effect_state, ('Observed','Unknown','Dispatched'))
        self.assertEqual(4, sum(len(owner.executions) for owner in self.owners.values()))

    def test_current_qualification_attempt_is_read_only_and_requires_existing_retained_authority(self):
        import qualify as q
        value={'bindingDigest':digest(self.binding),'attemptId':'7ba29076-fb70-4e51-b6f0-634f9b2709b7','targetCommit':self.binding['platformCommit'],'planSha256':q.plan_digest()}
        self.assertFalse(self.store.dispatch('qualification-attempt',value)['exact'])
        with closing(sqlite3.connect(self.path)) as db: self.assertEqual(0,db.execute('SELECT COUNT(*) FROM attempts').fetchone()[0])
        self.authority.reserve(self.binding,value['attemptId'])
        self.assertTrue(self.store.dispatch('qualification-attempt',value)['exact'])
        for key in value:
            self.assertFalse(self.store.dispatch('qualification-attempt',value|{key:'substituted'})['exact'])

    def test_current_case_attempt_requires_its_phase_original_before_any_case_can_execute(self):
        import qualify as q
        attempt=self.authority.reserve(self.binding,'fixture-current-case')['attemptId']
        value={'bindingDigest':digest(self.binding),'attemptId':attempt,'targetCommit':self.binding['platformCommit'],'planSha256':q.plan_digest(),'caseId':'S1.reject.missing-config'}
        self.assertFalse(self.store.dispatch('qualification-attempt',value)['exact'])
        self.runner().run()
        self.assertTrue(self.store.dispatch('qualification-attempt',value)['exact'])
        self.assertFalse(self.store.dispatch('qualification-attempt',value|{'caseId':'unapproved-case'})['exact'])

    def test_reservation_loss_after_stale_absence_preserves_unknown_and_known_identity(self):
        original = self.authority.reserve
        def interleaved(binding, attempt, readiness):
            retained = original(binding, "competing-coordinator", readiness)
            # Another coordinator can dispatch after our signed absence and before our failed reserve.
            request = {"protocol":"OQ-5.4-1.joint.v1", "attemptId":retained["attemptId"], "bindingDigest":digest(binding),
                       "phase":0,"owner":PHASES[0],"previousSha256":digest(binding),"cases":partitions()[0]}
            request["requestId"] = digest([request["attemptId"],request["bindingDigest"],0,request["previousSha256"]])
            self.authority.retain(request)
            self.owners[PHASES[0]].execute(request)
            raise TimeoutError("fixture reserve response lost")
        self.authority.reserve = interleaved
        with self.assertRaises(Blocked) as failure: self.runner().run()
        self.assertEqual("Unknown",failure.exception.effect_state)
        self.assertTrue(failure.exception.effects_started)
        self.assertEqual("ReservationUnknown",failure.exception.attempt_state)
        self.assertIsNotNone(failure.exception.attempt_id)
        self.authority.reserve = original
        result = self.runner().run()
        self.assertEqual(str(uuid.uuid5(uuid.NAMESPACE_URL,"competing-coordinator")),result["attemptId"])
        self.assertEqual(1,len(self.owners[PHASES[0]].executions))

    def test_independent_slow_readiness_is_acquired_as_one_fresh_bounded_set(self):
        from concurrent.futures import ThreadPoolExecutor
        import threading
        barrier = threading.Barrier(4)
        for owner in self.owners.values():
            previous = owner.preflight
            def ready(binding,phase,cases,previous=previous):
                barrier.wait(timeout=2)
                return previous(binding,phase,cases)
            owner.preflight = ready
        self.runner().preflight()
        with closing(sqlite3.connect(self.path)) as db:
            self.assertEqual(0,db.execute("SELECT COUNT(*) FROM attempts").fetchone()[0])

    def test_four_slow_valid_readiness_receipts_do_not_age_sequentially_before_reserve(self):
        import time
        baseline, started = int(time.time()), time.monotonic()
        for owner in self.owners.values():
            previous = owner.preflight
            def slow(binding,phase,cases,previous=previous):
                time.sleep(0.06)  # 12 seconds in this isolated scaled authority clock; four sequential calls exceed 30.
                return previous(binding,phase,cases)
            owner.preflight = slow
        with patch("time.time",side_effect=lambda:baseline+int((time.monotonic()-started)*200)):
            self.assertTrue(self.runner().run()["cohortPassed"])

    def test_readiness_total_deadline_reserves_nothing_and_cancels_pending_acquisition(self):
        import threading
        release = threading.Event()
        previous = self.owners[PHASES[0]].preflight
        def stalled(binding,phase,cases):
            release.wait(timeout=2)
            return previous(binding,phase,cases)
        self.owners[PHASES[0]].preflight = stalled
        try:
            with patch("joint_live_phase_runner.REQUEST_DEADLINE_SECONDS",0.03):
                with self.assertRaises(Blocked) as failure: self.runner().preflight()
            self.assertIn("BoundedFreshPhaseReadinessSet",failure.exception.missing)
            with closing(sqlite3.connect(self.path)) as db:
                self.assertEqual(0,db.execute("SELECT COUNT(*) FROM attempts").fetchone()[0])
        finally: release.set()

    def test_calendar_validation_uses_real_dates(self):
        self.assertEqual('2028-02-29', calendar_date('2028-02-29'))
        for value in ('2027-02-29', '2026-13-01', '2026-10-8', None):
            with self.assertRaises((ValueError, TypeError)): calendar_date(value)


if __name__ == '__main__': unittest.main()

class ReceivingServiceTests(unittest.TestCase):
    def setUp(self):
        self.fixture = JointRunnerTests(); self.fixture.setUp()
        import hashlib
        import time
        from cryptography.hazmat.primitives.asymmetric import ec
        self.signer = ec.generate_private_key(ec.SECP256R1())
        self.channel = Path(self.fixture.directory.name) / 'channel'
        self.channel.write_text('isolated-app-channel')
        with closing(sqlite3.connect(self.fixture.path)) as db, db:
            db.execute('INSERT INTO workload_grants VALUES(?,?,?,0)',
                       (hashlib.sha256(b'isolated-workload').hexdigest(), canonical(['preflight', 'current']), int(time.time()) + 60))

    def tearDown(self): self.fixture.tearDown()

    def test_authenticated_http_handler_signs_exact_current_proof_and_denies_substitution(self):
        from joint_authority_service import CohortHttpApplication
        import base64
        from cryptography.hazmat.primitives import hashes
        from cryptography.hazmat.primitives.asymmetric import ec
        app = CohortHttpApplication(self.fixture.path, self.signer, self.fixture.store.dispatch, 'file:' + str(self.channel),storage=self.fixture.storage)
        request = {'protocol': 'OQ-5.4-1.joint.v1', 'requestId': 'isolated-request', 'method': 'preflight', 'payload': {'binding': self.fixture.binding}}
        headers = {'dapr-api-token': 'isolated-app-channel', 'Authorization': 'Bearer isolated-workload'}
        result = app.handle('preflight', headers, canonical(request))
        self.signer.public_key().verify(base64.b64decode(result['signature']), canonical(result['payload']), ec.ECDSA(hashes.SHA256()))
        self.assertEqual([], result['payload']['result']['missingInputs'])
        with self.assertRaises(Blocked): app.handle('preflight', headers | {'Authorization': 'Bearer wrong'}, canonical(request))
        with self.assertRaises(Blocked): app.handle('preflight', headers | {'dapr-api-token': 'wrong'}, canonical(request))
        with self.assertRaises(Blocked): app.handle('reserve', headers, canonical(request | {'method': 'reserve'}))
        with closing(sqlite3.connect(self.fixture.path)) as db, db:
            self.assertEqual(0, db.execute('SELECT COUNT(*) FROM attempts').fetchone()[0])

    def test_receiving_owner_restart_with_null_original_only_looks_up(self):
        from joint_authority_service import RetainedPhaseOwner
        class Rpc:
            def __init__(self,store): self.store=store
            def call(self,method,payload): return self.store.dispatch(method,payload)
        class Executor:
            executions=0; lookups=0
            def execute(self,request): self.executions+=1; return None
            def lookup(self,request): self.lookups+=1; return None
        authority=self.fixture.authority
        reserved=authority.reserve(self.fixture.binding,'isolated-null-attempt'); key=digest(self.fixture.binding)
        request={'protocol':'OQ-5.4-1.joint.v1','attemptId':reserved['attemptId'],'bindingDigest':key,'phase':0,'owner':PHASES[0],
                 'previousSha256':key,'cases':partitions()[0],'requestId':digest([reserved['attemptId'],key,0,key])}
        authority.retain(request); executor=Executor()
        def receiver(): return RetainedPhaseOwner(self.fixture.path,0,self.fixture.binding,executor,lambda payload:{'payload':payload},Rpc(self.fixture.store),storage=self.fixture.storage,completion_capacity=self.fixture.completion_capacity)
        self.assertIsNone(receiver().dispatch('execute',request))
        self.assertIsNone(receiver().dispatch('execute',request))
        self.assertIsNone(receiver().dispatch('lookup',request))
        self.assertEqual(1,executor.executions); self.assertEqual(2,executor.lookups)

    def test_signed_current_absence_allows_one_conditional_first_delivery_and_changed_or_expired_proof_denies(self):
        import time
        from joint_authority_service import RetainedPhaseOwner
        class Rpc:
            def call(_,method,payload): return self.fixture.store.dispatch(method,payload)
        class Executor:
            executions=0; lookups=0
            def execute(self,request): self.executions+=1; return 'Passed',request['cases'],{'fixture':True}
            def lookup(self,request): self.lookups+=1; return None
        key=digest(self.fixture.binding);attempt=self.fixture.authority.reserve(self.fixture.binding,'fixture-dropped-first')['attemptId']
        request={'protocol':'OQ-5.4-1.joint.v1','attemptId':attempt,'bindingDigest':key,'phase':0,'owner':PHASES[0],'previousSha256':key,
                 'cases':partitions()[0],'requestId':digest([attempt,key,0,key])}
        self.fixture.authority.retain(request); executor=Executor()
        def receiver(): return RetainedPhaseOwner(self.fixture.path,0,self.fixture.binding,executor,lambda value:{'payload':value,'signature':'isolated-signature'},Rpc(),storage=self.fixture.storage,completion_capacity=self.fixture.completion_capacity)
        absence=receiver().dispatch('lookup',request);self.assertEqual('OwnerAbsent',absence['payload']['state']);self.assertEqual(0,executor.executions)
        changed=copy.deepcopy(absence);changed['payload']['attemptId']='changed'
        with self.assertRaises(Blocked): receiver().dispatch('execute-if-absent',{'request':request,'absence':changed})
        with patch_time(absence['payload']['validUntil']):
            with self.assertRaises(Blocked): receiver().dispatch('execute-if-absent',{'request':request,'absence':absence})
        result=receiver().dispatch('execute-if-absent',{'request':request,'absence':absence});self.assertEqual('Passed',result['payload']['state'])
        self.assertEqual(result,receiver().dispatch('execute-if-absent',{'request':request,'absence':absence}));self.assertEqual(1,executor.executions)

    def test_runner_recovers_dropped_first_delivery_using_receiving_absence_without_repeating_admitted_unknown(self):
        from joint_authority_service import RetainedPhaseOwner
        class Rpc:
            def call(_,method,payload): return self.fixture.store.dispatch(method,payload)
        class Executor:
            executions=0
            def execute(self,request): self.executions+=1; return 'Passed',request['cases'],{'fixture':True}
            def lookup(self,request): return None
        executor=Executor();receiver=RetainedPhaseOwner(self.fixture.path,0,self.fixture.binding,executor,lambda value:{'payload':value,'signature':'isolated-signature'},Rpc(),storage=self.fixture.storage,completion_capacity=self.fixture.completion_capacity)
        fixture=self.fixture
        class ReceivingOwner:
            dropped=False
            def preflight(self,binding,phase,cases): return Owner().preflight(binding,phase,cases)
            def execute(self,request): self.dropped=True;raise TimeoutError('fixture dropped before receiving admission')
            def lookup(self,request): return receiver.dispatch('lookup',request)
            def execute_if_absent(self,request,absence): return receiver.dispatch('execute-if-absent',{'request':request,'absence':absence})
        fixture.owners[PHASES[0]]=ReceivingOwner()
        with self.assertRaises(Blocked): fixture.runner().run()
        self.assertEqual(0,executor.executions)
        self.assertTrue(fixture.runner().run()['cohortPassed']);self.assertEqual(1,executor.executions)

    def test_direct_phase_call_cannot_bypass_current_retained_authority_admission(self):
        from joint_authority_service import RetainedPhaseOwner
        class Rpc:
            def __init__(self, store): self.store = store
            def call(self, method, payload): return self.store.dispatch(method, payload)
        class Executor:
            count = 0
            def execute(self, request): self.count += 1; return 'Passed', request['cases'], {'observed': 'isolated'}
            def lookup(self, request): return None
        executor = Executor(); authority = self.fixture.authority
        reserved = authority.reserve(self.fixture.binding, 'isolated-attempt')
        key = digest(self.fixture.binding)
        request = {'protocol': 'OQ-5.4-1.joint.v1', 'attemptId': reserved['attemptId'], 'bindingDigest': key,
                   'phase': 0, 'owner': PHASES[0], 'previousSha256': key, 'cases': partitions()[0],
                   'requestId': digest([reserved['attemptId'], key, 0, key])}
        owner = RetainedPhaseOwner(self.fixture.path, 0, self.fixture.binding, executor,
                                  lambda payload: {'payload': payload, 'signature': 'isolated-signature'}, Rpc(self.fixture.store),storage=self.fixture.storage,completion_capacity=self.fixture.completion_capacity)
        with self.assertRaises(Blocked): owner.dispatch('execute', request)
        self.assertEqual(0, executor.count)
        authority.retain(request)
        result = owner.dispatch('execute', request)
        self.assertEqual('Passed', result['payload']['state'])
        self.assertEqual(result, owner.dispatch('lookup', request))
        self.assertEqual(1, executor.count)
