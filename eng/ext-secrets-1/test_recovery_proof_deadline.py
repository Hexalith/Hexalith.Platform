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

if __name__=='__main__': unittest.main()
