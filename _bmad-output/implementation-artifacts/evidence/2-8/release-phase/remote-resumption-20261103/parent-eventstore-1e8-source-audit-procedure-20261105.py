import datetime,hashlib,json,subprocess,tempfile,xml.etree.ElementTree as ET,zipfile
from pathlib import Path
root=Path('/home/administrator/.cache/hexalith/story-2-8/eventstore-newer-tuple-20261105/Hexalith.EventStore')
evidence=root.parent/'adoption-evidence'
ev=Path(__file__).resolve().parent
source='1e8fd617b39db0c839a68031968f7639f9206518';base='2dca77cc2f22a9b85e4fcc3f8e18cbcaa532b135'
sha=lambda b:hashlib.sha256(b).hexdigest()
git=lambda *args:subprocess.check_output(['git','-C',str(root),*args])
assert git('rev-parse','HEAD').decode().strip()==source and not git('status','--porcelain').strip()
assert git('rev-parse',source+'^').decode().strip()==base
paths=git('diff','--name-only',base,source).decode().splitlines()
expected=['.config/dotnet-tools.json','references/Hexalith.Builds','references/Hexalith.Platform','tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs']
assert paths==expected
for p in [expected[0],expected[3]]:
 before=git('show',base+':'+p);after=git('show',source+':'+p)
 assert before.count(b'4.44.0')==1 and after==before.replace(b'4.44.0',b'4.44.3')
 assert (root/p).read_bytes()==after
assert git('rev-parse',source+':references/Hexalith.Builds').decode().strip()=='fc2122240a39f03628c04fa8433e5eece90b8b4c'
assert git('rev-parse',source+':references/Hexalith.Platform').decode().strip()=='ae7516ee6961dd31a5e319be59ead76449ab04a0'
message=(git('show','-s','--format=%B',source)).decode().strip()
validation=json.loads((evidence/'adoption-commitlint-command.json').read_bytes())
assert validation['exitCode']==0 and validation['owningLockedVersion']=='21.2.4' and message==validation['exactFullMessage'].strip()
assert message==(evidence/'adoption-commit-message.txt').read_text().strip()
raw=git('diff','--no-ext-diff','--binary','--submodule=short',base,source)
patch=Path(tempfile.mkstemp(prefix='story2-8-eventstore-1e8-reviewed-',suffix='.patch')[1]);patch.write_bytes(raw)
record=json.loads((evidence/'focused-caller-contracts-first.json').read_bytes())
xml=ET.parse(evidence/'focused-caller-contracts-first.xml').getroot();a=xml.find('assembly');tests=xml.findall('.//test')
assert a.attrib['passed']==a.attrib['total']=='13' and len(tests)==13 and len({t.attrib['name'] for t in tests})==13
assert all(a.attrib[k]=='0' for k in ['failed','errors','skipped','not-run']) and all(t.attrib['result']=='Pass' for t in tests)
assert record['source']==source and record['exitCode']==0 and record['counts']==a.attrib
assert record['allOutputDllsUnchanged'] is True and record['beforeDllHashes']==record['afterDllHashes']
output=root/'tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0'
for name,digest in record['beforeDllHashes'].items():assert sha((output/name).read_bytes())==digest,name
assert all(v is None for v in record['actualCiContext'].values())
build=json.loads((evidence/'contracts-committed-build-command.json').read_bytes());assert build['exitCode']==0
log=(evidence/'contracts-committed-build.log').read_text();assert 'Build succeeded.' in log and '0 Warning(s)' in log and '0 Error(s)' in log
prepared=json.loads((evidence/'normal-shared-preparation-first.json').read_bytes())
assert prepared['passed'] is True and len(prepared['commands'])==6 and all(c['exitCode']==0 for c in prepared['commands'])
assert all(v is None for v in prepared['actualCiContext'].values())
script=root/'references/Hexalith.Builds/Github/prepare-module-build/prepare_module_build.py'
assert sha(script.read_bytes())==prepared['scriptSha256']
assert script.read_bytes()==subprocess.check_output(['git','-C',str(root/'references/Hexalith.Builds'),'show','fc2122240a39f03628c04fa8433e5eece90b8b4c:Github/prepare-module-build/prepare_module_build.py'])
assert any(c['command'][-1]=='--version' and '4.44.3' in c['stdout'] for c in prepared['commands'])
p=prepared['actualProducerResult'];assert p['status']=='prepared' and p['outcome']['exitCode']=='Success' and not p['diagnostics']
w=Path(p['buildPreparation']['workspace']);mapping=json.loads((w/'source-mapping/mapping.json').read_bytes());versions=json.loads((w/'source-mapping/catalog-versions.json').read_bytes())
assert mapping['Mode']=='package' and mapping['HasGit'] is True and mapping['ActiveModule']=='Hexalith.EventStore' and mapping['IdentityFindings']==[]
identity=mapping['PlatformIdentity'];assert identity['Commit']==identity['CompositionCommit']==identity['CheckoutCommit']=='ae7516ee6961dd31a5e319be59ead76449ab04a0'
assert identity['ReleaseTag']=='v1.4.0' and identity['CatalogCommit']=='fc2122240a39f03628c04fa8433e5eece90b8b4c' and identity['ToolVersionRange']=='[4.44.3, 5.0.0-0)'
assert mapping['ContentHash']==versions['MappingHash']==p['buildPreparation']['mappingHash']
rawCatalog=subprocess.check_output(['git','-C',str(root/'references/Hexalith.Builds'),'show','fc2122240a39f03628c04fa8433e5eece90b8b4c:Props/Directory.Packages.props'])
material=(w/'source-mapping/pinned-tree/Props/Directory.Packages.props').read_bytes()
assert sha(rawCatalog)=='6f59b25451553af6ff7c5a1bef7833b1ea5e92f65d7daa9d460be4c57834f145'
assert sha(material)==versions['CatalogFileHash'] and rawCatalog.decode('utf-8-sig').splitlines()==material.decode('utf-8-sig').splitlines()
public=json.loads((evidence/'actual-restored-tool-byte-bindings.json').read_bytes());archive=Path(public['actualSignedArchive'])
assert public['archiveSha256']==sha(archive.read_bytes())=='32c2f517791bfce975bf775e73bbbf626cc710da83c68b89c5e305ef55acea32'
assert public['signatureVerification']['exitCode']==0 and public['signatureVerification']['certificateRevocationMode']=='offline'
toolRoot=Path(public['actualResolverEntry']['PathToExecutable']).parent
with zipfile.ZipFile(archive) as z:
 assert public['toolPayloadCount']==len(public['payloads'])==310
 for entry in public['payloads']:
  name=entry['path'];body=z.read(name);assert sha(body)==entry['sha256'];assert (toolRoot/Path(name).relative_to('tools/net10.0/any')).read_bytes()==body
