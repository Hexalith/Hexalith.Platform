"""Strict, source/output-bound direct xUnit execution of all continuing candidate inventories."""
from pathlib import Path
import subprocess,json,sys,xml.etree.ElementTree as ET,datetime,hashlib
sys.path.insert(0,str(Path.cwd()/'eng'))
from run_tuple_ci import snapshot_composition_test_outputs,verify_composition_test_outputs_unchanged,require_collector_execution,require_collector_source,REQUIRED_TOTALS,validate_profile
assembly=Path(sys.argv[1]);out=Path(sys.argv[2]);out.mkdir(parents=True,exist_ok=False)
revision=subprocess.check_output(['git','rev-parse','HEAD']).decode().strip();require_collector_source(Path.cwd(),revision)
snapshot={'before':snapshot_composition_test_outputs(assembly)}
source_files={str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted([Path('Directory.Build.props'),*Path('src/Hexalith.Platform.Composition').glob('*.cs'),*Path('tests/Hexalith.Platform.Composition.Tests').glob('*.cs')])}
(out/'source-and-output-before.json').write_text(json.dumps({'candidateRevision':revision,'sourceFiles':source_files,'output':snapshot['before']},indent=2)+'\n')
rows=[];jobs=[]
for filename,suite in [('tuple-ci-sdk42-current-suites.json','current42'),('tuple-ci-sdk42-base-suites.json','base42'),('tuple-ci-extension-suites.json','extensions')]:
 profile=json.loads((Path('eng')/filename).read_text());assert validate_profile(profile)==REQUIRED_TOTALS[suite]
 for number,group in enumerate(profile['groups']):jobs.append((filename,suite,number,group))
# Resolve changed reader/fixture cases first, retaining one execution of each declared group.
priority={('current42',12):0,('current42',25):1,('current42',23):2,('current42',0):3,('current42',19):4,('base42',2):5}
jobs.sort(key=lambda item:priority.get((item[1],item[2]),100))
for filename,suite,number,group in jobs:
 label=filename.removesuffix('.json')+'-'+str(number);xml=out/(label+'.xml')
 command=['dotnet',str(assembly),*[arg for selector in group['selectors'] for arg in [group['filter'],selector]],'-result-xml',str(xml)]
 row={'suite':suite,'profile':filename,'group':number,'selectors':group['selectors'],'required':group['requiredCount'],'command':command,'candidateRevision':revision,'startedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat()}
 with (out/(label+'.log')).open('w') as log:
  try:row['exitCode']=subprocess.run(command,stdout=log,stderr=subprocess.STDOUT,timeout=1200).returncode
  except subprocess.TimeoutExpired:row['exitCode']='timeout'
 if xml.exists():
  doc=ET.parse(xml).getroot().find('assembly');row['counts']=doc.attrib
  row['failures']=[{'name':t.attrib['name'],'message':t.find('failure/message').text if t.find('failure/message') is not None else ''} for t in doc.iter('test') if t.attrib.get('result')=='Fail']
  try:
   require_collector_execution(row['counts'],'net11.0')
   row['passed']=row['exitCode']==0 and int(row['counts']['total'])==group['requiredCount'] and int(row['counts']['passed'])==group['requiredCount'] and all(int(row['counts'].get(k,0))==0 for k in ['failed','errors','skipped','not-run'])
  except (ValueError,KeyError):row['passed']=False
 else:row['passed']=False
 rows.append(row);(out/'results.json').write_text(json.dumps(rows,indent=2)+'\n')
 print(suite,number,row.get('counts',{}).get('total'),row.get('counts',{}).get('failed'),row['passed'],flush=True)
verify_composition_test_outputs_unchanged(snapshot);require_collector_source(Path.cwd(),revision)
for p,digest in source_files.items():assert hashlib.sha256(Path(p).read_bytes()).hexdigest()==digest
(out/'source-and-output-after.json').write_text(json.dumps({'candidateRevision':revision,'unchanged':True,'output':snapshot},indent=2)+'\n')
summary={'candidateRevision':revision,'passed':len(rows)==len(jobs) and all(r['passed'] for r in rows),'totals':{suite:sum(int(r.get('counts',{}).get('total',0)) for r in rows if r['suite']==suite) for suite in REQUIRED_TOTALS},'requiredTotals':REQUIRED_TOTALS,'groups':len(rows),'skips':sum(int(r.get('counts',{}).get('skipped',0)) for r in rows),'candidateAcceptance':False,'hostedMainAcceptance':False}
(out/'summary.json').write_text(json.dumps(summary,indent=2)+'\n');raise SystemExit(not summary['passed'])
