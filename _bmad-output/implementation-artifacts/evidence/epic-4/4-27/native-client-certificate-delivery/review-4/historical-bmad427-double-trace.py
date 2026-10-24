exec(open('/tmp/bmad427-single-trace.py').read().split('case=t.CertificateTests',1)[0])
from unittest.mock import patch
primary=KeyboardInterrupt('primary load')
case=t.CertificateTests('test_exact_certificate_and_key_authenticate_real_peer_without_authorization');case.setUp();events=[]
def load(*args,**kwargs):raise primary
def record(frame,event,arg):
 if frame.f_code.co_filename==path and event=='line' and primary.__traceback__ is not None:events.append((frame.f_code.co_name,frame.f_lineno))
 return record
sys.settrace(record)
try:
 with patch.object(t.ssl.SSLContext,'load_cert_chain',load):case.exchange()
except BaseException:pass
finally:sys.settrace(None);case.doCleanups()
targets=list(dict.fromkeys(events));results=[]
for target in targets:
 case=t.CertificateTests('test_exact_certificate_and_key_authenticate_real_peer_without_authorization');case.setUp();before=t.descriptors();exc=None;hit=False;primary=KeyboardInterrupt('primary load')
 def trace(frame,event,arg):
  global hit
  if primary.__traceback__ is not None and not hit and frame.f_code.co_filename==path and event=='line' and (frame.f_code.co_name,frame.f_lineno)==target:
   hit=True;raise SystemExit('secondary')
  return trace
 sys.settrace(trace)
 try:
  with patch.object(t.ssl.SSLContext,'load_cert_chain',load):case.exchange()
 except BaseException as e:exc=e
 finally:sys.settrace(None)
 after=t.descriptors();leaks={fd:str(os.readlink('/proc/self/fd/'+str(fd))) for fd in after if fd not in before}
 if leaks or exc is not primary or any(not x.closed for x in case.streams):
  results.append({'target':target,'hit':hit,'exception':type(exc).__name__,'primary_preserved':exc is primary,'leaks':leaks,'unclosed':len([x for x in case.streams if not x.closed])})
 for fd in leaks:
  try:os.close(fd)
  except OSError:pass
 case.doCleanups();exc=None;primary=None;gc.collect()
print(json.dumps({'targets':len(targets),'findings':results},indent=2))
