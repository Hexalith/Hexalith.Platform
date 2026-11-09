"""Prepare a measured binding from a completed normal exact-source Builds publisher."""
import argparse,datetime,hashlib,json,re,subprocess,urllib.request,zipfile
import xml.etree.ElementTree as ET
from pathlib import Path
if not __debug__:raise ValueError('Publication identity checks require Python optimization to be disabled.')
p=argparse.ArgumentParser(description=__doc__)
p.add_argument('--version',required=True);p.add_argument('--source',required=True);p.add_argument('--ci-run',required=True);p.add_argument('--publisher-run',required=True);p.add_argument('--measurement',type=Path,required=True);p.add_argument('--output',type=Path,required=True);p.add_argument('--builds-root',type=Path,required=True)
a=p.parse_args()
assert re.fullmatch(r'4\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)',a.version) and re.fullmatch(r'[a-f0-9]{40}',a.source)
assert all(re.fullmatch(r'[1-9][0-9]*',run) for run in [a.ci_run,a.publisher_run])
a.output.mkdir(exist_ok=False)
def gh(path):return json.loads(subprocess.check_output(['gh','api',path],text=True))
def unique(pairs):
 result={}
 for key,value in pairs:
  if key in result:raise ValueError('Duplicate artifact property: '+key)
  result[key]=value
 return result
repository='Hexalith/Hexalith.Builds';base='repos/'+repository
ci=gh(base+'/actions/runs/'+a.ci_run);publisher=gh(base+'/actions/runs/'+a.publisher_run)
for run,event,path in [(ci,'push','.github/workflows/builds-ci.yml'),(publisher,'workflow_dispatch','.github/workflows/builds-release.yml')]:
 assert (run['repository']['full_name'],run['head_sha'],run['head_branch'],run['event'],run['path'],run['status'],run['conclusion'])==(repository,a.source,'main',event,path,'completed','success')
release=gh(base+'/releases/tags/v'+a.version);tag=gh(base+'/commits/v'+a.version)
assert tag['sha']==a.source and release['tag_name']=='v'+a.version and not release['draft'] and not release['prerelease']
assets={x['name']:x for x in release['assets']};assert len(assets)==len(release['assets']);asset=assets['g4-tool-package-inventory.json']
url=f'https://github.com/{repository}/releases/download/v{a.version}/g4-tool-package-inventory.json';assert asset['browser_download_url']==url
subprocess.run(['gh','release','download','v'+a.version,'--repo',repository,'--pattern','g4-tool-package-inventory.json','--dir',str(a.output)],check=True)
raw=(a.output/'g4-tool-package-inventory.json').read_bytes();inventory=json.loads(raw,object_pairs_hook=unique)
assert inventory['schema']=='hexalith.g4-tool-package-inventory.v1' and inventory['version']==a.version and inventory['configuration']=='Release'
q=inventory['qualification'];assert q['packageBuild']=={'mode':'official','result':'passed'} and q['controls']=={'mode':'executed','result':'passed'}
assert q['releaseEligible'] is True and q['ineligibilityReasons']==[] and q['sourceTree']=={'clean':True,'revision':a.source}
assert q['fixtures']['mode']=='repository-tracked'
assert q['sourceValidation']['mode']=='ci' and q['sourceValidation']['result']=='passed'
recorded_ci=q['sourceValidation']['ciRun'];assert recorded_ci['workflow']==ci['path']
assert (recorded_ci['repository'],recorded_ci['sha'],str(recorded_ci['id']),recorded_ci['runAttempt'],recorded_ci['branch'],recorded_ci['url'])==(repository,a.source,a.ci_run,ci['run_attempt'],'main',ci['html_url'])
m=json.loads(a.measurement.read_bytes(),object_pairs_hook=unique);assert m['repository']==repository and m['source']==a.source and m['version']==a.version
assert isinstance(m['packages'],list) and len(m['packages'])==1 and m['packages'][0]['id']=='Hexalith.Builds.Module.Cli'
package=m['packages'][0];assert package['repository']['commit']==a.source and package['version']==a.version
archive=a.output/'official-signed-module.nupkg'
official_url=f'https://api.nuget.org/v3-flatcontainer/hexalith.builds.module.cli/{a.version}/hexalith.builds.module.cli.{a.version}.nupkg'
assert package['url']==official_url
with urllib.request.urlopen(official_url,timeout=90) as response:
 assert response.status==200 and response.geturl()==official_url
 data=response.read(100_000_001)
