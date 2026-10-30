from pathlib import Path
import json,hashlib
b=Path('/tmp/ext-protection-loop5-evidence');a=json.loads((b/'native-runtime-v7-pins.json').read_text());z=json.loads((b/'qualification-runtime-v8-pins.json').read_text())
def files(root,manifest):
 result={}
 for row in json.loads((Path(root)/manifest).read_text())['files']:
  p=Path(root)/row['path'];assert p.is_file() and hashlib.sha256(p.read_bytes()).hexdigest()==row['sha256'];result[row['path']]=row['sha256']
 return result
sa=files(a['source'],'source-manifest.json');sz=files(z['source'],'source-manifest.json');ra=files(a['runtime'],'runtime-manifest.json');rz=files(z['runtime'],'runtime-manifest.json')
def delta(x,y):return [dict(path=k,nativeSha256=x.get(k),qualificationSha256=y.get(k)) for k in sorted(x.keys()|y.keys()) if x.get(k)!=y.get(k)]
sd=delta(sa,sz);rd=delta(ra,rz)
assert {r['path'] for r in sd}=={'platform/src/Hexalith.Platform.Protection/ProtectionQualificationInputSchema.cs','platform/src/Hexalith.Platform.Protection/ProtectionQualificationNativeDriver.cs','platform/tests/Hexalith.Platform.Protection.Tests/ErasureRootLoop5BoundaryTests.cs','platform/tests/Hexalith.Platform.Protection.Tests/ErasureRootLoop5ConfiguredSealTests.cs'}
assert {r['path'] for r in rd}=={'Hexalith.EventStore.Server.dll','Hexalith.EventStore.Server.pdb','Hexalith.Platform.Protection.Tests.dll','Hexalith.Platform.Protection.Tests.pdb','Hexalith.Platform.Protection.dll','Hexalith.Platform.Protection.pdb'}
comparison=json.loads((b/'server-runtime-v7-v8-comparison-complete-types.json').read_text());assert not comparison['differences'] and comparison['firstSemanticSha256']==comparison['secondSemanticSha256']
relevant=[k for k in sa if '/ErasureRoot' in k or '/OpenBao' in k or k.startswith('platform/deploy/erasure-root/') or k=='platform/eng/ext-protection-1/probe_erc_native.py']
# Two qualification-only tests changed; the entire actual native fixture/test dependency source remains equal.
relevant=[k for k in relevant if not k.endswith(('ErasureRootLoop5BoundaryTests.cs','ErasureRootLoop5ConfiguredSealTests.cs'))]
assert all(sa[k]==sz[k] for k in relevant)
proof=dict(nativePins=a,qualificationPins=z,sourceDelta=sd,runtimeDelta=rd,unchangedSourceFiles=len(sa)-len(sd),nativeRelevantSourceFiles=relevant,nativeRelevantSourceFilesByteIdentical=True,wholeRuntimeEqualityClaimed=False,repeatUnchangedNativeGeometry=False,serverComparison='server-runtime-v7-v8-comparison-complete-types.json',explanation=[
 'Qualification NativeDriver preflights known existing source/copy cells, atomic source/producer/completion/writer location distinctness and future owner/read phases. Qualification InputSchema additionally checks future race dependencies, serialized scalar result pointers and full 1000-target installation shape without treating placeholder installation identities as authenticated runtime values. Their actual callers are qualification recipe admission/startup; native five directly exercise custody/store/transition APIs and do not invoke either role/schema path.',
 'Two qualification component test classes add missing-cell, alias and future-reference negatives. All native test/fixture source, custody/index/store/transition code, shipped policy, harness and existing native geometry/assertions are byte-identical. The separate full Protection/host/roles/offline/source/harness checks run against v8.',
 'Protection and test DLL/PDB bytes differ and are separate exact graphs. Method token movement is not full binary equality. Every other runtime dependency except EventStore.Server DLL/PDB is byte-identical. The Server rebuild has identical 2516 complete/nested type and assembly metadata/IL records under the retained comparison; PE layout/MVID/debug/PDB remain distinct.'
])
(b/'native-v7-qualification-v8-equivalence.json').write_text(json.dumps(proof,indent=2)+'\n')
print(json.dumps(dict(sourceDelta=sd,runtimeDelta=rd,nativeRelevantFiles=len(relevant))))
