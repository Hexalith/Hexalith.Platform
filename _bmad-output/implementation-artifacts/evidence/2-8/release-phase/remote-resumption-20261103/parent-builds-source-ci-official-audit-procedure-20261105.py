import argparse,datetime,hashlib,json,os,struct,subprocess,urllib.request,zipfile,xml.etree.ElementTree as ET
from pathlib import Path,PurePosixPath
parser=argparse.ArgumentParser(description='Audit a genuinely published signed Builds tuple; no publication or native acceptance.')
parser.add_argument('--input',type=Path,required=True)
params=json.loads(parser.parse_args().input.read_bytes())
base=Path(params['cache'])
ev=Path('_bmad-output/implementation-artifacts/evidence/2-8/release-phase/remote-resumption-20261103')
source=params['source'];version=params['version']
sha=lambda b:hashlib.sha256(b).hexdigest()
inventory=base/'g4-tool-package-inventory.json';d=json.loads(inventory.read_bytes())
assert sha(inventory.read_bytes())==params['inventorySha256']
assert d['schema']=='hexalith.g4-tool-package-inventory.v1'
assert d['version']==version and d['configuration']=='Release' and len(d['packages'])==2
q=d['qualification'];assert q['releaseEligible'] is True and not q['ineligibilityReasons'] and q['sourceTree']=={'clean':True,'revision':source}
for k in ['packageBuild','sourceValidation','controls']:assert q[k]['result']=='passed'
# This adapter is deliberately CI-only; the historical executed-mode auditor remains unchanged.
assert q['sourceValidation']['mode']=='ci' and q['controls']['mode']=='executed'
repository='Hexalith/Hexalith.Builds'
branch='prerelease' if '-' in version else 'main'
def official_api(endpoint):
 return json.loads(subprocess.check_output(['gh','api','repos/'+repository+'/'+endpoint]))
proof=q['sourceValidation']['ciRun']
assert set(proof)=={'id','runAttempt','repository','workflow','sha','branch','url'}
assert isinstance(proof['id'],str) and proof['id'].isdigit() and int(proof['id'])>0
assert type(proof['runAttempt']) is int and proof['runAttempt']>0
source_ci=official_api('actions/runs/'+proof['id'])
assert type(source_ci['id']) is int and str(source_ci['id'])==proof['id']
assert source_ci['run_attempt']==proof['runAttempt']
assert source_ci['repository']['full_name']==proof['repository']==repository
assert source_ci['path']==proof['workflow']=='.github/workflows/ci.yml'
assert source_ci['head_sha']==proof['sha']==source
assert source_ci['head_branch']==proof['branch']==branch
assert source_ci['event']=='push' and source_ci['status']=='completed' and source_ci['conclusion']=='success'
assert source_ci['html_url']==proof['url']=='https://github.com/'+repository+'/actions/runs/'+proof['id']
source_ci_jobs=official_api('actions/runs/'+proof['id']+'/attempts/'+str(proof['runAttempt'])+'/jobs?per_page=100')
assert source_ci_jobs['total_count']==len(source_ci_jobs['jobs'])<100
for required_job in ['Package catalog contracts','Build and package contracts','.NET tests','Python contract tests','Build and test']:
 matches=[job for job in source_ci_jobs['jobs'] if job['name']==required_job]
 assert len(matches)==1 and matches[0]['status']=='completed' and matches[0]['conclusion']=='success',required_job
 assert matches[0]['head_sha']==source and matches[0]['run_attempt']==proof['runAttempt']
normal_release=official_api('actions/runs/'+str(params['releaseRun']))
assert normal_release['id']==params['releaseRun'] and normal_release['head_sha']==source
assert normal_release['path']=='.github/workflows/build-release.yml' and normal_release['head_branch']==branch
assert normal_release['event']=='workflow_dispatch' and normal_release['status']=='completed' and normal_release['conclusion']=='success'
assert normal_release['repository']['full_name']==repository
public_release=official_api('releases/'+str(params['releaseId']))
assert public_release['id']==params['releaseId'] and public_release['tag_name']=='v'+version and public_release['draft'] is False
assert public_release['prerelease']==('-' in version)
public_tag=official_api('git/ref/tags/v'+version)
assert public_tag['ref']=='refs/tags/v'+version
assert public_tag['url']=='https://api.github.com/repos/'+repository+'/git/refs/tags/v'+version
resolved_tag=public_tag['object']
annotated_tag=None
if resolved_tag['type']=='tag':
 annotated_tag=official_api('git/tags/'+resolved_tag['sha'])
 assert annotated_tag['tag']=='v'+version
 resolved_tag=annotated_tag['object']
