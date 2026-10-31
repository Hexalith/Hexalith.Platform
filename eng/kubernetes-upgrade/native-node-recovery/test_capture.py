import ast,base64,copy,hashlib,importlib.util,json,sys,unittest
from pathlib import Path
spec=importlib.util.spec_from_file_location('node_capture_under_test',Path(__file__).with_name('capture.py'))
c=importlib.util.module_from_spec(spec);spec.loader.exec_module(c)
def fixture():
 entries={p:{'uid':1000,'gid':1000,'mode':0o600,'kind':'file','bytes':10,'sha256':'a'*64} for p in ['etc/kubernetes/pki/etcd/ca.crt','etc/kubernetes/pki/etcd/client.crt','etc/kubernetes/pki/etcd/client.key','usr/local/bin/etcdctl','usr/local/bin/etcdutl']}
 return {'endpoint':'https://192.0.2.20:2379','nodeMetadata':{'kind':'node-file-metadata-only','entries':entries,'hashedBytes':50,'processSecurityProfile':{'onlyDacReadSearchEffectivePermittedAndBounded':True,'noNewPrivileges':True,'rootFilesystemReadOnly':True}},'ageSha256':'b'*64,'ageBytes':4096,'recipients':['ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA first','ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB second'],'deadlineEpoch':2000000000,'expectedEtcdIdentity':{'clusterId':'18446744073709551615','memberId':'1234'}}
