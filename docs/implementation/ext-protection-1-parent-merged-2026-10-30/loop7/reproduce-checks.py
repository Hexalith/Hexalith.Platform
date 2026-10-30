from pathlib import Path
import subprocess,json,time,hashlib,os
root=Path('/home/administrator/projects/hexalith/platform');es=root.parent/'eventstore';out=Path('/tmp/ext-protection-loop7-parent-final');out.mkdir(exist_ok=True)
flags=['-c','Debug','-p:UseHexalithProjectReferences=true','-p:HexalithEventStoreRoot='+str(es),'-p:HexalithCommonsRoot='+str(es/'references/Hexalith.Commons'),'-p:HexalithTenantsBasePath='+str(es/'references/Hexalith.Tenants/src'),'-m:1','-nodeReuse:false','-p:GeneratePackageOnBuild=false']
inv=json.loads((root/'docs/implementation/ext-protection-1-loop2-2026-10-29/reader/final-regression-commands.json').read_text())
selection=next(v['argv'] for v in inv if v['name']=='eventstore-final-tests')[:];selection[1]='tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll';selection[selection.index('-result-xml')+1]=str(out/'eventstore.xml')
commands=[('protection-build',root,['dotnet','build','tests/Hexalith.Platform.Protection.Tests/Hexalith.Platform.Protection.Tests.csproj',*flags]),('protection-tests',root,['dotnet','tests/Hexalith.Platform.Protection.Tests/bin/Debug/net10.0/Hexalith.Platform.Protection.Tests.dll','-noColor','-parallelMode','none','-result-xml',str(out/'protection.xml')]),('eventstore-build',es,['dotnet','build','tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj',*flags]),('eventstore-tests',es,selection),('payload-build',es,['dotnet','build','tests/Hexalith.EventStore.PayloadProtection.Tests/Hexalith.EventStore.PayloadProtection.Tests.csproj',*flags]),('payload-tests',es,['dotnet','tests/Hexalith.EventStore.PayloadProtection.Tests/bin/Debug/net10.0/Hexalith.EventStore.PayloadProtection.Tests.dll','-noColor','-parallelMode','none','-result-xml',str(out/'payload.xml')]),('payload-aot-build',es,['dotnet','build','src/Hexalith.EventStore.PayloadProtection/Hexalith.EventStore.PayloadProtection.csproj',*flags])]
commands[2:2]=[('custody-lifecycle-build',root,['dotnet','build','tests/Hexalith.Platform.Custody.Tests/Hexalith.Platform.Custody.Tests.csproj',*flags]),('custody-lifecycle-tests',root,['dotnet','tests/Hexalith.Platform.Custody.Tests/bin/Debug/net10.0/Hexalith.Platform.Custody.Tests.dll','-noColor','-class','Hexalith.Platform.Custody.Tests.CustodyKeyLifecycleTests','-class','Hexalith.Platform.Custody.Tests.DeletionCapabilityRevocationSubscriberTests','-class','Hexalith.Platform.Custody.Tests.DeletionCapabilitySigningActorTests','-result-xml',str(out/'custody-lifecycle.xml')])]
commands[4:4]=[('secrets-adapters-build',root,['dotnet','build','tests/Hexalith.Platform.Secrets.Runtime.Tests/Hexalith.Platform.Secrets.Runtime.Tests.csproj',*flags]),('secrets-adapters-tests',root,['dotnet','tests/Hexalith.Platform.Secrets.Runtime.Tests/bin/Debug/net10.0/Hexalith.Platform.Secrets.Runtime.Tests.dll','-noColor','-class','Hexalith.Platform.Secrets.Runtime.Tests.SecretsRuntimeAdapterTests','-class','Hexalith.Platform.Secrets.Runtime.Tests.SecretsAuthorityFailureTests','-class','Hexalith.Platform.Secrets.Runtime.Tests.CustodyBindingFeasibilityTests','-result-xml',str(out/'secrets-adapters.xml')])]
for index,(name,cwd,args) in enumerate(commands):
 if name=='secrets-adapters-build':commands[index]=('secrets-complete-build',cwd,args)
 if name=='secrets-adapters-tests':
  classes=['Hexalith.Platform.Secrets.Runtime.Tests.CustodyBindingContractTests', 'Hexalith.Platform.Secrets.Runtime.Tests.CustodyBindingFeasibilityTests', 'Hexalith.Platform.Secrets.Runtime.Tests.CustodyBindingOriginalTests', 'Hexalith.Platform.Secrets.Runtime.Tests.CustodyBindingDaprInvokerTests', 'Hexalith.Platform.Secrets.Runtime.Tests.SecretsRuntimeAdapterTests', 'Hexalith.Platform.Secrets.Runtime.Tests.SecretsAuthorityFailureTests', 'Hexalith.Platform.Secrets.Runtime.Tests.CustodyKeyLifecycleBindingTests', 'Hexalith.Platform.Secrets.Runtime.Tests.CustodyKeyLifecycleGuardAdmissionTests', 'Hexalith.Platform.Secrets.Runtime.Tests.CustodyLifecycleCancellationTests']
  flags_for_classes=[item for cls in classes for item in ['-class',cls]]
  commands[index]=('secrets-affected-tests',cwd,['dotnet','tests/Hexalith.Platform.Secrets.Runtime.Tests/bin/Debug/net10.0/Hexalith.Platform.Secrets.Runtime.Tests.dll','-noColor',*flags_for_classes,'-result-xml',str(out/'secrets-affected.xml')])
