import copy,hashlib,io,json,os,tempfile,unittest
from datetime import datetime,timezone,timedelta
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import patch
import telemetry_transfer as t
from test_telemetry_postgres_adapter import fixture
class Client:
 def __init__(self,data,now):
  self.data=data;self.now=now;self.puts=0;self.old=False;self.fail=False
  self.meta=SimpleNamespace(config=SimpleNamespace(retries={'total_max_attempts':1}),endpoint_url='https://s3.fr-par.scw.cloud',region_name='fr-par')
 def list_object_versions(self,**kwargs):return {'Versions':[{'Key':kwargs['Prefix'],'VersionId':'old'}]} if self.old else {}
 def put_object(self,**kwargs):
  self.puts+=1
  assert kwargs['IfNoneMatch']=='*'
  if self.fail:raise TimeoutError('unknown PUT outcome')
  assert kwargs['Body'].read()==self.data;return {'VersionId':'v1'}
 def head_object(self,**kwargs):return {'VersionId':'v1','ContentLength':len(self.data),'LastModified':self.now+timedelta(seconds=3),'ServerSideEncryption':'AES256','ObjectLockMode':'COMPLIANCE','ObjectLockRetainUntilDate':self.now+timedelta(days=30,seconds=3,microseconds=616000)}
 def get_object(self,**kwargs):return dict(self.head_object(**kwargs),Body=io.BytesIO(self.data))
 def get_object_retention(self,**kwargs):return {'Retention':{'Mode':'COMPLIANCE','RetainUntilDate':self.head_object(**kwargs)['ObjectLockRetainUntilDate']}}
class TransferTests(unittest.TestCase):
 def setUp(self):
  self.tmp=tempfile.TemporaryDirectory();self.path=Path(self.tmp.name)/'artifact.age';self.path.parent.chmod(0o700);self.raw=b'actual-synthetic-ciphertext';self.path.write_bytes(self.raw);self.path.chmod(0o600)
  self.now=datetime(2026,10,31,20,0,tzinfo=timezone.utc);self.value=fixture();self.value['observedAt']=t.iso(self.now);self.value['transfer']['store']='scaleway-fr-par-hexalith-recovery-points';self.value['transfer']['key']='memories/20261031t175250z/telemetry-postgresql.dump.age';self.writer=Client(self.raw,self.now);self.validator=Client(self.raw,self.now);self.saved=[]
  self.expected={'ciphertextBytes':len(self.raw),'ciphertextSha256':hashlib.sha256(self.raw).hexdigest(),'plaintextBytes':10,'plaintextSha256':'a'*64}
  self.events={'dataCutoff':t.iso(self.now+timedelta(microseconds=125000)),'proposalObservedAt':self.value['observedAt'],'localDumpCompletedAt':t.iso(self.now+timedelta(microseconds=750000))}
  self.observed=self.now+timedelta(seconds=4,microseconds=125001)
 def tearDown(self):self.tmp.cleanup()
 def clock(self):return {'providerDate':t.iso(self.now+timedelta(seconds=3)),'requestStartedAt':t.iso(self.now+timedelta(seconds=3,microseconds=100000)),'responseObservedAt':t.iso(self.now+timedelta(seconds=3,microseconds=200000))}
 def execute(self):
  with patch.object(t,'utcnow',return_value=self.observed):return t.upload(self.value,self.writer,self.validator,self.path,self.expected,self.events,self.clock,lambda:{'sourceObservedAt':t.iso(self.observed)},lambda name,body:(self.saved.append((name,body)) or True),lambda _:True)
 def test_actual_provider_capture_time_fractional_retention_and_conservative_rpo_remain_distinct(self):
  original=copy.deepcopy(self.events);result=self.execute();self.assertEqual(self.writer.puts,1);self.assertEqual(result['capturedAt'],'2026-10-31T20:00:03Z');self.assertEqual(result['dataCutoff'],self.events['dataCutoff']);self.assertEqual(result['localDumpCompletedAt'],self.events['localDumpCompletedAt']);self.assertEqual(result['objectLockRetainUntil'],'2026-11-30T20:00:03.616000Z');self.assertEqual(result['measuredRpo']['seconds'],5);self.assertEqual(self.events,original);json.dumps(self.saved)
 def test_provider_lastmodified_before_fractional_actual_cutoff_is_never_clamped(self):
  original=self.validator.head_object
  def head(**kwargs):
   x=original(**kwargs);x['LastModified']=self.now;return x
  self.validator.head_object=head
  with self.assertRaisesRegex(ValueError,'protection-time-or-readback'):self.execute()
  self.assertEqual(self.writer.puts,1);self.assertTrue(any(label=='telemetry-artifact-put-response' for label,_ in self.saved))
 def test_existing_version_or_client_retries_refuse_before_put(self):
  self.validator.old=True
  with self.assertRaisesRegex(ValueError,'preexists'):self.execute()
  self.assertEqual(self.writer.puts,0);self.validator.old=False;self.writer.meta.config.retries['total_max_attempts']=2
  with self.assertRaisesRegex(ValueError,'retry-profile'):self.execute()
  self.assertEqual(self.writer.puts,0)
 def test_ambiguous_put_is_not_retried_and_original_intent_is_retained(self):
  self.writer.fail=True
  with self.assertRaises(TimeoutError):self.execute()
  self.assertEqual(self.writer.puts,1);self.assertTrue(any(label=='telemetry-artifact-put-intent' for label,_ in self.saved))
 def test_real_provider_clock_wait_preserves_events_and_rejects_coarse_ordering_timeout(self):
  original=self.clock();clock=dict(original,providerDate=t.iso(self.now),requestStartedAt=t.iso(self.now+timedelta(microseconds=100000)),responseObservedAt=t.iso(self.now+timedelta(microseconds=200000)))
  with self.assertRaisesRegex(ValueError,'order-timeout'):t.wait_provider_order(self.now+timedelta(microseconds=1),lambda:clock,timeout=.01)
  self.assertEqual(original,self.clock())
 def test_known_pg_and_provider_utc_precision_is_preserved_and_excess_precision_refused(self):
  self.assertEqual(t.timestamp('2026-10-31 20:00:00.123456+00').microsecond,123456)
  self.assertEqual(t.timestamp('2026-10-31T20:00:00.616Z').microsecond,616000)
  for value in ['2026-10-31T20:00:00.1234567Z','2026-10-31 20:00:00.1234567+00','2026-10-31T20:00:00+01:00']:
   with self.subTest(value=value),self.assertRaises(ValueError):t.timestamp(value)
if __name__=='__main__':unittest.main()
