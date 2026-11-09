from pathlib import Path
import datetime,hashlib,json,os,subprocess,sys,tempfile,xml.etree.ElementTree as ET
p=Path('/tmp/story-3-2-broad-20261109-_z_wy84q/current');sys.path.insert(0,str(p/'eng'));from prepare_builds_artifacts import source_state
from run_tuple_ci import snapshot_composition_test_outputs,verify_test_sdk
out=Path(tempfile.mkdtemp(prefix='story32-integrated-sdk42-controls-'));Path('/tmp/story32-integrated-sdk42-control-locator.txt').write_text(str(out)+'\n');assembly=Path('/tmp/story32-sdk42-integrated-artifacts/bin/Hexalith.Platform.Composition.Tests/release/Hexalith.Platform.Composition.Tests.dll');verify_test_sdk(assembly,'4.2.0');before=source_state();outputs=snapshot_composition_test_outputs(assembly);result={'recordedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'scope':'Actual local measured default4.2 package candidate; no hosted environment spoofing or signed-fixture/host admission credit.','work':str(out),'sourceHead':subprocess.check_output(['git','rev-parse','HEAD'],cwd=p,text=True).strip(),'sourceBefore':before,'outputBefore':outputs,'tests':[],'candidateAcceptance':False,'hostedQualification':False,'signedFixtureCredit':False};(out/'before.json').write_text(json.dumps(result,indent=2)+'\n');env=os.environ.copy();env['NUGET_PACKAGES']='/tmp/story32-sdk42-upstream-catalog-package-cache';failed=False
for suite in ['upstream','base','extensions']:
 profile=json.loads((p/('eng/tuple-ci-sdk42-current-suites.json' if suite=='upstream' else 'eng/tuple-ci-sdk42-base-suites.json' if suite=='base' else 'eng/tuple-ci-extension-suites.json')).read_bytes());expected=456 if suite=='upstream' else 97 if suite=='base' else 9;assert sum(g['requiredCount'] for g in profile['groups'])==expected
 for index,group in enumerate(profile['groups']):
  name=suite+'-'+str(index);xml=out/(name+'.xml');argv=['dotnet',str(assembly)]+[a for selector in group['selectors'] for a in [group['filter'],selector]]+['-result-xml',str(xml)];record={'name':name,'argv':argv,'expected':group['requiredCount'],'purpose':group['purpose'],'startedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat()}
  with (out/(name+'.stdout')).open('w') as stdout,(out/(name+'.stderr')).open('w') as stderr:
   completed=subprocess.run(argv,cwd=p,env=env,stdout=stdout,stderr=stderr,timeout=1200)
  record['exitCode']=completed.returncode;record['completedUtc']=datetime.datetime.now(datetime.timezone.utc).isoformat();record['counts']=ET.parse(xml).getroot().find('assembly').attrib if xml.exists() else None;c=record['counts'];record['passed']=bool(c and completed.returncode==0 and int(c['total'])==group['requiredCount'] and int(c['passed'])==int(c['total']) and all(int(c.get(k,'0'))==0 for k in ['failed','errors','skipped','not-run']));failed|=not record['passed'];result['tests'].append(record);(out/'receipt.json').write_text(json.dumps(result,indent=2)+'\n');print(name,record['exitCode'],record['counts'],flush=True)
from collections import Counter
identities = Counter()
for file in out.glob('*.xml'):
 for case in ET.parse(file).getroot().iter('test'):
  identities[(case.attrib['type'], case.attrib['method'], case.attrib['name'])] += 1
result['executionCount'] = sum(identities.values())
result['uniqueCaseCount'] = len(identities)
result['duplicateCases'] = [{'type': key[0], 'method': key[1], 'name': key[2], 'executions': value} for key, value in sorted(identities.items()) if value != 1]
if result['executionCount'] != 562 or result['uniqueCaseCount'] != 559 or len(result['duplicateCases']) != 3 or any(case['executions'] != 2 for case in result['duplicateCases']):
 failed = True
result['sourceAfter']=source_state();result['outputAfter']=snapshot_composition_test_outputs(assembly);result['sourceUnchanged']=result['sourceBefore']==result['sourceAfter'];result['outputsUnchanged']=outputs==result['outputAfter'];result['status']='passed' if not failed and result['sourceUnchanged'] and result['outputsUnchanged'] else 'failed';result['files']={f.name:{'sha256':hashlib.sha256(f.read_bytes()).hexdigest(),'bytes':f.stat().st_size} for f in sorted(out.iterdir()) if f.is_file() and f.name!='receipt.json'};(out/'receipt.json').write_text(json.dumps(result,indent=2)+'\n');print(out,result['status'],flush=True);raise SystemExit(0 if result['status']=='passed' else 1)
