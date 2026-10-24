import hashlib,importlib.util,json,pathlib,shutil,sys,tempfile
p=pathlib.Path.cwd()/'_bmad-output/implementation-artifacts/evidence/epic-4/4-27/native-client-certificate-delivery/review-5/verify-receipt.py'
s=importlib.util.spec_from_file_location('checker',p);c=importlib.util.module_from_spec(s);sys.modules[s.name]=c;s.loader.exec_module(c)
for mode in ['prior inventory','sweep omissions']:
 with tempfile.TemporaryDirectory() as d:
  parent=pathlib.Path(d)/'packet';shutil.copytree(c.PACKET.parent,parent);packet=parent/'review-5';receipt=json.loads((packet/'verification.json').read_text())
  if mode=='prior inventory':
   (packet/'prior-packet-sha256.json').write_text('{}\n'); receipt['packetFileSha256']['prior-packet-sha256.json']=c.digest(b'{}\n')
   (parent/'review-4/sources/recovery_execution_boundary.py').write_text('# historical source corrupted\n')
  else:
   sweep=json.loads((packet/'cancellation-sweeps.json').read_text());meta=receipt['currentCancellationSweeps']
   for name in ['line','opcode','secondary']:
    sweep[name]['targets']=1;sweep[name]['terminalExternalCancellations']=[];meta['modes'][name]['targets']=1;meta['modes'][name]['terminalExternalCancellations']=0
   entry=sweep['preparedEntry'];entry['entryTransitions']=[next(row for row in entry['entryTransitions'] if row[0]==event and row[1]==qual) for event,qual in [('call','NativeTLS._guard_native'),('line','NativeTLS.exchange'),('opcode','NativeTLS.exchange')]];entry['targets']=3;meta['modes']['preparedEntry']['targets']=3
   meta.update(lineTargets=1,opcodeTargets=1,secondaryTargets=1,preparedEntryTargets=3,terminalExternalCancellationsAfterResourceRelease=0)
   (packet/'cancellation-sweeps.json').write_text(json.dumps(sweep));sha=c.digest((packet/'cancellation-sweeps.json').read_bytes());receipt['packetFileSha256']['cancellation-sweeps.json']=sha
   for item in receipt['verificationCommands']:
    if item['log']=='cancellation-sweeps.json':item['logSha256']=sha
  (packet/'verification.json').write_text(json.dumps(receipt))
  try:c.verify(c.ROOT,packet);print(mode,'ACCEPTED')
  except Exception as e:print(mode,'rejected:',e)
