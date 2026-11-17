from pathlib import Path
import datetime,json,os,subprocess
p=Path("/home/administrator/projects/hexalith/platform/artifacts/2-8-continuation-20261117");d=json.loads((p/'dispatch-5.json').read_text());r=json.loads((p/'run-5-ready-verification.json').read_text())
now=datetime.datetime.now(datetime.timezone.utc)
assert d['runId']==r['runId']==44073347507 and d['sha']==r['source']=='bccab615f9e3d1a92e3bbf648ee89e78a925670b'
assert d['cancellationRequests']==0 and r['allChecksPassed'] is True and r['traceSetupSucceeded'] is True
assert 0<=(now-datetime.datetime.fromisoformat(r['verifiedAtUtc'])).total_seconds()<8
assert 0<=(now-datetime.datetime.fromisoformat(r['readyAtUtc'].replace('Z','+00:00'))).total_seconds()<=15
assert now<datetime.datetime.fromisoformat(r['expiresAtUtc'].replace('Z','+00:00'))
actual=json.loads(subprocess.check_output(['gh','api','repos/Hexalith/Hexalith.Parties/actions/runs/44073347507']))
job=json.loads(subprocess.check_output(['gh','api','repos/Hexalith/Hexalith.Parties/actions/jobs/'+str(r['jobId'])]))
assert actual['head_sha']==d['sha'] and actual['run_attempt']==1 and actual['status']=='in_progress'
assert job['head_sha']==d['sha'] and job['run_id']==44073347507 and job['status']=='in_progress' and any(s['name']=='Await parent cancellation of this exact ready run' and s['status']=='in_progress' for s in job['steps'])
d['cancellationRequests']=1;d['cancellationRequestedAtUtc']=datetime.datetime.now(datetime.timezone.utc).isoformat();d['readyVerificationArtifactId']=r['artifactId']
temporary=p/'dispatch-5.json.new';temporary.write_text(json.dumps(d,indent=2)+'\n');os.replace(temporary,p/'dispatch-5.json')
result=subprocess.run(['gh','api','--method','POST','--include','repos/Hexalith/Hexalith.Parties/actions/runs/44073347507/cancel'],capture_output=True,text=True)
lines=result.stdout.splitlines();d['cancellationResponse']={'exitCode':result.returncode,'statusLine':next((x for x in lines if x.startswith('HTTP/')),''),'requestId':next((x for x in lines if x.lower().startswith('x-github-request-id:')),''),'stderr':result.stderr}
(p/'dispatch-5.json').write_text(json.dumps(d,indent=2)+'\n')
print(json.dumps({'runId':44073347507,'cancellationRequests':1,'requestedAtUtc':d['cancellationRequestedAtUtc'],'response':d['cancellationResponse']}))
raise SystemExit(result.returncode)
