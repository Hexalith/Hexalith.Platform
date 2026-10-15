import json
import os
from pathlib import Path
import subprocess
import time

root = Path('/home/administrator/projects/hexalith/platform')
logs = root / '_bmad-output/implementation-artifacts/evidence/story-1-7/parent-final'
logs.mkdir(parents=True, exist_ok=True)
environment = os.environ.copy()
for key in ['HEXALITH_PLATFORM_LIVE_SMOKE', 'HEXALITH_PLATFORM_BUILDS_LIVE_SMOKE']:
    environment.pop(key, None)
results = []

def run(name, args, cwd=root):
    print('START ' + name, flush=True)
    start = time.monotonic()
    path = logs / (name + '.txt')
    with path.open('w') as output:
        result = subprocess.run(args, cwd=cwd, env=environment, stdout=output, stderr=subprocess.STDOUT)
    record = {'check': name, 'exitCode': result.returncode, 'seconds': round(time.monotonic()-start, 3), 'command': args, 'cwd': str(cwd), 'log': path.name}
    results.append(record)
    (logs / 'results.json').write_text(json.dumps(results, indent=2))
    print(json.dumps(record), flush=True)
    if result.returncode:
        print('\n'.join(path.read_text().splitlines()[-65:]), flush=True)
        raise SystemExit(result.returncode)

platform = 'tests/Hexalith.Platform.Composition.Tests'
builds = 'references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests'
owner = root / 'references/Hexalith.EventStore'
run('solution-build', ['dotnet','build','Hexalith.Platform.slnx','-c','Debug','-m:1','--nologo'])
run('platform-build', ['dotnet','build',platform+'/Hexalith.Platform.Composition.Tests.csproj','-c','Debug','-m:1','--nologo'])
run('platform-tests', ['dotnet',platform+'/bin/Debug/net10.0/Hexalith.Platform.Composition.Tests.dll','-parallelMode','none'])
run('builds-build', ['dotnet','build',builds+'/Hexalith.Builds.Module.Tests.csproj','-c','Debug','-m:1','--nologo'])
run('builds-tests', ['dotnet',builds+'/bin/Debug/net10.0/Hexalith.Builds.Module.Tests.dll','-parallelMode','none','-class','*PlatformManifestValidationTests','-class','*WorkspaceAppHostTests'])
for project, filters in [
    ('Hexalith.EventStore.AppHost.Tests',['*DomainModuleAspireExtensionTests']),
    ('Hexalith.EventStore.Server.Tests',['*ProjectionSubscriptionMetadataTests','*EventStoreWorkloadRateLimitTests','*ProjectionChangeNotifierOptionsTests'])
]:
    run(project+'-build', ['dotnet','build',f'tests/{project}/{project}.csproj','-c','Debug','-m:1','--nologo'], owner)
    run(project+'-tests', ['dotnet',f'tests/{project}/bin/Debug/net10.0/{project}.dll','-parallelMode','none'] + [argument for pattern in filters for argument in ['-class',pattern]], owner)
run('helper-tests',['python3','eng/test_verify_platform_pins.py'])
run('timesheets-provenance',['python3','eng/timesheets-deployment/test_prepare.py'])
run('declarations',['python3','scripts/test-reference-module-declarations.py'])
run('local-scaffold',['bash','eng/verify-agents-host.sh','--mode','LocalScaffold','--artifacts-path','/tmp/story1-7-parent-scaffold-artifacts'])
run('architecture-lint',['python3','.agents/skills/bmad-architecture/scripts/lint_spine.py','--workspace','_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27'])
run('python-compile',['python3','-m','py_compile','eng/test-local-dapr-security.py','eng/verify-platform-pins.py','eng/test_verify_platform_pins.py'])
run('source-whitespace',['git','diff','HEAD','--check','--','.',':(exclude)_bmad-output/implementation-artifacts/evidence/',':(exclude)Hexalith.Platform.slnx',':(exclude)eng/ext-secrets-1/'])
for module in ['Hexalith.Builds','Hexalith.EventStore','Hexalith.Memories','Hexalith.Tenants']:
    run(module+'-whitespace',['git','diff','HEAD','--check'],root/'references'/module)
run('runtime-inventory',['docker','ps','--format','{{.Names}}\t{{.Ports}}'])
print('FINAL NON-LIVE CHECKS PASSED', flush=True)
