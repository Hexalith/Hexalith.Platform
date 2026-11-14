"""Reconstruct the proposed diffs and revalidate historical receipts.

Run under run-with-temp with --output pointing to a new persistent results
directory. All sources, indices and receipt copies use the supplied TMPDIR.
No source commit, branch, remote write or test execution is performed.
"""

import argparse
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess


parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--output', type=Path, required=True)
args = parser.parse_args()
packet = Path(__file__).resolve().parent
previous = packet.parent / 'package-b-local-2026-11-14'
bundle = packet.parent / 'package-a-pr-2026-11-14/package-a-pr.bundle'
scratch = Path(os.environ['TMPDIR']).resolve()
output = args.output.resolve()
if output.is_relative_to(scratch):
    raise SystemExit('Results must be outside the disposable TMPDIR')
output.mkdir(parents=True, exist_ok=False)
source = scratch / 'source'
source.mkdir()
receipt_copy = scratch / 'receipt-copy'
commands = []
assertions = []
base = '440b15349565bb15f7a88636477864e5a9f8cc72'
package_a = '96c3259b7c12a476edd208848153d3cf72a8c4f0'
expected_tree = '50f8ef2d3d3a240595d6088917f9d51afe04ec3c'


def digest(data):
    return hashlib.sha256(data).hexdigest()


def command(argv, cwd):
    result = subprocess.run(argv, cwd=cwd, capture_output=True)
    entry = {'argv': argv, 'exitCode': result.returncode,
             'stdoutSha256': digest(result.stdout),
             'stdoutBytes': len(result.stdout),
             'stderrSha256': digest(result.stderr)}
    if len(result.stdout) < 256:
        entry['stdout'] = result.stdout.decode()
    commands.append(entry)
    if result.returncode:
        raise RuntimeError(f'Command failed: {argv!r}: {result.stderr.decode()}')
    return result.stdout


def git(*argv):
    return command(['git', *argv], source)


def equal(name, actual, expected):
    passed = actual == expected
    assertions.append({'name': name, 'passed': passed})
    if not passed:
        raise AssertionError(name)


record = {'recordedUtc': datetime.now(timezone.utc).isoformat(),
          'scope': 'Source reconstruction and historical receipt revalidation; no test execution',
          'status': 'FAIL', 'commands': commands, 'assertions': assertions,
          'officialBase': base, 'packageABase': package_a,
          'expectedTree': expected_tree, 'disposableWorkspace': str(source),
          'disposableReceiptCopy': str(receipt_copy)}
try:
    equal('Retained A bundle identity', digest(bundle.read_bytes()),
          '2b76da6c491da467e3f1ac714cbb9de2f8e0d6e773e0ffe3f0dfa3efaddb9c51')
    patch = (packet / 'package-b.patch').read_bytes()
    equal('Reviewed B patch identity', digest(patch),
          '7d55faea09b30fe31228566601e72e04b2158bc64a65d9d45fb086c16a31e392')
    equal('B patch equals historical patch', patch, (previous / 'contribution.patch').read_bytes())
    git('init', '--quiet')
    git('fetch', '--quiet', '--depth=1', 'https://github.com/dapr/components-contrib.git', base)
    git('fetch', '--quiet', str(bundle), 'refs/heads/fix/rabbitmq-publish-errors-4764')
    equal('A has the approved sole parent', git('show', '-s', '--format=%P', package_a).decode().strip(), base)
    git('read-tree', package_a)
    git('apply', '--cached', str(packet / 'package-b.patch'))
    record['treeFromBOnlyPatch'] = git('write-tree').decode().strip()
    equal('B-only patch reconstructs reviewed tree', record['treeFromBOnlyPatch'], expected_tree)
    equal('B-only diff is exact', git('diff', '--cached', '--binary', '--full-index', package_a), patch)
    equal('Complete upstream diff is exact', git('diff', '--cached', '--binary', '--full-index', base),
          (packet / 'upstream-main.diff').read_bytes())
    equal('B diff statistics', git('diff', '--cached', '--stat', package_a), (packet / 'package-b.stat.txt').read_bytes())
    equal('Upstream diff statistics', git('diff', '--cached', '--stat', base), (packet / 'upstream-main.stat.txt').read_bytes())
    git('diff', '--cached', '--check', base)
    git('read-tree', base)
    git('apply', '--cached', str(packet / 'upstream-main.diff'))
    record['treeFromUpstreamDiff'] = git('write-tree').decode().strip()
    equal('Complete diff reconstructs reviewed tree', record['treeFromUpstreamDiff'], expected_tree)
    shutil.copytree(previous, receipt_copy)
    summary_output = command(['python3', '-B', str(receipt_copy / 'summarize-checks.py')], receipt_copy)
    (output / 'receipt-revalidation.txt').write_bytes(summary_output)
    original = (previous / 'test-results.json').read_bytes()
    equal('Recomputed historical summary is byte-identical',
          (receipt_copy / 'test-results.json').read_bytes(), original)
    record['receiptSummarySha256'] = digest(original)
    record['historicalTestSubject'] = json.loads(original)['subject']['head']
    record['status'] = 'PASS'
finally:
    (output / 'verification.json').write_text(json.dumps(record, indent=2) + '\n')
print(json.dumps({'status': record['status'], 'assertions': len(assertions),
                  'tree': record.get('treeFromUpstreamDiff')}))
