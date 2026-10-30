import json,pathlib,subprocess,time,os,hashlib
p=pathlib.Path('/home/administrator/hexalith-protection-worktrees/ext-protection-loop6-07bd8b0dea/platform');e=p.parent/'eventstore';out=pathlib.Path('/tmp/ext-protection-loop6-evidence/matrix-final-02');out.mkdir();prior=json.loads((p/'docs/implementation/ext-protection-1-loop5/verification.json').read_bytes())['commands'];results=[]
env=dict(os.environ,HexalithCommonsRoot='/home/administrator/projects/hexalith/eventstore/references/Hexalith.Commons')
pins=json.loads(pathlib.Path('/tmp/ext-protection-loop6-evidence/final-freeze-02.json').read_bytes())
for prefix in ['EXT_PROTECTION_SNAPSHOT_COMPONENT','EXT_PROTECTION_P13_COMPONENT','EXT_PROTECTION_P34_COMPONENT']:
 env[prefix+'_ASSEMBLY']=pins['root']+'/Hexalith.Platform.Protection.Tests.dll';env[prefix+'_SHA256']=pins['assemblySha256']
def run(name,cwd,argv):
 start=time.monotonic();log=out/(name+'.log')
 with log.open('xb') as stream:
  try:r=subprocess.run(argv,cwd=cwd,env=env,stdout=stream,stderr=subprocess.STDOUT,timeout=900);exit=r.returncode
  except subprocess.TimeoutExpired:exit=124
 x={'name':name,'cwd':str(cwd),'argv':argv,'exitCode':exit,'seconds':round(time.monotonic()-start,3),'log':str(log),'sha256':hashlib.sha256(log.read_bytes()).hexdigest()};results.append(x);(out/'commands.json').write_text(json.dumps(results,indent=2));print(name,exit,flush=True)
for c in prior:
 if c['name'] in ['protection-final-build','protection-final-tests']:continue
 argv=[a.replace('ext-protection-loop5-290f9f5f3d','ext-protection-loop6-07bd8b0dea') for a in c['argv']]
 argv=[str(out/pathlib.Path(a).name) if a.startswith('/tmp/ext-protection-loop5-evidence/') else a for a in argv]
 if len(argv)>1 and argv[1]=='build':argv+=['-p:GeneratePackageOnBuild=false']
 run(c['name'],e if c['cwd'].endswith('/eventstore') else p,argv)
flags=['-c','Debug','-m:1','-nodeReuse:false','-p:GeneratePackageOnBuild=false','-p:UseHexalithProjectReferences=true','-p:HexalithEventStoreRoot='+str(e),'-p:HexalithCommonsRoot='+env['HexalithCommonsRoot'],'-p:HexalithTenantsBasePath=/home/administrator/projects/hexalith/eventstore/references/Hexalith.Tenants/src']
run('secrets-complete-build',p,['dotnet','build','tests/Hexalith.Platform.Secrets.Runtime.Tests/Hexalith.Platform.Secrets.Runtime.Tests.csproj']+flags)
classes=['CustodyBindingContractTests','CustodyBindingFeasibilityTests','CustodyBindingOriginalTests','CustodyBindingDaprInvokerTests','SecretsRuntimeAdapterTests','SecretsAuthorityFailureTests','CustodyKeyLifecycleBindingTests','CustodyKeyLifecycleGuardAdmissionTests','CustodyLifecycleCancellationTests']
argv=['dotnet','tests/Hexalith.Platform.Secrets.Runtime.Tests/bin/Debug/net10.0/Hexalith.Platform.Secrets.Runtime.Tests.dll','-noColor','-result-xml',str(out/'secrets-affected.xml')]
for c in classes:argv+=['-class','Hexalith.Platform.Secrets.Runtime.Tests.'+c]
run('secrets-affected-tests',p,argv)
run('custody-revocation-tests',p,['dotnet','tests/Hexalith.Platform.Custody.Tests/bin/Debug/net10.0/Hexalith.Platform.Custody.Tests.dll','-noColor','-class','Hexalith.Platform.Custody.Tests.DeletionCapabilityRevocationSubscriberTests','-result-xml',str(out/'custody-revocation.xml')])
