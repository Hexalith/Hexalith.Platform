from pathlib import Path
import json,subprocess,os,time,hashlib
root=Path('/home/administrator/hexalith-protection-worktrees/ext-protection-loop4-00722d7d17/platform');out=Path('/tmp/ext-protection-loop4-evidence');records=[row for p in out.glob('commands-*.json') for row in json.loads(p.read_text())];results=[]
commands=[dict(name='protection-final-v5',cwd=str(root),argv=['dotnet',str(root/'tests/Hexalith.Platform.Protection.Tests/bin/Debug/net10.0/Hexalith.Platform.Protection.Tests.dll'),'-noColor','-result-xml',str(out/'protection-final-v5.xml')])]
for name in ['eventstore-final-build','eventstore-final-tests','contracts-final-build','contracts-final-tests','payload-final-build','payload-final-tests','payload-aot-final-build','Hexalith.Platform.EventStoreHost-final-build','Hexalith.Platform.Protection.Qualification.Executor-final-build','Hexalith.Platform.Protection.Qualification.Observer-final-build','Hexalith.Platform.Protection.Qualification.Enrollment-final-build','harness-final','source-final','offline-verifier-final']:
 row=next(r for r in records if r['name']==name);args=row['argv'][:]
 if '-result-xml' in args:args[args.index('-result-xml')+1]=str(out/(name+'-v3.xml'))
 commands.append(dict(name=name+'-v3',cwd=row['cwd'],argv=args))
for command in commands:
 name,cwd,args=command['name'],command['cwd'],command['argv'];log=out/(name+'.log');start=time.monotonic()
 env=dict(os.environ,HexalithCommonsRoot='/home/administrator/projects/hexalith/eventstore/references/Hexalith.Commons',HexalithTenantsBasePath='/home/administrator/projects/hexalith/eventstore/references/Hexalith.Tenants/src',HexalithEventStoreRoot=str(root.parent/'eventstore'))
 if name=='offline-verifier-final-v3':
  assembly=root/'tests/Hexalith.Platform.Protection.Tests/bin/Debug/net10.0/Hexalith.Platform.Protection.Tests.dll';digest=hashlib.sha256(assembly.read_bytes()).hexdigest()
  for case in ['P13','P34','SNAPSHOT']:env['EXT_PROTECTION_'+case+'_COMPONENT_ASSEMBLY']=str(assembly);env['EXT_PROTECTION_'+case+'_COMPONENT_SHA256']=digest
 with log.open('xb') as f:p=subprocess.run(args,cwd=cwd,env=env,stdout=f,stderr=subprocess.STDOUT)
 results.append(dict(**command,exitCode=p.returncode,seconds=time.monotonic()-start,sha256=hashlib.sha256(log.read_bytes()).hexdigest()));(out/'final-v5-complete-commands.json').write_text(json.dumps(results,indent=2)+'\n');print(name,p.returncode,flush=True)
 if p.returncode:break
