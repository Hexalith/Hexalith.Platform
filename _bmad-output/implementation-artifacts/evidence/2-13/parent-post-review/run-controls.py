from pathlib import Path
import datetime, hashlib, json, os, re, subprocess, sys, tarfile

root = Path.cwd()
output = root / '_bmad-output/implementation-artifacts/evidence/2-13/parent-post-review'
parse_existing = '--parse-existing' in sys.argv
output.mkdir(exist_ok=parse_existing)
with tarfile.open(root / '_bmad-output/implementation-artifacts/evidence/2-13/verification-artifacts.tar.gz') as archive:
    source_paths = json.load(archive.extractfile('non-xunit-source-binding.json'))['files']
source_hashes = {path: hashlib.sha256((root / path).read_bytes()).hexdigest() for path in source_paths}
commands = [
    ('python-controls', [sys.executable, '-B', str(root / '_bmad-output/implementation-artifacts/evidence/2-13/run-python-controls.py'), str(output / 'python')]),
    ('source-release', ['npm', 'run', 'test:source-release']),
    ('extension-node', ['node', '--test', 'eng/test-extension-release/gate.test.mjs']),
    ('extension-python', [sys.executable, '-B', 'eng/test-extension-release/run-controls.py']),
]
records = json.loads((output / 'commands.json').read_text()) if parse_existing else []
for label, command in ([] if parse_existing else commands):
    path = output / (label + '.log')
    with path.open('w') as log:
        completed = subprocess.run(command, stdout=log, stderr=subprocess.STDOUT, timeout=1200)
    records.append({'label': label, 'command': command, 'cwd': str(root), 'exitCode': completed.returncode,
                    'log': path.name, 'sha256': hashlib.sha256(path.read_bytes()).hexdigest()})
    (output / 'commands.json').write_text(json.dumps(records, indent=2) + '\n')
    print(label, completed.returncode, flush=True)
    assert completed.returncode == 0, label

def python_count(path):
    raw = path.read_text()
    counts = [int(n) for n in re.findall(r'^Ran (\d+) tests? in ', raw, re.M)]
    oks = re.findall(r'^OK$', raw, re.M)
    assert counts and min(counts) > 0 and len(counts) == len(oks), path
    assert not re.search(r'^(FAILED|OK \()', raw, re.M), path
    return sum(counts)

python_total = sum(python_count(path) for path in (output / 'python').glob('test_*.log'))
assert python_total == 174, python_total
counts = {'standalonePython': python_total, 'extensionPython': python_count(output / 'extension-python.log')}
assert counts['extensionPython'] == 30, counts
for label, expected in [('source-release', 83), ('extension-node', 14)]:
    raw = (output / (label + '.log')).read_text()
    fields = {k: int(v) for k, v in re.findall(r'^(?:#|ℹ) (tests|pass|fail|cancelled|skipped|todo) (\d+)$', raw, re.M)}
    assert fields['tests'] == fields['pass'] == expected, fields
    assert all(fields[k] == 0 for k in ['fail', 'cancelled', 'skipped', 'todo']), fields
    counts[label] = expected

receipt = {'recordedUtc': datetime.datetime.now(datetime.timezone.utc).isoformat(), 'passed': True,
           'counts': counts, 'skipped': 0, 'executionSubject': 'live post-review worktree sources',
           'noHostedMainAcceptance': True, 'commands': records,
           'logs': {str(p.relative_to(output)): hashlib.sha256(p.read_bytes()).hexdigest()
                    for p in output.rglob('*.log')}}
assert all(hashlib.sha256((root / path).read_bytes()).hexdigest() == digest for path, digest in source_hashes.items())
receipt['sourceFiles'] = source_hashes
receipt['sourcesUnchangedDuringEvidenceParsing'] = True
receipt['reporterParsingCorrection'] = 'All four commands exited zero; Node used its spec reporter. The evidence parser was corrected to accept spec and TAP summary prefixes; tests were not repeated.'
archive_path = output / 'raw-logs.tar.gz'
with tarfile.open(archive_path, 'w:gz') as archive:
    for path in sorted(output.rglob('*.log')):
        archive.add(path, arcname=str(path.relative_to(output)), recursive=False)
receipt['rawLogsArchive'] = {'file': archive_path.name, 'bytes': archive_path.stat().st_size,
                             'sha256': hashlib.sha256(archive_path.read_bytes()).hexdigest(),
                             'members': receipt['logs']}
(output / 'summary.json').write_text(json.dumps(receipt, indent=2) + '\n')
print(json.dumps(counts), flush=True)
