"""Read only published OCI objects; no Docker daemon, image execution or registry writes."""
import datetime,gzip,hashlib,importlib.util,io,json,re,shutil,subprocess,tarfile
from pathlib import Path,PurePosixPath
EV=Path('/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/evidence/2-8/release-phase/remote-resumption-20261103')
BASE=Path('/home/administrator/.cache/hexalith/story-2-8/memories-325-image-bodies-20261106');BASE.mkdir(exist_ok=False)
proof=json.loads((EV/'parent-memories325-normal-carrier-and-all-ten-public-package-audit-v2-20261106.json').read_bytes());identity=proof['identity'];assert proof['version']=='3.0.25' and proof['source']=='246157a7ff8e2dcd8d356c833a77a02514490d2f'
sha=lambda b:hashlib.sha256(b).hexdigest()
helper=Path('/home/administrator/.cache/hexalith/story-2-8/memories-3023-component-20261104/source/Hexalith.Builds/Github/publish-containers/oci_registry_validator.py');assert sha(helper.read_bytes())==identity['helpers']['Github/publish-containers/oci_registry_validator.py']
spec=importlib.util.spec_from_file_location('verified_oci_reader',helper);mod=importlib.util.module_from_spec(spec);spec.loader.exec_module(mod)
config=json.loads(Path('/home/administrator/.docker/config.json').read_bytes());store=config.get('credHelpers',{}).get('registry.hexalith.com') or config['credsStore'];assert re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9_.-]*',store);credential_helper=shutil.which('docker-credential-'+store);assert credential_helper
lookup=subprocess.run([credential_helper,'get'],input='registry.hexalith.com\n',capture_output=True,text=True,timeout=15);assert lookup.returncode==0
credential=json.loads(lookup.stdout);username=credential['Username'];secret=credential['Secret'];assert username and secret
# The already authenticated operator credential remains only in process memory.
# The verified helper exposes only literal HTTP GET and removes auth on cross-origin redirects.
# No credential is delivered to any CI job or persisted/printed here.
expected=mod.expected_provenance_labels(identity['repository'],identity['sourceSha'],identity['version'],identity['created'])
blobs={};images=[];allfiles=[]
for item in proof['images']:
 repository=item['repository'];assert re.fullmatch(r'memories(?:-[a-z-]+)?',repository);reference='registry.hexalith.com/'+repository+':'+identity['version'];assert reference==item['image']
 graph=mod.validate_registry(reference,username,secret,expected);assert graph['index_digest']==item['indexDigest'];directory=BASE/'oci'/repository;directory.mkdir(parents=True)
 (directory/'index.raw').write_bytes(graph['index_bytes']);children=[];amd64=None
 for child in graph['children']:
  arch=child['platform'].split('/')[1];(directory/('child-'+arch+'.manifest.raw')).write_bytes(child['manifest_bytes']);(directory/('child-'+arch+'.config.raw')).write_bytes(child['config_bytes']);children.append({k:v for k,v in child.items() if k not in ['manifest_bytes','config_bytes']})
  if arch=='amd64':amd64=child
 assert amd64 is not None and {c['platform'] for c in graph['children']}=={'linux/amd64','linux/arm64'}
 manifest=json.loads(amd64['manifest_bytes']);cfg=json.loads(amd64['config_bytes']);assert len(cfg['rootfs']['diff_ids'])==len(manifest['layers']);overlay={};chain=[];client=mod.RegistryClient('registry.hexalith.com',repository,username,secret)
 for layer,diff in zip(manifest['layers'],cfg['rootfs']['diff_ids'],strict=True):
  digest=layer['digest'];assert re.fullmatch(r'sha256:[a-f0-9]{64}',digest)
  if digest not in blobs:
   raw=client.blob(digest)['body_bytes'];assert len(raw)==layer['size'] and 'sha256:'+sha(raw)==digest;target=BASE/'blobs'/digest.split(':')[1];target.parent.mkdir(exist_ok=True);target.write_bytes(raw);uncompressed=gzip.decompress(raw) if raw[:2]==b'\x1f\x8b' else raw;blobs[digest]={'path':str(target),'size':len(raw),'diffId':'sha256:'+sha(uncompressed)}
  record=blobs[digest];assert record['diffId']==diff;chain.append({'compressedDigest':digest,'compressedBytes':record['size'],'actualDiffId':record['diffId']})
  with tarfile.open(record['path'],mode='r:*') as archive:
   for member in archive.getmembers():
    name=member.name.removeprefix('./');pp=PurePosixPath(name);assert not pp.is_absolute() and '..' not in pp.parts
    parent=pp.parent.as_posix();leaf=pp.name
    if leaf=='.wh..wh..opq':overlay={n:v for n,v in overlay.items() if not n.startswith(parent+'/')};continue
    if leaf.startswith('.wh.'):
     removed=(pp.parent/leaf[4:]).as_posix();overlay={n:v for n,v in overlay.items() if n!=removed and not n.startswith(removed+'/')};continue
    selected=parent=='app' and (leaf.startswith('Hexalith.') and leaf.endswith('.dll') or leaf.endswith('.deps.json') or leaf.endswith('.runtimeconfig.json'))
    if selected:
     assert member.isfile() and not member.issym() and not member.islnk() and member.size<32*1024*1024
     body=archive.extractfile(member).read();overlay[name]={'sha256':sha(body),'bytes':len(body),'sourceLayer':digest,'body':body}
 output=BASE/'images'/repository;files=[]
 for name,entry in sorted(overlay.items()):
  target=output/name;target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(entry['body']);row={'image':reference,'path':name,'sha256':entry['sha256'],'bytes':entry['bytes'],'sourceLayer':entry['sourceLayer'],'retained':str(target)};files.append(row);allfiles.append(row)
 if item['name']=='domain-service':
  assert any(x['path']=='app/Hexalith.Memories.EventStoreExtensions.dll' for x in files)
  deps=next(v['body'] for n,v in overlay.items() if n=='app/Hexalith.Memories.DomainService.deps.json');document=json.loads(deps);assert 'Hexalith.Memories.EventStoreExtensions/3.0.25' in document['libraries']
 images.append({'name':item['name'],'repository':repository,'image':reference,'indexDigest':graph['index_digest'],'bothPlatformGraphsVerified':children,'amd64RootfsChain':chain,'amd64ApplicationFiles':files,'config':cfg})
 summary={k:v for k,v in graph.items() if k not in ['index_bytes','children']};summary['children']=children;(directory/'validated-graph.json').write_text(json.dumps(summary,indent=2)+'\n')
 print(json.dumps({'image':reference,'indexDigest':graph['index_digest'],'appFiles':len(files),'bothPlatformGraphs':len(children),'layerCount':len(chain)}),flush=True)
result={'utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'source':identity['sourceSha'],'version':identity['version'],'registry':'registry.hexalith.com','actualExistingOperatorPrincipal':username,'credentialSource':'Existing Docker credential helper, only in process memory; no credential or policy changes and no CI credential delivery.','readOnlyTransport':'Exact approved helper uses only HTTP GET; no image startup, Docker daemon, provider or registry write.','approvedOciHelperSha256':sha(helper.read_bytes()),'images':images,'uniqueAmd64LayerBlobs':blobs,'allSelectedApplicationFiles':allfiles,'domainAdapterArtifactAndDepsPresent':True,'arm64LayerBytesFetched':False,'scope':'Direct genuine public OCI graphs/configs and actual amd64 layer/rootfs/application bytes. SDK/body metadata still inspected separately. No native or Story acceptance.','nativeAcceptance':False,'candidateAcceptance':False};out=EV/'parent-memories325-readonly-all-image-graphs-and-amd64-body-audit-20261106.json';assert not out.exists();out.write_text(json.dumps(result,indent=2)+'\n');print(json.dumps({'output':str(out),'images':len(images),'uniqueLayers':len(blobs),'applicationFiles':len(allfiles),'actualDomainAdapterPresent':True}))