assert len(data)<=100_000_000 and hashlib.sha256(data).hexdigest()==package['archiveSha256']
archive.write_bytes(data)
verification=subprocess.run(['dotnet','nuget','verify',str(archive),'--all'],capture_output=True,text=True,check=True,timeout=120)
(a.output/'signature.stdout').write_text(verification.stdout);(a.output/'signature.stderr').write_text(verification.stderr)
assert 'Signature type: Repository' in verification.stdout and 'NuGet.org Repository by Microsoft' in verification.stdout
assert package['url']==f'https://api.nuget.org/v3-flatcontainer/hexalith.builds.module.cli/{a.version}/hexalith.builds.module.cli.{a.version}.nupkg'
with zipfile.ZipFile(archive) as z:
 assert len(z.namelist())==len({name.casefold() for name in z.namelist()})
 nuspec_names=[n for n in z.namelist() if n.endswith('.nuspec')];assert len(nuspec_names)==1
 nuspec=ET.fromstring(z.read(nuspec_names[0]))
 for name,value in [('id','Hexalith.Builds.Module.Cli'),('version',a.version)]:
  nodes=[node for node in nuspec.iter() if node.tag.rsplit('}',1)[-1]==name];assert len(nodes)==1 and nodes[0].text==value
 repositories=[node.attrib for node in nuspec.iter() if node.tag.rsplit('}',1)[-1]=='repository']
 assert len(repositories)==1 and repositories[0]['type']=='git' and repositories[0]['commit']==a.source and repositories[0]['url'] in ['https://github.com/'+repository,'https://github.com/'+repository+'.git']
 hashes={n:hashlib.sha256(z.read(n)).hexdigest() for n in z.namelist() if not n.endswith('/')}
 assert '.signature.p7s' in hashes
 prefix='tools/net10.0/any/'
 apphost=hashes[prefix+'g4-host/Hexalith.Builds.Module.AppHost.dll']
 seed=hashes[prefix+'g4-host/projects/EventStore/Host.csproj']
 tooling=hashes[prefix+'Hexalith.Builds.Tooling.dll']
 assert hashes[prefix+'g4-host/Hexalith.Builds.Tooling.dll']==tooling
qualified_packages={item['id']:item for item in inventory['packages']}
assert len(qualified_packages)==len(inventory['packages']) and set(qualified_packages)=={'Hexalith.Builds.Module.Cli','Hexalith.Builds.Evidence.Cli'}
assert all(item['version']==a.version for item in inventory['packages'])
qualified=qualified_packages['Hexalith.Builds.Module.Cli']
assert qualified['version']==a.version
publisher_log=subprocess.check_output(['gh','run','view',a.publisher_run,'--repo',repository,'--attempt',str(publisher['run_attempt']),'--log'],text=True)
(a.output/'publisher.log').write_text(publisher_log)
message="Verified remote 'Hexalith.Builds.Module.Cli' '"+a.version+"' SHA-256 "+package['archiveSha256'].upper()+" (qualified local SHA-256 "+qualified['nupkg']['sha256'].upper()+")."
assert message in publisher_log, 'Normal publisher did not retain the matching remote-versus-qualified archive verification.'
carrier='g4-tool-qualification-evidence.zip'
assert assets[carrier]['browser_download_url']==f'https://github.com/{repository}/releases/download/v{a.version}/{carrier}'
subprocess.run(['gh','release','download','v'+a.version,'--repo',repository,'--pattern',carrier,'--dir',str(a.output)],check=True)
rows=inventory['qualificationEvidence'];names=[row['file'] for row in rows]
assert rows and len({name.casefold() for name in names})==len(names)
assert all(re.fullmatch(r'qualification-evidence/[A-Za-z0-9][A-Za-z0-9._-]*',name) for name in names)
with zipfile.ZipFile(a.output/carrier) as z:
 assert len(z.namelist())==len(names)+2 and set(z.namelist())==set(names)|{'g4-tool-package-inventory.json','producer-run.json'}
 assert z.read('g4-tool-package-inventory.json')==raw
 for row in rows:
  data=z.read(row['file']);assert type(row['sizeBytes']) is int and len(data)==row['sizeBytes'] and hashlib.sha256(data).hexdigest()==row['sha256'].lower()
 producer=json.loads(z.read('producer-run.json'),object_pairs_hook=unique)
 for key,value in {'schema':'hexalith.g4-qualification-carrier.v1','kind':'release','version':a.version,'sourceRevision':a.source,'inventorySha256':hashlib.sha256(raw).hexdigest(),'qualificationEvidenceFiles':len(rows),'GITHUB_REPOSITORY':repository,'GITHUB_RUN_ID':a.publisher_run,'GITHUB_RUN_ATTEMPT':str(publisher['run_attempt']),'GITHUB_JOB':'release','GITHUB_WORKFLOW_REF':repository+'/.github/workflows/builds-release.yml@refs/heads/main','GITHUB_WORKFLOW_SHA':a.source,'GITHUB_EVENT_NAME':'workflow_dispatch'}.items():assert producer[key]==value
 seed_receipt=json.loads(z.read('qualification-evidence/packaged-seed-subscriptions.json'),object_pairs_hook=unique)
 assert seed_receipt['schema']=='hexalith.g4-packaged-seed-subscriptions.v1' and seed_receipt['status']=='passed'
 assert seed_receipt['package']['id']=='Hexalith.Builds.Module.Cli' and seed_receipt['package']['version']==a.version and seed_receipt['package']['sha256'].lower()==qualified['nupkg']['sha256'].lower()
 seed_body=hashes[prefix+'g4-host/bin/EventStoreHost/Hexalith.Builds.Module.EventStoreHost.dll']
 assert seed_receipt['seed']['entry']==prefix+'g4-host/bin/EventStoreHost/Hexalith.Builds.Module.EventStoreHost.dll' and seed_receipt['seed']['sha256'].lower()==seed_body and seed_receipt['seed']['loadedSha256'].lower()==seed_body
