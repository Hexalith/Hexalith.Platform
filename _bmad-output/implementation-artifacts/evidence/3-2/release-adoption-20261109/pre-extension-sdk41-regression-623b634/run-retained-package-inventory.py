"""Run the exact retained inventory locally; never claim the hosted tuple gate."""
from pathlib import Path
import sys,json,subprocess,datetime,xml.etree.ElementTree as ET,os,hashlib
ROOT=Path('/tmp/story-3-2-broad-20261109-_z_wy84q/current')
sys.path.insert(0,str(ROOT/'eng'))
from prepare_builds_artifacts import source_state
from run_tuple_ci import validate_profile,snapshot_composition_test_outputs,verify_composition_test_outputs_unchanged,verify_test_sdk
out=Path('/tmp/story32-retained-sdk41-package-inventory');out.mkdir(exist_ok=False)
assembly=ROOT/'artifacts/story32-sdk41-regression/bin/Hexalith.Platform.Composition.Tests/release/Hexalith.Platform.Composition.Tests.dll'
profile=json.loads((ROOT/'eng/tuple-ci-suites.json').read_bytes());assert validate_profile(profile)==412
state=source_state();assert not state['dirty'],state['status']
revision=subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip()
verify_test_sdk(assembly,'4.1.0')
report={'scope':'Local retained SDK4.1/Builds4.45 signed-package regression; no hosted tuple, SDK4.2 default, fixture publication or host admission credit.','source':revision,'required':412,'candidateAcceptance':False,'qualificationCredit':False,'status':'running','sourceBefore':state,'groups':[],'outputs':{'before':snapshot_composition_test_outputs(assembly)}}
def save(): (out/'receipt.json').write_text(json.dumps(report,indent=2)+'\n')
save()
try:
 for i,group in enumerate(profile['groups']):
  xml=out/f'group-{i}.xml';selectors=[arg for name in group['selectors'] for arg in [group['filter'],name]]
  cmd=['dotnet',str(assembly),*selectors,'-result-xml',str(xml)]
  started=datetime.datetime.now(datetime.timezone.utc).isoformat()
  with (out/f'group-{i}.log').open('w') as log:
   result=subprocess.run(cmd,cwd=ROOT,stdout=log,stderr=subprocess.STDOUT,timeout=1200)
  counts=ET.parse(xml).getroot().find('assembly').attrib if xml.exists() else {}
  passed=result.returncode==0 and int(counts.get('total','0'))==group['requiredCount'] and int(counts.get('passed','0'))==group['requiredCount'] and all(int(counts.get(x,'0'))==0 for x in ['failed','errors','skipped','not-run'])
  report['groups'].append({'index':i,'purpose':group.get('purpose'),'command':cmd,'startedUtc':started,'exitCode':result.returncode,'expected':group['requiredCount'],'counts':counts,'passed':passed})
  save();print(json.dumps({'group':i,'expected':group['requiredCount'],'counts':counts,'passed':passed}),flush=True)
 report['status']='passed' if all(x['passed'] for x in report['groups']) else 'failed'
except BaseException as error:
 report['status']='failed';report['error']=f'{type(error).__name__}: {error}';raise
finally:
 verify_composition_test_outputs_unchanged(report['outputs'])
 report['sourceAfter']=source_state()
 report['sourceUnchanged']=report['sourceBefore']==report['sourceAfter']
 if not report['sourceUnchanged']:report['status']='failed'
 report['completedUtc']=datetime.datetime.now(datetime.timezone.utc).isoformat();save()
if report['status']!='passed':raise SystemExit(1)
print('All412 local retained controls passed with unchanged source and measured output bodies.')
