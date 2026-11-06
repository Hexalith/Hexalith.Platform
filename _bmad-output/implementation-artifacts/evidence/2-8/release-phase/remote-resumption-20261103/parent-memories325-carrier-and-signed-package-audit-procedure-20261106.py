import ast,base64,datetime,hashlib,io,json,os,subprocess,tarfile,urllib.request,xml.etree.ElementTree as ET,zipfile
from pathlib import Path,PurePosixPath
EV=Path('/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/evidence/2-8/release-phase/remote-resumption-20261103')
BASE=Path('/home/administrator/.cache/hexalith/story-2-8/memories-325-public-20261106')
BASE.mkdir(exist_ok=False)
SOURCE='246157a7ff8e2dcd8d356c833a77a02514490d2f';VERSION='3.0.25';REPO='Hexalith/Hexalith.Memories'
B=Path('/home/administrator/projects/hexalith/platform/references/Hexalith.Builds')
M=Path('/home/administrator/.cache/hexalith/story-2-8/memories-domain-coverage-docs-20261105/Hexalith.Memories')
sha=lambda b:hashlib.sha256(b).hexdigest()
proof=json.loads((EV/'parent-memories-corrected-real-publication-identity-20261106.json').read_bytes())
assert proof['source']==SOURCE and proof['version']==VERSION and proof['normalPublisher']['conclusion']=='success'
release=proof['publicRelease'];assert release['id']==435930782

def asset(name):
 a=next(x for x in release['assets'] if x['name']==name)
 raw=subprocess.check_output(['gh','api','-H','Accept: application/octet-stream',f'repos/{REPO}/releases/assets/{a["id"]}'])
 assert len(raw)==a['size'] and 'sha256:'+sha(raw)==a['digest']
 (BASE/name).write_bytes(raw)
 return a,raw

carrier,raw=asset('hexalith-memories-release-evidence.tar.gz')
files={};inventory=[]
with tarfile.open(fileobj=io.BytesIO(raw),mode='r:gz') as archive:
 for entry in archive.getmembers():
  pp=PurePosixPath(entry.name);assert not pp.is_absolute() and '..' not in pp.parts and not entry.issym() and not entry.islnk()
  if not entry.isfile():continue
  assert entry.name not in files
  data=archive.extractfile(entry).read();files[entry.name]=data
  target=BASE/'carrier'/entry.name;target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(data)
  inventory.append({'name':entry.name,'bytes':len(data),'sha256':sha(data)})
manifest=json.loads(files['evidence-manifest.json']);assert manifest['schemaVersion']=='hexalith.memories.release-evidence.v1' and manifest['result']=='pass' and manifest['identityReadable'] is True and manifest['sourceSha']==SOURCE and manifest['version']==VERSION
assert {x['path'] for x in manifest['files']}==set(files)-{'evidence-manifest.json'}
for row in manifest['files']:
 body=files[row['path']];assert len(body)==row['size'] and sha(body)==row['sha256']
identity=json.loads(files['artifacts/release/container-evidence/identity.json'])
assert identity['sourceSha']==identity['workflowSha']==SOURCE and identity['version']==VERSION
assert identity['repository']==REPO and identity['workflow']==REPO+'/.github/workflows/release.yml@refs/heads/main'
assert identity['run']['GITHUB_RUN_ID']=='42134622308' and identity['run']['GITHUB_RUN_ATTEMPT']=='1'
assert identity['buildsSha']=='81c88b34d09d34260b92d7f0928dbbc1c7446baa'
actual_set=sha(json.dumps({k:v for k,v in identity.items() if k!='setSha256'},sort_keys=True,separators=(',',':')).encode())
assert identity['setSha256']==manifest['setSha256']==actual_set
assert identity['manifestSha256']==sha(files['tools/release-containers.json'])
helpers=[]
for name,expected in identity['helpers'].items():
 body=subprocess.check_output(['git','-C',str(B),'show',identity['buildsSha']+':'+name]);assert sha(body)==expected
 helpers.append({'path':name,'sha256':expected})
assert len(helpers)==7
pub=json.loads(files['artifacts/release/publish-summary.json']);assert pub['status']=='succeeded' and pub['sourceSha']==SOURCE and pub['setSha256']==actual_set and pub['version']==VERSION and not pub['failed'] and not pub['notAttempted']
assert len(pub['families'])==2 and all(x['status']=='succeeded' and x['exitCode']==0 for x in pub['families'])
pre=json.loads(files['artifacts/release/container-evidence/preflight/publication-identity.json']);assert pre['source_sha']==SOURCE and pre['source']['live_sha']==SOURCE and pre['source']['ci_run']['id']==42132182708 and pre['source']['ci_run']['conclusion']=='success';assert pre['run']['id']=='42134622308' and pre['run']['workflow_sha']==SOURCE
images=[]
for image in identity['images']:
 name=image['name'];validation=json.loads(files[f'artifacts/release/container-evidence/{name}/oci-validation.json']);health=json.loads(files[f'artifacts/release/container-evidence/{name}/health.json']);assert validation['result']=='pass' and health['result']=='pass';assert health['setSha256']==actual_set and health['indexDigest']==validation['index_digest'];assert set(identity['platforms'])=={'linux/amd64','linux/arm64'}
 images.append({'name':name,'repository':image['repository'],'image':validation['image'],'indexDigest':validation['index_digest'],'validation':validation,'health':health})
