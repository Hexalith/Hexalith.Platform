import copy,unittest
from unittest.mock import patch
import telemetry_target as t
from test_telemetry_postgres_adapter import fixture

class Native:
 def __init__(self,fault=None):self.objects={};self.requests=[];self.fault=fault
 def run(self,verb,path,body=None,absent=False):
  self.requests.append((verb,path,copy.deepcopy(body)))
  if verb=='get':
   if path=='/api/v1/persistentvolumes?limit=500':return {'items':([{'metadata':{'uid':'new-PersistentVolume'},'spec':{'claimRef':{'uid':'new-PersistentVolumeClaim','namespace':'synthetic-isolated'},'hostPath':{'path':'/var/openebs/local/fresh-isolated-data'}}}] if self.objects else []),'metadata':{}}
   return copy.deepcopy(self.objects.get(path))
  if verb=='create':
   current=copy.deepcopy(body);current['metadata'].update(uid='new-'+body['kind'],resourceVersion='11')
   self.objects[t.path(body)]=current
   if self.fault=='lost':raise TimeoutError('create outcome unobserved')
   return copy.deepcopy(current)
  if verb=='delete':
   current=self.objects[path]
   assert body['preconditions']=={'uid':current['metadata']['uid'],'resourceVersion':current['metadata']['resourceVersion']}
   self.objects.clear();return {'status':'Success'}
  raise AssertionError('unexpected mutation')

