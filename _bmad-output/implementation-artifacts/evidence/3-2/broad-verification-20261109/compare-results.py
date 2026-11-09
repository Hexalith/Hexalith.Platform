import collections, hashlib, json, pathlib, sys
base=pathlib.Path(sys.argv[1]); plan=json.loads((base/'plan.json').read_text())
left=json.loads((base/'results-baseline/tests.json').read_text()); right=json.loads((base/'results-current/tests.json').read_text())
def key(row):
    # Test display names encode theory arguments. Neither run changes them.
    return row['name']
groups=[]
left_groups=collections.defaultdict(list); right_groups=collections.defaultdict(list)
for row in left: left_groups[key(row)].append(row)
for row in right: right_groups[key(row)].append(row)
a={}; b={}
for name in sorted(left_groups.keys() | right_groups.keys()):
    old_rows=left_groups[name]; new_rows=right_groups[name]
    duplicate=max(len(old_rows),len(new_rows))>1
    if duplicate:
        old_results=collections.Counter(r['result'] for r in old_rows); new_results=collections.Counter(r['result'] for r in new_rows)
        if len(old_rows)!=len(new_rows) or old_results!=new_results or set(old_results)!={'Pass'}:
            raise SystemExit('ambiguous duplicate result group requires individual triage: '+name)
        groups.append({'name':name,'baselineCount':len(old_rows),'currentCount':len(new_rows),'baselineResults':dict(old_results),'currentResults':dict(new_results),'comparison':'Equal multiplicity; every case passed on both revisions. Display strings truncate distinct theory inputs; ordering does not affect this all-pass outcome.'})
    for target,rows in ((a,old_rows),(b,new_rows)):
        for index,row in enumerate(rows):
            identity=name+(' [display occurrence '+str(index+1)+'/'+str(len(rows))+']' if duplicate else '')
            target[identity]=row

common=a.keys()&b.keys(); added=b.keys()-a.keys(); removed=a.keys()-b.keys()
summary={'baselineRevision':plan['baseline']['revision'],'currentRevision':plan['current']['revision'],'baseline':json.loads((base/'results-baseline/summary.json').read_text()),'current':json.loads((base/'results-current/summary.json').read_text()),'commonTests':len(common),'addedTests':len(added),'removedTests':len(removed),'commonFailures':sorted(n for n in common if a[n]['result']==b[n]['result']=='Fail'),'regressions':sorted(n for n in common if a[n]['result']!='Fail' and b[n]['result']=='Fail'),'resolvedFailures':sorted(n for n in common if a[n]['result']=='Fail' and b[n]['result']!='Fail'),'addedFailures':sorted(n for n in added if b[n]['result']=='Fail'),'removedFailures':sorted(n for n in removed if a[n]['result']=='Fail'),'addedTestNames':sorted(added),'removedTestNames':sorted(removed)}
summary['duplicateDisplayNameGroups']=groups
summary['failureClasses']=dict(collections.Counter(r['class'] for r in right if r['result']=='Fail'))
summary['artifacts']={str(p.relative_to(base)):hashlib.sha256(p.read_bytes()).hexdigest() for side in ('current','baseline') for p in (base/f'results-{side}').glob('*') if p.suffix in ('.json','.xml','.log')}
(base/'comparison.json').write_text(json.dumps(summary,indent=2)+'\n')
print(json.dumps({k:len(v) if isinstance(v,list) else v for k,v in summary.items() if k!='artifacts'},indent=2))