binding=json.loads((ev/'parent-builds-4443-qualified-tuple-binding-20261105.json').read_bytes())
assert sha((toolRoot/'Hexalith.Builds.Tooling.dll').read_bytes())==binding['binding']['publication']['toolingSha256']
assert sha((toolRoot/'Hexalith.Builds.Module.Cli.dll').read_bytes())==binding['moduleCliSha256']
evaluations=json.loads((evidence/'actual-prepared-environment-consumer-observations.json').read_bytes());assert len(evaluations['evaluations'])==2
for item,filename in zip(evaluations['evaluations'],['owned-contracts-prepared-environment.stdout','declared-native-project-prepared-environment.stdout']):
 assert item['exitCode']==0;props=json.loads((evidence/filename).read_text())['Properties'];assert props==item['actualProperties']
 for key,value in {'Configuration':'Release','UseHexalithProjectReferences':'false','UseNuGetDeps':'true','HexalithEventStoreVersion':'4.1.0','HexalithTenantsVersion':'7.0.2','HexalithMemoriesVersion':'3.0.23','HexalithPartiesVersion':'3.0.0','HexalithSourceMappingMode':'package','HexalithSourceMappingHash':mapping['ContentHash']}.items():assert props[key]==value
out={'at':datetime.datetime.now(datetime.timezone.utc).isoformat(),'source':source,'base':base,'paths':paths,'reviewedCompletePatch':str(patch),'patchBytes':len(raw),'patchSha256':sha(raw),'actualTests':[{k:t.attrib[k] for k in ['name','method','result']} for t in tests],'actualPassed':13,'outputDllBodiesIndependentlyRehashed':len(record['beforeDllHashes']),'outputBeforeAfterUnchanged':True,'ordinaryResourceFreePreparationPassed':True,'actualPreparationCommands':len(prepared['commands']),'actualCiIdentityAbsent':True,'platformIdentity':identity,'mappingHash':mapping['ContentHash'],'genuineSignedToolPayloadsComparedToActualResolvedBodies':310,'rawCatalogSha256':sha(rawCatalog),'normalPreparedCatalogSha256':sha(material),'catalogSelectionsUnchangedAcrossNormalBomAndLineEndingMaterialization':True,'actualMsbuildPreparedConsumers':evaluations['evaluations'],'nativeAcceptance':False,'candidateAcceptance':False,'limits':['Local resource-free and component evidence only; all hosted isolated/native/independent jobs remain required.','Signed archive is the already authenticated genuine release; offline repository signature result does not establish online revocation status.']}
p=ev/'parent-eventstore-1e8-source-and-control-audit-20261105.json';assert not p.exists();p.write_text(json.dumps(out,indent=2)+'\n');print(json.dumps({'output':str(p),'paths':len(paths),'passed':13,'actualBodies':len(record['beforeDllHashes']),'toolPayloads':310,'mapping':mapping['ContentHash']}))
