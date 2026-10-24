import importlib.util, pathlib, sys, time
from unittest.mock import patch
p=pathlib.Path.cwd()/'eng/recovery-preparation/test_native_client_certificate.py'
s=importlib.util.spec_from_file_location('probe_test',p); t=importlib.util.module_from_spec(s); sys.modules[s.name]=t;s.loader.exec_module(t)
b=t.b
v=t.CertificateTests('test_exact_certificate_and_key_authenticate_real_peer_without_authorization'); v.setUp()
class Invalid:
 _native_owned_stream=True
 closed=False
 def close(self): self.closed=True
stream=Invalid()
def acquire(custody,*args): custody.stream=stream
hit=[]; bad=[]; primary=KeyboardInterrupt('current cancellation')
def trace(frame,event,arg):
 if frame.f_code.co_filename==b.__file__ and '<genexpr>' in frame.f_code.co_qualname:
  if event=='exception' and arg[0] is GeneratorExit:
   hit.append((frame.f_code.co_qualname,frame.f_lineno)); raise primary
 return trace
sys.unraisablehook=lambda e:bad.append(type(e.exc_value).__name__)
caught=None
try:
 sys.settrace(trace); v.exchange(connector=b._OwnedConnector(acquire))
except BaseException as e: caught=e
finally:
 sys.settrace(None);v.doCleanups()
print({'hit':hit,'propagatedPrimary':caught is primary,'caught':type(caught).__name__,'unraisable':bad,'closed':stream.closed})
