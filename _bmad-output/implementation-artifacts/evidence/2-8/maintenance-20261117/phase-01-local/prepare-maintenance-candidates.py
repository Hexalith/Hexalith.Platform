"""Create genuine unpublished maintenance candidates and analyze their locked ranges."""
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import shutil
import subprocess
import time

ROOT = Path('/home/administrator/projects/hexalith/platform')
AREA = ROOT/'artifacts/2-8-maintenance-20261117'
OUT = AREA/'maintenance-candidates-01'
OUT.mkdir(exist_ok=False)
SCRATCH = Path(os.environ['TMPDIR'])/'candidates'
SCRATCH.mkdir()
NODE = Path('/home/administrator/.nvm/versions/node/v24.22.1/bin/node')
ENV = dict(os.environ, PATH=str(NODE.parent)+':'+os.environ['PATH'], PYTHONDONTWRITEBYTECODE='1')
RESULTS = []
RECORD = {'remoteMutations':False,'classification':'unpublished source proposals and locked version analysis only','candidates':[]}

def run(label,args,cwd=ROOT,env=ENV,timeout=300,payload=None):
    start=time.monotonic()
    with (OUT/(label+'.log')).open('w') as log:
        result=subprocess.run(args,cwd=cwd,env=env,input=payload,text=True,stdout=log,stderr=subprocess.STDOUT,timeout=timeout,check=False)
    RESULTS.append({'label':label,'exitCode':result.returncode,'seconds':round(time.monotonic()-start,3)})
    print(json.dumps(RESULTS[-1]),flush=True)
    if result.returncode:raise RuntimeError(label+' failed; see retained log')

def git(root,*args):
    return subprocess.check_output(['git','-c','submodule.recurse=false','-C',str(root),*args],env=ENV,text=True).strip()

try:
    owner=SCRATCH/'Hexalith.CiCd'
    run('clone-owner',['git','clone','--shared','--no-checkout',str(ROOT/'references/Hexalith.CiCd'),str(owner)])
    git(owner,'remote','set-url','origin','https://github.com/Hexalith/Hexalith.CiCd.git')
    run('import-owner',['git','fetch',str(AREA/'owner-candidate-01/cicd-candidate.bundle'),'refs/heads/fix/story-2-8-maintenance-lanes:refs/heads/fix/story-2-8-maintenance-lanes'],owner)
    run('checkout-owner',['git','-c','submodule.recurse=false','checkout','fix/story-2-8-maintenance-lanes'],owner)
    owner_revision=git(owner,'rev-parse','HEAD')
    spec=importlib.util.spec_from_file_location('owner_maintenance',owner/'tools/maintenance.py')
    tool=importlib.util.module_from_spec(spec);spec.loader.exec_module(tool)
    configurations=[('Builds',ROOT/'references/Hexalith.Builds','3c8096180ecf2aa539ac454815efbfceb8a92822','4.44.x','',
                     'fix(deps): support the patched 4.44 maintenance catalog\n'),
                    ('Platform',ROOT,'0f4e27f1f78310bd731c10c19dc51bb1e0345d44','1.7.x','conventionalcommits',
                     'fix(ci): qualify the 1.7 maintenance source lane\n')]
    for name,source,base,branch,preset,message in configurations:
        repository=SCRATCH/('Hexalith.'+name)
        run('clone-'+name,['git','clone','--shared','--no-checkout',str(source),str(repository)])
        git(repository,'remote','set-url','origin','https://github.com/Hexalith/Hexalith.'+name+'.git')
        run('branch-'+name,['git','-c','submodule.recurse=false','checkout','-b',branch,base],repository)
        # All workflow changes come from the committed owning policy, not the product projection.
        tool.render(owner,owner_revision,repository,base,'Hexalith/Hexalith.'+name,'maintenance',OUT/(name+'-owned-delivery'),apply=True)
        projection=AREA/(name.lower()+'-projection')
        changed=[]
        for source_file in sorted(projection.rglob('*')):
            if not source_file.is_file():continue
            relative=source_file.relative_to(projection)
            if '.github' in relative.parts or relative.name=='inputs.json':continue
            target=repository/relative;target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(source_file,target);changed.append(str(relative))
        run('npm-ci-'+name,['npm','ci','--ignore-scripts','--no-fund'],repository,timeout=300)
        run('npm-signatures-'+name,['npm','audit','signatures'],repository,timeout=300)
        (OUT/(name+'-commit-message.txt')).write_text(message)
        run('commitlint-'+name,[str(NODE),str(repository/'node_modules/@commitlint/cli/cli.js'),'--config',str(repository/'commitlint.config.mjs'),'--color=false'],repository,payload=message)
        run('stage-'+name,['git','add','--',*changed,'.github/workflows'],repository)
        run('diff-check-'+name,['git','diff','--cached','--check'],repository)
        run('commit-'+name,['git','commit','-F',str(OUT/(name+'-commit-message.txt'))],repository)
        head=git(repository,'rev-parse','HEAD')
        with (OUT/(name+'.diff')).open('wb') as stream:subprocess.run(['git','-C',str(repository),'diff','--binary',base,head],stdout=stream,check=True)
        if name=='Builds':
            run('actual-builds-guard-tests',['python3','-B','Tools/test-maintenance-release-source.py'],repository)
            run('actual-dispatcher-syntax',['bash','-n','Tools/verify-g4-release-source.sh'],repository)
        run('version-'+name,[str(NODE),str(AREA/'analyze-maintenance-version.mjs'),str(repository),base,branch,str(OUT/(name+'-version.json')),preset],repository)
        run('bundle-'+name,['git','bundle','create',str(OUT/(name+'.bundle')),'refs/heads/'+branch,'^'+base],repository)
        RECORD['candidates'].append({'repository':'Hexalith/Hexalith.'+name,'base':base,'head':head,'tree':git(repository,'rev-parse','HEAD^{tree}'),'branch':branch,
            'packageLockSha256':hashlib.sha256((repository/'package-lock.json').read_bytes()).hexdigest(),
            'state':'security maintenance source proposal' if name=='Builds' else 'support-only proposal; original tuple binding retained until genuine new publication exists'})
    RECORD['status']='verified-local-proposals'
except BaseException as error:
    RECORD.update(status='failed',reason=type(error).__name__+': '+str(error));raise
finally:
    RECORD['commands']=RESULTS;(OUT/'result.json').write_text(json.dumps(RECORD,indent=2)+'\n')
