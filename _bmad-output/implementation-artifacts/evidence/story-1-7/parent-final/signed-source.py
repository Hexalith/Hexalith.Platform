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

results = json.loads((logs / 'results.json').read_text())
environment.update(HEXALITH_PLATFORM_BUILDS_LIVE_SMOKE='1', HEXALITH_DAPR_HOME='/tmp/story-1-6-builds-toolchain', HEXALITH_ASPIRE_CLI='/tmp/story1-7-aspire-cli/aspire', HEXALITH_PLATFORM_BUILDS_SMOKE_LOG=str(logs/'builds-tenants-apphost.txt'))
run('builds-tenants-live', ['dotnet','tests/Hexalith.Platform.Composition.Tests/bin/Debug/net10.0/Hexalith.Platform.Composition.Tests.dll','-parallelMode','none','-method','*DomainTenantsStartsThroughTheRealBuildsEngineAsync'])
run('runtime-inventory-after-source', ['docker','ps','--format','{{.Names}}\t{{.Ports}}'])
print('FINAL SIGNED SOURCE CHECK PASSED', flush=True)
