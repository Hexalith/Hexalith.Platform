from pathlib import Path
import json,hashlib,re,xml.etree.ElementTree as ET
state=Path('/tmp/bmad-protection-audit-scope.json');s=json.loads(state.read_text());run=Path('/tmp/ext-protection-loop7-parent-final');commands=json.loads((run/'commands.json').read_text());assert len(commands)>=20 and all(row['exitCode']==0 for row in commands)
builds=[]
for row in commands:
 p=run/row['path'];assert hashlib.sha256(p.read_bytes()).hexdigest()==row['sha256']
 if 'build' in row['name']:
  text=p.read_text();assert re.search(r'\b0 Warning\(s\)',text) and re.search(r'\b0 Error\(s\)',text),row['name'];builds.append(row['name'])
passed={};skipped=[];metrics={}
for file in sorted(run.glob('*.xml')):
 tree=ET.parse(file);assemblies=[a.attrib for a in tree.iter('assembly')];metrics[file.stem]=[{k:a.get(k) for k in ['total','passed','failed','skipped','errors','time']} for a in assemblies]
 for test in tree.iter('test'):
  if test.attrib.get('result')=='Pass':passed[test.attrib['name']]={'file':str(file),'type':test.attrib.get('type'),'method':test.attrib.get('method')}
  elif test.attrib.get('result')=='Skip':skipped.append(test.attrib['name'])
  else:raise AssertionError(test.attrib)
prior=json.loads(Path('/tmp/ext-protection-loop6-parent-matrix-audit.json').read_text());rows={}
for row,methods in prior['rows'].items():
 rows[row]={}
 for method in methods:
  matches=[name for name,attrs in passed.items() if attrs['method']==method];assert matches,(row,method);rows[row][method]=matches
loop7={name:attrs for name,attrs in passed.items() if 'Loop7' in name or 'Readiness' in name}
counts={}
for name in ['harness','offline-verifier']:
 text=(run/(name+'.log')).read_text();match=re.search(r'Ran (\d+) tests? in ([\d.]+)s',text);assert match and re.search(r'^OK\s*$',text,re.M),name;counts[name]={'tests':int(match[1]),'seconds':float(match[2])}
source=json.loads((run/'source.log').read_text());assert source['mode']=='Source' and source['cases']==41 and source['liveExecuted'] is False
audit={'allCommandsPassed':True,'commandCount':len(commands),'allRequiredBuildsZeroWarningsErrors':builds,'rows':rows,'loop7AdditionalExecutedCases':loop7,'explicitIntegrationSkips':skipped,'metrics':metrics,'python':counts,'sourceMode':source,'nativePending':True,'countsOverlap':True}
Path('/tmp/ext-protection-loop7-parent-matrix-audit.json').write_text(json.dumps(audit,indent=2)+'\n');s['rederive7']['parentCombinedVerification']={'directory':str(run),'commands':len(commands),'allExitedZero':True,'metrics':metrics,'python':counts,'sourceMode':source,'matrixAudit':'/tmp/ext-protection-loop7-parent-matrix-audit.json'};state.write_text(json.dumps(s,indent=2)+'\n');print(json.dumps({'commands':len(commands),'matrixRows':len(rows),'loop7Cases':len(loop7),'metrics':metrics,'python':counts,'nativePending':True},indent=2))
