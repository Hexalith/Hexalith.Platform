import importlib.util, sys, os, json, gc
from pathlib import Path
root=Path('/home/administrator/projects/hexalith/platform/eng/recovery-preparation')
spec=importlib.util.spec_from_file_location('certtest',root/'test_native_client_certificate.py');t=importlib.util.module_from_spec(spec);sys.modules[spec.name]=t;spec.loader.exec_module(t)
b=t.b
path=b.__file__
case=t.CertificateTests('test_exact_certificate_and_key_authenticate_real_peer_without_authorization');case.setUp()
events=[]
def record(frame,event,arg):
 if frame.f_code.co_filename==path and event=='line': events.append((frame.f_code.co_name,frame.f_lineno))
 return record
sys.settrace(record)
case.exchange()
sys.settrace(None);case.doCleanups()
targets=list(dict.fromkeys(events));results=[]
for i,target in enumerate(targets):
 case=t.CertificateTests('test_exact_certificate_and_key_authenticate_real_peer_without_authorization');case.setUp(); before=t.descriptors();exc=None;hit=False
 def trace(frame,event,arg):
  global hit
  if not hit and frame.f_code.co_filename==path and event=='line' and (frame.f_code.co_name,frame.f_lineno)==target:
   hit=True;raise KeyboardInterrupt('probe')
  return trace
 sys.settrace(trace)
 try:case.exchange()
 except BaseException as e:exc=e
 finally:sys.settrace(None)
 after=t.descriptors(); leaks={fd:str(os.readlink('/proc/self/fd/'+str(fd))) for fd in after if fd not in before}
 if leaks or not isinstance(exc,KeyboardInterrupt) or any(not x.closed for x in case.streams):
  results.append({'target':target,'hit':hit,'exception':type(exc).__name__,'leaks':leaks,'unclosed':len([x for x in case.streams if not x.closed])})
 for fd in leaks:
  try:os.close(fd)
  except OSError:pass
 case.doCleanups();exc=None;gc.collect()
print(json.dumps({'targets':len(targets),'findings':results},indent=2))
