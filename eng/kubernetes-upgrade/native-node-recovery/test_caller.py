import copy,hashlib,importlib.util,unittest
from pathlib import Path
from unittest.mock import patch
from test_capture import fixture
spec=importlib.util.spec_from_file_location('capture_caller_under_test',Path(__file__).with_name('caller.py'));m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
class CallerTests(unittest.TestCase):
 def test_required_original_authentication_gate_precedes_native_client_and_creation(self):
  with patch.object(m.c.metadata,'Client') as client:
   with self.assertRaisesRegex(ValueError,'signed-policy-or-immutable-readback'):
    m.collect(None,[],fixture(),b'',None,lambda _:False)
  client.assert_not_called()
 def objects(self):return m.c.manifests('fixture','node1','1'*36,'2'*36,'docker.io/library/python@sha256:'+'c'*64,fixture())
 def test_added_network_access_changed_policy_identity_and_unreviewed_pod_fields_refuse(self):
  for desired in self.objects():
   current=copy.deepcopy(desired);current['metadata']['uid']='owned'
   m.assert_owned(current,desired,'owned')
   changed=copy.deepcopy(current)
   if desired['kind']=='Pod':changed['spec']['containers'][0]['lifecycle']={'postStart':{'exec':{'command':['injected']}}}
   else:changed['spec']['egress']=[{}]
   with self.assertRaises(ValueError):m.assert_owned(changed,desired,'owned')
   current['metadata']['uid']='replacement'
   with self.assertRaises(ValueError):m.assert_owned(current,desired,'owned')
 def test_actual_running_container_must_attest_exact_image_and_zero_restarts(self):
  desired=self.objects()[-1];current=copy.deepcopy(desired);current['metadata']['uid']='owned';status={'name':'metadata','restartCount':0,'state':{'running':{'startedAt':'actual'}},'imageID':'docker.io/library/python@sha256:'+'c'*64,'containerID':'containerd://'+'a'*64};current['status']={'phase':'Running','containerStatuses':[status]}
  m.assert_running(current,desired,'owned')
  for key,value in [('imageID','docker.io/library/python@sha256:'+'b'*64),('containerID','docker://unverified'),('restartCount',1)]:
   changed=copy.deepcopy(current);changed['status']['containerStatuses'][0][key]=value
   with self.subTest(key=key),self.assertRaises(ValueError):m.assert_running(changed,desired,'owned')
if __name__=='__main__':unittest.main()
