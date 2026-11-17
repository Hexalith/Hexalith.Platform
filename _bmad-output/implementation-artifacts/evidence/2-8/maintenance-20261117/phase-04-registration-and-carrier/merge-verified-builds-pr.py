"""One normal merge after exact latest CI, source, base, rules and message checks."""
import hashlib, json, os, pathlib, shutil, subprocess, sys, datetime
ROOT=pathlib.Path('/home/administrator/projects/hexalith/platform')
AREA=ROOT/'artifacts/2-8-maintenance-20261117'
PR=int(sys.argv[1]); RUN=int(sys.argv[2]); LABEL=sys.argv[3]
ATTEMPT=int(sys.argv[4]) if len(sys.argv)>4 else 1
assert PR in (187,188) and (ATTEMPT==1 or (PR==187 and ATTEMPT==2))
CI_FILE='ci.yml' if PR==188 else 'builds-ci.yml'
WORKFLOW_ID=320718206 if PR==188 else 411848291
OUT=AREA/LABEL; OUT.mkdir(exist_ok=False)
REPO=pathlib.Path(os.environ['TMPDIR'])/'Hexalith.Builds'
NODE=pathlib.Path('/home/administrator/.nvm/versions/node/v24.22.1/bin/node')
ENV=dict(os.environ,PATH=str(NODE.parent)+':'+os.environ['PATH'])
P=next(row for row in json.loads((AREA/'merge-preparation-01/result.json').read_text())['proposals'] if row['pr']==PR)
R={'proposal':P,'run':RUN,'attempt':ATTEMPT,'mutationRequests':0,'commands':[]}
if ATTEMPT==2:
    approval=json.loads((AREA/'mac-retry-approval.json').read_text())
    retry=json.loads((AREA/'mac-retry-01/result.json').read_text())
    assert approval['limit']==1 and approval['job']==134163397914 and approval['source']==P['source']
    assert retry['run']==RUN and retry['mutationRequests']==1 and retry['status']=='single-authorized-macos-retry-requested'
    R['sameSourceRerunException']='user-approved one macOS capacity retry; original failure retained'
def run(label,args,cwd=None,payload=None):
    p=subprocess.run(args,cwd=cwd or REPO,env=ENV,input=payload,capture_output=True,text=True,timeout=300)
    (OUT/(label+'.log')).write_text(p.stdout+p.stderr);R['commands'].append({'label':label,'exitCode':p.returncode});print(label,p.returncode,flush=True)
    if p.returncode:raise RuntimeError(label)
    return p.stdout.strip()
def api(label,path):
    value=json.loads(run(label,['gh','api','repos/Hexalith/Hexalith.Builds/'+path]));(OUT/(label+'.json')).write_text(json.dumps(value,indent=2)+'\n');return value
def current(label):
    pr=api(label+'-pr','pulls/'+str(PR))
    assert pr['state']=='open' and not pr['merged'] and pr['head']['sha']==P['source'] and pr['base']['sha']==P['base']
    assert pr['mergeable'] is True
    branch='4.44.x' if PR==188 else 'main';assert pr['base']['ref']==branch
    latest=api(label+'-latest','actions/workflows/'+CI_FILE+'/runs?event=pull_request&head_sha='+P['source']+'&per_page=100')['workflow_runs']
    assert latest and latest[0]['id']==RUN and latest[0]['run_attempt']==ATTEMPT and latest[0]['status']=='completed' and latest[0]['conclusion']=='success'
    return pr
