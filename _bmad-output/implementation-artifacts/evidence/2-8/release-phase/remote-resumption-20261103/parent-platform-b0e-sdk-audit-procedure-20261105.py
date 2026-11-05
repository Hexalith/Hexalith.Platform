import datetime,hashlib,importlib.util,json,subprocess,zipfile
from pathlib import Path
root=Path('/home/administrator/.cache/hexalith/story-2-8/platform-sdk410-migration-20261105/Hexalith.Platform')
base=Path('/home/administrator/.cache/hexalith/story-2-8/platform-b0e-hosted-20261105')
ev=Path(__file__).resolve().parent
source='b0e37b68b50023df50f1b7bbcb8361d3766f6354'
metadata=json.loads((ev/'parent-platform-b0e-sdk-artifact-list-20261105.json').read_bytes())['artifacts']
assert len(metadata)==5
specs=[]
for a in sorted(metadata,key=lambda a:a['name']):
 assert a['workflow_run']['head_sha']==source and a['workflow_run']['id']==42012774383 and not a['expired']
 specs.append((a['name'].removeprefix('catalog-builder-consumer-binding-'),a['id'],a['digest'].removeprefix('sha256:')))

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
 assert r['candidateAcceptance'] is False and r['consumerBindingVerified'] is True
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
  if c.get('activationBindingAccepted'):assert c['policies']==17 and c['sampleRoutes']==7 and c['projectionQueries']==1 and c['domainServiceQueries']==(9 if sdk=='4.1.0' else 8) and c['authorityRegistered'] is False
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
g=json.loads(gate.read_bytes());assert g['status']=='passed' and g['runtimeAcceptance'] is False and g['count']==236 and len(g['cases'])==236
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
owner=base/'catalog-builder-consumer-binding-4.1.0/catalog-public-owner-evidence'
o=json.loads((owner/'result.json').read_bytes())
assert o['status']=='passed' and o['candidateAcceptance'] is False and o['caseCount']==len(o['cases'])==14
assert o['ownerBindingsMatchActualDeclaredDomainAppIds'] is True and o['originalPublicFilesUnchanged'] is True
assert (owner/'verifier-source.py').read_bytes()==git('tests/catalog-builder/verify_public_owner_cohorts.py')
assert (owner/'consumer-helper-source.py').read_bytes()==git('tests/catalog-builder/verify_catalog_builder.py')
files=o['executedArtifacts']['files']
for name,digest in files.items():assert sha((owner/'reader-output'/name).read_bytes())==digest,name
current_source=git('src/Hexalith.Platform.CatalogBuilder/PublishedCatalogArtifactCohort.cs').decode()
import re
allMaps=re.findall(r'new Dictionary<string, string>\(StringComparer.Ordinal\)\s*\{(.*?)\n    \}',current_source,re.S)
expectedNames=['Hexalith.EventStore.Contracts','Hexalith.EventStore.Client','Hexalith.EventStore.DomainService','Hexalith.EventStore.ServiceDefaults','Hexalith.Tenants.Contracts','Hexalith.Tenants.Server','Hexalith.Memories.Contracts','Hexalith.Memories.EventStore','Hexalith.Memories.EventStoreExtensions','Hexalith.Parties.Contracts','Hexalith.Parties.EventStoreExtensions','Hexalith.McpCli.Abstractions']
# Exact current signed public identities are finite entries in the committed source.
for name in expectedNames:assert ('["'+name+'"] = "'+files[name+'.dll']+'"') in current_source,name
ownerCases=[]
for c in o['cases']:
 name=c['name'];request=json.loads((owner/(name+'-request.json')).read_bytes())
 if c['expectedSuccess']:
  assert c['exitCode']==0 and c['loadedExternal']==[]
  d=owner/(name+'-output');catalog=json.loads((d/'catalog.json').read_bytes());receipt=json.loads((d/'receipt.json').read_bytes())
  assert sha((d/'catalog.json').read_bytes())==c['catalogSha256']==c['rootDigest']==receipt['rootDigest']
  assert c['counts']=={key:len(catalog[key]) for key in ['routes','operations','declarations','idempotency']}
  expected={'routes':23,'operations':23,'declarations':23,'idempotency':12} if name=='current-base' else {'routes':70,'operations':70,'declarations':70,'idempotency':46}
  assert c['counts']==expected
  assert c['declarationKinds']=={kind:sum(x['kind']==kind for x in catalog['declarations']) for kind in ['command','query','projection']}
  for loaded in receipt['loadedArtifacts']:
   n=Path(loaded['path']).name;assert files[n]==loaded['sha256']
   if n[:-4] in expectedNames:assert ('["'+n[:-4]+'"] = "'+loaded['sha256']+'"') in current_source,n
  assert not any(Path(x['path']).name in ['Hexalith.EventStore.Server.dll'] or Path(x['path']).name.startswith('ModelContextProtocol') for x in receipt['loadedArtifacts'])
 else:
  assert c['exitCode']!=0 and c['refusedBeforeOutput'] is True
  d=owner/(name+'-output');assert not d.exists() or not list(d.iterdir())
  log=(owner/(name+'.log')).read_text();assert 'exact signed public artifact bytes' in log or 'not qualified' in log
 ownerCases.append(c)
assert sum(c['expectedSuccess'] for c in ownerCases)==2
out={'at' :datetime.datetime.now(datetime.timezone.utc).isoformat(),'source':source,'runId':42012774383,'attempt':1,'sdkArtifacts':reports,'sdkOnlyPublicOwnerCases':ownerCases,'retainedPublicOwnerBodiesVerified':len(files),'gateEvidenceSha256':sha(gate.read_bytes()),'finiteCapabilityCases':236,'finiteCapabilityScope':g['scope'],'declarationsRoutesOperationsIndependentlyCompared':True,'policyFieldsExceptConsumerAndDigestIndependentlyCompared':True,'nativeAcceptance':False,'candidateAcceptance':False,'limitations':['Only retained binary bodies were rehashed; all absent bodies are explicitly manifest-only.','Legacy baseline receipt omits loadedArtifacts; its retained executable bodies were independently hashed against the identity manifest.','Existing sample/Tenants catalog fixture controls; these are not reference native services.','All reports explicitly refuse candidate acceptance; consumer binding checks do not confer runtime acceptance.']}
p=ev/'parent-platform-b0e-sdk-catalog-audit-20261105.json';assert not p.exists();p.write_text(json.dumps(out,indent=2)+'\n')
print(json.dumps({'output':str(p),'sdkArtifacts':[{'sdk':r['sdk'],'cases':len(r['cases']),'retained':r['verifiedRetainedMembers'],'manifestOnly':r['manifestOnlyMembers']} for r in reports],'finiteCapabilityCases':236}))

