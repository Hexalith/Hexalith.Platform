"""Independent handback verification; no build, test, review cycle or native mutation."""
from pathlib import Path
import datetime as dt
import hashlib
import json
import re
import runpy
import subprocess
import time
import xml.etree.ElementTree as ET

HERE = Path(__file__).resolve().parent
OUT = HERE.parent
ROOT = OUT.parents[3]
started = dt.datetime.now(dt.timezone.utc)
tick = time.monotonic()


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def document(path):
    return json.loads(path.read_bytes())


def require(value, message):
    if not value:
        raise RuntimeError(message)


manifest = document(OUT / 'artifact-manifest.json')['files']
listed = {entry['path'] for entry in manifest}
observed = {path.relative_to(OUT).as_posix() for path in OUT.rglob('*') if path.is_file()
            and HERE not in path.parents and path.name != 'artifact-manifest.json'}
require(listed == observed, 'Implementation packet file set changed')
for entry in manifest:
    require(digest(OUT / entry['path']) == entry['sha256'], 'Implementation artifact hash changed: ' + entry['path'])

before = document(OUT / 'preservation-before.json')
for name, expected in before['historicalFiles'].items():
    require(digest(ROOT / name) == expected, 'Historical evidence changed: ' + name)
spec_path = ROOT / '_bmad-output/implementation-artifacts/spec-ext-protection-1-production-engine.md'
spec = spec_path.read_bytes()
spec_before = (OUT / 'spec-before.md').read_bytes()
require(digest(OUT / 'spec-before.md') == before['specSha256'], 'Initial spec snapshot changed')
require(spec.startswith(spec_before), 'Initial spec prefix changed')
frozen = re.search(rb'<frozen-after-approval\b[^>]*>.*?</frozen-after-approval>', spec_before, re.S)
require(frozen is not None and frozen.group() in spec, 'Frozen intent changed')
for exact in (b"status: 'in-progress'", b'review_loop_iteration: 10',
              b"baseline_commit: '231b0f2b57e96984a919ac1cdce97cc36df18542'",
              b"eventstore_baseline_commit: 'a62241679854fa9c359c0f552114a42ca0f6a638'"):
    require(exact in spec.split(b'---', 2)[1], 'Protected frontmatter changed')

freeze = document(OUT / 'freeze.json')
runtime = Path(freeze['runtime'])
runtime_manifest = runtime / 'runtime-manifest.json'
require(digest(runtime_manifest) == freeze['manifestSha256'] == digest(OUT / 'runtime-manifest.json'), 'Runtime manifest changed')
runtime_entries = document(runtime_manifest)['files']
require({entry['path'] for entry in runtime_entries} ==
        {path.relative_to(runtime).as_posix() for path in runtime.rglob('*') if path.is_file() and path != runtime_manifest},
        'Runtime closure changed')
require(not any(path.is_symlink() for path in runtime.rglob('*')), 'Runtime symlink found')
for entry in runtime_entries:
    require(digest(runtime / entry['path']) == entry['sha256'], 'Runtime bytes changed: ' + entry['path'])
source_manifest = document(OUT / 'source-manifest.json')
require(digest(OUT / 'source-manifest.json') == freeze['sourceManifestSha256'], 'Source manifest changed')
current_changes = []
for entry in source_manifest['files']:
    relative = Path(entry['repository']) / entry['path']
    require(digest(Path(source_manifest['frozenSourceRoot']) / relative) == entry['sha256'], 'Frozen source changed: ' + str(relative))
    current = Path(source_manifest['roots'][entry['repository']]) / entry['path']
    if not current.is_file() or digest(current) != entry['sha256']:
        current_changes.append(relative.as_posix())
verification = document(OUT / 'verification.json')
for entry in verification['changedFiles']:
    require(digest(ROOT / entry['path']) == entry['sha256'], 'Task source changed after verification: ' + entry['path'])
baseline = document(OUT / 'implementation-baseline.json')
protected = ('tests/Hexalith.Platform.Protection.Tests/OpenBaoErasureRootIntegrationTests.cs',
             'tests/Hexalith.Platform.Protection.Tests/OpenBaoNativeCommandChannel.cs',
             'deploy/erasure-root/custody-policy.hcl')
for name in protected:
    require(digest(ROOT / name) == next(entry['sha256'] for entry in baseline['scopedBefore'] if entry['path'] == name), 'Protected native source changed')

commands = sorted((document(path) for path in OUT.glob('*.command.json')), key=lambda value: value['startedUtc'])
for command in commands:
    require(digest(OUT / command['log']) == command['logSha256'], 'Command log changed')
    require(command['startedUtc'] <= command['finishedUtc'], 'Invalid command interval')
overlaps = [[left['label'], right['label']] for index, left in enumerate(commands)
            for right in commands[index + 1:] if right['startedUtc'] < left['finishedUtc']]
require(not overlaps, 'Recorded build/test/native command overlap')
build = next(command for command in commands if command['label'] == 'build-protection')
require(build['exitCode'] == 0 and all(flag in build['argv'] for flag in
        ('Debug', '-m:1', '-nodeReuse:false', '-p:UseHexalithProjectReferences=true', '-p:GeneratePackageOnBuild=false',
         '-p:HexalithEventStoreRoot=/home/administrator/projects/hexalith/eventstore',
         '-p:HexalithCommonsRoot=/home/administrator/projects/hexalith/eventstore/references/Hexalith.Commons',
         '-p:HexalithTenantsBasePath=/home/administrator/projects/hexalith/eventstore/references/Hexalith.Tenants/src')),
        'Required build settings missing')
