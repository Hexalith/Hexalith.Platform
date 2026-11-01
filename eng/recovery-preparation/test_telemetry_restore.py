import hashlib,sys,unittest
from unittest.mock import patch
import telemetry_restore as r
from test_telemetry_postgres_adapter import fixture,restore_binding,canary_bytes,canary_records

class TelemetryRestoreTests(unittest.TestCase):
 def command(self,code):return [sys.executable,'-I','-S','-B','-c',code]
 def test_large_binary_stdin_is_complete_and_stdout_is_bounded(self):
  data=bytes(range(256))*32768
  result=r.bounded_run(self.command('import sys,hashlib;print(hashlib.sha256(sys.stdin.buffer.read()).hexdigest())'),data,timeout=10)
  self.assertEqual(result.returncode,0);self.assertEqual(result.stdout.decode().strip(),hashlib.sha256(data).hexdigest())
 def test_timeout_and_output_overrun_refuse_and_reap(self):
  for code,reason in [('import time;time.sleep(30)','deadline'),('import os;os.write(1,b"x"*65536)','output-bound')]:
   with self.subTest(reason=reason),self.assertRaisesRegex(ValueError,reason):r.bounded_run(self.command(code),timeout=.2,maximum=32)
 def test_false_authentication_or_target_binding_drift_precedes_age_or_target_dispatch(self):
  value=fixture();binding=restore_binding(value)
  for authorised,target in [(False,binding),(True,{})]:
   with patch.object(r,'bounded_run') as run,self.assertRaises(ValueError):
    r.restore(value,binding,[],b'cipher','age','identity',b'',lambda _:authorised,lambda:target)
   run.assert_not_called()
 def test_wrong_immutable_ciphertext_cannot_reach_decryption(self):
  value=fixture();binding=restore_binding(value)
  with patch.object(r,'bounded_run') as run,self.assertRaisesRegex(ValueError,'ciphertext-binding'):
   r.restore(value,binding,[],b'not-the-artifact','age','identity',b'',lambda _:True,lambda:binding)
  run.assert_not_called()
if __name__=='__main__':unittest.main()
