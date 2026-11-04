import datetime,hashlib,importlib.util,json,subprocess,zipfile
from pathlib import Path
root=Path('/home/administrator/.cache/hexalith/story-2-8/platform-full-parties-20261104/Hexalith.Platform')
base=Path('/home/administrator/.cache/hexalith/story-2-8/platform-338a-hosted-20261104')
ev=Path(__file__).resolve().parent
source='338a6afc31850761f49c0de7d399266dd15cfe84'
specs=[
('3.130.0',13315530827,'ccfcf8832b4410dbd34cb90c266b2498db5bf66624f45f4957451fe9edc4dc04'),
('3.130.1',13315231380,'9614884c2674015e8c371f8add2377c7f00b479f9422b8d0ec7c8f5c1d2f18ba'),
('3.130.2',13315261785,'3081174527315321d3bebbf776f101b7fc92e9ff351b91a7b2537b1106e217f7'),
('3.132.1',13315122723,'d072f94bcdc0a20c785566699342b0ad1f365065d2e598f2b73488ecb190c2c3')]
sha=lambda b:hashlib.sha256(b).hexdigest()
git=lambda p:subprocess.check_output(['git','-C',str(root),'show',source+':'+p])
closure={n:sha(git('src/Hexalith.Platform.CatalogBuilder/'+n)) for n in ['Hexalith.Platform.CatalogBuilder.csproj','Program.cs','PublishedCatalogArtifactCohort.cs']}
reports=[]
for sdk,aid,digest in specs:
 archive=base/('catalog-builder-consumer-binding-'+sdk+'.zip');assert sha(archive.read_bytes())==digest
 d=base/('catalog-builder-consumer-binding-'+sdk)/'catalog-builder-evidence'
 r=json.loads((d/'result.json').read_bytes())
 assert r['sdk']==sdk and r['status']=='passed' and len(r['cases'])==15
 assert r['toolSourceSha256']==closure['Program.cs'] and r['toolSourceClosureSha256']==closure
 assert r['allDeclarationsBindingsAndOperationsUnchanged'] is True and r['policyFieldsExceptConsumerAndDigestUnchanged'] is True
 assert r['authorityRegistered'] is False and r['originalProducerWrites'] is False
 for c in r['cases']: assert any(c.get(k) is True for k in ['activationRefused','activationBindingAccepted','refusedBeforeOutput'])
 before=json.loads((d/'baseline-0-catalog.json').read_bytes())
 after=json.loads((d/'actual-1-catalog.json').read_bytes())
 for k in ['schemaVersion','generation','profile','allowRuntimeOverrides','declarations','routes','operations','retainedIdempotency','routeFacetDigest']:assert before[k]==after[k],k
 strip=lambda p:{k:v for k,v in p.items() if k not in ['consumerAppId','contentDigest']}
 assert list(map(strip,before['idempotency']))==list(map(strip,after['idempotency']))
 ids=[];retained=0;manifestOnly=0
 for p in sorted((d/'executed').glob('*/identity.json')):
  ident=json.loads(p.read_bytes());matched={};missing={}
  for n,h in ident['files'].items():
   f=p.parent/n
   if f.exists():assert sha(f.read_bytes())==h;matched[n]=h
   else:missing[n]=h
  if p.parent.name in ['actual','baseline','default-consumer','foreign-consumer','legacy129']:assert 'Hexalith.Platform.CatalogBuilder.dll' in matched
  retained+=len(matched);manifestOnly+=len(missing)
  ids.append({'role':p.parent.name,'verifiedRetainedMembers':matched,'manifestOnlyMembers':missing})
 for c in r['cases'][:5]:
  name=c['case'];rec=json.loads((d/(name+'-receipt.json')).read_bytes())
  ident=json.loads((d/'executed'/name.rsplit('-',1)[0]/'identity.json').read_bytes())
  for loaded in rec.get('loadedArtifacts',[]):
   n=Path(loaded['path']).name;assert loaded['sha256']==ident['files'][n]
  catalog=json.loads((d/(name+'-catalog.json')).read_bytes())
  if c.get('activationBindingAccepted'):assert c['policies']==17 and c['sampleRoutes']==7 and c['projectionQueries']==1 and c['domainServiceQueries']==8 and c['authorityRegistered'] is False
 sample=r['executedArtifacts']['sample']
 retainedSources={sha(p.read_bytes()) for p in (d/'genuine-sample-source').glob('*.cs')}
 declaredSources={v['sha256'] for v in sample['sourceFiles'].values()}
 assert retainedSources==declaredSources
 for role in ['actual','baseline','default-consumer','foreign-consumer']:
  assert r['executedArtifacts'][role]['resolvedSdkPackages']['Hexalith.EventStore.Contracts']==sdk
  assert r['executedArtifacts'][role]['files']['Hexalith.EventStore.Contracts.dll']==sample['publicContractsDllSha256']
 row={'sdk':sdk,'artifactId':aid,'archiveSha256':digest,'cases':r['cases'],'toolSourceClosureSha256':closure,'verifiedRetainedMembers':retained,'manifestOnlyMembers':manifestOnly,'identities':ids,'retainedSampleSourcesVerified':len(retainedSources),'artifactCandidateAcceptanceFieldObserved':r['candidateAcceptance']}
 reports.append(row)
