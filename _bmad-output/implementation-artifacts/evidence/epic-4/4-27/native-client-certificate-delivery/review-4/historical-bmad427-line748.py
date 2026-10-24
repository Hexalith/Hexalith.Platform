exec(open('/tmp/bmad427-single-trace.py').read().split('case=t.CertificateTests',1)[0])
case=t.CertificateTests('test_exact_certificate_and_key_authenticate_real_peer_without_authorization');case.setUp()
def trace(frame,event,arg):
 if frame.f_code.co_filename==path and event=='line' and frame.f_code.co_name=='exchange' and frame.f_lineno==748: raise KeyboardInterrupt('at try')
 return trace
sys.settrace(trace)
try:case.exchange()
except BaseException as exc:
 sys.settrace(None)
 import traceback;traceback.print_exception(exc)
 print([(x.closed, bytes(x.sent)) for x in case.streams])
finally:sys.settrace(None);case.doCleanups()
