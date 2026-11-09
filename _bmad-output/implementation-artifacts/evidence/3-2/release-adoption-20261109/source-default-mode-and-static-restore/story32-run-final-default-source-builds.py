from pathlib import Path
import subprocess,sys,os,json,hashlib,datetime,xml.etree.ElementTree as ET
root=Path('/tmp/story32-default-source-mode-probe-_o2d3b9r/repository')
output=Path('/tmp/story32-default-source-mode-final-builds')
assert not output.exists();output.mkdir()
sys.path.insert(0,str(root/'eng'))
from prepare_builds_artifacts import source_state
from run_tuple_ci import snapshot_composition_test_outputs
start=source_state()
r={'sourceRevision':subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip(),'scope':'Plain Debug project and solution defaults from empty private package caches and absent outputs; no source, SDK, static restore or version override. Uncommitted isolated repair probe only.','sourceBefore':start,'candidateAcceptance':False,'qualificationCredit':False,'groups':[]}
for name,entry in [('project','tests/Hexalith.Platform.Composition.Tests/Hexalith.Platform.Composition.Tests.csproj'),('solution','Hexalith.Platform.slnx')]:
    artifacts=root/'artifacts'/('final-cold-default-'+name)
    cache=output/('private-cache-'+name)
    assert not artifacts.exists() and not cache.exists()
    cache.mkdir();assert not any(cache.iterdir())
    args=['dotnet','build',entry,'-c','Debug','-m:1','-nodeReuse:false','--disable-build-servers','--artifacts-path',str(artifacts),'-p:HexalithBuildsPublishedToolRoot=/tmp/story32-builds447-local-qualified-artifacts/published/tools/net10.0/any']
    env=os.environ.copy();env['NUGET_PACKAGES']=str(cache)
    group={'name':name,'command':args,'privateCache':str(cache),'cacheInitiallyEmpty':True,'outputInitiallyAbsent':True,'startedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat()}
    (output/'receipt.json').write_text(json.dumps(r,indent=2)+'\n')
    with (output/(name+'.stdout')).open('w') as stdout,(output/(name+'.stderr')).open('w') as stderr:
        result=subprocess.run(args,cwd=root,env=env,stdout=stdout,stderr=stderr,timeout=1200)
    group.update({'exitCode':result.returncode,'completedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat()})
    log=(output/(name+'.stdout')).read_text()
    group['zeroWarningsAndErrors']=result.returncode==0 and '0 Warning(s)' in log and '0 Error(s)' in log
    assembly=artifacts/'bin/Hexalith.Platform.Composition.Tests/debug/Hexalith.Platform.Composition.Tests.dll'
    if group['zeroWarningsAndErrors']:
        group['outputs']=snapshot_composition_test_outputs(assembly)
        deps=assembly.with_suffix('.deps.json');document=json.loads(deps.read_bytes())
        selected={k:v for k,v in document['libraries'].items() if k.startswith('Hexalith.EventStore.')}
        group['eventStoreDepsLibraries']=selected
        group['depsSha256']=hashlib.sha256(deps.read_bytes()).hexdigest()
        group['sdkCoherent']=all(k.endswith('/4.2.0') and v['type']=='project' for k,v in selected.items()) and 'Hexalith.EventStore.DomainService/4.2.0' in selected
        assert group['sdkCoherent'],selected
    r['groups'].append(group);r['sourceAfter']=source_state();r['sourceUnchanged']=start==r['sourceAfter']
    r['status']='running' if name=='project' and group['zeroWarningsAndErrors'] else ('passed' if group['zeroWarningsAndErrors'] and r['sourceUnchanged'] else 'failed')
    (output/'receipt.json').write_text(json.dumps(r,indent=2)+'\n')
    print(name,group['exitCode'],group['zeroWarningsAndErrors'],r['sourceUnchanged'],flush=True)
    if not group['zeroWarningsAndErrors'] or not r['sourceUnchanged']:raise SystemExit(1)
r['files']={f.name:{'sha256':hashlib.sha256(f.read_bytes()).hexdigest(),'bytes':f.stat().st_size} for f in sorted(output.iterdir()) if f.is_file() and f.name!='receipt.json'}
(output/'receipt.json').write_text(json.dumps(r,indent=2)+'\n')
print(output,flush=True)
