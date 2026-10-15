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

results = json.loads((logs / 'results.json').read_text())[:-1]
paths = [path for path in json.loads(Path('/tmp/story-1-7-root-paths.json').read_text()) if not path.startswith('_bmad-output/implementation-artifacts/evidence/')]
run('source-whitespace', ['git', '-c', 'core.whitespace=blank-at-eol,blank-at-eof,space-before-tab,cr-at-eol', 'diff', 'HEAD', '--check', '--'] + paths)
for module in ['Hexalith.Builds', 'Hexalith.EventStore', 'Hexalith.Memories', 'Hexalith.Tenants']:
    run(module+'-whitespace', ['git', 'diff', 'HEAD', '--check'], root/'references'/module)
run('runtime-inventory-before-live', ['docker','ps','--format','{{.Names}}\t{{.Ports}}'])
environment.update(HEXALITH_PLATFORM_BUILDS_LIVE_SMOKE='1', HEXALITH_DAPR_HOME='/tmp/story-1-6-builds-toolchain', HEXALITH_ASPIRE_CLI='/tmp/story1-7-aspire-cli/aspire', HEXALITH_PLATFORM_SECURITY_EVIDENCE=str(logs/'security'))
run('security-live', ['dotnet','tests/Hexalith.Platform.Composition.Tests/bin/Debug/net10.0/Hexalith.Platform.Composition.Tests.dll','-parallelMode','none','-method','*LiveLocalDaprEnforcesInvocationAndDeclarativeDeadLetterDeliveryAsync'])
run('runtime-inventory-after-live', ['docker','ps','--format','{{.Names}}\t{{.Ports}}'])
print('FINAL CHECKS PASSED', flush=True)
