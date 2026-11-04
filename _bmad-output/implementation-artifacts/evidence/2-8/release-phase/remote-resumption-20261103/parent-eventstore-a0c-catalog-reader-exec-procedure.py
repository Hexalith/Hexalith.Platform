import datetime,hashlib,json,os,subprocess
from pathlib import Path
ev=Path('_bmad-output/implementation-artifacts/evidence/2-8/release-phase/remote-resumption-20261103')
base=Path('/home/administrator/.cache/hexalith/story-2-8/eventstore-a0c-runtime-catalog-component-20261104')
build=json.loads((ev/'parent-eventstore-a0c-catalog-host-role-component-first.json').read_bytes());assert build['exitCode']==0
target=Path(build['command'][2]).parent;output=base/'qualified-reader-output';assert not output.exists();output.mkdir(mode=0o700)
request={'qualificationCohort':'eventstore-4.0.4-tenants-7.0.0','generation':1,'instanceId':'parent-mapped-reader-component','outputDirectory':str(output),'gatewayAppId':'eventstore-parent-component','sourceMappingHash':build['preparedMappingHash'],'mode':'package','owners':[{'assembly':'Hexalith.Tenants.Contracts','adapterAssembly':'Hexalith.Tenants.Server','appId':'tenants-parent-component'}]}
path=base/'qualified-reader-request.json';assert not path.exists();path.write_text(json.dumps(request,indent=2)+'\n')
cmd=['dotnet',str(target/'output/Hexalith.Platform.CatalogBuilder.dll'),str(path)];r=subprocess.run(cmd,cwd=target,capture_output=True,text=True);assert r.returncode==0,r.stderr
catalog=json.loads((output/'catalog.json').read_bytes());receipt=json.loads((output/'receipt.json').read_bytes())
expected={'Hexalith.EventStore.Contracts.dll':'5680933ce8003475a3c323ddd48f299be0eeac52810424814ca6e39bab763d53','Hexalith.EventStore.ServiceDefaults.dll':'860b843eb81e5e1b1e6176b8865e79a116b62182c28cf5a39230f00bfac2115d','Hexalith.EventStore.Client.dll':'0bca680b2fc475720a113e05a73d328f01f7a7a37e98fb2babc94a35532e0e9c','Hexalith.EventStore.DomainService.dll':'f35786f3df58263d34e60ddfa83844cb43b2e7ac345420cb901f9ef49b7e3eb8','Hexalith.Tenants.Contracts.dll':'5676f656752395e149296ffbe9bc9d5e5e699e4bd6d4265df7de0f3442f05a40','Hexalith.Tenants.Server.dll':'4c03251c6378d131287a02b5b3871387874afcfff6f059f8bd52f9b640e15c93'}
verified={}
for name,digest in expected.items():
 raw=(target/'output'/name).read_bytes();assert hashlib.sha256(raw).hexdigest()==digest;verified[name]=digest
for n in ['catalog.json','receipt.json']:
 p=ev/('parent-eventstore-a0c-host-role-component-'+n);assert not p.exists();p.write_bytes((output/n).read_bytes())
out={'at':datetime.datetime.now(datetime.timezone.utc).isoformat(),'actualSource':'728baa7c0f006d86e71d6c9b29527dabc8e16e49','callerSource':'a0c65bec419138e79d763557c73573f894217f24','negativeComponent':'parent-eventstore-a0c-catalog-build-component-first.json','positiveBuild':'parent-eventstore-a0c-catalog-host-role-component-first.json','command':cmd,'exitCode':r.returncode,'stdout':r.stdout,'stderr':r.stderr,'sourceMappingHash':build['preparedMappingHash'],'actualPublicDllHashesVerified':verified,'catalogSha256':hashlib.sha256((output/'catalog.json').read_bytes()).hexdigest(),'routes':len(catalog['routes']),'intentPolicies':len(catalog['idempotency']),'qualifiedReaderName':request['qualificationCohort'],'authorityRegistered':False,'nativeAcceptance':False,'candidateAcceptance':False,'limitation':'Hosted build.log was not retained; this component independently demonstrates HXW005 under the actual prepared consumer mapping and green existing hosts-role placement. No claim of recovering the exact hosted compiler log or actual backend execution.'}
p=ev/'parent-eventstore-a0c-catalog-host-role-component-audit.json';assert not p.exists();p.write_text(json.dumps(out,indent=2)+'\n');print(json.dumps(out,indent=2))

