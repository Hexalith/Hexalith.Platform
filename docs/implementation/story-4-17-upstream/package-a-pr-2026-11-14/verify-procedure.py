"""Exercise local guard failures using disposable fixtures; run-with-temp required.

Does not call a remote service or alter the authoritative receipts. Verifies each
required receipt is mandatory, count floors reject undersized successful runs,
the upstream state guard rejects each forbidden state, and UTF-8 readback rejects
newline/Unicode/head mismatches while accepting JSON's own trailing newline.
"""
from datetime import datetime, timezone
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
from contextlib import redirect_stdout
import io
from unittest import mock

sys.dont_write_bytecode = True
out = Path(__file__).resolve().parent
fixture = Path(os.environ['TMPDIR']) / 'procedure-fixture'
fixture.mkdir()
for path in out.iterdir():
    if path.is_file() and (path.name.endswith(('.receipt.json', '.log', '.log.gz')) or path.name in
                           {'commit-identity.json', 'toolchain-identity.json', 'summarize-checks.py', 'required-checks.jsonl',
                            'preflight.py', 'readback.py', 'bundle.json', 'package-a-pr.bundle',
                            'subject-verification.json', 'pr-title.txt', 'pr-body.md'}):
        shutil.copy2(path, fixture / path.name)


def load(name, folder=out):
    spec = importlib.util.spec_from_file_location(name, folder / (name + '.py'))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def summarize():
    run = subprocess.run([sys.executable, '-B', str(fixture / 'summarize-checks.py')], capture_output=True, text=True)
    return run.returncode, json.loads((fixture / 'test-results.json').read_text())


checks = {}
code, record = summarize()
checks['completeReceiptsPass'] = code == 0 and record['status'] == record['requiredStatus'] == 'PASS'
checks['allFourteenReceiptsRequired'] = record['requiredReceiptCount'] == record['presentReceiptCount'] == 14
for entry in record['checks']:
    name = entry['id']
    path = fixture / (name + '.receipt.json')
    original = path.read_bytes()
    path.unlink()
    code, result = summarize()
    checks['missingReceiptRejected:' + name] = code != 0 and result['status'] == result['requiredStatus'] == 'FAIL'
    path.write_bytes(original)

for name in ('full-unit', 'focused-race', 'legacy-fixture-race'):
    path = fixture / (name + '.receipt.json')
    original = path.read_bytes()
    receipt = json.loads(original)
    # A successful one-test run with valid hashes must still fail the baseline.
    data = ('{"Action":"pass","Test":"TestOne"}\n{"Action":"pass"}\n').encode()
    log = name + '-undersized.log'
    (fixture / log).write_bytes(data)
    for key in ('uncompressedLogSha256', 'storedLogSha256', 'logEncoding'):
        receipt.pop(key, None)
    receipt.update(log=log, logSha256=hashlib.sha256(data).hexdigest(), logBytes=len(data))
    path.write_text(json.dumps(receipt))
    code, result = summarize()
    entry = next(entry for entry in result['checks'] if entry['id'] == name)
    checks['undersizedRunRejected:' + name] = (code != 0 and result['status'] == 'FAIL'
                                             and not entry['meetsBaselineFloors'] and entry['logHashVerified'])
    path.write_bytes(original)

# A correctly hashed log under a mislabeled command must fail, including race flags.
for name, replacement in (
    ('vet', ['true']),
    ('focused-race', ['go', 'test', '-json', '-count=10', '-timeout=180s', '-run',
                      '^(TestPublishPublic|TestInit)', './pubsub/rabbitmq']),
    ('legacy-fixture-race', ['go', 'test', '-race', '-json', '-count=1', '-run',
                             '^TestMustReconnectOnAMQPError$', './pubsub/rabbitmq']),
):
    path = fixture / (name + '.receipt.json')
    original = path.read_bytes()
    receipt = json.loads(original)
    receipt['argv'] = replacement
    path.write_text(json.dumps(receipt))
    code, result = summarize()
    entry = next(entry for entry in result['checks'] if entry['id'] == name)
    checks['mislabeledCommandRejected:' + name] = code != 0 and not entry['commandMatchesRequired']
    path.write_bytes(original)

# Exercise the actual --bundle boundary with a stale saved PASS summary. Only the
# upstream observation and bundle verification are mocked; receipt recomputation
# runs the real summarizer in its own disposable directory.
gate = load('preflight', fixture)
real_run = subprocess.run