root=a.builds_root
assert subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip()==a.source
catalog=subprocess.check_output(['git','show',a.source+':Props/Directory.Packages.props'],cwd=root)
blob=subprocess.check_output(['git','rev-parse',a.source+':Props/Directory.Packages.props'],cwd=root,text=True).strip();catalog_hash=hashlib.sha256(catalog).hexdigest()
binding={'schemaVersion':1,'candidateAcceptance':False,'producer':{'repository':'https://github.com/'+repository+'.git','sourceRevision':a.source,'catalog':{'path':'Props/Directory.Packages.props','blob':blob,'sha256':catalog_hash}},'publication':{'version':a.version,'sourceRevision':a.source,'releaseTag':'v'+a.version,'catalogSha256':catalog_hash,'archiveSha256':package['archiveSha256'],'toolingSha256':tooling,'inventorySha256':hashlib.sha256(raw).hexdigest(),'inventoryUrl':url}}
(a.output/'builds-tuple.json').write_text(json.dumps(binding,indent=2)+'\n')
receipt={'recordedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'ci':{'run':int(a.ci_run),'attempt':ci['run_attempt'],'source':a.source,'url':ci['html_url']},'publisher':{'run':int(a.publisher_run),'attempt':publisher['run_attempt'],'source':a.source,'url':publisher['html_url']},'release':release['html_url'],'binding':binding,'appHostSha256':apphost,'seedProjectSha256':seed,'allArchiveBodies':hashes,'catalogSha256':catalog_hash,'inventoryQualifiedArchiveSha256':qualified['nupkg']['sha256'],'qualifiedEvidenceFiles':len(rows),'qualifiedCarrierSha256':hashlib.sha256((a.output/carrier).read_bytes()).hexdigest(),'publisherPayloadVerification':message,'note':'Fresh exact official NuGet URL archive was independently repository-signature verified. Normal publisher verifies all non-signature payloads against the qualified archive; original unsigned ZIP hash differs after repository signing. This receipt grants artifact identity only; Platform tuple qualification remains required.','qualificationCredit':False}
(a.output/'measurement.json').write_text(json.dumps(receipt,indent=2)+'\n')
for name,data in [('exact-source-ci.json',ci),('exact-source-publisher.json',publisher),('release.json',release)]: (a.output/name).write_text(json.dumps(data,indent=2)+'\n')
print(json.dumps({'version':a.version,'source':a.source,'tooling':tooling,'apphost':apphost,'seed':seed,'catalog':catalog_hash,'output':str(a.output)},indent=2))
