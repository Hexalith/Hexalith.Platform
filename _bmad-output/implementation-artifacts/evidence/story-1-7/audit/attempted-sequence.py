import os,subprocess,json,time
from pathlib import Path
root=Path('/home/administrator/projects/hexalith/platform')
env=os.environ.copy()
live=env|{'HEXALITH_PLATFORM_BUILDS_LIVE_SMOKE':'1','HEXALITH_DAPR_HOME':'/tmp/story-1-6-builds-toolchain','HEXALITH_ASPIRE_CLI':'/tmp/story1-7-aspire-cli/aspire'}
def run(name,args,environment=env):
 p=Path('/tmp/story1-7-audit-'+name+'.log')
 print('START '+name,flush=True)
 start=time.monotonic()
 with p.open('w') as log:
  result=subprocess.run(args,cwd=root,env=environment,stdout=log,stderr=subprocess.STDOUT)
 print(json.dumps({'check':name,'exitCode':result.returncode,'seconds':round(time.monotonic()-start,3),'log':str(p)}),flush=True)
 if result.returncode:
  print('\n'.join(p.read_text().splitlines()[-70:]),flush=True)
  raise SystemExit(result.returncode)
platform='tests/Hexalith.Platform.Composition.Tests/bin/Debug/net10.0/Hexalith.Platform.Composition.Tests.dll'
run('platform-tests',['dotnet',platform,'-parallelMode','none'],env|{'HEXALITH_PLATFORM_CLI_EVIDENCE':'/tmp/story1-7-cli-subscriptions.json'})
run('builds-build',['dotnet','build','references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests/Hexalith.Builds.Module.Tests.csproj','-c','Debug','-m:1','--nologo'])
run('builds-tests',['dotnet','references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests/bin/Debug/net10.0/Hexalith.Builds.Module.Tests.dll','-class','*PlatformManifestValidationTests','-class','*WorkspaceAppHostTests'])
run('declarations',['python3','scripts/test-reference-module-declarations.py'])
run('security-live',['dotnet',platform,'-method','*LiveLocalDaprEnforcesInvocationAndDeclarativeDeadLetterDeliveryAsync'],live|{'HEXALITH_PLATFORM_SECURITY_EVIDENCE':'/tmp/story1-7-audit-security-evidence'})
run('builds-tenants-live',['dotnet',platform,'-method','*DomainTenantsStartsThroughTheRealBuildsEngineAsync'],live|{'HEXALITH_PLATFORM_BUILDS_SMOKE_LOG':'/tmp/story1-7-audit-builds-tenants-apphost.log'})
run('architecture-lint',['python3','.agents/skills/bmad-architecture/scripts/lint_spine.py','--workspace','_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27'])
run('python-compile',['python3','-m','py_compile','eng/test-local-dapr-security.py','eng/verify-platform-pins.py'])
for d in ['.','references/Hexalith.Builds','references/Hexalith.EventStore','references/Hexalith.Memories','references/Hexalith.Tenants']:
 run('diff-check-'+Path(d).name,['git','-C',d,'diff','HEAD','--check'])
run('cleanup',['docker','ps','--format','{{.Names}}'])
print('ALL FOCUSED AUDIT CHECKS PASSED',flush=True)