build_text = (OUT / build['log']).read_text()
require('0 Warning(s)' in build_text and '0 Error(s)' in build_text, 'Build diagnostics not zero')
require(next(command for command in commands if command['label'] == 'bootstrap-prefixed')['exitCode'] == 1,
        'Original regression failure record missing')
components = ET.parse(OUT / 'transport-components.xml').getroot()
component_counts = {key: sum(int(value.get(key, '0')) for value in components.iter('assembly'))
                    for key in ('total', 'passed', 'failed', 'skipped', 'errors')}
require(component_counts == {'total': 44, 'passed': 44, 'failed': 0, 'skipped': 0, 'errors': 0}, 'Unexpected component results')

shipped = runpy.run_path(str(ROOT / 'deploy/erasure-root/bootstrap.py'))
native = []
for name in ('policy-diagnosis.result.json', 'policy-diagnosis-quoted.result.json',
             'native-bootstrap.result.json', 'native-five.result.json'):
    result = document(OUT / name)
    require(result['failure'] is None and result['cleanup'] ==
            {'complete': True, 'containerAbsent': True, 'containerTmpfsUnmounted': True}, 'Native attempt/cleanup failed')
    require(result['observations']['native-version']['version'] == '2.6.2', 'Native version mismatch')
    require(result['observations']['container-isolation']['verified'] is True, 'Unverified native isolation')
    identity = next(value['argv'][-1] for value in result['commands'] if value['stage'] == 'container-inspect')
    require(re.fullmatch('[0-9a-f]{64}', identity), 'Invalid owned container identity')
    argv = ['docker', '--host', 'unix:///var/run/docker.sock', 'inspect', identity]
    command_start = dt.datetime.now(dt.timezone.utc).isoformat()
    checked = subprocess.run(argv, capture_output=True, text=True, timeout=10, check=False)
    require(checked.returncode == 1 and json.loads(checked.stdout) == [] and
            'no such object' in checked.stderr.lower() and identity in checked.stderr, 'Owned container absence unverified')
    policies = result.get('runtimePolicies', [])
    for policy in policies:
        enrollment = {'transitMount': policy['transitMount'], 'headMount': policy['headMount'],
                      'nativeKeys': {'tenant-a': 'epoch-a', 'tenant-b': 'epoch-b'}}
        text = shipped['render_policy'](enrollment, policy['tenantId'], policy['scope'])
        require(hashlib.sha256(text.encode()).hexdigest() == policy['renderedPolicySha256'], 'Rendered runtime policy mismatch')
    native.append({'file': name, 'outcome': result['outcome'], 'summary': result.get('nativeTestSummary'),
                   'verifiedRuntimePolicies': len(policies), 'cleanup': result['cleanup'],
                   'independentAbsenceCommand': {'argv': argv, 'startedUtc': command_start,
                       'finishedUtc': dt.datetime.now(dt.timezone.utc).isoformat(), 'exitCode': checked.returncode,
                       'stdout': checked.stdout.strip(), 'stderr': checked.stderr.strip()}})
five = document(OUT / 'native-five.result.json')
bootstrap = document(OUT / 'native-bootstrap.result.json')
require(five['nativeTestSummary'] == {'total': 5, 'errors': 0, 'failed': 0, 'skipped': 0, 'notRun': 0}, 'Incomplete five-case native evidence')
require(len(five['safeCaseResults']) == 5 and all(value['outcome'] == 'Pass' for value in five['safeCaseResults']), 'Missing native pass evidence')
require(len(five['runtimePolicies']) == 10 and len(bootstrap['runtimePolicies']) == 2, 'Incomplete tenant policy evidence')
require(bootstrap['bootstrapHeadConfiguration'] == {'statusCode': 200, 'maxVersions': 0, 'casRequired': False}, 'Fresh native defaults evidence missing')

report = {'scope': 'Independent parent handback verification; no additional review cycle',
          'startedUtc': started.isoformat(), 'finishedUtc': dt.datetime.now(dt.timezone.utc).isoformat(),
          'elapsedSeconds': round(time.monotonic() - tick, 3), 'status': 'in-progress', 'reviewLoopIteration': 10,
          'implementationArtifactsVerified': len(manifest), 'implementationPacketExactFileSet': True,
          'historicalFilesVerified': len(before['historicalFiles']), 'originalSpecPreservedAsPrefix': True,
          'frozenIntentAndBaselinesUnchanged': True, 'runtimeFilesVerified': len(runtime_entries),
          'frozenSourceFilesVerified': len(source_manifest['files']), 'currentDifferencesFromFreeze': current_changes,
          'changedTaskSourcesMatchTestedSnapshot': True, 'fiveCaseBodiesAndCliTransportUnchanged': True,
          'commandLogsVerified': len(commands), 'recordedCommandOverlaps': overlaps,
          'originalRegressionFailurePreserved': True, 'componentResult': component_counts,
          'nativeAttempts': native, 'nativeElapsedSeconds': five['nativeSuiteElapsedSeconds'],
          'remainingObservedAuthorizedSourceGaps': [], 'installed41AndPhysicalQualificationEstablished': False}
with (HERE / 'audit.json').open('x') as output:
    json.dump(report, output, indent=2)
    output.write('\n')
print(json.dumps({'verified': True, 'implementationArtifacts': len(manifest),
                  'historicalFiles': len(before['historicalFiles']), 'runtimeFiles': len(runtime_entries),
                  'frozenSourceFiles': len(source_manifest['files']), 'commands': len(commands),
                  'nativeFivePassed': 5, 'ownedContainersIndependentlyAbsent': len(native),
                  'currentSourceDifferences': len(current_changes), 'audit': str(HERE / 'audit.json')}))
