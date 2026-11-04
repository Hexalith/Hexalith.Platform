import datetime,hashlib,json,os,subprocess,xml.etree.ElementTree as ET
from pathlib import Path
base=Path('/home/administrator/.cache/hexalith/story-2-8/eventstore-a0c-runtime-catalog-component-20261104');base.mkdir(parents=True,exist_ok=True)
caller=Path('/home/administrator/.cache/hexalith/story-2-8/eventstore-platform117-adoption-20261103/Hexalith.EventStore')
platform=caller/'references/Hexalith.Platform'
propsFile=Path('/home/administrator/.cache/hexalith/story-2-8/eventstore-platform117-adoption-20261103/platform121-validation/prepared-build.env')
envFile=dict(l.split('=',1) for l in propsFile.read_text().splitlines() if '=' in l)
assert set(envFile)=={'AfterMicrosoftNETSdkTargets','Configuration','CustomAfterDirectoryBuildTargets','CustomAfterMicrosoftCommonProps','CustomBeforeDirectoryBuildProps','CustomBeforeDirectoryBuildTargets','HEXALITH_SOURCE_MAPPING_HASH'}
assert subprocess.check_output(['git','-C',str(caller),'rev-parse','HEAD']).decode().strip()=='a0c65bec419138e79d763557c73573f894217f24'
assert subprocess.check_output(['git','-C',str(platform),'rev-parse','HEAD']).decode().strip()=='728baa7c0f006d86e71d6c9b29527dabc8e16e49'
project=base/'Hexalith.Platform.CatalogBuilder.csproj';assert not project.exists()
tree=ET.parse(platform/'src/Hexalith.Platform.CatalogBuilder/Hexalith.Platform.CatalogBuilder.csproj');root=tree.getroot()
root.insert(0,ET.Element('Import',Project=str(platform/'Directory.Packages.props')));root.insert(0,ET.Element('Import',Project=str(platform/'Directory.Build.props')));tree.write(project,encoding='utf-8',xml_declaration=True)
for n in ['Program.cs','PublishedCatalogArtifactCohort.cs']:(base/n).write_bytes((platform/'src/Hexalith.Platform.CatalogBuilder'/n).read_bytes())
bindings=ET.Element('Project');group=ET.SubElement(bindings,'ItemGroup')
for name in ['Hexalith.Tenants.Contracts','Hexalith.Tenants.Server']:
 ET.SubElement(group,'PackageReference',Include=name);ET.SubElement(group,'PackageVersion',Update=name,Version='7.0.0')
ET.ElementTree(bindings).write(base/'bindings.props',encoding='utf-8',xml_declaration=True)
env=os.environ.copy();env.update(envFile);env.update(NUGET_PACKAGES='/home/administrator/.cache/hexalith/story-2-8/tenants7-official-nuget-20261103',NUGET_CERT_REVOCATION_MODE='offline')
cmd=['dotnet','build',str(project),'-c','Release','--output',str(base/'output'),'-m:1','-nodeReuse:false','--disable-build-servers','-p:GeneratePackageOnBuild=false','-p:CentralPackageVersionsFileImported=true','-p:PlatformCatalogBindings='+str(base/'bindings.props'),'--nologo']
started=datetime.datetime.now(datetime.timezone.utc).isoformat();r=subprocess.run(cmd,cwd=platform,env=env,capture_output=True,text=True);log=base/'build-first.log';log.write_text(r.stdout+r.stderr)
receipt={'startedAt':started,'completedAt':datetime.datetime.now(datetime.timezone.utc).isoformat(),'command':cmd,'cwd':str(platform),'exitCode':r.returncode,'log':str(log),'logSha256':hashlib.sha256(log.read_bytes()).hexdigest(),'preparedMappingHash':envFile['HEXALITH_SOURCE_MAPPING_HASH'],'scope':'Component replica of unchanged PlatformRoutingCatalog.Generate project/imports/command under actual CLI4.44 prepared source-mapping environment; no service, run/owner proof or GitHub identity fabricated.','nativeAcceptance':False,'candidateAcceptance':False}
out=Path('_bmad-output/implementation-artifacts/evidence/2-8/release-phase/remote-resumption-20261103/parent-eventstore-a0c-catalog-build-component-first.json');assert not out.exists();out.write_text(json.dumps(receipt,indent=2)+'\n')
print(json.dumps(receipt,indent=2));print('\n'.join(l for l in (r.stdout+r.stderr).splitlines() if ': error ' in l or 'Error(s)' in l)[:6000])

