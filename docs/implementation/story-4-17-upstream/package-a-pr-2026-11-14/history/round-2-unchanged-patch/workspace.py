"""Held disposable checkout for the Package A upstream pull request.

Invoke with run-with-temp. Clones official dapr/components-contrib main into
TMPDIR, records its identity, guards the reviewed base, applies the reviewed
contribution.patch unchanged, then the mechanical lint-fix.patch delta (round 2;
see README), and commits once (signed off) on the PR branch.
Prints WORKSPACE_READY, then accepts stdin commands:
  bundle   write the thin bundle <base>..<branch> and bundle.json here
  cleanup  release the checkout (run-with-temp then removes it)
Only read-only remote operations occur (clone/fetch and gh reads); no fork,
push, comment or pull request is created here.
"""
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
from datetime import datetime, timezone

OFFICIAL = 'https://github.com/dapr/components-contrib.git'
REVIEWED_BASE = '440b15349565bb15f7a88636477864e5a9f8cc72'
OVERLAPPING_PR = '4766'
OVERLAPPING_HEAD = '14bf2f783dff41ef2c927fe1aeb6999c113ffff3'
BRANCH = 'fix/rabbitmq-publish-errors-4764'
TRAILER = 'Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>'
FILES = sorted([
    'pubsub/rabbitmq/rabbitmq.go',
    'pubsub/rabbitmq/rabbitmq_integration_test.go',
    'pubsub/rabbitmq/rabbitmq_publish_test.go',
    'pubsub/rabbitmq/rabbitmq_publish_transport_test.go',
    'pubsub/rabbitmq/rabbitmq_test.go',
])

out = Path(__file__).resolve().parent
local = out.parent / 'package-a-local-2026-11-14'
patch = local / 'contribution.patch'
lint_fix = out / 'lint-fix.patch'
source = Path(os.environ['TMPDIR']) / 'components-contrib'


def now():
    return datetime.now(timezone.utc).isoformat()


def sha(data):
    return hashlib.sha256(data).hexdigest()


def git(*args, **kwargs):
    return subprocess.check_output(['git', *args], cwd=source, text=True, **kwargs).strip()


def write_json(name, value):
    (out / name).write_text(json.dumps(value, indent=2, ensure_ascii=False) + '\n')


def halt(reason, **detail):
    write_json('halt.json', {'haltedUtc': now(), 'reason': reason, **detail})
    print('WORKSPACE_HALT ' + reason, flush=True)
    sys.exit(3)


# Reviewed patch identity: the bytes recorded by the local contribution slice.
recorded = json.loads((local / 'test-results.json').read_text())['finalPatchSha256']
verified = json.loads((local / 'artifact-verification.json').read_text())
patch_bytes = patch.read_bytes()
if not (sha(patch_bytes) == recorded == verified['patchSha256']):
    halt('reviewed patch hash mismatch', actual=sha(patch_bytes), recorded=recorded)

subprocess.run(['git', 'clone', '--depth=1', '--branch=main', '--single-branch', OFFICIAL, str(source)], check=True)
base = git('rev-parse', 'HEAD')
identity = {
    'repository': git('remote', 'get-url', 'origin'),
    'requestedRef': 'refs/heads/main',
    'commit': base,
    'tree': git('rev-parse', 'HEAD^{tree}'),
    'observedUtc': now(),
    'commitMetadata': git('show', '-s', '--format=%H%n%T%n%cI%n%s', 'HEAD'),
    'initialStatus': git('status', '--porcelain'),
    'sourceUrl': 'https://github.com/dapr/components-contrib/commit/' + base,
}
write_json('source-identity.json', identity)

# Base guard: main equals the reviewed base or has no pubsub/rabbitmq change.
rabbitmq_changes = []
if base != REVIEWED_BASE:
    subprocess.run(['git', 'fetch', '--depth=1', 'origin', REVIEWED_BASE], cwd=source, check=True)
    rabbitmq_changes = git('diff', '--name-only', REVIEWED_BASE, base, '--', 'pubsub/rabbitmq').splitlines()
overlap = json.loads(subprocess.check_output(
    ['gh', 'pr', 'view', OVERLAPPING_PR, '-R', 'dapr/components-contrib', '--json', 'number,state,mergedAt,headRefOid,isDraft,title'],
    text=True))
preflight = {
    'checkedUtc': now(),
    'reviewedBase': REVIEWED_BASE,
    'officialMain': base,
    'mainEqualsReviewedBase': base == REVIEWED_BASE,
    'rabbitmqChangesSinceReviewedBase': rabbitmq_changes,
    'overlappingPullRequest': overlap,
    'overlappingHeadUnchanged': overlap['headRefOid'] == OVERLAPPING_HEAD,
    'method': 'read-only clone, fetch and gh pr view; no remote write',
}
write_json('preflight.json', preflight)
if rabbitmq_changes:
    halt('official main changed pubsub/rabbitmq since the reviewed base', changes=rabbitmq_changes)
if overlap['state'] == 'MERGED' or overlap['mergedAt']:
    halt('#4766 merged first; a rebase would change the reviewed patch', overlap=overlap)

# Apply the reviewed patch unchanged.
subprocess.run(['git', 'apply', '--check', '--whitespace=error-all', str(patch)], cwd=source, check=True)
subprocess.run(['git', 'apply', '--whitespace=error-all', str(patch)], cwd=source, check=True)
changed = sorted(git('diff', '--name-only').splitlines() + git('ls-files', '--others', '--exclude-standard').splitlines())
if changed != FILES:
    halt('patched file set differs', changed=changed)
