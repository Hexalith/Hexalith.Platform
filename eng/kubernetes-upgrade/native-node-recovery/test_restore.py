import copy
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import sys
import tempfile
import unittest
from types import SimpleNamespace
from unittest.mock import patch

spec=importlib.util.spec_from_file_location('isolated_recovery_restore',Path(__file__).with_name('restore.py'))
r=importlib.util.module_from_spec(spec);spec.loader.exec_module(r)

class RecoveryRestoreTests(unittest.TestCase):
 def test_unauthenticated_policy_refuses_before_artifact_or_backend_access(self):
  with patch.object(r,'private_artifact') as artifact,patch.object(r.capture.metadata,'bounded_command') as backend:
   with self.assertRaisesRegex(ValueError,'authenticated-policy'):
    r.restore(SimpleNamespace(),{},None,None,lambda _:False)
   artifact.assert_not_called();backend.assert_not_called()

 def test_ciphertext_custody_replacement_digest_and_permissions_are_checked(self):
  with tempfile.TemporaryDirectory() as directory:
   root=Path(directory);root.chmod(0o700);path=root/'point.age';path.write_bytes(b'ciphertext');path.chmod(0o600)
   r.private_artifact(path,10,hashlib.sha256(b'ciphertext').hexdigest())
   with self.assertRaisesRegex(ValueError,'digest-or-content'):r.private_artifact(path,10,'a'*64)
   path.chmod(0o644)
   with self.assertRaisesRegex(ValueError,'custody'):r.private_artifact(path,10,'a'*64)
   path.unlink();path.symlink_to('/etc/passwd')
   with self.assertRaisesRegex(ValueError,'custody'):r.private_artifact(path,10,'a'*64)

 def test_exact_owned_target_rejects_external_network_mount_privilege_or_identity(self):
  cid='a'*64;image='sha256:'+'b'*64
  current={'Id':cid,'Name':'/owned','Image':image,'Config':{'Labels':{'hexalith.io/node-recovery-attempt':'attempt','hexalith.io/node-recovery-role':'file'}},'Mounts':[],
   'HostConfig':{'NetworkMode':'none','ReadonlyRootfs':True,'Privileged':False,'CapDrop':['ALL'],'CapAdd':['CAP_CHOWN','CAP_FOWNER','CAP_DAC_READ_SEARCH'],'IpcMode':'private','Memory':1610612736,'MemorySwap':1610612736,'Tmpfs':dict.fromkeys(['/input','/restore','/tmp'],'bounded')}}
  r.inspect_owned(current,cid,'owned',image,'attempt','file',['CHOWN','FOWNER','DAC_READ_SEARCH'])
  for change in [lambda v:v.update(Id='c'*64),lambda v:v.update(Image='sha256:'+'c'*64),lambda v:v['HostConfig'].update(NetworkMode='bridge'),lambda v:v['HostConfig'].update(Binds=['/source:/input:ro']),lambda v:v['HostConfig'].update(Privileged=True),lambda v:v['HostConfig'].update(CapAdd=['CAP_SYS_ADMIN']),lambda v:v['HostConfig'].update(Memory=0)]:
   changed=copy.deepcopy(current);change(changed)
   with self.subTest(changed=changed),self.assertRaises(ValueError):r.inspect_owned(changed,cid,'owned',image,'attempt','file',['CHOWN','FOWNER','DAC_READ_SEARCH'])

 def test_real_eight_mib_binary_pipe_retains_plaintext_in_owned_pipe_only(self):
  size=8*1024*1024
  source=[sys.executable,'-I','-S','-c',"import os\nb=bytes(range(256))*256\nfor _ in range(128):os.write(1,b)"]
  sink=[sys.executable,'-I','-S','-c',"import sys,hashlib,json\nh=hashlib.sha256();n=0\nwhile True:\n b=sys.stdin.buffer.read(65536)\n if not b:break\n n+=len(b);h.update(b)\nprint(json.dumps({'bytes':n,'sha256':h.hexdigest()}))"]
  result=r.pipe_plaintext(source,sink)
  self.assertEqual(result['receiver'],{'bytes':size,'sha256':hashlib.sha256(bytes(range(256))*32768).hexdigest()})

 def test_source_failure_timeout_and_cleanup_do_not_erase_primary_or_leave_children(self):
  sink=[sys.executable,'-I','-S','-c',"import sys,json\nsys.stdin.buffer.read();print('{}')"]
  with self.assertRaisesRegex(ValueError,'source-or-target'):
   r.pipe_plaintext([sys.executable,'-I','-S','-c',"import os;os.write(1,b'partial');raise SystemExit(7)"],sink)
  real=r.transfer.reap
  def broken(child):real(child);raise RuntimeError('cleanup-diagnostic')
  with patch.object(r.transfer,'reap',broken),self.assertRaises(BaseExceptionGroup) as caught:
   r.pipe_plaintext([sys.executable,'-I','-S','-c','import time;time.sleep(10)'],sink,timeout=.1)
  self.assertTrue(any(isinstance(v,ValueError) and 'deadline' in str(v) for v in caught.exception.exceptions))
  self.assertTrue(any(isinstance(v,RuntimeError) for v in caught.exception.exceptions))

 def test_source_fence_rejects_loopback_untyped_or_extra_endpoint_authority(self):
  for value in [[{'host':'127.0.0.1','port':2379}],[{'host':'0.0.0.0','port':2379}],[{'host':'192.168.1.30','port':True}],[{'host':'192.168.1.30','port':2379,'proxy':'unreviewed'}],[]]:
   with self.subTest(value=value),self.assertRaises(ValueError):r.target_profile_program(0,value)

 def test_failed_preflight_reports_no_targets_and_retains_original_plus_custody_error(self):
  import subprocess
  with tempfile.TemporaryDirectory() as directory:
   root=Path(directory);root.chmod(0o700);config=root/'config';config.mkdir(mode=0o700)
   for name in ['docker','age','identity','snapshot.age','archive.age']:
    (root/name).write_bytes(b'ciphertext');(root/name).chmod(0o600)
   binding={'ciphertextBytes':10,'ciphertextSha256':hashlib.sha256(b'ciphertext').hexdigest()}
   source={'kind':'node-file-metadata-only','hashedBytes':5,'entries':{p:{'kind':'file'} for p in ['etc/kubernetes/pki/etcd/ca.crt','etc/kubernetes/pki/etcd/client.crt','etc/kubernetes/pki/etcd/client.key','usr/local/bin/etcdctl','usr/local/bin/etcdutl']}}
   captured={'error':None,'cleanupPassed':True,'localCaptureCompleted':True,'summary':{'sourceNodeMetadataUnchanged':True,'recoveryAccepted':False,'artifacts':{'snapshot.db.age':binding,'node-recovery.tar.gz.age':binding},'archiveManifest':{'entries':source['entries']}}}
   args=SimpleNamespace(attempt_id='fixture',image='sha256:'+'a'*64,docker_host='unix:///var/run/docker.sock',source_endpoints=[{'host':'192.0.2.20','port':2379}],snapshot=root/'snapshot.age',archive=root/'archive.age',docker=root/'docker',age=root/'age',readback_identity=root/'identity',docker_config=config,recipient='fixture')
   class Custody:
    def __init__(self,fail=False):self.fail=fail
    def encrypt(self,name,*unused):
     if self.fail and name=='restore-original-failure':raise RuntimeError('custody-failure')
   backend=subprocess.CompletedProcess([],1,b'',b'No such image')
   with patch.object(r.capture.metadata,'bounded_command',return_value=backend):
    result=r.restore(args,captured,source,Custody(),lambda _:True)
    self.assertEqual(result['state'],'failed');self.assertFalse(result['cleanupPassed'])
    self.assertTrue(result['cleanupNotApplicableNoTargetsCreated']);self.assertEqual(result['targetCreationIntents'],0)
    with self.assertRaises(BaseExceptionGroup) as caught:r.restore(args,captured,source,Custody(True),lambda _:True)
   self.assertTrue(any(isinstance(v,ValueError) and 'owned-command' in str(v) for v in caught.exception.exceptions))
   self.assertTrue(any(isinstance(v,RuntimeError) and 'custody-failure' in str(v) for v in caught.exception.exceptions))

 def test_receiver_rejects_arbitrary_filename_or_unbound_length_before_dispatch(self):
  for name,size,digest in [('../../source',1,'a'*64),('snapshot.db',0,'a'*64),('snapshot.db',1,'g'*64)]:
   with self.subTest(name=name),self.assertRaises(ValueError):r.receiver_program(name,size,digest)

if __name__=='__main__':unittest.main()
