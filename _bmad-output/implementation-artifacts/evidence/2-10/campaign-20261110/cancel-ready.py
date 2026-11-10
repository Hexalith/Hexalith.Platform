from pathlib import Path
from datetime import datetime,timezone
import hashlib,io,json,re,subprocess,sys,zipfile
phase=sys.argv[1];assert phase in ('startup','execution')
repo='Hexalith/Hexalith.Parties';source='c9eb1fe37373b2b25d3759a4ac8f33ac46754e17';ref='qualify/story-2-10-20261110-c9eb1fe3'
out=Path('/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/evidence/2-10/campaign-20261110')
path=out/f'cancel-{phase}-dispatch.json';request=json.loads(path.read_text());run=request['workflowRunId']
def api(suffix):return json.loads(subprocess.check_output(['gh','api',f'repos/{repo}/'+suffix],text=True,timeout=30))
def utc(s):return datetime.fromisoformat(s.replace('Z','+00:00'))
if request.get('cancellationRequestIssued'):
    print(json.dumps({'run':run,'cancellationAlreadyIssued':True}));raise SystemExit(0)
v=api(f'actions/runs/{run}');assert v['head_sha']==source and v['head_branch']==ref and v['run_attempt']==1 and v['event']=='workflow_dispatch'
if v['status']=='completed':
    print(json.dumps({'run':run,'status':'completed-before-cancellation','conclusion':v['conclusion']}));raise SystemExit(0)
jobs=api(f'actions/runs/{run}/jobs')['jobs'];job=next((j for j in jobs if j['name']=='qualification'),None)
waiting=job is not None and any(s['name']=='Await parent cancellation of this exact ready run' and s['status']=='in_progress' for s in job['steps'])
if not waiting:
    print(json.dumps({'run':run,'readyToCancel':False,'stages':[{'job':j['name'],'step':[s['name'] for s in j['steps'] if s['status']=='in_progress']} for j in jobs]}));raise SystemExit(0)
artifacts=api(f'actions/runs/{run}/artifacts')['artifacts'];a=next(a for a in artifacts if a['name']=='lifecycle-cancellation-ready')
assert not a['expired'] and a['size_in_bytes']<67108864
body=subprocess.check_output(['gh','api',f'repos/{repo}/actions/artifacts/{a["id"]}/zip'],timeout=30)
digest=hashlib.sha256(body).hexdigest();assert a['digest']=='sha256:'+digest
private=Path('/tmp/hexalith-story-2-10-20261110')/f'cancel-{phase}-ready.zip';private.write_bytes(body);private.chmod(0o600)
with zipfile.ZipFile(io.BytesIO(body)) as z:
    assert 'observations.json' in z.namelist();journal=json.loads(z.read('observations.json'))
assert journal['source']==source and journal['classification']=='baseline-only'
c=journal['executionContext'];assert c['kind']=='ci' and c['repository']==repo and c['workflowRunId']==run and c['workflowAttempt']==1 and c['jobId']=='qualification' and c['runnerKind']=='github-hosted' and c['operatingSystem']=='Linux' and c['disposable'] is True
owners=journal['owners'];assert len(owners)==1;owner=owners[0];assert owner['label']=='cancel' and owner['cancellationPhase']==phase and re.fullmatch('[0-9a-f]{32}',owner['runId'])
ready=[o for o in journal['observations'] if o['kind']=='actual-cancellation-ready'];assert len(ready)==1 and ready[0]['phase']==phase and ready[0]['ownerRunId']==owner['runId']
if phase=='startup':
    starting=next(o for o in journal['observations'] if o['kind']=='startup-cancellation-ready')
    assert starting['ownerRunId']==owner['runId'] and starting['actualOwnedContainers'] and starting['appHostProcessId']==owner['pausedHost']['pid']
    assert utc(starting['startupDeadlineUtc'])>datetime.now(timezone.utc)
else:
    assert owner['executionCheckpoint']['realPersistenceAssertionsPassed'] is True
    admitted=owner['admission'];assert admitted['ownerRunId']==owner['runId'] and admitted.get('endedAtUtc') is None and utc(admitted['expiresAtUtc'])>datetime.now(timezone.utc)
current=api(f'actions/jobs/{job["id"]}');assert any(s['name']=='Await parent cancellation of this exact ready run' and s['status']=='in_progress' for s in current['steps'])
receipt={'schema':'hexalith.story-2-10.verified-cancellation-request.v1','workflowRunId':run,'workflowAttempt':1,'repository':repo,'sourceRevision':source,'phase':phase,'ownerRunId':owner['runId'],'readyArtifactId':a['id'],'readyArtifactDigest':a['digest'],'downloadSha256':digest,'readyAtUtc':ready[0]['readyAtUtc'],'waitingJobId':job['id'],'waitingStepVerified':True,'requestIssuedAtUtc':datetime.now(timezone.utc).isoformat(),'candidateAcceptance':False}
request['cancellationRequestIssued']=True;request['cancellationRequestIssuedAtUtc']=receipt['requestIssuedAtUtc'];path.write_text(json.dumps(request,indent=2)+'\n')
rpath=out/f'cancel-{phase}-request.json';rpath.write_text(json.dumps(receipt,indent=2)+'\n')
p=subprocess.run(['gh','run','cancel',run,'--repo',repo],capture_output=True,text=True,timeout=30)
receipt['commandExitCode']=p.returncode;receipt['cancellationAcknowledged']=p.returncode==0;rpath.write_text(json.dumps(receipt,indent=2)+'\n')
print(json.dumps({'run':run,'phase':phase,'readyArtifactVerified':True,'exactWaitingStepVerified':True,'oneCancellationRequestIssued':True,'acknowledged':p.returncode==0}))
