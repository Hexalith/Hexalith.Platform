from pathlib import Path
import sys,json,subprocess,datetime,xml.etree.ElementTree as ET,os,hashlib
from types import SimpleNamespace
p=Path('/tmp/story-3-2-broad-20261109-_z_wy84q/current');out=Path('/tmp/story32-linear-tuple-freeze-a3393a84/actual-L');root=out/'repository';evidence=out/'evidence-attempt2';evidence.mkdir(exist_ok=False);a=SimpleNamespace(label='L');required=349;source='87bf914eb2e990023d46dff1d14fdbf62f42320e';current='a88ae34328af405237a5ac51a3bbbaf5f58a493d';sys.path.insert(0,str(root/'eng'))
from prepare_builds_artifacts import source_state
from run_tuple_ci import validate_profile,verify_historical_catalog_pin,snapshot_composition_test_outputs,verify_test_sdk
from legacy_tuple_source import verify
comparison=verify(root,current,source);(evidence/'actual-source-comparison.json').write_text(json.dumps(comparison,indent=2)+'\n');verify_historical_catalog_pin(root/'references/Hexalith.Builds/Props/Directory.Packages.props')
previous=json.loads((out/'evidence/receipt.json').read_text());build=previous['buildCommand'];metadata=previous['metadataCommand'];artifacts=root/'artifacts/replay-L';env=os.environ.copy();env['NUGET_PACKAGES']='/tmp/story32-sdk42-upstream-catalog-package-cache'
assembly=artifacts/'bin/Hexalith.Platform.Composition.Tests/release/Hexalith.Platform.Composition.Tests.dll';profile=json.loads((root/'eng/tuple-ci-suites.json').read_bytes());assert validate_profile(profile)==required;verify_test_sdk(assembly,'4.1.0');state=source_state();assert not state['dirty'],state['status'];outputs=snapshot_composition_test_outputs(assembly)
r={'scope':'Actual committed same-code historical checkout, signed published owning tool, actual SDK4.1 package inventory; local controls only, no hosted/defaultSDK4.2/signed-fixture acceptance credit.','label':a.label,'source':source,'currentSource':current,'required':required,'sourceComparison':comparison,'buildCommand':build,'metadataCommand':metadata,'sourceBefore':state,'outputBefore':outputs,'groups':[],'status':'running','candidateAcceptance':False,'hostedQualification':False,'signedFixtureCredit':False}
def save():(evidence/'receipt.json').write_text(json.dumps(r,indent=2)+'\n')
save();failed=False
try:
 for i,g in enumerate(profile['groups']):
  xml=evidence/('group-'+str(i)+'.xml');argv=['dotnet',str(assembly)]+[arg for name in g['selectors'] for arg in [g['filter'],name]]+['-result-xml',str(xml)]
  with (evidence/('group-'+str(i)+'.stdout')).open('w') as stdout,(evidence/('group-'+str(i)+'.stderr')).open('w') as stderr:c=subprocess.run(argv,cwd=root,env=env,stdout=stdout,stderr=stderr,timeout=1200)
  counts=ET.parse(xml).getroot().find('assembly').attrib if xml.exists() else {};okay=c.returncode==0 and int(counts.get('total',0))==g['requiredCount'] and int(counts.get('passed',0))==g['requiredCount'] and all(int(counts.get(k,0))==0 for k in ['failed','errors','skipped','not-run']);failed|=not okay;r['groups'].append({'index':i,'purpose':g.get('purpose'),'command':argv,'expected':g['requiredCount'],'counts':counts,'exitCode':c.returncode,'passed':okay});save();print(a.label,'group',i,'actual',counts,'passed',okay,flush=True)
except BaseException as error:
 r['error']=repr(error);failed=True;raise
finally:
 r['sourceAfter']=source_state();r['outputAfter']=snapshot_composition_test_outputs(assembly);r['sourceUnchanged']=r['sourceBefore']==r['sourceAfter'];r['outputsUnchanged']=r['outputBefore']==r['outputAfter'];r['actualPass']=sum(int(x['counts'].get('passed',0)) for x in r['groups']);r['actualFail']=sum(int(x['counts'].get('failed',0)) for x in r['groups']);r['actualExecutions']=sum(int(x['counts'].get('total',0)) for x in r['groups']);r['status']='passed' if not failed and r['actualExecutions']==required and r['sourceUnchanged'] and r['outputsUnchanged'] else 'failed';r['files']={str(f.relative_to(evidence)):{'sha256':hashlib.sha256(f.read_bytes()).hexdigest(),'bytes':f.stat().st_size} for f in sorted(evidence.rglob('*')) if f.is_file() and f.name!='receipt.json'};save()
print(a.label,r['status'],r['actualPass'],r['actualFail'],evidence,flush=True);raise SystemExit(0 if r['status']=='passed' else 1)
