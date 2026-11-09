from pathlib import Path
import subprocess,os,json
root=Path('/tmp/story32-default-source-mode-probe-_o2d3b9r/repository');out=Path('/tmp/story32-default-source-mode-ci-shaped-preflight');assert not out.exists();out.mkdir()
env=os.environ.copy();env['NUGET_PACKAGES']='/tmp/story32-sdk42-upstream-catalog-package-cache';commands=[]
for label,args in [('restore',['dotnet','restore','Hexalith.Platform.slnx','-p:Configuration=Release','-p:UseHexalithProjectReferences=false','-p:HexalithBuildsPublishedToolRoot=/tmp/story32-builds447-local-qualified-artifacts/published/tools/net10.0/any']),('controls',['python3','-B','eng/test_source_sdk_propagation.py','--evidence',str(out/'controls'),'--published-tool-root','/tmp/story32-builds447-local-qualified-artifacts/published/tools/net10.0/any'])]:
 with (out/(label+'.stdout')).open('w') as stdout,(out/(label+'.stderr')).open('w') as stderr:r=subprocess.run(args,cwd=root,env=env,stdout=stdout,stderr=stderr,timeout=1200)
 commands.append({'label':label,'command':args,'exitCode':r.returncode});(out/'receipt.json').write_text(json.dumps({'commands':commands,'qualificationCredit':False},indent=2)+'\n');print(label,r.returncode,flush=True)
 if r.returncode:raise SystemExit(r.returncode)
