"""Summarize round-2 check receipts into test-results.json (offline, read-only).

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
SUPPLEMENTARY = {'golangci-lint-all-issues', 'mod-tidy-compat-diff', 'certification-mod-tidy-diff',
                 'certification-mod-tidy-compat-diff'}


def sha(data):
    return hashlib.sha256(data).hexdigest()


checks = []
for name in ORDER:
    receipt_path = out / (name + '.receipt.json')
    if not receipt_path.exists():
        continue
    receipt = json.loads(receipt_path.read_text())
    stored = (out / receipt['log']).read_bytes()
    data = gzip.decompress(stored) if receipt.get('logEncoding') == 'gzip' else stored
    expected_hash = receipt.get('uncompressedLogSha256', receipt.get('logSha256'))
    entry = {'id': name, 'argv': receipt['argv'], 'startedUtc': receipt['startedUtc'], 'endedUtc': receipt['endedUtc'],
             'exitCode': receipt['exitCode'], 'log': receipt['log'], 'logHashVerified': sha(data) == expected_hash,
             'subjectHead': receipt['subjectHead'], 'subjectIsCommit': receipt['subjectHead'] == receipt['subjectHeadAfter'] == commit['head'],
             'cleanTree': receipt['cleanTreeBefore'] and receipt['cleanTreeAfter'], 'supplementary': name in SUPPLEMENTARY}
    passed = receipt['exitCode'] == 0 and entry['logHashVerified'] and entry['subjectIsCommit'] and entry['cleanTree']
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
    entry['result'] = 'PASS' if passed else 'FAIL'
    checks.append(entry)

required = [c for c in checks if not c['supplementary']]
record = {
    'status': 'PASS' if checks and all(c['result'] == 'PASS' for c in checks) else 'FAIL',
    'requiredStatus': 'PASS' if required and all(c['result'] == 'PASS' for c in required) else 'FAIL',
    'subject': {'base': commit['base'], 'head': commit['head'], 'tree': commit['tree'],
                'patchSha256': commit['patchSha256'], 'lintFixPatchSha256': commit['lintFixPatchSha256']},
    'toolchain': json.loads((out / 'toolchain-identity.json').read_text()),
    'checks': checks,
    'round1': 'round-1-reviewed-patch/ holds the same checks on base plus the unchanged reviewed patch: all passed '
              'except golangci-lint (17 new issues with unlimited counts), which lint-fix.patch resolves.',
    'scope': 'RabbitMQ component unit/protocol-peer tests, static analysis, compile-only and module tidy checks. '
             'No broker, Dapr runtime or qualification execution.',
}
(out / 'test-results.json').write_text(json.dumps(record, indent=2, ensure_ascii=False) + '\n')
print(json.dumps({k: record[k] for k in ('status', 'requiredStatus')}, indent=2))
for c in checks:
    print(c['id'], c['result'], {k: c[k] for k in c if k.startswith(('passed', 'unique', 'lintSummary', 'outputEmpty'))})
