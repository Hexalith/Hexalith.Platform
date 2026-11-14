"""Summarize the current subject check receipts into test-results.json (offline, read-only).

Re-hashes every stored log, counts go test -json pass/skip/fail events and
applies the per-check expectation (exit 0 plus empty output where the tool
reports findings on stdout, ignoring module "go: downloading" progress lines,
and "0 issues." for golangci-lint).
"""
import gzip
import hashlib
import json
from pathlib import Path

out = Path(__file__).resolve().parent
commit = json.loads((out / 'commit-identity.json').read_text())
EMPTY_OUTPUT = {'gofmt', 'diff-check', 'vet', 'mod-tidy-diff', 'mod-tidy-compat-diff',
                'certification-mod-tidy-diff', 'certification-mod-tidy-compat-diff'}
ORDER = ['diff-stat', 'gofmt', 'diff-check', 'golangci-lint', 'golangci-lint-all-issues', 'vet', 'full-unit',
         'focused-race', 'legacy-fixture-race', 'integration-compile-only', 'mod-tidy-diff', 'mod-tidy-compat-diff',
         'certification-mod-tidy-diff', 'certification-mod-tidy-compat-diff']
COMMANDS = {job['log']: job['argv'] for job in
            (json.loads(line) for line in (out / 'required-checks.jsonl').read_text().splitlines() if line.strip())}
if set(COMMANDS) != set(ORDER):
    raise SystemExit('required-checks.jsonl must declare exactly the required receipt commands')
SUPPLEMENTARY = {'golangci-lint-all-issues', 'mod-tidy-compat-diff', 'certification-mod-tidy-diff',
                 'certification-mod-tidy-compat-diff'}


def sha(data):
    return hashlib.sha256(data).hexdigest()


checks = []
for name in ORDER:
    receipt_path = out / (name + '.receipt.json')
    if not receipt_path.exists():
        checks.append({'id': name, 'result': 'FAIL', 'missingReceipt': True, 'supplementary': name in SUPPLEMENTARY})
        continue
    receipt = json.loads(receipt_path.read_text())
    stored = (out / receipt['log']).read_bytes()
    data = gzip.decompress(stored) if receipt.get('logEncoding') == 'gzip' else stored
    expected_hash = receipt.get('uncompressedLogSha256', receipt.get('logSha256'))
    entry = {'id': name, 'argv': receipt['argv'], 'startedUtc': receipt['startedUtc'], 'endedUtc': receipt['endedUtc'],
             'exitCode': receipt['exitCode'], 'log': receipt['log'],
             'commandMatchesRequired': receipt['argv'] == COMMANDS[name], 'logHashVerified': sha(data) == expected_hash,
             'storedLogHashVerified': sha(stored) == receipt.get('storedLogSha256', receipt.get('logSha256')),
             'subjectHead': receipt['subjectHead'], 'subjectIsCommit': receipt['subjectHead'] == receipt['subjectHeadAfter'] == commit['head'],
             'cleanTree': receipt['cleanTreeBefore'] and receipt['cleanTreeAfter'], 'supplementary': name in SUPPLEMENTARY}
    passed = entry['commandMatchesRequired'] and receipt['exitCode'] == 0 and entry['logHashVerified'] and entry['storedLogHashVerified'] and entry['subjectIsCommit'] and entry['cleanTree']
    if name in EMPTY_OUTPUT:
        findings = [line for line in data.decode().splitlines() if line.strip() and not line.startswith('go: downloading ')]
        entry['outputEmpty'] = findings == []
        passed = passed and entry['outputEmpty']
    if name.startswith('golangci-lint'):
        entry['lintSummary'] = data.decode().strip().splitlines()[-1] if data.strip() else ''
        passed = passed and entry['lintSummary'] == '0 issues.'
    if '-json' in receipt['argv']:
        events = [json.loads(line) for line in data.decode().splitlines() if line.startswith('{')]
        tests = [e for e in events if e.get('Test')]
        entry['passedTopLevelEvents'] = sum(1 for e in tests if e['Action'] == 'pass' and '/' not in e['Test'])
        entry['passedTestAndSubtestEvents'] = sum(1 for e in tests if e['Action'] == 'pass')
        entry['uniquePassedTestAndSubtestNames'] = len({e['Test'] for e in tests if e['Action'] == 'pass'})
        entry['uniquePassedTopLevelNames'] = len({e['Test'] for e in tests if e['Action'] == 'pass' and '/' not in e['Test']})
        entry['skipped'] = sorted({e['Test'] for e in tests if e['Action'] == 'skip'})
        entry['failed'] = sorted({e['Test'] for e in tests if e['Action'] == 'fail'})
        package_pass = any(e['Action'] == 'pass' and not e.get('Test') for e in events)
        passed = passed and package_pass and not entry['skipped'] and not entry['failed']
    floors = {
        'full-unit': {'passedTopLevelEvents': 61, 'passedTestAndSubtestEvents': 192, 'uniquePassedTestAndSubtestNames': 192},
        'focused-race': {'passedTopLevelEvents': 220, 'passedTestAndSubtestEvents': 680, 'uniquePassedTestAndSubtestNames': 68},
        'legacy-fixture-race': {'passedTopLevelEvents': 10, 'passedTestAndSubtestEvents': 10, 'uniquePassedTestAndSubtestNames': 1},
    }
    if name in floors:
        entry['baselineFloors'] = floors[name]
        entry['meetsBaselineFloors'] = all(entry.get(key, 0) >= floor for key, floor in floors[name].items())
        passed = passed and entry['meetsBaselineFloors']
    entry['result'] = 'PASS' if passed else 'FAIL'
    checks.append(entry)

required = checks
record = {
    'status': 'PASS' if checks and all(c['result'] == 'PASS' for c in checks) else 'FAIL',
    'requiredStatus': 'PASS' if required and all(c['result'] == 'PASS' for c in required) else 'FAIL',
    'subject': {'base': commit['base'], 'head': commit['head'], 'tree': commit['tree'],
                'patchSha256': commit['patchSha256'], 'lintFixPatchSha256': commit['lintFixPatchSha256'], 'prDeltaPatchSha256': commit['prDeltaPatchSha256']},
    'toolchain': json.loads((out / 'toolchain-identity.json').read_text()),
    'checks': checks,
    'requiredReceiptCount': len(ORDER),
    'presentReceiptCount': sum(not c.get('missingReceipt', False) for c in checks),
    'history': 'history/round-2-unchanged-patch/ preserves superseded evidence. Its round-1-reviewed-patch/ '
               'has the original checks except the two certification-module tidy checks added in round 2. '
               'Current receipts cover logging restoration and its focused test.',
    'scope': 'RabbitMQ component unit/protocol-peer tests, static analysis, compile-only and module tidy checks. '
             'No broker, Dapr runtime or qualification execution.',
}
(out / 'test-results.json').write_text(json.dumps(record, indent=2, ensure_ascii=False) + '\n')
print(json.dumps({k: record[k] for k in ('status', 'requiredStatus')}, indent=2))
for c in checks:
    print(c['id'], c['result'], {k: c[k] for k in c if k.startswith(('passed', 'unique', 'lintSummary', 'outputEmpty'))})

raise SystemExit(0 if record['status'] == record['requiredStatus'] == 'PASS' else 1)
