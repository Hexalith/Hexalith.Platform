"""Download immutable normal NuGet delivery and verify its declared owner/source identity."""
from pathlib import Path
import sys,json,urllib.request,urllib.error,hashlib,zipfile,xml.etree.ElementTree as ET,subprocess
owner,version,sha,manifest=sys.argv[1:]
root=Path('/home/administrator/projects/hexalith/platform');evidence=Path(__file__).parent
folder=root/'artifacts/story33-delivered/nuget'/owner.lower()/version;folder.mkdir(parents=True,exist_ok=True)
ids=[x.get('id',x.get('packageId')) for x in json.loads(Path(manifest).read_text())['packages']];rows=[]
for package in ids:
 name=package.lower()+'.'+version+'.nupkg';target=folder/name
 urls=['https://api.nuget.org/v3-flatcontainer/'+package.lower()+'/'+version+'/'+name,'https://www.nuget.org/api/v2/package/'+package+'/'+version]
 attempts=[]
 for url in urls:
  try:
   with urllib.request.urlopen(url,timeout=45) as response:
    payload=response.read();resolved=response.url
   attempts.append({'requested':url,'resolved':resolved,'status':200});break
  except urllib.error.HTTPError as failure: attempts.append({'requested':url,'status':failure.code})
 else: raise RuntimeError('Normal package not yet available: '+package)
 with zipfile.ZipFile(__import__('io').BytesIO(payload)) as archive:
  spec=ET.fromstring(archive.read(next(n for n in archive.namelist() if n.endswith('.nuspec'))));metadata=spec.find('{*}metadata');repo=metadata.find('{*}repository')
  assert metadata.find('{*}id').text==package and metadata.find('{*}version').text==version
  assert repo.get('commit')==sha and repo.get('url').removesuffix('.git')=='https://github.com/Hexalith/Hexalith.'+owner
  assert '.signature.p7s' in archive.namelist()
  assemblies={n:hashlib.sha256(archive.read(n)).hexdigest() for n in archive.namelist() if n.startswith('lib/') and n.endswith('.dll')}
 target.write_bytes(payload);rows.append({'id':package,'version':version,'sourceCommit':sha,'archive':str(target),'sha256':hashlib.sha256(payload).hexdigest(),'repositorySignaturePresent':True,'requests':attempts,'assemblies':assemblies})
 print(package+' '+version+' delivered',flush=True)
(evidence/(owner.lower()+'-delivered-downloads.json')).write_text(json.dumps(rows,indent=2)+'\n')
raise SystemExit(subprocess.run(['dotnet','nuget','verify','--all']+[row['archive'] for row in rows]).returncode)
