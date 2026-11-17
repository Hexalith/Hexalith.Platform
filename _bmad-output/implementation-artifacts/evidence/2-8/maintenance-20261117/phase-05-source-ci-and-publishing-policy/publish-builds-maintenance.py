"""Validate the intended publication with the delivered guard, then dispatch once.

The local guard check validates intended dispatch/source metadata. It supplies no
executing-job or release qualification credit. The real workflow repeats its
normal checks with GitHub's actual context before any publication.
"""
import datetime, importlib.util, json, os, pathlib, subprocess
ROOT=pathlib.Path('/home/administrator/projects/hexalith/platform')
AREA=ROOT/'artifacts/2-8-maintenance-20261117'
OUT=AREA/'builds-publication-01';OUT.mkdir(exist_ok=False)
REPO=pathlib.Path(os.environ['TMPDIR'])/'Hexalith.Builds'
SOURCE='979cfd3e2c4145be516fbd833dacfc6b8e8fdce5'
CI=44129245136
R={'source':SOURCE,'expectedSourceCi':CI,'sourceCiAttempt':1,'publicationDispatches':0,'commands':[],
   'classification':'intended-dispatch metadata preflight; actual workflow must qualify publication'}
def run(label,args,cwd=None):
 p=subprocess.run(args,cwd=cwd or REPO,capture_output=True,text=True,timeout=90)
 (OUT/(label+'.log')).write_text(p.stdout+p.stderr);R['commands'].append({'label':label,'exitCode':p.returncode});print(label,p.returncode,flush=True)
 if p.returncode:raise RuntimeError(label+' failed; no automatic retry')
 return p.stdout.strip()
calls=[]
def api(path):
 label='read-'+str(len(calls)+1).zfill(2)
 value=json.loads(run(label,['gh','api',path]));(OUT/(label+'.json')).write_text(json.dumps(value,indent=2)+'\n');calls.append({'label':label,'path':path});return value
try:
 run('clone',['git','clone','--shared','--no-checkout',str(ROOT/'references/Hexalith.Builds'),str(REPO)],ROOT)
 run('origin',['git','remote','set-url','origin','https://github.com/Hexalith/Hexalith.Builds.git'])
 run('fetch-source',['git','fetch','origin',SOURCE])
 run('checkout',['git','checkout','--detach',SOURCE]);assert run('source',['git','rev-parse','HEAD'])==SOURCE
 module=importlib.util.spec_from_file_location('delivered_maintenance_guard',REPO/'Tools/verify-maintenance-release-source.py')
 guard=importlib.util.module_from_spec(module);module.loader.exec_module(guard)
 def ancestor(base,source):
  return subprocess.run(['git','merge-base','--is-ancestor',base,source],cwd=REPO,capture_output=True).returncode==0
 intent={'REPOSITORY':'Hexalith/Hexalith.Builds','DISPATCH_REF':'refs/heads/4.44.x','DISPATCH_SHA':SOURCE,
         'GITHUB_EVENT_NAME':'workflow_dispatch','GITHUB_WORKFLOW_REF':'Hexalith/Hexalith.Builds/.github/workflows/build-release.yml@refs/heads/4.44.x',
         'GITHUB_WORKFLOW_SHA':SOURCE,'GITHUB_SHA':SOURCE,'EXPECTED_CI_RUN_ID':str(CI),'EXPECTED_CI_RUN_ATTEMPT':'1'}
 receipt={'classification':'intended dispatch only; not an executing GitHub job or release qualification'}
 selected=guard.verify(api,ancestor,intent,receipt);assert selected==(CI,1)
 (OUT/'intended-source-check.json').write_text(json.dumps(receipt,indent=2)+'\n')
 publisher=api('repos/Hexalith/Hexalith.Builds/actions/workflows/build-release.yml')
 assert publisher['id']==152656352 and publisher['path']=='.github/workflows/build-release.yml' and publisher['state']=='active'
 path='repos/Hexalith/Hexalith.Builds/contents/.github/workflows/build-release.yml'
 expected=api(path+'?ref=7afc4981e21bc2fcce687199a3fd2c61fe87336e')
 current=api(path+'?ref=main');assert current['sha']==expected['sha']
 prior=api('repos/Hexalith/Hexalith.Builds/actions/workflows/build-release.yml/runs?head_sha='+SOURCE+'&event=workflow_dispatch&per_page=100')
 assert prior['total_count']==0 and prior['workflow_runs']==[],'An existing publication requires observation, not another dispatch'
 # No GitHub run/job identity is invented. The real event below supplies it.
 (OUT/'request.json').write_text(json.dumps({'ref':'4.44.x'},indent=2)+'\n')
 R['publicationDispatches']=1;R['requestedAtUtc']=datetime.datetime.now(datetime.timezone.utc).isoformat()
 (OUT/'result.json').write_text(json.dumps(R,indent=2)+'\n')
 response=run('normal-publication-dispatch',['gh','api','--method','POST','--include','repos/Hexalith/Hexalith.Builds/actions/workflows/build-release.yml/dispatches','--input',str(OUT/'request.json')])
 body=response.replace('\r\n','\n').split('\n\n',1)[-1].strip()
 if body.startswith('{'):
  payload=json.loads(body)
  if type(payload.get('workflow_run_id')) is int:R['releaseRunId']=payload['workflow_run_id']
 R['status']='one-normal-maintenance-publication-dispatched'
 print(json.dumps({key:R[key] for key in ('source','publicationDispatches','status')}))
except BaseException as e:R.update(status='failed',reason=type(e).__name__+': '+str(e));raise
finally:
 R['apiReads']=calls;(OUT/'result.json').write_text(json.dumps(R,indent=2)+'\n')
