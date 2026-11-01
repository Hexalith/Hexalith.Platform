import copy,hashlib,io,json,os,subprocess,tempfile,unittest
from datetime import datetime,timezone,timedelta
from pathlib import Path
from types import SimpleNamespace
import telemetry_policy as p
class Validator:
 def __init__(self,policy,signature):
  self.records={'policy':policy,'signature':signature};self.calls=[];self.corrupt=False;self.short=False
  self.meta=SimpleNamespace(endpoint_url='https://s3.fr-par.scw.cloud',region_name='fr-par',config=SimpleNamespace(retries={'total_max_attempts':1}))
 def head_object(self,**kw):
  self.calls.append(('head',kw));key='signature' if kw['Key'].endswith('.sig') else 'policy';now=datetime.now(timezone.utc)
  return {'VersionId':kw['VersionId'],'ContentLength':len(self.records[key]),'LastModified':now,'ServerSideEncryption':'AES256','ObjectLockMode':'COMPLIANCE','ObjectLockRetainUntilDate':now+timedelta(days=31)}
 def get_object(self,**kw):
  self.calls.append(('get',kw));key='signature' if kw['Key'].endswith('.sig') else 'policy';body=self.records[key]
  if self.corrupt and key=='policy':body=bytes([body[0]^1])+body[1:]
  return {'VersionId':kw['VersionId'],'ContentLength':len(body),'ServerSideEncryption':'AES256','Body':io.BytesIO(body)}
 def get_object_retention(self,**kw):
  self.calls.append(('retention',kw))
  # Use the HEAD result's actual datetime so the exact matching guard is exercised.
  return {'Retention':{'Mode':'COMPLIANCE','RetainUntilDate':self.last_head['ObjectLockRetainUntilDate']}}
class PolicyTests(unittest.TestCase):
 def setUp(self):
  self.tmp=tempfile.TemporaryDirectory();self.root=Path(self.tmp.name);self.root.chmod(0o700);key=self.root/'synthetic-key'
  subprocess.run(['/usr/bin/ssh-keygen','-q','-t','ed25519','-N','','-f',str(key)],capture_output=True,check=True)
  self.principal='synthetic@example.invalid';self.rid='20261031t175250z';self.policy=(json.dumps({'kind':'hexalith.recovery-policy','recoveryId':self.rid,'syntheticOnly':True})+'\n').encode();(self.root/'policy').write_bytes(self.policy)
  subprocess.run(['/usr/bin/ssh-keygen','-Y','sign','-n','hexalith-recovery','-f',str(key),str(self.root/'policy')],capture_output=True,check=True)
  self.sig=(self.root/'policy.sig').read_bytes();pub=key.with_suffix('.pub').read_text().split();self.trust=(self.principal+' namespaces="hexalith-recovery" '+pub[0]+' '+pub[1]+'\n').encode();self.sha=hashlib.sha256(self.trust).hexdigest();self.validator=Validator(self.policy,self.sig)
  original=self.validator.head_object
  def head(**kw):self.validator.last_head=original(**kw);return self.validator.last_head
  self.validator.head_object=head;self.saved=[]
 def tearDown(self):self.tmp.cleanup()
 def execute(self):return p.immutable_originals(self.policy,self.sig,self.trust,self.sha,self.principal,self.rid,{'policy':'policy-version','signature':'signature-version'},self.validator,lambda label,data:(self.saved.append((label,data)) or True))
 def test_original_real_ssh_signature_and_both_exact_immutable_buffers_are_required(self):
  result=self.execute();self.assertTrue(result['originalSignatureVerifiedBeforeAndAfterReadback']);self.assertEqual(len(self.validator.calls),6);self.assertEqual(result['records']['policy']['versionId'],'policy-version');json.dumps(self.saved)
 def test_invalid_original_signature_refuses_before_any_provider_request(self):
  self.sig=b'not an original signature'
  with self.assertRaisesRegex(ValueError,'original-signature'):self.execute()
  self.assertEqual(self.validator.calls,[])
 def test_corrupted_offnode_bytes_and_independent_version_mismatch_cannot_pass(self):
  self.validator.corrupt=True
  with self.assertRaisesRegex(ValueError,'immutable-readback'):self.execute()
  self.assertEqual(len(self.validator.calls),3)
  self.validator.corrupt=False;original=self.validator.get_object
  def wrong(**kw):x=original(**kw);x['VersionId']='substituted';return x
  self.validator.get_object=wrong
  with self.assertRaisesRegex(ValueError,'immutable-readback'):self.execute()
 def test_wrong_namespace_or_trust_root_is_not_accepted_as_owner_approval(self):
  with self.assertRaisesRegex(ValueError,'original-input'):p.verify_original(self.policy,self.sig,self.trust,'0'*64,self.principal)
  wrong=self.trust.replace(b'hexalith-recovery',b'other-namespace')
  with self.assertRaisesRegex(ValueError,'original-signature'):p.verify_original(self.policy,self.sig,wrong,hashlib.sha256(wrong).hexdigest(),self.principal)
if __name__=='__main__':unittest.main()