class CaptureTests(unittest.TestCase):
 def test_fixed_payload_is_parseable_and_bound_to_exact_private_scope(self):
  config=fixture();body=c.program(config);ast.parse(body)
  self.assertIn("'/work/snapshot.db'",body);self.assertNotIn("--count-only",body)
  self.assertIn("integrity['totalKey']==full_visible['keyCount']",body)
  self.assertIn('RLIMIT_FSIZE',body);self.assertIn('snapshotPlaintextOnlyInMemoryBackedHelper',body)
  self.assertNotIn('skip-hash-check',body);self.assertNotIn('bump-revision',body)
 def test_endpoint_injection_hostname_and_invalid_ports_refuse(self):
  for endpoint in ['http://192.0.2.20:2379','https://etcd.invalid:2379','https://192.0.2.20:2379;id','https://192.0.2.20:65536']:
   value=fixture();value['endpoint']=endpoint
   with self.subTest(endpoint=endpoint),self.assertRaises(ValueError):c.program(value)
 def test_missing_or_oversized_source_and_tools_refuse(self):
  for change in [lambda x:x['nodeMetadata']['entries'].pop('usr/local/bin/etcdutl'),lambda x:x.update(ageBytes=16*1024*1024+1),lambda x:x['nodeMetadata'].update(hashedBytes=513*1024*1024),lambda x:x['nodeMetadata']['entries']['etc/kubernetes/pki/etcd/client.key'].update(bytes=128*1024*1024+1)]:
   value=fixture();change(value)
   with self.subTest(change=change),self.assertRaises(ValueError):c.program(value)
 def test_traversal_duplicate_recipient_extra_authority_and_float_ids_refuse(self):
  for change in [lambda x:x['nodeMetadata']['entries'].update({'etc/../../outside':x['nodeMetadata']['entries']['usr/local/bin/etcdctl']}),lambda x:x['recipients'].__setitem__(1,x['recipients'][0]),lambda x:x.update(extra='unreviewed'),lambda x:x['expectedEtcdIdentity'].update(memberId=1.0)]:
   value=fixture();change(value)
   with self.subTest(change=change),self.assertRaises(ValueError):c.program(value)
 def test_capture_manifests_keep_exact_readonly_source_mounts_and_dac_only(self):
  policy,pod=c.manifests('synthetic','node1','11111111-1111-1111-1111-111111111111','22222222-2222-2222-2222-222222222222','docker.io/library/python@sha256:'+'c'*64,fixture())
  s=pod['spec'];container=s['containers'][0]
  self.assertEqual(container['securityContext']['capabilities'],{'drop':['ALL'],'add':['DAC_READ_SEARCH']})
  self.assertTrue(container['securityContext']['readOnlyRootFilesystem']);self.assertFalse(container['securityContext']['allowPrivilegeEscalation'])
  self.assertFalse(s['automountServiceAccountToken']);self.assertTrue(all(not s[k] for k in ('hostPID','hostIPC','hostNetwork')))
  self.assertEqual(s['volumes'][-1],{'name':'private-work','emptyDir':{'medium':'Memory','sizeLimit':'1Gi'}})
  self.assertTrue(all(v['readOnly'] for v in container['volumeMounts'] if v['name']!='private-work'))
  self.assertFalse(any(v.get('hostPath',{}).get('path')=='/' for v in s['volumes']))
  self.assertEqual(policy['spec']['egress'],[{'to':[{'ipBlock':{'cidr':'192.0.2.20/32'}}],'ports':[{'protocol':'TCP','port':2379}]}])
  self.assertEqual(policy['spec']['podSelector']['matchLabels'],pod['metadata']['labels'])
  self.assertTrue(all(pod['metadata']['labels'].get(k)==v for k,v in policy['spec']['podSelector']['matchLabels'].items()))
  self.assertEqual(pod['metadata']['annotations']['hexalith.io/program-sha256'],hashlib.sha256(container['command'][-1].encode()).hexdigest())
 def test_public_install_has_create_only_exact_hash_size_and_partial_visibility_guard(self):
  body=c.installation_program('d'*64,10);ast.parse(body)
  self.assertIn('os.O_EXCL|os.O_NOFOLLOW',body);self.assertIn('count>10',body);self.assertIn("'d"+'d'*63+"'",body)
  self.assertIn("os.path.lexists('/work/age.partial')",c.program(fixture()))
  with self.assertRaises(ValueError):c.installation_program('bad',10)
 def test_full_visible_hash_uses_actual_complete_native_records_not_native_crc(self):
  row={'key':base64.b64encode(b'/registry/synthetic').decode(),'value':base64.b64encode(b'actual-value').decode(),'create_revision':2,'mod_revision':3,'version':2}
  obj={'count':1,'kvs':[row]};proof=c.visible_hash(obj,3)
  expected=json.dumps([row],sort_keys=True,separators=(',',':')).encode()
  self.assertEqual(proof['sha256'],hashlib.sha256(expected).hexdigest())
  changed=copy.deepcopy(obj);changed['kvs'][0]['value']=base64.b64encode(b'changed-value').decode()
  self.assertNotEqual(proof['sha256'],c.visible_hash(changed,3)['sha256'])
  for attack in [dict(obj,count=2),{'count':2,'kvs':[row,row]},dict(obj,kvs=[dict(row,mod_revision=4)]),dict(obj,kvs=[dict(row,value='invalid-base64!')])]:
   with self.subTest(attack=attack),self.assertRaises(ValueError):c.visible_hash(attack,3)
 def test_failed_command_preserves_stderr_without_source_kv_stdout_in_plain_diagnostic(self):
  import subprocess
  source=b'private-source-kubernetes-secret-value'
  error=b'--count-only is only for --write-out=fields'
  raw=c.command_diagnostic(['etcdctl','get','--fixed-scope'],subprocess.CompletedProcess([],2,source,error))
  value=json.loads(raw)
  self.assertNotIn(source,raw);self.assertNotIn(base64.b64encode(source),raw)
  self.assertEqual(value['stdoutSha256'],hashlib.sha256(source).hexdigest())
  self.assertEqual(base64.b64decode(value['stderrBase64']),error)
  body=c.program(fixture());ast.parse(body)
  self.assertIn("'encrypted-node-command-failure'",body)
  self.assertIn("['/work/age','-r',config['recipients'][0],'-r',config['recipients'][1]]",body)
if __name__=='__main__':unittest.main()
