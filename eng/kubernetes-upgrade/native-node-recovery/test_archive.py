import copy,hashlib,importlib.util,io,os
from pathlib import Path
import tarfile,tempfile,unittest
from unittest.mock import patch
spec=importlib.util.spec_from_file_location('node_recovery_archive',Path(__file__).with_name('archive.py'));a=importlib.util.module_from_spec(spec);spec.loader.exec_module(a)
spec=importlib.util.spec_from_file_location('node_archive_metadata',Path(__file__).parent.parent/'native-node-metadata/metadata.py');m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)

class ArchiveTests(unittest.TestCase):
 def setUp(self):
  self.temp=tempfile.TemporaryDirectory();self.root=Path(self.temp.name);self.source=self.root/'source';self.source.mkdir();(self.source/'etc').mkdir();(self.source/'etc/conf').write_bytes(b'synthetic-private-source-content');(self.source/'etc/conf').chmod(0o640);(self.source/'etc/empty').mkdir();outside=self.root/'outside';outside.write_bytes(b'outside-secret-never-archive');(self.source/'etc/link').symlink_to(outside)
  self.metadata=m.collect_metadata(self.source,['etc'],[],require_readonly=False)
 def tearDown(self):self.temp.cleanup()
 def create(self):
  output=io.BytesIO();manifest=a.create(self.source,self.metadata,output);return manifest,output.getvalue()
 def target(self):
  p=self.root/('target-'+str(len(list(self.root.iterdir()))));p.mkdir(mode=0o700);return p
 def test_numeric_owners_modes_hashes_links_and_empty_directories_survive_fenced_file_restore(self):
  manifest,raw=self.create();target=self.target();result=a.extract(target,manifest,io.BytesIO(raw))
  self.assertTrue(result['numericOwnersAndModesApplied']);self.assertFalse(result['nativeBootabilityProven'])
  self.assertEqual(m.collect_metadata(target,['etc'],[],require_readonly=False)['entries'],self.metadata['entries'])
  self.assertEqual((target/'etc/conf').read_bytes(),b'synthetic-private-source-content')
  self.assertEqual(os.readlink(target/'etc/link'),os.readlink(self.source/'etc/link'))
  with tarfile.open(fileobj=io.BytesIO(raw),mode='r:gz') as archive:
   self.assertNotIn(b'outside-secret-never-archive',b''.join(archive.extractfile(x).read() for x in archive if x.isfile()))
 def test_same_length_source_content_drift_refuses_capture(self):
  path=self.source/'etc/conf';path.write_bytes(b'S'+path.read_bytes()[1:])
  with self.assertRaisesRegex(ValueError,'content-drift'):self.create()
 def test_hardlinks_refuse_capture(self):
  os.link(self.source/'etc/conf',self.source/'etc/hardlink')
  self.metadata=m.collect_metadata(self.source,['etc'],[],require_readonly=False)
  with self.assertRaisesRegex(ValueError,'file-profile'):self.create()
 def test_extended_attributes_refuse_capture_without_silent_metadata_loss(self):
  os.setxattr(self.source/'etc/conf',b'user.s427',b'qualification')
  with self.assertRaisesRegex(ValueError,'acl-or-xattr'):self.create()
 def test_directory_child_added_after_inventory_refuses_incomplete_capture(self):
  (self.source/'etc/uninventoried').write_bytes(b'unexpected')
  with self.assertRaisesRegex(ValueError,'directory-census-drift'):self.create()
 def test_source_ancestor_symlink_is_never_followed(self):
  outside=self.root/'external-directory';outside.mkdir();(outside/'secret').write_bytes(b'external-secret');(self.source/'redirect').symlink_to(outside)
  bad={'entries':{'redirect/secret':{'kind':'file','uid':os.getuid(),'gid':os.getgid(),'mode':0o644,'bytes':15,'sha256':hashlib.sha256(b'external-secret').hexdigest()}}}
  with self.assertRaises(OSError):a.create(self.source,bad,io.BytesIO())
 def malicious(self,members):
  out=io.BytesIO()
  with tarfile.open(fileobj=out,mode='w:gz',format=tarfile.PAX_FORMAT) as tar:
   for member,payload in members:tar.addfile(member,io.BytesIO(payload) if payload is not None else None)
  return out.getvalue()
 def test_unapproved_traversal_duplicate_and_special_members_cannot_escape_target(self):
  manifest,raw=self.create()
  for name,kind in [('../escaped',tarfile.REGTYPE),('/absolute',tarfile.REGTYPE),('etc/device',tarfile.CHRTYPE),('etc/hardlink',tarfile.LNKTYPE)]:
   member=tarfile.TarInfo(name);member.type=kind
   with self.subTest(name=name),self.assertRaises(ValueError):a.extract(self.target(),manifest,io.BytesIO(self.malicious([(member,None)])))
  self.assertFalse((self.root/'escaped').exists())
  with tarfile.open(fileobj=io.BytesIO(raw),mode='r:gz') as original:member=original.getmembers()[0]
  duplicate=self.malicious([(copy.deepcopy(member),None),(copy.deepcopy(member),None)])
  with self.assertRaisesRegex(ValueError,'unapproved-or-duplicate'):a.extract(self.target(),manifest,io.BytesIO(duplicate))
 def test_incomplete_archive_owner_mode_and_digest_substitution_refuse(self):
  manifest,raw=self.create()
  for change in [lambda x:x['entries']['etc/conf'].update(mode=0o600),lambda x:x['entries']['etc/conf'].update(sha256='f'*64),lambda x:x['entries'].update({'etc/missing':copy.deepcopy(x['entries']['etc/conf'])})]:
   bad=copy.deepcopy(manifest);change(bad)
   with self.subTest(change=change),self.assertRaises(ValueError):a.extract(self.target(),bad,io.BytesIO(raw))
 def test_existing_or_symlink_target_and_byte_limit_fail_closed(self):
  manifest,raw=self.create();target=self.target();(target/'preserved').write_bytes(b'untouched')
  with self.assertRaisesRegex(ValueError,'new-private-empty'):a.extract(target,manifest,io.BytesIO(raw))
  self.assertEqual((target/'preserved').read_bytes(),b'untouched')
  link=self.root/'target-link';link.symlink_to(self.target())
  with self.assertRaisesRegex(ValueError,'new-private-empty'):a.extract(link,manifest,io.BytesIO(raw))
  with patch.object(a,'MAX_BYTES',1),self.assertRaisesRegex(ValueError,'total-bound'):self.create()
if __name__=='__main__':unittest.main()
