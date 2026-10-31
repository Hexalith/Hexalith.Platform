import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import time

platform = Path('/home/administrator/projects/hexalith/platform')
eventstore = platform.parent / 'eventstore'
root = Path(json.loads(Path('/tmp/ext-protection-loop10-active.json').read_text())['root'])
environment = dict(os.environ, HexalithCommonsRoot=str(eventstore / 'references/Hexalith.Commons'))
common = ['-c', 'Debug', '-nodeReuse:false', '-m:1', '-p:UseHexalithProjectReferences=true', '-p:GeneratePackageOnBuild=false',
          '-p:HexalithEventStoreRoot=' + str(eventstore), '-p:HexalithCommonsRoot=' + environment['HexalithCommonsRoot'],
          '-p:HexalithTenantsBasePath=' + str(eventstore / 'references/Hexalith.Tenants/src')]
records = []

def run(name, argv, cwd):
    log = root / (name + '.log')
    start = time.time()
    print('START ' + name, flush=True)
    with log.open('wb') as output:
        result = subprocess.run(argv, cwd=cwd, env=environment, stdout=output, stderr=subprocess.STDOUT)
    record = {'name': name, 'argv': argv, 'cwd': str(cwd), 'exitCode': result.returncode, 'elapsedSeconds': round(time.time()-start, 3),
              'environmentPins': {'HexalithCommonsRoot': environment['HexalithCommonsRoot']}, 'log': str(log), 'logSha256': hashlib.sha256(log.read_bytes()).hexdigest()}
    records.append(record)
    (root / 'commands.json').write_text(json.dumps(records, indent=2) + '\n')
    print('END ' + name + ' ' + str(result.returncode), flush=True)
    print('\n'.join(log.read_text(errors='replace').splitlines()[-12:]), flush=True)
    if result.returncode:
        raise SystemExit(result.returncode)

run('bootstrap-tests', ['python3', 'eng/ext-protection-1/test_bootstrap.py', '-v'], platform)
run('platform-build', ['dotnet', 'build', 'tests/Hexalith.Platform.Protection.Tests/Hexalith.Platform.Protection.Tests.csproj', *common], platform)
run('eventstore-build', ['dotnet', 'build', 'tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj', *common], eventstore)
for repo, name, assembly in [(platform, 'platform', 'Hexalith.Platform.Protection.Tests'), (eventstore, 'eventstore', 'Hexalith.EventStore.Server.Tests')]:
    source = repo / 'tests' / assembly / 'bin/Debug/net10.0'
    dest = root / 'runtime' / name
    shutil.copytree(source, dest)
    files = [{'path': str(p.relative_to(dest)), 'sha256': hashlib.sha256(p.read_bytes()).hexdigest()} for p in sorted(dest.rglob('*')) if p.is_file()]
    (root / (name + '-runtime.json')).write_text(json.dumps(files, indent=2) + '\n')
platform_classes = ['OpenBaoErasureRootStoreTests', 'ErasureRootCustodyTests', 'CustodyOccurrenceProviderTests', 'ErasureRootUnissuedReplacementTests', 'ErasureRootLoop8AdmissionTests', 'ErasureRootReceivingTests']
eventstore_classes = ['InteractionOccurrenceRegistryActorTests', 'PublishedTrustReplacementAuthorityTests', 'PublishedTrustOwnerStateAuthorityTests', 'PublishedTrustDeletionConsumptionAuthorityTests', 'GovernanceScopeGuardTests']
for name, assembly, classes, namespace in [('platform', 'Hexalith.Platform.Protection.Tests', platform_classes, 'Hexalith.Platform.Protection.Tests'),
                                           ('eventstore', 'Hexalith.EventStore.Server.Tests', eventstore_classes, 'Hexalith.EventStore.Server.Tests.Security')]:
    argv = ['dotnet', str(root / 'runtime' / name / (assembly + '.dll')), '-parallel', 'none', '-xml', str(root / (name + '-tests.xml'))]
    for selected in classes:
        argv += ['-class', namespace + '.' + selected]
    run(name + '-tests', argv, platform if name == 'platform' else eventstore)
print('COMPLETE ' + str(root), flush=True)
