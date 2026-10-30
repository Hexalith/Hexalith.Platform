from pathlib import Path
import subprocess,os,json,xml.etree.ElementTree as E,hashlib
platform=Path('/home/administrator/hexalith-protection-worktrees/ext-protection-loop3-b8fd390531/platform');eventstore=platform.parent/'eventstore';out=Path('/tmp/ext-protection-loop3-evidence');commands=[]
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
for name,project,assembly,prior in [
 ('eventstore-final','tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj','tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll','eventstore-final-tests-verified.xml'),
 ('contracts-anchor-final','tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj','tests/Hexalith.EventStore.Contracts.Tests/bin/Debug/net10.0/Hexalith.EventStore.Contracts.Tests.dll','contracts-anchor-final-tests-verified.xml')]:
 build(name+'-build',project,eventstore)
 classes=sorted({t.attrib['type'] for t in E.parse(base/prior).iter('test')})
 args=['dotnet',assembly,*[a for c in classes for a in ('-class',c)],'-noColor','-result-xml',str(out/(name+'.xml'))]
 run(name+'-tests',args,eventstore)
build('payload-standalone-aot-build','src/Hexalith.EventStore.PayloadProtection/Hexalith.EventStore.PayloadProtection.csproj',eventstore,['-p:EnableAotAnalyzer=true','-p:EnableTrimAnalyzer=true'])
build('payload-final-build','tests/Hexalith.EventStore.PayloadProtection.Tests/Hexalith.EventStore.PayloadProtection.Tests.csproj',eventstore)
run('payload-final-tests',['dotnet','tests/Hexalith.EventStore.PayloadProtection.Tests/bin/Debug/net10.0/Hexalith.EventStore.PayloadProtection.Tests.dll','-noColor','-result-xml',str(out/'payload-final.xml')],eventstore)
for short in ['EventStoreHost','Protection.Qualification.Executor','Protection.Qualification.Observer','Protection.Qualification.Enrollment']:
 project='src/Hexalith.Platform.'+short+'/Hexalith.Platform.'+short+'.csproj'
 build(short.lower().replace('.','-')+'-final-build',project,platform)
