import copy,hashlib,importlib.util,json
from pathlib import Path
import sys,unittest,os,tempfile
from types import SimpleNamespace
from unittest.mock import patch
SOURCE=Path(__file__).with_name('etcd_metadata.py')
spec=importlib.util.spec_from_file_location('etcd_metadata_under_test',SOURCE);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
spec=importlib.util.spec_from_file_location('node_metadata_test_fixtures',SOURCE.parent.parent/'native-node-metadata/test_metadata.py');f=importlib.util.module_from_spec(spec);spec.loader.exec_module(f)
ENDPOINT='https://192.0.2.10:12379'
BINDINGS={p:{'uid':1000,'gid':1000,'mode':0o600,'kind':'file','bytes':10,'sha256':'a'*64} for p in ['etc/kubernetes/pki/etcd/ca.crt','etc/kubernetes/pki/etcd/client.crt','etc/kubernetes/pki/etcd/client.key','usr/local/bin/etcdctl']}

def observed():
 cid=18000000000000000001;mid=17000000000000000001
 header={'cluster_id':cid,'member_id':mid,'revision':1234}
 return {'kind':'external-etcd-readonly-metadata','endpoint':ENDPOINT,'observations':{
  'health':[{'endpoint':ENDPOINT,'health':True}],
  'status':[{'Endpoint':ENDPOINT,'Status':{'header':header,'version':'3.6.5','leader':mid,'dbSize':100000,'dbSizeInUse':50000}}],
  'members':{'header':header,'members':[{'ID':mid,'name':'synthetic-node','peerURLs':['https://192.0.2.10:12380'],'clientURLs':[ENDPOINT]}]},
  'alarms':{'header':header}},'certificates':{'ca':{},'client':{}},'sourceFileBindingsVerifiedBeforeAndAfter':True,
  'networkCommandCount':4,'sourceContentExported':False,'snapshotTaken':False,'archiveCreated':False,
  'nodeMetadata':{'entries':{'etc/synthetic':{}}},'archiveProfile':{'allSelectedMembersInspected':True,'memberCount':1,'noAclXattrOrHardlinkProfileSupported':True,'unsupported':[]},
  'processSecurityProfile':{'onlyDacReadSearchEffectivePermittedAndBounded':True,'noNewPrivileges':True,'rootFilesystemReadOnly':True}}

class RpcClient(f.FakeClient):
 def run(self,verb,path,body=None,absent=False):
  value=super().run(verb,path,body,absent)
  if verb=='create' and body['kind']=='NetworkPolicy':
   value['spec']['egress']=copy.deepcopy(body['spec']['egress'])
   self.objects[m.transport.resource_path(body)]['spec']['egress']=copy.deepcopy(body['spec']['egress'])
  return observed() if path.endswith('/log') else value