hashes = {name: sha((source / name).read_bytes()) for name in FILES}
if hashes != verified['upstreamFileSha256']:
    halt('patched file hashes differ from the reviewed checkout', hashes=hashes)
# Behavior-neutral golangci-lint delta, applied on top of the unchanged patch.
subprocess.run(['git', 'apply', '--check', '--whitespace=error-all', str(lint_fix)], cwd=source, check=True)
subprocess.run(['git', 'apply', '--whitespace=error-all', str(lint_fix)], cwd=source, check=True)

subprocess.run(['git', 'switch', '--quiet', '--create', BRANCH], cwd=source, check=True)
subprocess.run(['git', 'add', '--', *FILES], cwd=source, check=True)
subprocess.run(['git', 'commit', '--quiet', '--signoff', '--no-verify', '--cleanup=whitespace',
                '-F', str(out / 'commit-message-input.txt'), '--trailer', TRAILER], cwd=source, check=True)

head = git('rev-parse', 'HEAD')
raw = subprocess.check_output(['git', 'cat-file', 'commit', head], cwd=source)
message = raw.split(b'\n\n', 1)[1]
(out / 'commit-message.txt').write_bytes(message)
lines = message.decode().rstrip('\n').split('\n')
author_ident = git('var', 'GIT_AUTHOR_IDENT').rsplit(' ', 2)[0]
signoff = 'Signed-off-by: ' + author_ident

# Expected tree: the official base tree plus the patch, built in a scratch index.
index = Path(os.environ['TMPDIR']) / 'expected.index'
env = dict(os.environ, GIT_INDEX_FILE=str(index))
subprocess.run(['git', 'read-tree', base], cwd=source, env=env, check=True)
subprocess.run(['git', 'apply', '--cached', str(patch)], cwd=source, env=env, check=True)
reviewed_tree = subprocess.check_output(['git', 'write-tree'], cwd=source, env=env, text=True).strip()
subprocess.run(['git', 'apply', '--cached', str(lint_fix)], cwd=source, env=env, check=True)
expected_tree = subprocess.check_output(['git', 'write-tree'], cwd=source, env=env, text=True).strip()
lint_delta = subprocess.check_output(['git', 'diff', '--binary', '--full-index', reviewed_tree, expected_tree], cwd=source)

commit = {
    'recordedUtc': now(),
    'branch': BRANCH,
    'base': base,
    'head': head,
    'tree': git('rev-parse', 'HEAD^{tree}'),
    'reviewedTreeBasePlusPatch': reviewed_tree,
    'expectedTreeBasePlusPatchPlusLintFix': expected_tree,
    'treeEqualsBasePlusPatchPlusLintFix': git('rev-parse', 'HEAD^{tree}') == expected_tree,
    'lintDeltaEqualsLintFixPatch': lint_delta == lint_fix.read_bytes(),
    'parents': git('show', '-s', '--format=%P', 'HEAD').split(),
    'author': git('show', '-s', '--format=%an <%ae>', 'HEAD'),
    'committer': git('show', '-s', '--format=%cn <%ce>', 'HEAD'),
    'commitCount': int(git('rev-list', '--count', base + '..HEAD')),
    'messageFile': 'commit-message.txt',
    'messageSha256': sha(message),
    'subject': lines[0],
    'signedOffBy': signoff,
    'signedOffByPresent': signoff in lines,
    'finalLine': lines[-1],
    'endsWithAttribution': lines[-1] == TRAILER,
    'patchSha256': sha(patch_bytes),
    'lintFixPatchSha256': sha(lint_fix.read_bytes()),
    'diffStat': subprocess.check_output(['git', 'diff', '--stat', base, 'HEAD'], cwd=source, text=True),
    'changedFiles': git('diff', '--name-only', base, 'HEAD').splitlines(),
    'statusAfterCommit': git('status', '--porcelain', '--untracked-files=all'),
}
write_json('commit-identity.json', commit)
if not (commit['treeEqualsBasePlusPatchPlusLintFix'] and commit['lintDeltaEqualsLintFixPatch'] and commit['signedOffByPresent'] and commit['endsWithAttribution']
        and commit['commitCount'] == 1 and commit['changedFiles'] == FILES and not commit['statusAfterCommit']):
    halt('commit identity check failed', commit=commit)
(out / 'workspace-path.txt').write_text(str(source) + '\n')
print('WORKSPACE_READY ' + str(source), flush=True)

for line in sys.stdin:
    command = line.strip()
    if command == 'cleanup':
        break
    if command == 'bundle':
        bundle = out / 'package-a-pr.bundle'
        subprocess.run(['git', 'bundle', 'create', '--quiet', str(bundle), base + '..' + BRANCH], cwd=source, check=True)
        verify = subprocess.run(['git', 'bundle', 'verify', str(bundle)], cwd=source, capture_output=True, text=True)
        record = {
            'createdUtc': now(),
            'file': bundle.name,
            'sha256': sha(bundle.read_bytes()),
            'bytes': bundle.stat().st_size,
            'range': base + '..' + BRANCH,
            'ref': 'refs/heads/' + BRANCH,
            'head': git('rev-parse', BRANCH),
            'tree': git('rev-parse', BRANCH + '^{tree}'),
            'prerequisite': base,
            'listHeads': git('bundle', 'list-heads', str(bundle)),
            'verifyExitCode': verify.returncode,
            'verifyOutput': (verify.stdout + verify.stderr).strip(),
        }
        write_json('bundle.json', record)
        print('BUNDLE_READY ' + json.dumps(record), flush=True)
print('WORKSPACE_RELEASED', flush=True)
