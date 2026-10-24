exec(open('/tmp/bmad427-single-trace.py').read().split('case=t.CertificateTests',1)[0])
import dis
case=t.CertificateTests('test_exact_certificate_and_key_authenticate_real_peer_without_authorization');case.setUp();events=[];codes={}
def record(frame,event,arg):
 if frame.f_code.co_filename==path:
  frame.f_trace_opcodes=True
  if event=='opcode':
   events.append((frame.f_code.co_qualname,frame.f_lasti));codes[frame.f_code.co_qualname]=frame.f_code
 return record
sys.settrace(record)
case.exchange()
sys.settrace(None);case.doCleanups()
targets=list(dict.fromkeys(events));results=[]
for target in targets:
 case=t.CertificateTests('test_exact_certificate_and_key_authenticate_real_peer_without_authorization');case.setUp();before=t.descriptors();exc=None;hit=False
 def trace(frame,event,arg):
  global hit
  if frame.f_code.co_filename==path:
   frame.f_trace_opcodes=True
   if not hit and event=='opcode' and (frame.f_code.co_qualname,frame.f_lasti)==target:
    hit=True;raise KeyboardInterrupt('probe')
  return trace
 sys.settrace(trace)
 try:case.exchange()
 except BaseException as e:exc=e
 finally:sys.settrace(None)
 after=t.descriptors();leaks={fd:str(os.readlink('/proc/self/fd/'+str(fd))) for fd in after if fd not in before}
 if leaks or not isinstance(exc,KeyboardInterrupt) or any(not x.closed for x in case.streams):
  ins=next(i for i in dis.get_instructions(codes[target[0]]) if i.offset==target[1])
  results.append({'target':target,'instruction':str(ins),'hit':hit,'exception':type(exc).__name__,'leaks':leaks,'unclosed':len([x for x in case.streams if not x.closed])})
 for fd in leaks:
  try:os.close(fd)
  except OSError:pass
 case.doCleanups();exc=None;gc.collect()
print(json.dumps({'targets':len(targets),'findings':results},indent=2))