class EtcdMetadataTests(unittest.TestCase):
 def setUp(self):
  self.objects=m.manifests('test-rpc','node1',f.NODE_UID,f.NAMESPACE_UID,f.IMAGE,ENDPOINT,BINDINGS,1800000000)
  self.args=SimpleNamespace(node='node1',node_uid=f.NODE_UID,namespace_uid=f.NAMESPACE_UID,age='unused',recipient='unused')
  RpcClient.fault=None
 def test_program_and_manifest_bind_only_exact_four_readonly_rpcs_and_tls_files(self):
  source=self.objects[1]['spec']['containers'][0]['command'][-1]
  compile(source,'fixed-probe','exec')
  self.assertNotIn('snapshot save',source)
  self.assertTrue(all(x['readOnly'] for x in self.objects[1]['spec']['containers'][0]['volumeMounts']))
  self.assertIn('root-ssh-parent',{x['name'] for x in self.objects[1]['spec']['volumes']})
  self.assertEqual(self.objects[0]['spec']['egress'],[{'to':[{'ipBlock':{'cidr':'192.0.2.10/32'}}],'ports':[{'protocol':'TCP','port':12379}]}])
  self.assertEqual(self.objects[1]['metadata']['annotations']['hexalith.io/program-sha256'],hashlib.sha256(source.encode()).hexdigest())
  self.assertEqual(self.objects[0]['spec']['podSelector']['matchLabels'],self.objects[1]['metadata']['labels'])
  self.assertTrue(all(self.objects[1]['metadata']['labels'].get(k)==v for k,v in self.objects[0]['spec']['podSelector']['matchLabels'].items()))
 def test_endpoint_tls_binding_and_additional_allowance_injection_refuse(self):
  for endpoint in ['https://192.0.2.10:12379/other','https://999.0.2.10:12379','https://192.0.2.10:99999','http://192.0.2.10:12379']:
   with self.subTest(endpoint=endpoint),self.assertRaises(ValueError):m.program(endpoint,BINDINGS,1800000000)
  policy=copy.deepcopy(self.objects[0]);policy['metadata']['uid']='owned';policy['spec']['egress'].append({})
  with self.assertRaises(ValueError):m.assert_owned(policy,self.objects[0],'owned')
 def test_exact_unsigned_native_ids_above_ieee_precision_remain_exact_decimal_strings(self):
  result=m.validate_observation(json.loads(json.dumps(observed())),ENDPOINT)
  self.assertEqual(result['clusterId'],'18000000000000000001');self.assertEqual(result['memberId'],'17000000000000000001')
 def test_single_pem_profile_rejects_extra_unmeasured_certificates_and_trailing_content(self):
  lexical=b'-----BEGIN CERTIFICATE-----\nAAAA\n-----END CERTIFICATE-----\n'
  self.assertTrue(m.single_certificate_file(lexical))
  for raw in [lexical+lexical,lexical+b'unreviewed',b'not PEM',b' '*65537]:
   with self.subTest(bytes=len(raw)):self.assertFalse(m.single_certificate_file(raw))
 def test_real_xattrs_hardlinks_and_ancestor_symlinks_are_not_reported_as_supported(self):
  with tempfile.TemporaryDirectory() as temp:
   root=Path(temp);(root/'selected').mkdir();path=root/'selected/file';path.write_bytes(b'synthetic')
   metadata={'entries':{'selected/file':{}}}
   self.assertTrue(m.attributes_profile(metadata,root)['noAclXattrOrHardlinkProfileSupported'])
   os.setxattr(path,b'user.s427',b'qualification')
   result=m.attributes_profile(metadata,root);self.assertFalse(result['noAclXattrOrHardlinkProfileSupported']);self.assertEqual(result['unsupported'][0]['xattrNames'],['user.s427'])
   os.removexattr(path,b'user.s427');os.link(path,root/'selected/other')
   self.assertFalse(m.attributes_profile(metadata,root)['noAclXattrOrHardlinkProfileSupported'])
   (root/'outside').mkdir();(root/'outside/key').write_bytes(b'outside');(root/'redirect').symlink_to(root/'outside')
   with self.assertRaises(OSError):m.attributes_profile({'entries':{'redirect/key':{}}},root)
 def test_health_member_cluster_version_and_alarm_semantics_are_all_required(self):
  attacks=[lambda x:x['observations']['health'][0].update(health=False),
   lambda x:x['observations']['status'][0]['Status'].update(version='3.6.4'),
   lambda x:x['observations']['status'][0]['Status'].update(leader=42),
   lambda x:x['observations']['members']['members'][0].update(clientURLs=['https://other:12379']),
   lambda x:x['observations']['alarms'].update(alarms=[{'alarm':1}]),
   lambda x:x['observations']['alarms']['header'].update(cluster_id=42),
   lambda x:x['observations']['status'][0]['Status']['header'].update(cluster_id=1.8e19),
   lambda x:x.update(snapshotTaken=True),lambda x:x.update(sourceContentExported=True)]
  for attack in attacks:
   value=json.loads(json.dumps(observed()));attack(value)
   with self.subTest(attack=attack),self.assertRaises(ValueError):m.validate_observation(value,ENDPOINT)
 def test_real_controller_uses_source_uid_checks_and_exact_owned_cleanup(self):
  with patch.object(m.transport,'Client',RpcClient):result=m.collect(self.args,self.objects,f.Sink(),ENDPOINT)
  self.assertIsNone(result['error']);self.assertTrue(result['cleanupPassed']);self.assertTrue(result['sourceNodeAndNamespaceUidRevalidated'])
  self.assertEqual(result['helperObjectsCreated'],2)
 def test_lost_create_never_invents_cleanup_authority_or_reports_clean_pass(self):
  RpcClient.fault='lost-create-response'
  with patch.object(m.transport,'Client',RpcClient):result=m.collect(self.args,self.objects,f.Sink(),ENDPOINT)
  self.assertIn('TimeoutError',result['error']);self.assertFalse(result['cleanupPassed']);self.assertEqual(result['helperObjectsCreated'],0)
  self.assertEqual(result['helperCreateOutcomesUnverified'],1)
 def test_source_node_replacement_after_observation_refuses_and_still_cleans_only_owned(self):
  RpcClient.fault='replace-node'
  with patch.object(m.transport,'Client',RpcClient):result=m.collect(self.args,self.objects,f.Sink(),ENDPOINT)
  self.assertIn('source-identity-drift',result['error']);self.assertFalse(result['sourceNodeAndNamespaceUidRevalidated']);self.assertTrue(result['cleanupPassed'])
if __name__=='__main__':unittest.main()
