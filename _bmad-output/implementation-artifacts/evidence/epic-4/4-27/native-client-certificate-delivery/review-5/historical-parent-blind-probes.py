from pathlib import Path
import importlib.util, sys, inspect, time, tempfile, shutil, json, hashlib
from unittest.mock import patch
root=Path.cwd()
def load(name,path):
 s=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(s);sys.modules[name]=m;s.loader.exec_module(m);return m
t=load('blind_cert_probes',root/'eng/recovery-preparation/test_native_client_certificate.py')
x=t.CertificateTests('test_exact_certificate_and_key_authenticate_real_peer_without_authorization');x.setUp()
try:
 old=KeyboardInterrupt('previously handled');current=KeyboardInterrupt('current loading failure')
 try:raise old
 except KeyboardInterrupt:
  try:raise current
  except KeyboardInterrupt:pass
 try:
  with patch.object(t.ssl.SSLContext,'load_cert_chain',side_effect=current):x.exchange()
 except BaseException as e:print('stale_context',{'propagatedOld':e is old,'propagatedCurrent':e is current})
 stream=t.f.Stream(t.f.http(b'{}'));native=t.b.NativeTLS(t.f.owned_connector(stream));src,start=inspect.getsourcelines(t.b.NativeTLS.exchange);line=start+next(i for i,text in enumerate(src) if 'owned = object.__new__' in text)
 def trace(frame,event,arg):
  if frame.f_code is t.b.NativeTLS.exchange.__code__ and event=='line' and frame.f_lineno==line:raise KeyboardInterrupt('pre-guard allocation')
  return trace
 try:
  sys.settrace(trace);native.exchange(x.request,time.monotonic()+3,x.binding,x.credential)
 except BaseException:print('pre_guard_prepared',{'closed':stream.closed})
 finally:sys.settrace(None);stream.close()
finally:x.doCleanups()
packet=root/'_bmad-output/implementation-artifacts/evidence/epic-4/4-27/native-client-certificate-delivery/review-4'
m=load('blind_receipt_probes',packet/'verify-receipt.py')
with tempfile.TemporaryDirectory() as d:
 p=Path(d)/'packets/review-4';shutil.copytree(packet.parent,p.parent)
 receipt=json.loads((p/'verification.json').read_text());sweep=json.loads((p/'cancellation-sweeps.json').read_text());sweep['line']['findings']=[{'descriptorTablePreserved':False,'injected':True}]
 raw=(json.dumps(sweep)+'\n').encode();(p/'cancellation-sweeps.json').write_bytes(raw);h=hashlib.sha256(raw).hexdigest();receipt['packetFileSha256']['cancellation-sweeps.json']=h
 for item in receipt['verificationCommands']:
  if item['log']=='cancellation-sweeps.json':item['logSha256']=h
 (p/'verification.json').write_text(json.dumps(receipt));m.verify(root,p);print('adverse_sweep_accepted',True)
 receipt['sourceSha256']={};(p/'verification.json').write_text(json.dumps(receipt));m.verify(root,p);print('empty_source_inventory_accepted',True)
 receipt['packetFileSha256']={};(p/'verification.json').write_text(json.dumps(receipt));m.verify(root,p);print('empty_packet_inventory_accepted',True)