def run_gate_command(argv, **kwargs):
    if argv[:3] == ['git', 'bundle', 'verify']:
        return subprocess.CompletedProcess(argv, 0, stdout='fixture bundle verified', stderr='')
    return real_run(argv, **kwargs)


def bundle_gate():
    upstream = {'status': 'PASS', 'haltReasons': []}
    with mock.patch.object(gate, 'check_upstream', return_value=upstream), \
         mock.patch.object(gate.subprocess, 'run', side_effect=run_gate_command), \
         mock.patch.object(sys, 'argv', ['preflight.py', '--source', str(fixture), '--bundle']), \
         redirect_stdout(io.StringIO()):
        try:
            gate.main()
        except SystemExit as error:
            code = error.code
    return code, json.loads((fixture / 'submission-preflight.json').read_text())

# Save PASS once and intentionally leave it stale through all missing-receipt cases.
code, saved = summarize()
code, gate_result = bundle_gate()
checks['bundleBoundaryAcceptsCompleteEvidence'] = code == 0 and gate_result['localGatesPass']
for name in [entry['id'] for entry in saved['checks']]:
    path = fixture / (name + '.receipt.json')
    original = path.read_bytes()
    evidence_before = {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in fixture.iterdir()
                       if p.is_file() and p.name != 'submission-preflight.json'}
    path.unlink()
    evidence_before.pop(path.name)
    code, result = bundle_gate()
    checks['bundleBoundaryMissingReceiptRejected:' + name] = (
        code != 0 and result['status'] == 'HALT' and not result['localGatesPass']
        and result['receiptGateRecomputed']['status'] == 'FAIL'
        and json.loads((fixture / 'test-results.json').read_text())['status'] == 'PASS')
    evidence_after = {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in fixture.iterdir()
                      if p.is_file() and p.name != 'submission-preflight.json'}
    checks['bundleBoundaryPreservesEvidence:' + name] = evidence_before == evidence_after
    path.write_bytes(original)

preflight = load('preflight')
open_pr = {'state': 'OPEN', 'mergedAt': None, 'headRefOid': preflight.OVERLAPPING_HEAD}
# Required paths are intentionally independent of implementation GUARD_PATHS.
required_paths = ['pubsub/rabbitmq', 'common/component/rabbitmq', 'go.mod', 'go.sum', '.golangci.yml']
checks['allFiveGuardPathsPresent'] = set(preflight.GUARD_PATHS) == set(required_paths)
origin = fixture.parent / 'upstream-fixture'
origin.mkdir()

def git(source, *args):
    return subprocess.check_output(['git', *args], cwd=source, text=True, stderr=subprocess.PIPE).strip()

git(origin, 'init', '--quiet', '--initial-branch=main')
git(origin, 'config', 'user.name', 'Procedure Fixture')
git(origin, 'config', 'user.email', 'fixture@example.invalid')
targets = ['pubsub/rabbitmq/component.go', 'common/component/rabbitmq/common.go',
           'go.mod', 'go.sum', '.golangci.yml', 'unrelated.txt']
for name in targets:
    target = origin / name
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text('base\n')
git(origin, 'add', '.')
git(origin, '-c', 'core.hooksPath=/dev/null', '-c', 'commit.gpgsign=false', 'commit', '--quiet', '-m', 'base fixture')
base = git(origin, 'rev-parse', 'HEAD')
real_check_output = subprocess.check_output

def upstream_output(argv, **kwargs):
    if argv[0] == 'gh':
        return json.dumps(open_pr)
    return real_check_output(argv, **kwargs)

with mock.patch.object(preflight, 'BASE', base), mock.patch.object(preflight, 'OFFICIAL', origin.as_uri()), \
     mock.patch.object(preflight.subprocess, 'check_output', side_effect=upstream_output):
    for index, name in enumerate([None, *targets]):
        git(origin, 'reset', '--quiet', '--hard', base)
        if name:
            (origin / name).write_text('changed\n')
            git(origin, 'add', name)
            git(origin, '-c', 'core.hooksPath=/dev/null', '-c', 'commit.gpgsign=false', 'commit', '--quiet', '-m', name)
        clone = fixture.parent / ('shallow-upstream-' + str(index))
        real_run(['git', 'clone', '--quiet', '--depth=1', '--branch=main', origin.as_uri(), str(clone)], check=True)
        if name:
            absent = real_run(['git', 'cat-file', '-e', base + '^{commit}'], cwd=clone, capture_output=True).returncode != 0
            checks['baseAbsentBeforeFetch:' + name] = absent
        result = preflight.check_upstream(clone)
        expected = 'HALT' if name in targets[:-1] else 'PASS'
        checks['actualUpstreamGuard:' + str(name)] = result['status'] == expected
        checks['baseRetrievedBySha:' + str(name)] = (result['baseFetchedBySha']
            and real_run(['git', 'cat-file', '-e', base + '^{commit}'], cwd=clone, capture_output=True).returncode == 0)
        if name in targets[:-1]:
            checks['actualGuardedDiff:' + name] = result['guardedChangesSinceReviewedBase'] == [name]
