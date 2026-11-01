import copy,subprocess,unittest
import telemetry_fence as f
class FenceTests(unittest.TestCase):
 def objects(self):return f.manifests('s427-target-fence','s427-owned-target','11111111-1111-1111-1111-111111111111')
 def test_exact_target_uid_name_and_create_limit(self):
  p,b=self.objects();self.assertEqual(p['spec']['failurePolicy'],'Fail');self.assertEqual(p['spec']['matchConstraints']['resourceRules'][0]['operations'],['CREATE']);self.assertEqual(p['spec']['matchConditions'][0]['expression'],"request.namespace == 's427-owned-target'");self.assertEqual(p['spec']['validations'][0]['expression'],'false');self.assertEqual(p['metadata']['annotations']['hexalith.io/target-namespace-uid'],'11111111-1111-1111-1111-111111111111');self.assertEqual(b['spec'],{'policyName':'s427-target-fence','validationActions':['Deny'],'matchResources':{'matchPolicy':'Equivalent','namespaceSelector':{},'objectSelector':{}}})
 def test_spec_uid_namespace_replacement_and_warning_refuse(self):
  for desired in self.objects():
   ns={'metadata':{'name':'s427-owned-target','uid':'11111111-1111-1111-1111-111111111111'}};current=copy.deepcopy(desired);current['metadata']['uid']='owned';f.assert_bound(current,desired,'owned',ns)
   for attack in [lambda x:x['metadata'].update(uid='replaced'),lambda x:x['metadata'].update(ownerReferences=[{'uid':'unowned'}]),lambda x:x['spec'].update(extra='unreviewed'),lambda x:x['metadata']['annotations'].update({'hexalith.io/target-namespace-uid':'replaced'})]:
    changed=copy.deepcopy(current);attack(changed)
    with self.assertRaises(ValueError):f.assert_bound(changed,desired,'owned',ns)
   ns['metadata']['uid']='replacement'
   with self.assertRaises(ValueError):f.assert_bound(current,desired,'owned',ns)
  p=self.objects()[0];current=copy.deepcopy(p);current['metadata']['uid']='u';current['status']={'typeChecking':{'expressionWarnings':[{'warning':'bad-expression'}]}}
  with self.assertRaises(ValueError):f.assert_bound(current,p,'u',{'metadata':{'name':'s427-owned-target','uid':'11111111-1111-1111-1111-111111111111'}})
 def test_unrelated_refusals_are_not_enforcement_evidence(self):
  for error in [b'Forbidden: other policy',b'network timeout',b'unauthorized']:self.assertFalse(f.typed_denial(subprocess.CompletedProcess([],1,b'',error),'p','b'))
  self.assertTrue(f.typed_denial(subprocess.CompletedProcess([],1,b'',b'Forbidden: ValidatingAdmissionPolicy p with binding b denied request: s427 exact target namespace creation fence'),'p','b'))
 def test_scope_injection_refused(self):
  with self.assertRaises(ValueError):f.manifests('safe',"target' || true",'1'*36)
if __name__=='__main__':unittest.main()
