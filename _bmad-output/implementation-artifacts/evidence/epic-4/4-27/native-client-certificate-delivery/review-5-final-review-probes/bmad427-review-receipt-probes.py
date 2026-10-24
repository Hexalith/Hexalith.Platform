import importlib.util,json,pathlib,sys
from unittest.mock import patch
p=pathlib.Path.cwd()/'_bmad-output/implementation-artifacts/evidence/epic-4/4-27/native-client-certificate-delivery/review-5/verify-receipt.py'
s=importlib.util.spec_from_file_location('checker',p);c=importlib.util.module_from_spec(s);sys.modules[s.name]=c;s.loader.exec_module(c)
raw=(c.PACKET/'verification.json').read_text();original=json.loads(raw);real=json.loads
c.verify(c.ROOT,c.PACKET)
for kind in ['verificationCommands','operationalClaims','runtimeClaim']:
 changed=json.loads(raw)
 if kind=='verificationCommands':changed['verificationCommands']=[]
 elif kind=='operationalClaims':
  for key in ['credentialAccess','configurationAccess','endpointAccess','operationalCertificateIssuance','recoveryAttemptAllocation','recoverySigning','recoveryExecution','uploads']:
   if key in changed:changed[key]=True
  print('claim keys', [(k,v) for k,v in changed.items() if v is True])
 else:changed['runtime']={'python':'inconsistent'}
 def read(text,*args,**kwargs):return changed if text==raw else real(text,*args,**kwargs)
 try:
  with patch.object(c.json,'loads',side_effect=read):c.verify(c.ROOT,c.PACKET)
  print(kind,'ACCEPTED')
 except Exception as e:print(kind,'rejected',str(e))