commands.extend([('contracts-build',es,['dotnet','build','tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj',*flags]),('contracts-tests',es,['dotnet','tests/Hexalith.EventStore.Contracts.Tests/bin/Debug/net10.0/Hexalith.EventStore.Contracts.Tests.dll','-noColor','-class','Hexalith.EventStore.Contracts.Tests.Security.RecoverableAnchoredStateTests','-result-xml',str(out/'contracts.xml')])])
state=json.loads(Path('/tmp/bmad-protection-audit-scope.json').read_text());isolated=Path(state['rederive7']['roots']['platform']);isolatedEs=Path(state['rederive7']['roots']['eventstore'])
for p in ['Hexalith.Platform.EventStoreHost',*['Hexalith.Platform.Protection.Qualification.'+role for role in ['Executor','Observer','Enrollment']]]:
 # Build the preserved current shared catalog host against the current paired EventStore source.
 buildRoot=root
 buildFlags=[('-p:HexalithEventStoreRoot='+str(isolatedEs)) if arg.startswith('-p:HexalithEventStoreRoot=') and buildRoot==isolated else arg for arg in flags]
 commands.append((p+'-build',buildRoot,['dotnet','build',f'src/{p}/{p}.csproj',*buildFlags]))
commands.extend([('harness',root,['python3','-m','unittest','discover','-s','eng/ext-protection-1','-p','test_*.py','-v']),('source',es,['python3','tools/verify-ext-protection-1.py','--mode','Source']),('offline-verifier',es,['python3','-m','unittest','discover','-s','tools/tests','-p','test_ext_protection_1*.py','-v'])])
start_index=int(os.environ.get('EXT_PROTECTION_PARENT_START_INDEX','0'))
results=json.loads((out/'commands.json').read_text())[:start_index] if start_index else []
for name,cwd,args in commands[start_index:]:
 start=time.monotonic();p=out/(name+'.log');env=dict(os.environ,HexalithCommonsRoot=str((es/'references/Hexalith.Commons').resolve()),HexalithTenantsBasePath=str((es/'references/Hexalith.Tenants/src').resolve()))
 # Ordinary parent checks never opt into a different session's native/local transport fixture.
 env.pop('EXT_SECRETS_1_LOCAL_TRANSPORT',None)
 for variable in list(env):
  if variable.startswith('ERC_NATIVE_'):env.pop(variable,None)
 if name=='offline-verifier':
  assembly=root/'tests/Hexalith.Platform.Protection.Tests/bin/Debug/net10.0/Hexalith.Platform.Protection.Tests.dll';digest=hashlib.sha256(assembly.read_bytes()).hexdigest()
  for case in ['P13','P34','SNAPSHOT','LOOP7']:env['EXT_PROTECTION_'+case+'_COMPONENT_ASSEMBLY']=str(assembly);env['EXT_PROTECTION_'+case+'_COMPONENT_SHA256']=digest
 with p.open('wb') as f:r=subprocess.run(args,cwd=cwd,env=env,stdout=f,stderr=subprocess.STDOUT)
 row=dict(name=name,cwd=str(cwd),argv=args,exitCode=r.returncode,seconds=round(time.monotonic()-start,3),path=p.name,sha256=hashlib.sha256(p.read_bytes()).hexdigest());results.append(row);(out/'commands.json').write_text(json.dumps(results,indent=2)+'\n');print(name,r.returncode,flush=True)
 if r.returncode:raise SystemExit(r.returncode)
