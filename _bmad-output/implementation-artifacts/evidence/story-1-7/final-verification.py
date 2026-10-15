import os,subprocess,json,time
from pathlib import Path
root=Path('/home/administrator/projects/hexalith/platform')
logroot=Path('/tmp')
env=os.environ.copy()
live=env|{'HEXALITH_PLATFORM_BUILDS_LIVE_SMOKE':'1','HEXALITH_DAPR_HOME':'/tmp/story-1-6-builds-toolchain','HEXALITH_ASPIRE_CLI':'/tmp/story1-7-aspire-cli/aspire'}
def run(name,args,environment=env):
    path=logroot/('story1-7-'+name+'.log')
    print('START '+name,flush=True)
    start=time.monotonic()
    with path.open('w') as log:
        result=subprocess.run(args,cwd=root,env=environment,stdout=log,stderr=subprocess.STDOUT)
    duration=round(time.monotonic()-start,3)
    print(json.dumps({'check':name,'exitCode':result.returncode,'seconds':duration,'log':str(path)}),flush=True)
    if result.returncode:
        print('\n'.join(path.read_text().splitlines()[-65:]),flush=True)
        raise SystemExit(result.returncode)
    return result
platform='tests/Hexalith.Platform.Composition.Tests/bin/Debug/net10.0/Hexalith.Platform.Composition.Tests.dll'
run('security-live',['dotnet',platform,'-method','*LiveLocalDaprEnforcesInvocationAndDeclarativeDeadLetterDeliveryAsync'],live|{'HEXALITH_PLATFORM_SECURITY_EVIDENCE':'/tmp/story1-7-security-evidence'})
run('builds-eventstore-live',['dotnet',platform,'-method','*TechnicalEventStoreStartsThroughTheRealBuildsEngineAsync'],live|{'HEXALITH_PLATFORM_BUILDS_SMOKE_LOG':'/tmp/story1-7-builds-eventstore-apphost.log'})
run('builds-build',['dotnet','build','references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests/Hexalith.Builds.Module.Tests.csproj','-c','Debug','-m:1','--nologo'])
run('builds-tests',['dotnet','references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests/bin/Debug/net10.0/Hexalith.Builds.Module.Tests.dll','-class','*PlatformManifestValidationTests','-class','*WorkspaceAppHostTests'])
run('memories-eventstore-build',['dotnet','build','references/Hexalith.Memories/tests/Hexalith.Memories.EventStore.Tests/Hexalith.Memories.EventStore.Tests.csproj','-c','Debug','-m:1','--nologo'])
run('memories-eventstore-tests',['dotnet','references/Hexalith.Memories/tests/Hexalith.Memories.EventStore.Tests/bin/Debug/net10.0/Hexalith.Memories.EventStore.Tests.dll','-class','*EventIngestionControllerTests','-class','*ResolveConfiguredTopicTests'])
run('memories-server-build',['dotnet','build','references/Hexalith.Memories/tests/Hexalith.Memories.Server.Tests/Hexalith.Memories.Server.Tests.csproj','-c','Debug','-m:1','--nologo'])
run('memories-server-tests',['dotnet','references/Hexalith.Memories/tests/Hexalith.Memories.Server.Tests/bin/Debug/net10.0/Hexalith.Memories.Server.Tests.dll','-class','*EmbeddingClientConfigTests','-class','*EmbeddingClientTests','-class','*HexalithMemoriesAspireSecretStoreTests'])
run('solution-build',['dotnet','build','Hexalith.Platform.slnx','-c','Debug','-m:1','--nologo'])
run('build',['dotnet','build','tests/Hexalith.Platform.Composition.Tests/Hexalith.Platform.Composition.Tests.csproj','-c','Debug','-m:1','--nologo'])
run('platform-tests',['dotnet',platform,'-parallelMode','none'])
run('local-scaffold',['bash','eng/verify-agents-host.sh','--mode','LocalScaffold','--artifacts-path','/tmp/story1-7-scaffold-artifacts'])
run('declarations',['python3','scripts/test-reference-module-declarations.py'])
run('architecture-lint',['python3','.agents/skills/bmad-architecture/scripts/lint_spine.py','--workspace','_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27'])
run('python-compile',['python3','-m','py_compile','eng/test-local-dapr-security.py','eng/verify-platform-pins.py','scripts/test-reference-module-declarations.py'])
for directory in ['.','references/Hexalith.Builds','references/Hexalith.EventStore','references/Hexalith.Memories','references/Hexalith.Tenants']:
    run('diff-check-'+Path(directory).name,['git','-C',directory,'diff','--check'])
run('cleanup',['docker','ps','--format','{{.Names}}'])
print('ALL FINAL SEQUENTIAL CHECKS PASSED',flush=True)
