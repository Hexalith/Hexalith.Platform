import ast,importlib.util,unittest
from pathlib import Path
spec=importlib.util.spec_from_file_location('vp',Path(__file__).with_name('volume_probe.py'));v=importlib.util.module_from_spec(spec);spec.loader.exec_module(v)
class VolumeProbeTests(unittest.TestCase):
 def expected(self):return {'source-claim':{'state':'directory','stat':None},'target-claim':{'state':'absent','stat':None}}
 def test_source_directory_positive_and_typed_absence_with_readonly_parent(self):
  body=v.program('/var/openebs/local',self.expected());ast.parse(body)
  self.assertIn('except FileNotFoundError',body);self.assertIn('os.O_NOFOLLOW',body);self.assertNotIn('os.mkdir',body);self.assertNotIn('os.unlink',body)
  policy,pod=v.manifests('fixture','node1','1'*36,'2'*36,'docker.io/library/python@sha256:'+'a'*64,'/var/openebs/local',self.expected())
  self.assertEqual(pod['spec']['volumes'],[{'name':'exact-parent','hostPath':{'path':'/var/openebs/local','type':'Directory'}}]);self.assertTrue(pod['spec']['containers'][0]['volumeMounts'][0]['readOnly'])
  self.assertEqual(policy['spec']['podSelector']['matchLabels'],pod['metadata']['labels'])
  self.assertTrue(all(pod['metadata']['labels'].get(k)==v for k,v in policy['spec']['podSelector']['matchLabels'].items()))
 def test_root_traversal_arbitrary_leaf_and_no_positive_control_refuse(self):
  for parent,expected in [('/',self.expected()),('/var/../etc',self.expected()),('/var/openebs/local',{'../../source':{'state':'directory','stat':None}}),('/var/openebs/local',{'target':{'state':'absent','stat':None}})]:
   with self.subTest(parent=parent),self.assertRaises(ValueError):v.program(parent,expected)
 def test_unmeasured_or_extra_stat_fields_are_not_accepted(self):
  expected=self.expected();expected['source-claim']['stat']={'inode':1}
  with self.assertRaises(ValueError):v.program('/var/openebs/local',expected)
if __name__=='__main__':unittest.main()