try:
    run('clone',['git','clone','--shared','--no-checkout',str(ROOT/'references/Hexalith.Builds'),str(REPO)],ROOT)
    run('origin',['git','remote','set-url','origin','https://github.com/Hexalith/Hexalith.Builds.git'])
    branch='4.44.x' if PR==188 else 'fix/story-2-8-maintenance-registration'
    bundle=AREA/('builds-source-05/Builds.bundle' if PR==188 else 'registration-fixtures-02/Builds.bundle')
    run('import',['git','fetch',str(bundle),'refs/heads/'+branch+':refs/heads/'+branch])
    run('checkout',['git','checkout',branch])
    assert run('source',['git','rev-parse','HEAD'])==P['source']
    install=REPO if PR==188 else REPO/'Github/guard'
    run('npm-ci',['npm','ci','--ignore-scripts','--no-fund'],install)
    run('signatures',['npm','audit','signatures'],install)
    if PR==187:(REPO/'node_modules').symlink_to(install/'node_modules',target_is_directory=True)
    message=(AREA/'merge-preparation-01'/P['messageFile']).read_text()
    assert hashlib.sha256(message.encode()).hexdigest()==P['exactMessageSha256']
    cli=[str(NODE),str(REPO/'node_modules/@commitlint/cli/cli.js'),'--config',str(REPO/'commitlint.config.mjs'),'--color=false']
    run('prospective-message',cli,payload=message)
    run('api-message-form',cli,payload=message.rstrip('\n'))
    pr=current('preflight')
    metadata=api('source-run','actions/runs/'+str(RUN))
    assert metadata['head_sha']==P['source'] and metadata['event']=='pull_request' and metadata['workflow_id']==WORKFLOW_ID and metadata['path']=='.github/workflows/'+CI_FILE
    assert metadata['repository']['full_name']=='Hexalith/Hexalith.Builds' and metadata['run_attempt']==ATTEMPT
    if PR==188:assert metadata['name']=='CI'
    jobs=api('source-jobs','actions/runs/'+str(RUN)+'/jobs?per_page=100')['jobs']
    expected={'Package catalog contracts','Build and package contracts','.NET tests','Python contract tests','CodeQL / analyze','Dependency review / dependency-review','Build and test'} if PR==188 else {'Package catalog contracts','Workflow lint and public API','Python contract tests (runner)','Python contract tests (3.12)','.NET tests / Build and tests','.NET tests / Restore owned CI/CD content','Sample module / Build and tests','Sample module / Restore owned CI/CD content','Sample Windows tests','Sample macOS tests'}
    assert {job['name'] for job in jobs}==expected and len(jobs)==len(expected)
    for job in jobs:
        contextual_skip=PR==187 and job['name'].endswith('/ Restore owned CI/CD content')
        assert job['status']=='completed' and job['conclusion']==('skipped' if contextual_skip else 'success')
    if ATTEMPT==2:
        mac=next(job for job in jobs if job['name']=='Sample macOS tests')
        assert mac['id']==134183570648 and mac['run_attempt']==2 and len(mac['steps'])>0
    registry=api('registry','actions/workflows/'+CI_FILE);assert registry['id']==WORKFLOW_ID and registry['path']=='.github/workflows/'+CI_FILE and registry['state']=='active'
    rules=api('branch-rules','rules/branches/'+('4.44.x' if PR==188 else 'main'))
    assert all(rule['type'] in ('deletion','non_fast_forward') for rule in rules),'New branch approval/check rules require a fresh audit'
    basebranch='4.44.x' if PR==188 else 'main'
    run('fresh-base',['git','fetch','origin','refs/heads/'+basebranch+':refs/remotes/origin/'+basebranch])
    assert run('base',['git','rev-parse','refs/remotes/origin/'+basebranch])==P['base']
    tree=run('prospective-tree',['git','merge-tree','--write-tree',P['base'],P['source']]).splitlines()[0]
    assert tree==P['expectedTree']
    current('late')
    title,body=message.rstrip('\n').split('\n\n',1)
    request={'sha':P['source'],'merge_method':'merge','commit_title':title,'commit_message':body}
    (OUT/'request.json').write_text(json.dumps(request,indent=2)+'\n')
    R['mutationRequests']=1;R['requestedAtUtc']=datetime.datetime.now(datetime.timezone.utc).isoformat();(OUT/'result.json').write_text(json.dumps(R,indent=2)+'\n')
    response=json.loads(run('normal-merge',['gh','api','--method','PUT','repos/Hexalith/Hexalith.Builds/pulls/'+str(PR)+'/merge','--input',str(OUT/'request.json')]))
    assert response.get('merged') is True
    sha=response['sha'];R['mergeCommit']=sha
    run('fetch-actual-merge',['git','fetch','origin',sha])
    assert run('actual-tree',['git','rev-parse',sha+'^{tree}'])==P['expectedTree']
    assert run('actual-parents',['git','show','-s','--format=%P',sha])==P['base']+' '+P['source']
    raw=subprocess.check_output(['git','cat-file','commit',sha],cwd=REPO).partition(b'\n\n')[2].decode()
    run('actual-message',cli,payload=raw)
    (OUT/'actual-message.txt').write_text(raw)
    R.update(status='normally-merged-and-independently-verified',actualMessageSha256=hashlib.sha256(raw.encode()).hexdigest())
except BaseException as e:R.update(status='failed',reason=type(e).__name__+': '+str(e));raise
finally:(OUT/'result.json').write_text(json.dumps(R,indent=2)+'\n')
