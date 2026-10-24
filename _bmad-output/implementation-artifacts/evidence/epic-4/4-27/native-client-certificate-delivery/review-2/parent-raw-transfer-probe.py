from pathlib import Path
import dis, importlib.util, json, sys
p=Path('eng/recovery-preparation/test_native_client_certificate.py').absolute()
spec=importlib.util.spec_from_file_location('parent_certificate_audit',p)
m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
b=m.b
instructions=list(dis.get_instructions(b.sealed))
targets=[i for n,i in enumerate(instructions) if i.opname=='STORE_FAST' and i.argval in ('low','fd') and n and instructions[n-1].opname=='CALL']
assert {i.argval for i in targets} == {'low','fd'} and len(targets)==2
results=[]
for instruction in targets:
 for occurrence in (1,2):
  case=m.CertificateTests();case.setUp();before=m.descriptors()
  error=KeyboardInterrupt('parent raw-transfer probe');seen=[];fired=[]
  def trace(frame,event,arg):
   if frame.f_code is b.sealed.__code__:
    frame.f_trace_opcodes=True
    if event=='opcode' and frame.f_lasti==instruction.offset and frame.f_locals.get('custody') is not None:
     seen.append(True)
     if len(seen)==occurrence:
      fired.append(True);raise error
   return trace
  caught=None
  try:
   try:
    sys.settrace(trace);case.exchange()
   except BaseException as exc:caught=exc
   finally:sys.settrace(None)
   after=m.descriptors()
   result={'rawResult':instruction.argval,'occurrence':occurrence,'injected':bool(fired),'originalInterruptPreserved':caught is error,'newDescriptors':sorted(set(after)-set(before)),'descriptorSnapshotPreserved':after==before}
   assert result['injected'] and result['originalInterruptPreserved'] and result['descriptorSnapshotPreserved'],result
   results.append(result)
  finally:case.doCleanups()
print(json.dumps({'scope':'independent parent, synthetic fixtures only, no sockets','results':results},indent=2))