class TelemetryTargetTests(unittest.TestCase):
 def paths(self):return [{'pvUid':'new-PersistentVolume','pvcUid':'new-PersistentVolumeClaim','nodeUid':'source-node-uid','path':'/var/openebs/local/fresh-isolated-data'}]
 def target(self,native=None,gate=None,record=None):
  return t.Target(fixture(),native or Native(),gate or (lambda _:True),record or (lambda *args:True))
 def test_explicit_target_create_and_uid_rv_cleanup_preserve_only_new_owned_scope(self):
  target=self.target();created=target.create();self.assertEqual(len(created),5)
  secret=next(obj for obj,_ in target.owned if obj['kind']=='Secret')
  password=__import__('base64').b64decode(secret['data']['bootstrap-password']).decode('ascii')
  self.assertRegex(password,r'^[A-Za-z0-9_-]{43}$')
  self.assertTrue(all(x['uid'] for x in created));result=target.cleanup(self.paths(),lambda _:True)
  self.assertTrue(result['targetNamespaceAbsent'] and result['targetPvAbsent'] and result['ownedVolumePathAbsent'])
  deletes=[x for x in target.native.requests if x[0]=='delete'];self.assertEqual(len(deletes),1)
  self.assertTrue(deletes[0][1].startswith('/api/v1/namespaces/synthetic-isolated'))
 def test_failed_gate_and_preexisting_names_cannot_create_or_adopt(self):
  target=self.target(gate=lambda _:False)
  with self.assertRaises(ValueError):target.create()
  self.assertEqual(target.native.requests,[])
  target=self.target();target.native.objects['/api/v1/namespaces/synthetic-isolated']={'metadata':{'uid':'unowned'}}
  with self.assertRaises(ValueError):target.create()
  self.assertEqual(target.intents(),[])
 def test_lost_create_response_retains_intent_and_refuses_unverified_namespace_deletion(self):
  saved=[];native=Native('lost');target=self.target(native,record=lambda label,x:(saved.append((label,copy.deepcopy(x))) or True))
  with self.assertRaises(TimeoutError):target.create()
  self.assertEqual(target.intents()[0]['uid'],None);self.assertTrue(saved)
  with self.assertRaisesRegex(ValueError,'ownership-unverified'):target.cleanup(self.paths(),lambda _:True)
  self.assertFalse(any(x[0]=='delete' for x in native.requests))
 def test_failed_outcome_custody_preserves_actual_lost_create_error_and_unknown_ownership(self):
  def custody(label,value):
   if label=='target-creation-outcome':raise OSError('private custody unavailable')
   return True
  target=self.target(Native('lost'),record=custody)
  with self.assertRaises(BaseExceptionGroup) as caught:target.create()
  self.assertEqual([type(x) for x in caught.exception.exceptions],[TimeoutError,OSError])
  self.assertEqual(target.intents()[0]['uid'],None)
  self.assertFalse(any(x[0]=='delete' for x in target.native.requests))
  with self.assertRaisesRegex(ValueError,'ownership-unverified'):target.cleanup(self.paths(),lambda _:True)
 def test_unknown_pod_lifecycle_sidecars_mounts_or_source_credentials_refuse(self):
  desired=next(x for x in t.adapter._manifests(fixture()) if x['kind']=='StatefulSet')
  for attack in [lambda x:x['containers'][0].update(lifecycle={'postStart':{'exec':{'command':['unreviewed']}}}),lambda x:x['containers'].append(copy.deepcopy(x['containers'][0])),lambda x:x['volumes'].append({'name':'source','hostPath':{'path':'/'}}),lambda x:x['containers'][0]['env'].append({'name':'SOURCE_PASSWORD','value':'injected'})]:
   actual=copy.deepcopy(desired['spec']['template']['spec']);attack(actual)
   with self.subTest(attack=attack),self.assertRaises(ValueError):t.assert_pod_spec(actual,desired['spec']['template']['spec'])
 def test_safe_omitempty_false_flags_and_api_defaults_are_explicitly_normalized(self):
  desired=next(x for x in t.adapter._manifests(fixture()) if x['kind']=='StatefulSet');actual=copy.deepcopy(desired);actual['metadata'].update(uid='new',resourceVersion='1')
  actual['spec']['template']['metadata']['creationTimestamp']=None
  pod=actual['spec']['template']['spec']
  for key in ('hostNetwork','hostPID','hostIPC'):pod.pop(key)
  pod.update(dnsPolicy='ClusterFirst',restartPolicy='Always',terminationGracePeriodSeconds=30,schedulerName='default-scheduler')
  pod['containers'][0].update(terminationMessagePath='/dev/termination-log',terminationMessagePolicy='File')
  t.assert_created(actual,desired,'new')
 def test_integral_millicpu_serialization_preserves_exact_value_and_other_resource_fields(self):
  desired=next(x for x in t.adapter._manifests(fixture()) if x['kind']=='StatefulSet')['spec']['template']['spec']
  desired['containers'][0]['resources']['limits']['cpu']='1000m'
  actual=copy.deepcopy(desired);actual['containers'][0]['resources']['limits']['cpu']='1'
  t.assert_pod_spec(actual,desired)
  for attack in ['999m','1001m','2']:
   wrong=copy.deepcopy(actual);wrong['containers'][0]['resources']['limits']['cpu']=attack
   with self.subTest(attack=attack),self.assertRaises(ValueError):t.assert_pod_spec(wrong,desired)
  wrong=copy.deepcopy(actual);wrong['containers'][0]['resources']['limits']['memory']='1Gi'
  with self.assertRaises(ValueError):t.assert_pod_spec(wrong,desired)
  wrong=copy.deepcopy(actual);wrong['containers'][0]['resources']['claims']=[{'name':'unreviewed-resource'}]
  with self.assertRaises(ValueError):t.assert_pod_spec(wrong,desired)
 def test_actual_pod_rejects_admission_sidecar_lifecycle_credentials_and_host_namespace(self):
  desired=next(x for x in t.adapter._manifests(fixture()) if x['kind']=='StatefulSet')
  pod={'metadata':{'name':desired['metadata']['name']+'-0','namespace':desired['metadata']['namespace'],'uid':'fresh-pod','labels':dict(desired['spec']['template']['metadata']['labels'],**{'apps.kubernetes.io/pod-index':'0','statefulset.kubernetes.io/pod-name':desired['metadata']['name']+'-0','controller-revision-hash':'owned-revision'}),'ownerReferences':[{'apiVersion':'apps/v1','kind':'StatefulSet','name':desired['metadata']['name'],'uid':'fresh-sts','controller':True,'blockOwnerDeletion':True}]},'spec':copy.deepcopy(desired['spec']['template']['spec'])}
  pod['spec'].update(nodeName='node1',hostname=pod['metadata']['name'],subdomain=desired['spec']['serviceName'],tolerations=[{'key':'node.kubernetes.io/not-ready','operator':'Exists','effect':'NoExecute','tolerationSeconds':300},{'key':'node.kubernetes.io/unreachable','operator':'Exists','effect':'NoExecute','tolerationSeconds':300}])
  t.assert_runtime_pod(pod,desired,'fresh-sts','node1')
  for attack in [lambda x:x['spec']['containers'][0].update(lifecycle={'postStart':{'exec':{'command':['unreviewed']}}}),lambda x:x['spec']['containers'].append(copy.deepcopy(x['spec']['containers'][0])),lambda x:x['spec']['volumes'].append({'name':'source','hostPath':{'path':'/'}}),lambda x:x['spec'].update(hostNetwork=True),lambda x:x['metadata']['ownerReferences'][0].update(uid='replaced-sts')]:
   altered=copy.deepcopy(pod);attack(altered)
   with self.subTest(attack=attack),self.assertRaises(ValueError):t.assert_runtime_pod(altered,desired,'fresh-sts','node1')
 def test_namespace_replacement_or_unverified_node_path_absence_cannot_complete_cleanup(self):
  target=self.target();target.create();target.native.objects['/api/v1/namespaces/synthetic-isolated']['metadata']['uid']='recreated'
  with self.assertRaisesRegex(ValueError,'uid-drift'):target.cleanup(self.paths(),lambda _:True)
  self.assertFalse(any(x[0]=='delete' for x in target.native.requests))
  target=self.target();target.create()
  with self.assertRaisesRegex(ValueError,'path-absence-unverified'):target.cleanup(self.paths(),lambda _:False)
 def test_empty_overlapping_wrong_claim_and_wrong_node_paths_refuse_before_delete(self):
  for paths in [[],[dict(self.paths()[0],path='/var/openebs/local/source-data/child')],[dict(self.paths()[0],pvcUid='unowned')],[dict(self.paths()[0],nodeUid='other-node')]]:
   target=self.target();target.create()
   with self.subTest(paths=paths),self.assertRaises(ValueError):target.cleanup(paths,lambda _:True)
   self.assertFalse(any(x[0]=='delete' for x in target.native.requests))
if __name__=='__main__':unittest.main()