gate=base/'catalog-builder-consumer-binding-3.132.1/catalog-sdk-gates.json'
g=json.loads(gate.read_bytes());assert g['status']=='passed' and g['runtimeAcceptance'] is False and g['count']==208 and len(g['cases'])==208
scriptPath=root/'tests/catalog-builder/verify_sdk_gates.py'
assert scriptPath.read_bytes()==git('tests/catalog-builder/verify_sdk_gates.py')
moduleSpec=importlib.util.spec_from_file_location('sdk_gate_expectations',scriptPath);m=importlib.util.module_from_spec(moduleSpec);moduleSpec.loader.exec_module(m)
byPath={v:k for k,v in m.PROJECTS.items()}
for c in g['cases']:
 assert c['status']=='passed' and c['exitCode']==0 and c['actualVersion']==c['expectedVersion']
 expected=m.expected_flags(byPath[c['project']],c['expectedVersion'],c['source'])
 props=json.loads(c['stdout'])['Properties']
 assert set(c['expected'])==set(c['actual'])==expected==m.FLAGS.intersection(props['DefineConstants'].split(';'))
 assert props['HexalithEventStoreVersion']==c['expectedVersion']
out={'at':datetime.datetime.now(datetime.timezone.utc).isoformat(),'source':source,'runId':41816978023,'attempt':1,'sdkArtifacts':reports,'gateEvidenceSha256':sha(gate.read_bytes()),'finiteCapabilityCases':208,'finiteCapabilityScope':g['scope'],'declarationsRoutesOperationsIndependentlyCompared':True,'policyFieldsExceptConsumerAndDigestIndependentlyCompared':True,'nativeAcceptance':False,'candidateAcceptance':False,'limitations':['Only retained binary bodies were rehashed; all absent bodies are explicitly manifest-only.','Legacy baseline receipt omits loadedArtifacts; its retained executable bodies were independently hashed against the identity manifest.','Existing sample/Tenants catalog fixture controls; these are not reference native services.','Producer component candidateAcceptance field is retained but does not confer Story 2.8 candidate acceptance.']}
p=ev/'parent-platform-338a-sdk-catalog-audit.json';assert not p.exists();p.write_text(json.dumps(out,indent=2)+'\n')
print(json.dumps({'output':str(p),'sdkArtifacts':[{'sdk':r['sdk'],'cases':len(r['cases']),'retained':r['verifiedRetainedMembers'],'manifestOnly':r['manifestOnlyMembers']} for r in reports],'finiteCapabilityCases':208}))

