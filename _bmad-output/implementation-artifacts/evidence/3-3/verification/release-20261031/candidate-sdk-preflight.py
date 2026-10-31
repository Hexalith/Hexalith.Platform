"""Compile-only SDK candidate preflight; confers no delivered release acceptance."""
from pathlib import Path
import hashlib,json,subprocess
base=Path('/tmp/hexalith-story33-release-0_wjbwkj')
root=base/'Hexalith.EventStore'
feed=base/'preflight-feed'
feed.mkdir(exist_ok=True)
version='3.129.0-story33-preflight.1'
props=str(base/'Hexalith.Builds/Props/Directory.Packages.props')
record={'purpose':__doc__,'source':subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip(),'version':version,'published':False,'commands':[]}
evidence=Path(__file__).parent/'candidate-sdk-preflight.json'
try:
    for item in json.loads((root/'tools/release-packages.json').read_bytes())['packages']:
        cmd=['dotnet','pack',item['project'],'-c','Release','--output',str(feed),'-p:Version='+version,'-p:GeneratePackageOnBuild=false','-p:UseHexalithProjectReferences=false','-p:HexalithBuildPackageVersions1='+props,'-p:Hexalith1BuildPackageProps='+props,'-m:1','-nr:false','--disable-build-servers']
        print(json.dumps({'package':item['id'],'command':cmd}),flush=True)
        r=subprocess.run(cmd,cwd=root,timeout=300)
        record['commands'].append({'id':item['id'],'argv':cmd,'exitCode':r.returncode})
        if r.returncode: raise RuntimeError(item['id']+' candidate pack failed')
    record['packages']={x.name:hashlib.sha256(x.read_bytes()).hexdigest() for x in feed.glob('*.nupkg')}
    record['result']='passed'
finally:
    evidence.write_text(json.dumps(record,indent=2)+'\n')
