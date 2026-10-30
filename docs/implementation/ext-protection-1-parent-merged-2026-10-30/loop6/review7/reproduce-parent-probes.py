from pathlib import Path
import importlib.util,copy,json,os,hashlib
root=Path('/home/administrator/projects/hexalith/eventstore');reports=[]
spec=importlib.util.spec_from_file_location('review7_fixture',root/'tools/tests/test_ext_protection_1.py');module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module);v=module.v
fixture=module.CapabilityOriginalEvidenceTests();fixture.setUp()
try:
 observed=copy.deepcopy(fixture.observation);observed['exactResults']=[None,None];fixture.evaluate(observed);reports.append({'claim':'BH5','mutation':'exactResults=[null,null]','accepted':True})
 observed=copy.deepcopy(fixture.observation);observed['reservationRecoveries']=[{'reservationReceiptId':'invented'}]
 v.evaluate(observed,{'steps':[],'checks':['erased','receipts','capability','reserved-recovery']},fixture.targets,b'offline-canary','a'*40,'b'*64,fixture.trust,fixture.roles);reports.append({'claim':'BH6','mutation':'reservationRecoveries=[invented receipt]','accepted':True})
 observed=copy.deepcopy(fixture.observation);observed['replays']=[{'target':t,'kind':'target-event','field':{'State':'Erased'},'externalOutcome':'Unreadable','sourceIdentity':'foreign','sourceDigest':'invented','copyId':'unrelated'} for t in fixture.targets]
 fixture.evaluate(observed);reports.append({'claim':'BH4','mutation':'Foreign replay source/copy identifiers','accepted':True,'limitation':'Focused signed owner/provider fixture has no source-step coverage; source projection and parser were separately traced.'})
 engine=fixture.directory/'engine.dll';engine.write_bytes(b'offline-binary');profile={'schema':'ext-protection-1-profile/v2','targetCommit':'a'*40,'platformTargetCommit':'c'*40,'platformRoot':str(fixture.directory),'roleProgramContract':'ext-protection-1-role-program/v1','planSha256':'b'*64,'tenantReferences':{'primary':'tenant-a','other':'other'},'roles':fixture.roles,'engineBinary':{'path':str(engine),'sha256':v.digest(engine.read_bytes())},'capabilityTrust':fixture.trust};p=fixture.directory/'profile.json';p.write_bytes(v.canonical(profile));record={'profileSha256':v.digest(p.read_bytes()),'TargetVersionOrCommit':'a'*40,'platformTargetCommit':'c'*40}
 with module.patch.dict(v.os.environ,{'OFFLINE_'+name.upper():'same-private-value' for name in v.ROLE_NAMES}):
  v.validate_profile(p,record,'b'*64);reports.append({'claim':'BH2','mutation':'All three different credential env refs contain one identical bearer','accepted':True});reports.append({'claim':'BH3','mutation':'Each role installation manifest lists only its executable','accepted':True})
finally:fixture.doCleanups()
pins=json.loads(Path('/tmp/ext-protection-loop6-evidence/final-freeze-07.json').read_text());assembly=Path(pins['root'])/'Hexalith.Platform.Protection.Tests.dll';os.environ['EXT_PROTECTION_P34_COMPONENT_ASSEMBLY']=str(assembly);os.environ['EXT_PROTECTION_P34_COMPONENT_SHA256']=hashlib.sha256(assembly.read_bytes()).hexdigest()
spec=importlib.util.spec_from_file_location('review7_p34_fixture',root/'tools/tests/test_ext_protection_1_source_p34.py');p34=importlib.util.module_from_spec(spec);spec.loader.exec_module(p34)
try:
 p34.SourcePostActivationEvidenceTests.setUpClass();test=p34.SourcePostActivationEvidenceTests();original=p34.SourcePostActivationEvidenceTests.actual['observation'];test.verify(original);observed=copy.deepcopy(original);del observed['rejectedEffectOriginals'];test.verify(observed);reports.append({'claim':'BH1','mutation':'Remove all rejectedEffectOriginals from actual compiled P34 evidence','baselineAccepted':True,'mutationAccepted':True,'actualReplays':len(original.get('replays',[]))})
finally:p34.SourcePostActivationEvidenceTests.doClassCleanups()
Path('/tmp/ext-protection-review7-parent-probes.json').write_text(json.dumps(reports,indent=2)+'\n');print(json.dumps(reports,indent=2))
