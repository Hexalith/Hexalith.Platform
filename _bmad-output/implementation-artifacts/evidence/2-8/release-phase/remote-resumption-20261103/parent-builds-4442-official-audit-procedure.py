import datetime,hashlib,json,os,struct,subprocess,urllib.request,zipfile,xml.etree.ElementTree as ET
from pathlib import Path,PurePosixPath
base=Path('/home/administrator/.cache/hexalith/story-2-8/builds-4442-official-20261104')
ev=Path('_bmad-output/implementation-artifacts/evidence/2-8/release-phase/remote-resumption-20261103')
source='ae68c59bd2f4145a23ccf68c3711f98eaf62195f';version='4.44.2'
sha=lambda b:hashlib.sha256(b).hexdigest()
inventory=base/'g4-tool-package-inventory.json';d=json.loads(inventory.read_bytes())
assert sha(inventory.read_bytes())=='5922e0ada052d0c7c74a875dccb42070672b3d34e8bba2a3fa1e64c5b7c782ff'
assert d['version']==version and d['configuration']=='Release' and len(d['packages'])==2
q=d['qualification'];assert q['releaseEligible'] is True and not q['ineligibilityReasons'] and q['sourceTree']=={'clean':True,'revision':source}
for k in ['packageBuild','sourceValidation','controls']:assert q[k]['result']=='passed'
producer=json.loads((base/'qualification/producer-run.json').read_bytes())
assert producer['sourceRevision']==producer['GITHUB_WORKFLOW_SHA']==source and producer['GITHUB_RUN_ID']=='41798378661' and producer['GITHUB_RUN_ATTEMPT']=='1' and producer['inventorySha256']==sha(inventory.read_bytes())
assert (base/'qualification/g4-tool-package-inventory.json').read_bytes()==inventory.read_bytes()
payloads=[]
for f in d['qualificationEvidence']:
 p=base/'qualification'/f['file'];raw=p.read_bytes()
 assert sha(raw)==f['sha256'].lower() and len(raw)==f['sizeBytes']
 payloads.append({'file':f['file'],'sha256':sha(raw),'bytes':len(raw)})
assert len(payloads)==55
fixtures=[]
for f in q['fixtures']['files']:
 raw=subprocess.check_output(['git','-C','/home/administrator/.cache/hexalith/story-2-8/builds-audit-main-lineage-20261104/Hexalith.Builds','show',source+':test/fixtures/'+f['file']])
 conversion='Git blob bytes'
 if f['file'].endswith('.cs'):
  raw=raw.replace(b'\r\n',b'\n').replace(b'\n',b'\r\n');conversion='Checkout CRLF required by owning .gitattributes *.cs text eol=crlf'
 assert sha(raw)==f['sha256'].lower() and len(raw)==f['sizeBytes']
 fixtures.append({'file':f['file'],'sha256':sha(raw),'conversion':conversion})
assert len(fixtures)==296
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
 env=os.environ.copy();env['NUGET_CERT_REVOCATION_MODE']='offline'
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
out={'at':datetime.datetime.now(datetime.timezone.utc).isoformat(),'source':source,'version':version,'releaseId':433744499,'normalProducer':producer,'inventorySha256':sha(inventory.read_bytes()),'qualificationZipSha256':sha((base/'g4-tool-qualification-evidence.zip').read_bytes()),'qualificationPayloads':payloads,'trackedFixtureHashesVerified':fixtures,'packages':packages,'revocationMode':'offline; no fresh online revocation claim','signedToUnsignedMethod':'Remove signature local and central records, adjust ZIP offsets/counts, preserve other compressed bytes and metadata; compare exact reconstructed archive with official qualified inventory hash.','sourceFixtureCheckoutConversion':'Applied actual owning C#CRLF checkout semantics before comparison; no invented source bytes.','privateCache':str(base),'scope':'Official released package and retained producer qualification identity, not reference native acceptance.','candidateAcceptance':False,'nativeAcceptance':False}
p=ev/'parent-builds-4442-official-audit.json';assert not p.exists();p.write_text(json.dumps(out,indent=2)+'\n')
print(json.dumps({'output':str(p),'packages':[{'id':x['id'],'signatureExit':x['repositorySignatureExitCode'],'version':x['versionExecution']['stdout'].strip(),'members':len(x['memberHashes'])} for x in packages],'qualificationPayloads':len(payloads),'fixtureHashes':len(fixtures)}))

