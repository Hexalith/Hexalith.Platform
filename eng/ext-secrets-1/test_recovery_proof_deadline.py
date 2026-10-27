import tempfile
import time
import unittest
from unittest.mock import patch
from pathlib import Path
import hashlib
import qualify as q
from joint_live_phase_runner import DaprCohortRpc, Blocked
from test_qualify import Fixture

class RecoveryProofAndDeadlineTests(unittest.TestCase):
    def test_retained_original_signature_uses_original_time_but_current_verification_remains_fresh(self):
        with tempfile.TemporaryDirectory() as directory:
            fixture=Fixture(directory); then=int(time.time())-86400
            envelope=fixture.sign('executor',{'protocol':q.PROTOCOL,'observedAt':then,'validUntil':then+25})
            with self.assertRaises(q.QualificationFailed): q.verify_signed(envelope,'executor',fixture.config,fixture.binding)
            self.assertEqual(then,q.verify_signed(envelope,'executor',fixture.config,fixture.binding,original_time=then)['observedAt'])
            with self.assertRaises(q.QualificationFailed): q.verify_signed(envelope,'executor',fixture.config,fixture.binding,original_time=then+25)
            envelope['payload']['observedAt']+=1
            with self.assertRaises(q.QualificationFailed): q.verify_signed(envelope,'executor',fixture.config,fixture.binding,original_time=then)

    def test_total_rpc_deadline_bounds_a_slow_trickling_reader(self):
        with tempfile.TemporaryDirectory() as directory:
            fixture=Fixture(directory); anchor=fixture.config['qualification']['executor']['publicAnchorFile']
            descriptor={'appId':'isolated-owner','credentialReference':'file:/isolated','publicAnchorFile':anchor,'publicAnchorSha256':hashlib.sha256(Path(anchor).read_bytes()).hexdigest()}
            class Socket:
                def settimeout(self,value): pass
                def shutdown(self,value): pass
            class Connection:
                sock=Socket()
                def __init__(self,*args,**kwargs): pass
                def connect(self): pass
                def request(self,*args,**kwargs): pass
                def getresponse(self): return self
                status=200
                def read1(self,n): time.sleep(.1); return b'x'
                def close(self): pass
            rpc=DaprCohortRpc(descriptor,'http://127.0.0.1:3500','file:/isolated')
            started=time.monotonic()
            with patch('joint_live_phase_runner.REQUEST_DEADLINE_SECONDS',.04),patch('http.client.HTTPConnection',Connection),patch('qualify.read_mounted_credential',return_value='fixture-token'):
                with self.assertRaises((Blocked,TimeoutError)): rpc.call('lookup',{'fixtureOnly':True})
            self.assertLess(time.monotonic()-started,.25)

class ActualRpcWorkerCapacityTests(unittest.TestCase):
    def test_stalled_credentials_retain_finite_worker_slots_until_cleanup_and_never_send_late(self):
        import threading
        import joint_live_phase_runner as runner
        import json
        with tempfile.TemporaryDirectory() as directory:
            fixture=Fixture(directory);anchor=fixture.config['qualification']['executor']['publicAnchorFile']
            descriptor={'appId':'isolated-owner','credentialReference':'file:/isolated','publicAnchorFile':anchor,'publicAnchorSha256':hashlib.sha256(Path(anchor).read_bytes()).hexdigest()}
            rpc=DaprCohortRpc(descriptor,'http://127.0.0.1:3500','file:/isolated')
            release=threading.Event();entered=[];connects=[];closed=[];workers=threading.BoundedSemaphore(2)
            def mounted(*args):
                entered.append(threading.current_thread());release.wait(5);return 'fixture-token'
            class Socket:
                def settimeout(self,value): pass
                def shutdown(self,value): pass
            class Connection:
                def __init__(self,*args,**kwargs): self.sock=Socket();self.raw=b''
                def connect(self): connects.append(True)
                def request(self,method,url,body,headers):
                    request=json.loads(body);now=int(time.time());envelope=fixture.sign('executor',{'requestId':request['requestId'],'method':request['method'],'requestSha256':hashlib.sha256(body).hexdigest(),'observedAt':now,'validUntil':now+25,'result':{'recovered':True}})
                    import base64
                    from cryptography.hazmat.primitives.asymmetric.utils import encode_dss_signature
                    signature=base64.b64decode(envelope['signature']);envelope['signature']=base64.b64encode(encode_dss_signature(int.from_bytes(signature[:32],'big'),int.from_bytes(signature[32:],'big'))).decode();self.raw=runner.canonical(envelope)
                def getresponse(self): return self
                status=200
                def read1(self,n): raw,self.raw=self.raw[:n],self.raw[n:];return raw
                def close(self): closed.append(True)
            try:
                with patch.object(runner,'_RPC_WORKERS',workers),patch.object(runner,'REQUEST_DEADLINE_SECONDS',.04),patch('qualify.read_mounted_credential',side_effect=mounted),patch('http.client.HTTPConnection',Connection):
                    for _ in range(7):
                        with self.assertRaises((Blocked,TimeoutError)): rpc.call('lookup',{'fixtureOnly':True})
                    self.assertEqual(2,len(entered));self.assertEqual([],connects)
                    self.assertFalse(workers.acquire(blocking=False))
                    release.set()
                    for worker in entered: worker.join(1);self.assertFalse(worker.is_alive())
                    self.assertEqual([],connects) # Timed-out credentials cannot dispatch after finally returning.
                    self.assertTrue(workers.acquire(timeout=1));self.assertTrue(workers.acquire(timeout=1));workers.release();workers.release()
                with patch.object(runner,'_RPC_WORKERS',workers),patch('qualify.read_mounted_credential',return_value='fixture-token'),patch('http.client.HTTPConnection',Connection):
                    self.assertEqual({'recovered':True},rpc.call('lookup',{'fixtureOnly':True}))
                self.assertEqual(1,len(connects))
            finally: release.set()

if __name__=='__main__': unittest.main()