assert resolved_tag['type']=='commit' and resolved_tag['sha']==source
live_public_binding={'sourceCiRun':source_ci,'sourceCiAttemptJobs':source_ci_jobs,'normalReleaseRun':normal_release,'publicRelease':public_release,'publicTag':public_tag,'annotatedTag':annotated_tag}
(base/'authenticated-publication-and-source-ci-api.json').write_text(json.dumps(live_public_binding,indent=2)+'\n')
producer=json.loads((base/'qualification/producer-run.json').read_bytes())
assert producer['sourceRevision']==producer['GITHUB_WORKFLOW_SHA']==source and producer['GITHUB_RUN_ID']==str(params['releaseRun']) and producer['GITHUB_RUN_ATTEMPT']=='1' and producer['inventorySha256']==sha(inventory.read_bytes())
assert (base/'qualification/g4-tool-package-inventory.json').read_bytes()==inventory.read_bytes()
assert producer['GITHUB_REPOSITORY']==repository and producer['GITHUB_JOB']=='release'
assert producer['GITHUB_WORKFLOW_REF']==repository+'/.github/workflows/build-release.yml@refs/heads/'+branch
assert producer['GITHUB_EVENT_NAME']=='workflow_dispatch' and producer['schema']=='hexalith.g4-qualification-carrier.v1'
assert producer['kind']=='release' and producer['version']==version
assert int(producer['GITHUB_RUN_ATTEMPT'])==normal_release['run_attempt']
payloads=[]
for f in d['qualificationEvidence']:
 p=base/'qualification'/f['file'];raw=p.read_bytes()
 assert sha(raw)==f['sha256'].lower() and len(raw)==f['sizeBytes']
 payloads.append({'file':f['file'],'sha256':sha(raw),'bytes':len(raw)})
assert len(payloads)==len(d['qualificationEvidence'])>=55
assert len({x['file'] for x in payloads})==len(payloads)
fixtures=[]
for f in q['fixtures']['files']:
 raw=subprocess.check_output(['git','-C',params['buildsRepository'],'show',source+':test/fixtures/'+f['file']])
 conversion='Git blob bytes'
 if f['file'].endswith('.cs'):
  raw=raw.replace(b'\r\n',b'\n').replace(b'\n',b'\r\n');conversion='Checkout CRLF required by owning .gitattributes *.cs text eol=crlf'
 assert sha(raw)==f['sha256'].lower() and len(raw)==f['sizeBytes']
 fixtures.append({'file':f['file'],'sha256':sha(raw),'conversion':conversion})
assert len(fixtures)==len(q['fixtures']['files'])>=296
def reconstruct_unsigned(data):
 end=data.rfind(b'PK\x05\x06');assert end>=0
 e=bytearray(data[end:]);disk,cd_disk,n_disk,n,sz,off,comment=struct.unpack_from('<4H2IH',e,4)
 assert disk==cd_disk==0 and n_disk==n and off+sz==end and len(e)==22+comment
 with zipfile.ZipFile(__import__('io').BytesIO(data)) as z:
  infos=z.infolist();sig=z.getinfo('.signature.p7s');positions=sorted(i.header_offset for i in infos)
  sigstart=sig.header_offset;sigend=next((x for x in positions if x>sigstart),off)
 removed=sigend-sigstart;records=[];p=off;count=0
 while p<off+sz:
  assert data[p:p+4]==b'PK\x01\x02'
  nl,xl,cl=struct.unpack_from('<3H',data,p+28);size=46+nl+xl+cl
  rec=bytearray(data[p:p+size]);name=data[p+46:p+46+nl]
  if name!=b'.signature.p7s':
   local=struct.unpack_from('<I',rec,42)[0]
   if local>sigstart:struct.pack_into('<I',rec,42,local-removed)
   records.append(rec);count+=1
  p+=size
 assert count==n-1
 central=b''.join(records);struct.pack_into('<4H2I',e,4,0,0,count,count,len(central),off-removed)
 return data[:sigstart]+data[sigend:off]+central+e