for state in ('CLOSED', 'MERGED'):
    checks['overlapStateRejected:' + state] = bool(preflight.evaluate(preflight.BASE, [], dict(open_pr, state=state)))
checks['overlapMergedTimestampRejected'] = bool(preflight.evaluate(preflight.BASE, [], dict(open_pr, mergedAt='now')))
checks['overlapHeadChangeRejected'] = bool(preflight.evaluate(preflight.BASE, [], dict(open_pr, headRefOid='changed')))

readback = load('readback')
title, body, head = b'Title', 'Text\n\n\U0001f916 attribution'.encode(), 'approved-head'
response = {'title': title.decode(), 'body': body.decode(), 'headRefOid': head}
parsed = json.loads(json.dumps(response, ensure_ascii=True).encode() + b'\n')
checks['jsonTrailingNewlineAccepted'] = all(readback.compare(parsed, title, body, head).values())
checks['bodyTrailingNewlineRejected'] = not readback.compare(dict(response, body=body.decode()+'\n'), title, body, head)['bodyBytesMatch']
checks['unicodeByteChangeRejected'] = not readback.compare(dict(response, body=body.decode().replace('\U0001f916', '?')), title, body, head)['bodyBytesMatch']
checks['titleChangeRejected'] = not readback.compare(dict(response, title='other'), title, body, head)['titleBytesMatch']
checks['headChangeRejected'] = not readback.compare(dict(response, headRefOid='other'), title, body, head)['headMatches']
# Mock gh on PATH so the complete readback CLI must persist command/parse failures.
mock_bin = fixture.parent / 'mock-bin'
mock_bin.mkdir()
gh = mock_bin / 'gh'
gh.write_text("#!/usr/bin/env python3\nimport os,sys\nsys.stdout.write(os.environ['MOCK_GH_STDOUT'])\nsys.stderr.write(os.environ['MOCK_GH_STDERR'])\nsys.exit(int(os.environ['MOCK_GH_EXIT']))\n")
gh.chmod(0o755)
for name, stdout, stderr, exit_code, expected_status in (
    ('command failure', '', 'read-only gh network failure\n', 1, 'COMMAND_FAILED'),
    ('malformed JSON', '{invalid', 'diagnostic\n', 0, 'INVALID_RESPONSE'),
    ('invalid schema', '{}', '', 0, 'INVALID_RESPONSE'),
):
    env = dict(os.environ, PATH=str(mock_bin) + os.pathsep + os.environ['PATH'],
               MOCK_GH_STDOUT=stdout, MOCK_GH_STDERR=stderr, MOCK_GH_EXIT=str(exit_code))
    result = real_run([sys.executable, '-B', str(fixture / 'readback.py'), '987654'], env=env, capture_output=True)
    failure = json.loads((fixture / 'pr-readback.json').read_text())
    checks['durableReadbackFailure:' + name] = (result.returncode != 0 and failure['status'] == expected_status
        and failure['pullRequest'] == '987654' and failure['exitCode'] == exit_code
        and (fixture / failure['rawResponse']).read_bytes() == stdout.encode()
        and (fixture / failure['rawStderr']).read_bytes() == stderr.encode())

record = {'checkedUtc': datetime.now(timezone.utc).isoformat(), 'status': 'PASS' if all(checks.values()) else 'FAIL',
          'checks': checks, 'method': 'disposable receipts and shallow Git fixtures with mocked gh/bundle/readback responses; no remote calls'}
(out / 'procedure-verification.json').write_text(json.dumps(record, indent=2) + '\n')
print(json.dumps(record, indent=2))
raise SystemExit(0 if all(checks.values()) else 1)
