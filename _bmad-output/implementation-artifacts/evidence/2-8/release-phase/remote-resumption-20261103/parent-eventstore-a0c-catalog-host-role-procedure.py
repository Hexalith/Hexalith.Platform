import datetime,hashlib,json,os,shutil,subprocess
from pathlib import Path
base=Path('/home/administrator/.cache/hexalith/story-2-8/eventstore-a0c-runtime-catalog-component-20261104')
envPath=Path('/home/administrator/.cache/hexalith/story-2-8/eventstore-platform117-adoption-20261103/platform121-validation/prepared-build.env')
envValues=dict(l.split('=',1) for l in envPath.read_text().splitlines() if '=' in l)
workspace=Path(envValues['CustomAfterMicrosoftCommonProps']).parent.parent
target=workspace/'hosts/catalog-reader-parent-probe-20261104';assert not target.exists();target.mkdir(parents=True,mode=0o700)
for n in ['Hexalith.Platform.CatalogBuilder.csproj','Program.cs','PublishedCatalogArtifactCohort.cs','bindings.props']:(target/n).write_bytes((base/n).read_bytes())
env=os.environ.copy();env.update(envValues);env.update(NUGET_PACKAGES='/home/administrator/.cache/hexalith/story-2-8/tenants7-official-nuget-20261103',NUGET_CERT_REVOCATION_MODE='offline')
platform=Path('/home/administrator/.cache/hexalith/story-2-8/eventstore-platform117-adoption-20261103/Hexalith.EventStore/references/Hexalith.Platform')
cmd=['dotnet','build',str(target/'Hexalith.Platform.CatalogBuilder.csproj'),'-c','Release','--output',str(target/'output'),'-m:1','-nodeReuse:false','--disable-build-servers','-p:GeneratePackageOnBuild=false','-p:CentralPackageVersionsFileImported=true','-p:PlatformCatalogBindings='+str(target/'bindings.props'),'--nologo']
started=datetime.datetime.now(datetime.timezone.utc).isoformat();r=subprocess.run(cmd,cwd=platform,env=env,capture_output=True,text=True);log=base/'host-role-build-first.log';log.write_text(r.stdout+r.stderr)
receipt={'startedAt':started,'completedAt':datetime.datetime.now(datetime.timezone.utc).isoformat(),'command':cmd,'cwd':str(platform),'exitCode':r.returncode,'log':str(log),'logSha256':hashlib.sha256(log.read_bytes()).hexdigest(),'preparedMappingHash':envValues['HEXALITH_SOURCE_MAPPING_HASH'],'differenceFromNegative':'Only generated-reader placement now uses existing materialized workspace/hosts role; same released source, imports, catalog, bindings and all seven mapping environment hooks remain.','scope':'File/build component only, not production source or guard change; no services or authenticated owner proof/CI identity.','nativeAcceptance':False,'candidateAcceptance':False}
out=Path('_bmad-output/implementation-artifacts/evidence/2-8/release-phase/remote-resumption-20261103/parent-eventstore-a0c-catalog-host-role-component-first.json');assert not out.exists();out.write_text(json.dumps(receipt,indent=2)+'\n');print(json.dumps(receipt,indent=2));print('\n'.join(l for l in (r.stdout+r.stderr).splitlines() if ': error ' in l or 'Warning(s)' in l or 'Error(s)' in l)[:5000])
if r.returncode==0:
 assets=json.loads((target/'obj/project.assets.json').read_bytes());print('actual SDK packages',{k:v['type'] for k,v in assets['libraries'].items() if k.startswith('Hexalith.EventStore.')})

