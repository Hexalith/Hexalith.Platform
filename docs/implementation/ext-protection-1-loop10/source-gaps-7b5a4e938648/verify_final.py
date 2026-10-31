"""Retain final integrity, command serialization and owned cleanup checks."""
from pathlib import Path
import datetime
import difflib
import hashlib
import json
import re
import subprocess
import xml.etree.ElementTree as ET

OUT = Path(__file__).resolve().parent
ROOT = OUT.parents[3]


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


baseline = json.loads((OUT / 'implementation-baseline.json').read_text())
freeze = json.loads((OUT / 'freeze.json').read_text())
runtime = Path(freeze['runtime'])
manifest = json.loads((runtime / 'runtime-manifest.json').read_text())
listed = {entry['path'] for entry in manifest['files']}
observed = {str(path.relative_to(runtime)) for path in runtime.rglob('*') if path.is_file() and path.name != 'runtime-manifest.json'}
runtime_bad = [entry['path'] for entry in manifest['files'] if not (runtime / entry['path']).is_file() or digest(runtime / entry['path']) != entry['sha256']]
source_manifest = json.loads((OUT / 'source-manifest.json').read_text())
source_bad = []
current_changed = []
for entry in source_manifest['files']:
    relative = Path(entry['repository']) / entry['path']
    frozen = Path(source_manifest['frozenSourceRoot']) / relative
    current = Path(source_manifest['roots'][entry['repository']]) / entry['path']
    if not frozen.is_file() or digest(frozen) != entry['sha256']:
        source_bad.append(str(relative))
    if not current.is_file() or digest(current) != entry['sha256']:
        current_changed.append(str(relative))
historical_bad = [entry['path'] for entry in baseline['historicalFiles'] if not (ROOT / entry['path']).is_file() or digest(ROOT / entry['path']) != entry['sha256']]
authority_bad = [entry['path'] for entry in baseline['authorities'] if digest(Path(entry['path'])) != entry['sha256']]
commands = [json.loads(path.read_text()) for path in OUT.glob('*.command.json')]
commands.sort(key=lambda value: value['startedUtc'])
command_bad = [entry['label'] for entry in commands if digest(OUT / entry['log']) != entry['logSha256']]
overlaps = []
for earlier, later in zip(commands, commands[1:]):
    if earlier['finishedUtc'] > later['startedUtc']:
        overlaps.append([earlier['label'], later['label']])
native = []
for name in ('policy-diagnosis.result.json', 'policy-diagnosis-quoted.result.json', 'native-bootstrap.result.json', 'native-five.result.json'):
    result = json.loads((OUT / name).read_text())
    identity = next(command['argv'][-1] for command in result['commands'] if command['stage'] == 'container-inspect')
    if not re.fullmatch('[0-9a-f]{64}', identity):
        raise SystemExit('Exact owned container identity required')
    started = datetime.datetime.now(datetime.timezone.utc).isoformat()
    argv = ['docker', '--host', 'unix:///var/run/docker.sock', 'inspect', identity]
    checked = subprocess.run(argv, capture_output=True, text=True, timeout=10)
    absent = checked.returncode != 0 and 'no such' in checked.stderr.lower() and identity in checked.stderr
    native.append({'result': name, 'outcome': result['outcome'], 'failure': result['failure'],
        'summary': result.get('nativeTestSummary'), 'cleanup': result['cleanup'],
        'independentReadOnlyInspection': {'argv': argv, 'startedUtc': started,
            'finishedUtc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
            'exitCode': checked.returncode, 'containerAbsent': absent,
            'stdout': checked.stdout.strip(), 'stderr': checked.stderr.strip()}})
before_root = OUT / 'implementation-before'
delta = []
changed = []
for entry in baseline['scopedBefore']:
    before = before_root / entry['path']
    after = ROOT / entry['path']
    if before.read_bytes() != after.read_bytes():
        changed.append(entry['path'])
        delta.extend(difflib.unified_diff(before.read_text().splitlines(keepends=True), after.read_text().splitlines(keepends=True),
            fromfile='before/' + entry['path'], tofile='after/' + entry['path']))
# The store test received only one new method; preserve its unchanged prior body
# from the previous hash-bound runtime's source snapshot for this additive delta.
test = 'tests/Hexalith.Platform.Protection.Tests/OpenBaoErasureRootStoreTests.cs'
previous = Path('/tmp/ext-protection-loop10-native-fixtures-44662c3e1c3a/source/platform') / test
after = ROOT / test
text = after.read_text()
start = text.index('    [Theory]\n    [InlineData(2)]\n    [InlineData(64)]\n    public async Task TrimMinimumVersions')
end = text.index('    [Theory]\n    [InlineData(2)]\n    [InlineData(3)]', start)
reconstructed = text[:start] + text[end:]
test_before_matches = previous.is_file() and previous.read_text() == reconstructed
(OUT / 'store-tests-before-reconstructed.cs').write_text(reconstructed)
changed.append(test)
delta.extend(difflib.unified_diff(reconstructed.splitlines(keepends=True), text.splitlines(keepends=True), fromfile='before/' + test, tofile='after/' + test))
(OUT / 'source-delta.patch').write_text(''.join(delta))
component = ET.parse(OUT / 'transport-components.xml').getroot()
summary = {key: sum(int(assembly.get(key, '0')) for assembly in component.iter('assembly'))
           for key in ('total', 'passed', 'failed', 'skipped', 'errors')}
result = {'verifiedUtc': datetime.datetime.now(datetime.timezone.utc).isoformat(), 'status': 'in-progress', 'reviewLoopIteration': 10,
    'runtime': {'filesVerified': len(listed), 'exactClosure': listed == observed, 'bad': runtime_bad,
                'manifestSha256': digest(runtime / 'runtime-manifest.json')},
    'source': {'frozenFilesVerified': len(source_manifest['files']), 'bad': source_bad,
               'currentDifferencesFromFreeze': current_changed, 'changedTaskFiles': changed,
               'storeTestBeforeMatchesPriorFrozenSource': test_before_matches,
               'unchangedFiveCaseTestBody': digest(ROOT / 'tests/Hexalith.Platform.Protection.Tests/OpenBaoErasureRootIntegrationTests.cs') ==
                   digest(before_root / 'tests/Hexalith.Platform.Protection.Tests/OpenBaoErasureRootIntegrationTests.cs'),
               'unchangedCliTransport': digest(ROOT / 'tests/Hexalith.Platform.Protection.Tests/OpenBaoNativeCommandChannel.cs') ==
                   digest(before_root / 'tests/Hexalith.Platform.Protection.Tests/OpenBaoNativeCommandChannel.cs')},
    'preservation': {'historicalFilesVerified': len(baseline['historicalFiles']), 'historicalBad': historical_bad, 'authorityBad': authority_bad},
    'commands': {'records': len(commands), 'badLogHashes': command_bad, 'overlaps': overlaps},
    'componentSummary': summary, 'nativeAttempts': native}
with (OUT / 'final-integrity.json').open('x') as output:
    json.dump(result, output, indent=2)
    output.write('\n')
print(json.dumps(result))
if listed != observed or runtime_bad or source_bad or historical_bad or authority_bad or command_bad or overlaps \
        or not test_before_matches or not result['source']['unchangedFiveCaseTestBody'] or not result['source']['unchangedCliTransport'] \
        or any(not value['cleanup']['complete'] or not value['independentReadOnlyInspection']['containerAbsent'] for value in native):
    raise SystemExit(1)