assert len(images)==5
prior=ast.parse((EV/'parent-builds-source-ci-official-audit-procedure-20261105.py').read_text());func=next(n for n in prior.body if isinstance(n,ast.FunctionDef) and n.name=='reconstruct_unsigned');import struct
exec(compile(ast.Module(body=[func],type_ignores=[]),'<retained-exact-zip-reconstruction>','exec'))
packages=[]
for row in json.loads(files['tools/release-packages.json'])['packages']:
 pid=row['id'];name=pid+'.'+VERSION+'.nupkg';official,unsigned=asset(name);path=BASE/(pid.lower()+'.'+VERSION+'.nupkg')
 url='https://api.nuget.org/v3-flatcontainer/'+pid.lower()+'/'+VERSION+'/'+pid.lower()+'.'+VERSION+'.nupkg'
 with urllib.request.urlopen(url,timeout=120) as response:signed=response.read()
 path.write_bytes(signed);assert reconstruct_unsigned(signed)==unsigned
 env=os.environ.copy();env.pop('NUGET_CERT_REVOCATION_MODE',None);env.update({'DOTNET_CLI_HOME':str(BASE/'dotnet-home'),'DOTNET_GENERATE_ASPNET_CERTIFICATE':'false','DOTNET_CLI_TELEMETRY_OPTOUT':'1','DOTNET_SKIP_FIRST_TIME_EXPERIENCE':'1'})
 verify=subprocess.run(['dotnet','nuget','verify','--all',str(path)],env=env,capture_output=True,text=True);log=BASE/(pid+'-normal-signature.txt');log.write_text(verify.stdout+verify.stderr);assert verify.returncode==0,pid
 members={}
 with zipfile.ZipFile(path) as archive:
  root=ET.fromstring(archive.read(pid+'.nuspec'));ns={'n':root.tag.split('}')[0].removeprefix('{')};metadata=root.find('n:metadata',ns);assert metadata.findtext('n:id',namespaces=ns)==pid and metadata.findtext('n:version',namespaces=ns)==VERSION;repository=metadata.find('n:repository',ns);assert repository.attrib['commit']==SOURCE
  for info in archive.infolist():
   pp=PurePosixPath(info.filename);assert not pp.is_absolute() and '..' not in pp.parts
   if not info.is_dir():members[info.filename]=sha(archive.read(info))
 packages.append({'id':pid,'url':url,'signedArchiveSha256':sha(signed),'officialUnsignedAsset':official,'unsignedArchiveSha256':sha(unsigned),'exactSignedToUnsignedReconstruction':True,'normalSignatureExit':verify.returncode,'normalSignatureLogSha256':sha(log.read_bytes()),'repository':repository.attrib,'members':members})
assert len(packages)==10
result={'utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'version':VERSION,'source':SOURCE,'releaseId':release['id'],'normalPublisherRun':42134622308,'exactSourceCi':42132182708,'carrierAsset':carrier,'carrierSha256':sha(raw),'allCarrierMembers':inventory,'everyManifestPayloadAuthenticated':len(manifest['files']),'identity':identity,'setHashIndependentlyRecomputed':actual_set,'allSevenApprovedHelperBodiesVerified':helpers,'preflightSourceIdentity':pre,'completePublicationSummary':pub,'images':images,'allTenPublicSignedPackages':packages,'signatureRevocationMode':'Normal dotnet nuget verify --all, no task-specific revocation override','scope':'Actual normal corrected public producer/carrier and signed-package authentication. Registry/application/SDK image bytes still need direct read-only measurement before final consumer binding; no native or Story acceptance.','nativeAcceptance':False,'candidateAcceptance':False}
out=EV/'parent-memories325-normal-carrier-and-all-ten-public-package-audit-20261106.json';assert not out.exists();out.write_text(json.dumps(result,indent=2)+'\n');print(json.dumps({'output':str(out),'version':VERSION,'source':SOURCE,'carrierMembers':len(inventory),'manifestPayloads':len(manifest['files']),'normalPackageSignatures':len(packages),'images':[{'name':x['name'],'digest':x['indexDigest']} for x in images]}))
