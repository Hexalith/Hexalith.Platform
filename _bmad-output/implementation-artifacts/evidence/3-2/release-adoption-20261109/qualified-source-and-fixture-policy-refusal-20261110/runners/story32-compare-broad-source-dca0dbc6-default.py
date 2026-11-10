from pathlib import Path
from collections import defaultdict,Counter
import json,hashlib
root=Path('/tmp/story32-import-closure-candidate-6xkn9mrn/repository');out=Path(Path('/tmp/story32-broad-source-dca0dbc6-default-locator.txt').read_text().strip());receipt=json.loads((out/'receipt.json').read_text());assert receipt.get('fullInventoryExecuted') and receipt['sourceUnchanged'] and receipt['outputsUnchanged'];current=json.loads((out/'tests.json').read_text());destination=out.parent/(out.name+'-comparison');assert not destination.exists();destination.mkdir();saved=root/'_bmad-output/implementation-artifacts/evidence/3-2/broad-verification-20261109'
def groups(rows):
 result=defaultdict(list)
 for row in rows:result[(row['class'],row['method'],row['name'])].append(row)
 return result
new=groups(current)
for label,path,source in [('original-baseline',saved/'baseline-results.json','f7c68398cdcb494d535e789e3d9577af9e8d5d36'),('retained-previous-current',saved/'current-results.json','59fb32933d76a7e71d9479e1903087a73ea66f2b'),('previous-frozen-explicit-source',root/'_bmad-output/implementation-artifacts/evidence/3-2/release-adoption-20261109/second-frozen-tuple-observations/broad-explicit-source/tests.json','862fd14f17bf48432c76993afd668b5a067e887d'),('previous-frozen-plain-default',Path('/tmp/story32-broad-source-9ea927c0-default-ydin7dkv/tests.json'),'9ea927c0733a6ac4acdfd25dc6e513b1c5e7be4f')]:
 raw=path.read_bytes();previous=json.loads(raw);old=groups(previous);common=set(old)&set(new);added=set(new)-set(old);removed=set(old)-set(new);report={'historicalSource':source,'currentSource':receipt['source'],'historicalFile':str(path),'historicalFileSha256':hashlib.sha256(raw).hexdigest(),'historicalCounts':dict(Counter(r['result'] for r in previous)),'currentCounts':dict(Counter(r['result'] for r in current)),'identity':'exact class + method + xUnit display name; ambiguous truncated theory display groups receive separate multiplicity/results triage','commonDisplayIdentities':len(common),'commonFailures':[],'regressions':[],'resolvedFailures':[],'newlySkipped':[],'retainedSkipped':[],'addedTests':[list(k) for k in sorted(added)],'removedTests':[list(k) for k in sorted(removed)],'addedFailures':[list(k) for k in sorted(added) if any(r['result']=='Fail' for r in new[k])],'addedSkipped':[list(k) for k in sorted(added) if any(r['result']=='Skip' for r in new[k])],'duplicateDisplayGroups':[],'qualificationCredit':False}
 for k in sorted(common):
  a,b=old[k],new[k];left,right=Counter(r['result'] for r in a),Counter(r['result'] for r in b)
  if len(a)>1 or len(b)>1:
   report['duplicateDisplayGroups'].append({'identity':list(k),'historicalCount':len(a),'currentCount':len(b),'historicalResults':dict(left),'currentResults':dict(right),'allPassSameMultiplicity':len(a)==len(b) and set(left)==set(right)=={'Pass'}});continue
  before,after=a[0]['result'],b[0]['result']
  field='commonFailures' if before==after=='Fail' else 'regressions' if before=='Pass' and after=='Fail' else 'resolvedFailures' if before=='Fail' and after=='Pass' else 'newlySkipped' if before!='Skip' and after=='Skip' else 'retainedSkipped' if before==after=='Skip' else None
  if field:report[field].append(list(k))
 old_methods={(r['class'],r['method']) for r in previous};new_methods={(r['class'],r['method']) for r in current};report['addedMethods']=[list(k) for k in sorted(new_methods-old_methods)];report['removedMethods']=[list(k) for k in sorted(old_methods-new_methods)];report['failureDetails']=[r for r in current if r['result']=='Fail'];(destination/(label+'.json')).write_text(json.dumps(report,indent=2)+'\n');print(label,{k:len(report[k]) for k in ['commonFailures','regressions','resolvedFailures','newlySkipped','retainedSkipped','addedTests','removedTests','addedFailures','addedSkipped','addedMethods','removedMethods']},flush=True)
print(destination)
