from pathlib import Path
import subprocess,os,json,xml.etree.ElementTree as E,hashlib
platform=Path('/home/administrator/hexalith-protection-worktrees/ext-protection-loop3-b8fd390531/platform');eventstore=platform.parent/'eventstore';out=Path('/tmp/ext-protection-loop3-evidence');commands=json.loads((out/'final-project-commands.json').read_text())['commands']
env=dict(os.environ,HexalithCommonsRoot='/home/administrator/projects/hexalith/eventstore/references/Hexalith.Commons',HexalithTenantsBasePath='/home/administrator/projects/hexalith/eventstore/references/Hexalith.Tenants/src')
flags=['-c','Debug','-p:UseHexalithProjectReferences=true','-p:HexalithEventStoreRoot='+str(eventstore),'-p:HexalithCommonsRoot='+env['HexalithCommonsRoot'],'-p:HexalithTenantsBasePath='+env['HexalithTenantsBasePath'],'-m:1','-nodeReuse:false']
def run(name,args,cwd):
 with (out/(name+'.log')).open('w') as log:r=subprocess.run(args,cwd=cwd,env=env,stdout=log,stderr=subprocess.STDOUT)
 commands.append({'name':name,'argv':args,'cwd':str(cwd),'exitCode':r.returncode,'logSha256':hashlib.sha256((out/(name+'.log')).read_bytes()).hexdigest()})
 (out/'final-project-commands.json').write_text(json.dumps({'environment':{k:env[k] for k in ('HexalithCommonsRoot','HexalithTenantsBasePath')},'commands':commands},indent=2)+'\n')
 print(name,r.returncode,flush=True)
 if r.returncode:raise SystemExit(r.returncode)
def build(name,project,cwd,extra=[]):run(name,['dotnet','build',project,*flags,*extra],cwd)
base=platform/'docs/implementation/ext-protection-1-loop2-2026-10-29/reader'
build('payload-standalone-aot-scoped-build','src/Hexalith.EventStore.PayloadProtection/Hexalith.EventStore.PayloadProtection.csproj',eventstore)
build('payload-final-build','tests/Hexalith.EventStore.PayloadProtection.Tests/Hexalith.EventStore.PayloadProtection.Tests.csproj',eventstore)
run('payload-final-tests',['dotnet','tests/Hexalith.EventStore.PayloadProtection.Tests/bin/Debug/net10.0/Hexalith.EventStore.PayloadProtection.Tests.dll','-noColor','-result-xml',str(out/'payload-final.xml')],eventstore)
for short in ['EventStoreHost','Protection.Qualification.Executor','Protection.Qualification.Observer','Protection.Qualification.Enrollment']:
 project='src/Hexalith.Platform.'+short+'/Hexalith.Platform.'+short+'.csproj'
 build(short.lower().replace('.','-')+'-final-build',project,platform)
