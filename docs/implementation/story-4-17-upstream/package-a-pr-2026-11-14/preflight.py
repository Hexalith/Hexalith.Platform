"""Read-only upstream guard. Invoke in a run-with-temp official clone.

Before submission use --bundle: fetches the reviewed base by SHA before bundle
verification. This tool never forks, pushes or creates/changes a PR or issue.
"""
import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import subprocess
import os
import shutil
import sys
import tempfile

OFFICIAL = 'https://github.com/dapr/components-contrib.git'
BASE = '440b15349565bb15f7a88636477864e5a9f8cc72'
OVERLAPPING_HEAD = '14bf2f783dff41ef2c927fe1aeb6999c113ffff3'
GUARD_PATHS = ['pubsub/rabbitmq', 'common/component/rabbitmq', 'go.mod', 'go.sum', '.golangci.yml']


def evaluate(main, changed_paths, overlap):
    reasons = []
    if main != BASE and changed_paths:
        reasons.append('official main changed a guarded path since the reviewed base')
    if overlap['state'] != 'OPEN' or overlap.get('mergedAt'):
        reasons.append('#4766 must remain OPEN and unmerged')
    if overlap['headRefOid'] != OVERLAPPING_HEAD:
        reasons.append('#4766 head changed from the reviewed head')
    return reasons


def check_upstream(source):
    def git(*args):
        return subprocess.check_output(['git', *args], cwd=source, text=True).strip()

    origin = git('remote', 'get-url', 'origin')
    if origin != OFFICIAL:
        raise RuntimeError('preflight requires the official origin: ' + origin)
    subprocess.run(['git', 'fetch', '--quiet', '--depth=1', 'origin', 'main'], cwd=source, check=True)
    main = git('rev-parse', 'FETCH_HEAD')
    # Always obtain the prerequisite explicitly, even in a shallow moved-main clone.
    subprocess.run(['git', 'fetch', '--quiet', '--depth=1', 'origin', BASE], cwd=source, check=True)
    changed = git('diff', '--name-only', BASE, main, '--', *GUARD_PATHS).splitlines()
    overlap = json.loads(subprocess.check_output(
        ['gh', 'pr', 'view', '4766', '-R', 'dapr/components-contrib',
         '--json', 'number,state,mergedAt,headRefOid,isDraft,title'], text=True))
    reasons = evaluate(main, changed, overlap)
    return {
        'checkedUtc': datetime.now(timezone.utc).isoformat(),
        'status': 'HALT' if reasons else 'PASS',
        'reviewedBase': BASE,
        'officialMain': main,
        'mainEqualsReviewedBase': main == BASE,
        'guardPaths': GUARD_PATHS,
        'guardedChangesSinceReviewedBase': changed,
        'baseFetchedBySha': True,
        'overlappingPullRequest': overlap,
        'overlappingHeadUnchanged': overlap['headRefOid'] == OVERLAPPING_HEAD,
        'haltReasons': reasons,
        'method': 'read-only official fetch and gh pr view; no remote write',
    }



def recompute_receipt_gate(out):
    """Recheck current evidence in private scratch without changing saved results."""
    with tempfile.TemporaryDirectory(prefix='submission-receipts-', dir=os.environ['TMPDIR']) as scratch:
        scratch = Path(scratch)
        for path in out.iterdir():
            if path.is_file() and (path.name.endswith(('.receipt.json', '.log', '.log.gz')) or path.name in
                                  {'commit-identity.json', 'toolchain-identity.json', 'summarize-checks.py',
                                   'required-checks.jsonl'}):
                shutil.copy2(path, scratch / path.name)
        result = subprocess.run([sys.executable, '-B', str(scratch / 'summarize-checks.py')],
                                capture_output=True, text=True)
        result_path = scratch / 'test-results.json'
        summary = json.loads(result_path.read_text()) if result_path.exists() else None
        passed = (result.returncode == 0 and summary is not None
                  and summary['status'] == summary['requiredStatus'] == 'PASS')
        return {'status': 'PASS' if passed else 'FAIL', 'exitCode': result.returncode,
                'summary': summary, 'stdout': result.stdout, 'stderr': result.stderr,
                'method': 'fresh offline summary in disposable storage; original evidence unchanged'}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path, required=True)
    parser.add_argument('--bundle', action='store_true', help='also verify the prepared bundle and local result gates')
    args = parser.parse_args()
    out = Path(__file__).resolve().parent
    try:
        record = check_upstream(args.source)
        if args.bundle and record['status'] == 'PASS':
            bundle = json.loads((out / 'bundle.json').read_text())
            commit = json.loads((out / 'commit-identity.json').read_text())
            path = out / bundle['file']
            result = subprocess.run(['git', 'bundle', 'verify', str(path)], cwd=args.source,
                                    capture_output=True, text=True)
            record['bundleVerification'] = {
                'exitCode': result.returncode,
                'output': result.stdout + result.stderr,
                'hashMatches': hashlib.sha256(path.read_bytes()).hexdigest() == bundle['sha256'],
                'identityMatches': bundle['head'] == commit['head'] and bundle['tree'] == commit['tree'],
            }
            record['receiptGateRecomputed'] = recompute_receipt_gate(out)
            results = record['receiptGateRecomputed']['summary']
            verification = json.loads((out / 'subject-verification.json').read_text())
            record['localGatesPass'] = (record['receiptGateRecomputed']['status'] == 'PASS' and results is not None
                                       and results['status'] == results['requiredStatus'] == verification['status'] == 'PASS'
                                       and results['subject']['head'] == verification['head'] == commit['head'])
            record['bundleMatches'] = (result.returncode == 0 and record['bundleVerification']['hashMatches']
                                       and record['bundleVerification']['identityMatches'])
            if not record['bundleMatches'] or not record['localGatesPass']:
                record['status'] = 'HALT'
                record['haltReasons'].append('bundle or local result gate failed')
    except Exception as error:
        record = {'status': 'HALT', 'haltReasons': [str(error)]}
    destination = 'submission-preflight.json' if args.bundle else 'preflight.json'
    (out / destination).write_text(json.dumps(record, indent=2, ensure_ascii=False) + '\n')
    print(json.dumps(record, indent=2, ensure_ascii=False))
    raise SystemExit(0 if record['status'] == 'PASS' else 3)


if __name__ == '__main__':
    main()
