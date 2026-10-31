import hashlib,os,sys,tempfile,unittest
from pathlib import Path
import transfer as t
from unittest.mock import patch
class TransferTests(unittest.TestCase):
 def test_binary_large_payload_and_real_length_digest_are_preserved(self):
  raw=bytes(range(256))*32768
  with tempfile.TemporaryDirectory() as directory:
   root=Path(directory);root.chmod(0o700);out=root/'cipher.age'
   result=t.read_stream([sys.executable,'-I','-S','-c',"import os\nb=bytes(range(256))*256\nfor _ in range(128):os.write(1,b)"],len(raw),hashlib.sha256(raw).hexdigest(),output=out)
   self.assertTrue(result['streamComplete']);self.assertEqual(out.read_bytes(),raw);self.assertEqual(out.stat().st_mode&0o777,0o600)
 def test_partial_wrong_digest_nonzero_and_timeout_cannot_pass(self):
  for code,count,digest,reason in [("import os;os.write(1,b'abc')",4,hashlib.sha256(b'abc!').hexdigest(),'exit-length-or-digest'),("import os;os.write(1,b'abc');raise SystemExit(9)",3,hashlib.sha256(b'abc').hexdigest(),'exit-length-or-digest'),("import os;os.write(1,b'abc')",2,'a'*64,'size-bound'),('import time;time.sleep(10)',1,'a'*64,'deadline')]:
   with self.subTest(code=code),self.assertRaisesRegex(ValueError,reason):t.read_stream([sys.executable,'-I','-S','-c',code],count,digest,timeout=.15)
 def test_existing_output_and_insecure_custody_refuse_before_dispatch(self):
  with tempfile.TemporaryDirectory() as directory:
   root=Path(directory);root.chmod(0o700);out=root/'preserved';out.write_bytes(b'original')
   with self.assertRaises(FileExistsError):t.read_stream(['unavailable'],1,'a'*64,output=out)
   self.assertEqual(out.read_bytes(),b'original');root.chmod(0o755)
   with self.assertRaisesRegex(ValueError,'custody'):t.read_stream(['unavailable'],1,'a'*64,output=root/'new')
 def test_remote_getter_rejects_arbitrary_paths_and_has_no_symlink_following(self):
  with self.assertRaises(ValueError):t.retrieval_program('../../etc/shadow',1)
  self.assertIn('os.O_NOFOLLOW',t.retrieval_program('snapshot.db.age',100))
  self.assertIn('retrieval-source-drift',t.retrieval_program('snapshot.db.age',100))
 def test_malformed_digest_never_dispatches_and_cleanup_cannot_mask_primary_failure(self):
  with patch.object(t.subprocess,'Popen') as popen,self.assertRaisesRegex(ValueError,'binding-refused'):
   t.read_stream(['unavailable'],1,'G'*64)
  popen.assert_not_called()
  real=t.reap
  def failed_reap(child):real(child);raise RuntimeError('cleanup-failed-after-reap')
  with patch.object(t,'reap',failed_reap),self.assertRaises(BaseExceptionGroup) as caught:
   t.read_stream([sys.executable,'-I','-S','-c',"print('bad')"],1,'a'*64)
  self.assertTrue(any(isinstance(e,ValueError) and 'size-bound' in str(e) for e in caught.exception.exceptions))
  self.assertTrue(any(isinstance(e,RuntimeError) for e in caught.exception.exceptions))
if __name__=='__main__':unittest.main()