packages=[]
for item in d['packages']:
 pid=item['id'];lo=pid.lower();p=base/(lo+'.'+version+'.nupkg')
 if not p.exists():
  with urllib.request.urlopen('https://api.nuget.org/v3-flatcontainer/'+lo+'/'+version+'/'+lo+'.'+version+'.nupkg',timeout=120) as response:p.write_bytes(response.read())
 raw=p.read_bytes();reconstructed=reconstruct_unsigned(raw)
 assert sha(reconstructed)==item['nupkg']['sha256'].lower() and len(reconstructed)==item['nupkg']['sizeBytes']
 env=os.environ.copy();env.pop('NUGET_CERT_REVOCATION_MODE', None)
 c=subprocess.run(['dotnet','nuget','verify','--all',str(p)],env=env,capture_output=True,text=True)
 log=base/(lo+'-repository-signature.txt');log.write_text(c.stdout+c.stderr);assert c.returncode==0
 memberHashes={}
 with zipfile.ZipFile(p) as z:
  nuspec=ET.fromstring(z.read(pid+'.nuspec'));ns={'n':nuspec.tag.split('}')[0].removeprefix('{')};metadata=nuspec.find('n:metadata',ns)
  assert metadata.findtext('n:version',namespaces=ns)==version
  repository=metadata.find('n:repository',ns);assert repository.attrib['commit']==source
  for i in z.infolist():
   pp=PurePosixPath(i.filename);assert not pp.is_absolute() and '..' not in pp.parts
   if not i.is_dir():memberHashes[i.filename]=sha(z.read(i))
  target=base/(lo+'-extracted');z.extractall(target)
  dll=target/'tools/net10.0/any'/(pid+'.dll')
 cver=subprocess.run(['dotnet',str(dll),'--version'],capture_output=True,text=True)
 assert cver.returncode==0 and cver.stdout.strip()==version+'+'+source
 if pid=='Hexalith.Builds.Module.Cli':assert 'tools/net10.0/any/Hexalith.Builds.Tooling.dll' in memberHashes
 packages.append({'id':pid,'signedArchiveSha256':sha(raw),'qualifiedUnsignedArchiveSha256':sha(reconstructed),'qualifiedUnsignedArchiveBytes':len(reconstructed),'repository':repository.attrib,'repositorySignatureExitCode':c.returncode,'repositorySignatureLogSha256':sha(log.read_bytes()),'versionExecution':{'exitCode':cver.returncode,'stdout':cver.stdout,'stderr':cver.stderr},'memberHashes':memberHashes})
out={'at':datetime.datetime.now(datetime.timezone.utc).isoformat(),'source':source,'version':version,'releaseId':params['releaseId'],'normalProducer':producer,'authenticatedSourceCiPublication':live_public_binding,'inventorySha256':sha(inventory.read_bytes()),'qualificationZipSha256':sha((base/'g4-tool-qualification-evidence.zip').read_bytes()),'qualificationPayloads':payloads,'trackedFixtureHashesVerified':fixtures,'packages':packages,'revocationMode':'normal NuGet verification; no task-specific revocation override','signedToUnsignedMethod':'Remove signature local and central records, adjust ZIP offsets/counts, preserve other compressed bytes and metadata; compare exact reconstructed archive with official qualified inventory hash.','sourceFixtureCheckoutConversion':'Applied actual owning C#CRLF checkout semantics before comparison; no invented source bytes.','privateCache':str(base),'scope':'Official released package and retained producer qualification identity, not reference native acceptance.','candidateAcceptance':False,'nativeAcceptance':False}
p=ev/params['outputName'];assert not p.exists();p.write_text(json.dumps(out,indent=2)+'\n')
print(json.dumps({'output':str(p),'packages':[{'id':x['id'],'signatureExit':x['repositorySignatureExitCode'],'version':x['versionExecution']['stdout'].strip(),'members':len(x['memberHashes'])} for x in packages],'qualificationPayloads':len(payloads),'fixtureHashes':len(fixtures)}))

