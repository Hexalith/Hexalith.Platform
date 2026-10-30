import json,pathlib,subprocess,time,os,hashlib,shutil
p=pathlib.Path('/home/administrator/hexalith-protection-sessions/ext-protection-loop7-recovered-cd78452ebe/platform');e=p.parent/'eventstore';out=pathlib.Path('/tmp/ext-protection-loop7-evidence-ac76e4b97d26/matrix-final-13');out.mkdir();prior=json.loads((p/'docs/implementation/ext-protection-1-loop5/verification.json').read_bytes())['commands'];results=[]
env=dict(os.environ,HexalithCommonsRoot='/home/administrator/projects/hexalith/eventstore/references/Hexalith.Commons',HexalithTenantsBasePath='/home/administrator/projects/hexalith/eventstore/references/Hexalith.Tenants/src',EXT_PROTECTION_COMMITLINT_CLI='/home/administrator/projects/hexalith/eventstore/node_modules/@commitlint/cli/cli.js',EXT_PROTECTION_COMMITLINT_CONFIG='/home/administrator/projects/hexalith/eventstore/commitlint.config.mjs')
flags=['-c','Debug','-m:1','-nodeReuse:false','-p:GeneratePackageOnBuild=false','-p:UseHexalithProjectReferences=true','-p:HexalithEventStoreRoot='+str(e),'-p:HexalithCommonsRoot='+env['HexalithCommonsRoot'],'-p:HexalithTenantsBasePath='+env['HexalithTenantsBasePath']]
def run(name,cwd,argv):
 start=time.monotonic();log=out/(name+'.log')
 with log.open('xb') as stream:
  try:r=subprocess.run(argv,cwd=cwd,env=env,stdout=stream,stderr=subprocess.STDOUT,timeout=1200);code=r.returncode
  except subprocess.TimeoutExpired:code=124
 x={'name':name,'cwd':str(cwd),'argv':argv,'exitCode':code,'seconds':round(time.monotonic()-start,3),'log':str(log),'sha256':hashlib.sha256(log.read_bytes()).hexdigest()};results.append(x);(out/'commands.json').write_text(json.dumps(results,indent=2));print(name,code,flush=True)
 return code
if run('protection-final-build',p,['dotnet','build','tests/Hexalith.Platform.Protection.Tests/Hexalith.Platform.Protection.Tests.csproj']+flags):raise SystemExit(1)
frozen=out/'qualification-runtime';shutil.copytree(p/'tests/Hexalith.Platform.Protection.Tests/bin/Debug/net10.0',frozen);assembly=frozen/'Hexalith.Platform.Protection.Tests.dll';sha=hashlib.sha256(assembly.read_bytes()).hexdigest()
manifest=[{'path':str(path.relative_to(frozen)),'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'bytes':path.stat().st_size} for path in sorted(frozen.rglob('*')) if path.is_file()];(out/'qualification-runtime-manifest.json').write_text(json.dumps({'root':str(frozen),'assemblySha256':sha,'files':manifest},indent=2))
for prefix in ['EXT_PROTECTION_SNAPSHOT_COMPONENT','EXT_PROTECTION_P13_COMPONENT','EXT_PROTECTION_P34_COMPONENT','EXT_PROTECTION_LOOP7_COMPONENT']:env[prefix+'_ASSEMBLY']=str(assembly);env[prefix+'_SHA256']=sha
run('offline-verifier-final',e,['python3','-m','unittest','discover','-s','tools/tests','-p','test_ext_protection_1*.py','-v'])
run('protection-final-tests',p,['dotnet',str(assembly),'-noColor','-result-xml',str(out/'protection-final.xml')])
for c in prior:
 if c['name'] in ['protection-final-build','protection-final-tests','offline-verifier-final']:continue
 argv=[a.replace('/home/administrator/hexalith-protection-worktrees/ext-protection-loop5-290f9f5f3d',str(p.parent)) for a in c['argv']]
 argv=[str(out/pathlib.Path(a).name) if a.startswith('/tmp/ext-protection-loop5-evidence/') else a for a in argv]
 if len(argv)>1 and argv[1]=='build':argv+=['-p:GeneratePackageOnBuild=false']
 run(c['name'],e if c['cwd'].endswith('/eventstore') else p,argv)
run('secrets-complete-build',p,['dotnet','build','tests/Hexalith.Platform.Secrets.Runtime.Tests/Hexalith.Platform.Secrets.Runtime.Tests.csproj']+flags)
classes=['CustodyBindingContractTests','CustodyBindingFeasibilityTests','CustodyBindingOriginalTests','CustodyBindingDaprInvokerTests','SecretsRuntimeAdapterTests','SecretsAuthorityFailureTests','CustodyKeyLifecycleBindingTests','CustodyKeyLifecycleGuardAdmissionTests','CustodyLifecycleCancellationTests']
argv=['dotnet','tests/Hexalith.Platform.Secrets.Runtime.Tests/bin/Debug/net10.0/Hexalith.Platform.Secrets.Runtime.Tests.dll','-noColor','-result-xml',str(out/'secrets-affected.xml')]
for c in classes:argv+=['-class','Hexalith.Platform.Secrets.Runtime.Tests.'+c]
run('secrets-affected-tests',p,argv)
run('custody-revocation-signing-tests',p,['dotnet','tests/Hexalith.Platform.Custody.Tests/bin/Debug/net10.0/Hexalith.Platform.Custody.Tests.dll','-noColor','-class','Hexalith.Platform.Custody.Tests.DeletionCapabilityRevocationSubscriberTests','-class','Hexalith.Platform.Custody.Tests.DeletionCapabilitySigningActorTests','-result-xml',str(out/'custody-revocation-signing.xml')])
