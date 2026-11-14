"""Summarize the Package B check receipts into test-results.json (offline, read-only).

Re-hashes every stored log, counts go test -json pass/skip/fail events and
applies the per-check expectation (exit 0 plus empty output where the tool
reports findings on stdout, ignoring module "go: downloading" progress lines,
"0 issues." for golangci-lint, and an analyzer run without missing metadata).
Requires the 15 receipts from required-checks.jsonl at Package A's final
count floors, the supplementary negative controls, the current
contribution.patch bytes and every coverage-map.json test name passing in the
named log. Exits nonzero unless everything passes.
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
         'certification-mod-tidy-diff', 'certification-mod-tidy-compat-diff', 'component-metadata']
COMMANDS = {job['log']: job['argv'] for job in
            (json.loads(line) for line in (out / 'required-checks.jsonl').read_text().splitlines() if line.strip())}
if set(COMMANDS) != set(ORDER):
    raise SystemExit('required-checks.jsonl must declare exactly the required receipt commands')
# Package A's final PR receipts (package-a-pr-2026-11-14/test-results.json).
FLOORS = {
    'full-unit': {'passedTopLevelEvents': 62, 'passedTestAndSubtestEvents': 196, 'uniquePassedTestAndSubtestNames': 196},
    'focused-race': {'passedTopLevelEvents': 230, 'passedTestAndSubtestEvents': 720, 'uniquePassedTestAndSubtestNames': 72},
    'legacy-fixture-race': {'passedTopLevelEvents': 10, 'passedTestAndSubtestEvents': 10, 'uniquePassedTestAndSubtestNames': 1},
}
EXPECTED_FILES = ['pubsub/rabbitmq/metadata.go', 'pubsub/rabbitmq/metadata.yaml', 'pubsub/rabbitmq/metadata_test.go',
                  'pubsub/rabbitmq/rabbitmq.go', 'pubsub/rabbitmq/rabbitmq_integration_test.go',
                  'pubsub/rabbitmq/rabbitmq_publish_test.go', 'pubsub/rabbitmq/rabbitmq_publish_transport_test.go']


def sha(data):
    return hashlib.sha256(data).hexdigest()


def load(receipt):
    stored = (out / receipt['log']).read_bytes()
    data = gzip.decompress(stored) if receipt.get('logEncoding') == 'gzip' else stored
    return stored, data


def subject_entry(name, receipt, stored, data):
    return {'id': name, 'argv': receipt['argv'], 'startedUtc': receipt['startedUtc'], 'endedUtc': receipt['endedUtc'],
            'exitCode': receipt['exitCode'], 'log': receipt['log'],
            'logHashVerified': sha(data) == receipt.get('uncompressedLogSha256', receipt.get('logSha256')),
            'storedLogHashVerified': sha(stored) == receipt.get('storedLogSha256', receipt.get('logSha256')),
            'subjectHead': receipt['subjectHead'],
            'subjectIsCommit': receipt['subjectHead'] == receipt['subjectHeadAfter'] == commit['head'],
            'cleanTree': receipt['cleanTreeBefore'] and receipt['cleanTreeAfter']}


passed_names = {}
checks = []
for name in ORDER:
    receipt_path = out / (name + '.receipt.json')
    if not receipt_path.exists():
        checks.append({'id': name, 'result': 'FAIL', 'missingReceipt': True})
        continue
    receipt = json.loads(receipt_path.read_text())
    stored, data = load(receipt)
    entry = subject_entry(name, receipt, stored, data)
    entry['commandMatchesRequired'] = receipt['argv'] == COMMANDS[name]
    passed = (entry['commandMatchesRequired'] and receipt['exitCode'] == 0 and entry['logHashVerified']
              and entry['storedLogHashVerified'] and entry['subjectIsCommit'] and entry['cleanTree'])
    text = data.decode()
    if name in EMPTY_OUTPUT:
        findings = [line for line in text.splitlines() if line.strip() and not line.startswith('go: downloading ')]
        entry['outputEmpty'] = findings == []
        passed = passed and entry['outputEmpty']
    if name == 'diff-stat':
        changed = sorted(line.split('|')[0].strip() for line in text.splitlines() if '|' in line)
        entry['changedFiles'] = changed
        entry['onlyRabbitMQFiles'] = changed == EXPECTED_FILES
        passed = passed and entry['onlyRabbitMQFiles']
    if name.startswith('golangci-lint'):
        entry['lintSummary'] = text.strip().splitlines()[-1] if text.strip() else ''
        passed = passed and entry['lintSummary'] == '0 issues.'
    if name == 'component-metadata':
        entry['analyzerRan'] = './metadataanalyzer ../' in text
        entry['missingMetadataReported'] = 'missing metadata' in text
        passed = passed and entry['analyzerRan'] and not entry['missingMetadataReported']
    if '-json' in receipt['argv']:
        events = [json.loads(line) for line in text.splitlines() if line.startswith('{')]
        tests = [e for e in events if e.get('Test')]
        passes = [e['Test'] for e in tests if e['Action'] == 'pass']
        passed_names[name] = set(passes)
        entry['passedTopLevelEvents'] = sum(1 for test in passes if '/' not in test)
        entry['passedTestAndSubtestEvents'] = len(passes)
        entry['uniquePassedTestAndSubtestNames'] = len(set(passes))
        entry['uniquePassedTopLevelNames'] = len({test for test in passes if '/' not in test})
        entry['skipped'] = sorted({e['Test'] for e in tests if e['Action'] == 'skip'})
        entry['failed'] = sorted({e['Test'] for e in tests if e['Action'] == 'fail'})
        package_pass = any(e['Action'] == 'pass' and not e.get('Test') for e in events)
        passed = passed and package_pass and not entry['skipped'] and not entry['failed']
    if name in FLOORS:
        entry['baselineFloors'] = FLOORS[name]
        entry['meetsBaselineFloors'] = all(entry.get(key, 0) >= floor for key, floor in FLOORS[name].items())
        passed = passed and entry['meetsBaselineFloors']
    entry['result'] = 'PASS' if passed else 'FAIL'
    checks.append(entry)

# Supplementary negative controls, outside the Package A check set.
negative = {'id': 'negative-controls', 'result': 'FAIL', 'missingReceipt': True}
receipt_path = out / 'negative-controls.receipt.json'
if receipt_path.exists():
    receipt = json.loads(receipt_path.read_text())
    stored, data = load(receipt)
    negative = subject_entry('negative-controls', receipt, stored, data)
    records = [json.loads(line) for line in data.decode().splitlines() if line.startswith('{')]
    summary = next((r['summary'] for r in records if 'summary' in r), {})
    negative['controls'] = [{'control': r['control'], 'detected': r['detected'], 'failedTopLevelTests': r['failedTopLevelTests']}
                            for r in records if 'control' in r]
    negative['summary'] = summary
    negative['result'] = 'PASS' if (receipt['argv'] == ['negative-controls'] and receipt['exitCode'] == 0
                                    and negative['logHashVerified'] and negative['storedLogHashVerified']
                                    and negative['subjectIsCommit'] and negative['cleanTree']
                                    and summary.get('result') == 'PASS' and summary.get('controls', 0) >= 18) else 'FAIL'

# Every coverage-map test name must pass in its named log.
coverage = json.loads((out / 'coverage-map.json').read_text())
missing = sorted({f'{row}: {log}: {test}' for row, entries in coverage['rows'].items()
                  for log, tests in entries.items() for test in tests
                  if test not in passed_names.get(log, set())})
coverage_result = {'rows': len(coverage['rows']), 'missingOrUnpassed': missing,
                   'result': 'PASS' if coverage['rows'] and not missing else 'FAIL'}

patch_bytes = (out / 'contribution.patch').read_bytes()
subject = {'base': commit['base'], 'officialBase': commit['officialBase'], 'head': commit['head'], 'tree': commit['tree'],
           'patchSha256': commit['patchSha256'], 'patchFileMatchesSubject': sha(patch_bytes) == commit['patchSha256']}
required_status = 'PASS' if checks and all(c['result'] == 'PASS' for c in checks) else 'FAIL'
record = {
    'status': 'PASS' if (required_status == 'PASS' and negative['result'] == 'PASS' and coverage_result['result'] == 'PASS'
                         and subject['patchFileMatchesSubject']) else 'FAIL',
    'requiredStatus': required_status,
    'subject': subject,
    'toolchain': json.loads((out / 'toolchain-identity.json').read_text()),
    'checks': checks,
    'requiredReceiptCount': len(ORDER),
    'presentReceiptCount': sum(not c.get('missingReceipt', False) for c in checks),
    'negativeControls': negative,
    'coverage': coverage_result,
    'scope': 'RabbitMQ component unit/protocol-peer tests, static analysis, compile-only, module tidy and '
             'component metadata checks, plus source negative controls. No broker, Dapr runtime, '
             'conformance/certification suite or qualification execution.',
}
(out / 'test-results.json').write_text(json.dumps(record, indent=2, ensure_ascii=False) + '\n')
print(json.dumps({k: record[k] for k in ('status', 'requiredStatus')}, indent=2))
for c in [*checks, negative]:
    print(c['id'], c['result'], {k: c[k] for k in c if k.startswith(('passed', 'unique', 'lintSummary', 'outputEmpty', 'only'))})
print('coverage', coverage_result['result'], coverage_result['missingOrUnpassed'])
raise SystemExit(0 if record['status'] == 'PASS' else 1)
