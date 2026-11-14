"""Held disposable checkout for the Package B local contribution.

Invoke with run-with-temp. Fetches the official dapr/components-contrib base
440b1534 by SHA into TMPDIR, then Package A's exact head 96c3259b from the
retained Platform bundle, and checks that head read-only against the open
#4767 pull-request ref. Applies contribution.patch unchanged and commits it
once (signed off) on a local-only branch, as a disposable test subject.
Prints WORKSPACE_READY, then accepts stdin commands:
  cleanup  release the checkout (run-with-temp then removes it)
Only read-only remote operations occur (fetch and ls-remote). Nothing is
pushed, forked, commented on or opened, and the local commit is not an
approved submission.
"""
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
from datetime import datetime, timezone

OFFICIAL = 'https://github.com/dapr/components-contrib.git'
OFFICIAL_BASE = '440b15349565bb15f7a88636477864e5a9f8cc72'
PACKAGE_A_HEAD = '96c3259b7c12a476edd208848153d3cf72a8c4f0'
PACKAGE_A_TREE = '055ab8a8a908eacb09828847232c94ed1c74b07f'
PACKAGE_A_PR_REF = 'refs/pull/4767/head'
PACKAGE_A_BRANCH = 'refs/heads/fix/rabbitmq-publish-errors-4764'
BRANCH = 'local/rabbitmq-mandatory-returns-4765'
TRAILER = 'Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>'
FILES = sorted([
    'pubsub/rabbitmq/metadata.go',
    'pubsub/rabbitmq/metadata.yaml',
    'pubsub/rabbitmq/metadata_test.go',
    'pubsub/rabbitmq/rabbitmq.go',
    'pubsub/rabbitmq/rabbitmq_integration_test.go',
    'pubsub/rabbitmq/rabbitmq_publish_test.go',
    'pubsub/rabbitmq/rabbitmq_publish_transport_test.go',
])

out = Path(__file__).resolve().parent
package_a = out.parent / 'package-a-pr-2026-11-14'
bundle = package_a / 'package-a-pr.bundle'
patch = out / 'contribution.patch'
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


# The retained Package A bundle must be the one recorded at submission.
recorded_bundle = json.loads((package_a / 'bundle.json').read_text())
if sha(bundle.read_bytes()) != recorded_bundle['sha256'] or recorded_bundle['head'] != PACKAGE_A_HEAD:
    halt('Package A bundle differs from its submission record', actual=sha(bundle.read_bytes()))

# Read-only observation: the open Package A pull request still points at the base.
observed = {}
for ref in (PACKAGE_A_PR_REF, 'refs/heads/main'):
    listing = subprocess.check_output(['git', 'ls-remote', OFFICIAL, ref], text=True).split()
    observed[ref] = listing[0] if listing else None
if observed[PACKAGE_A_PR_REF] != PACKAGE_A_HEAD:
    halt('pull request #4767 head moved', observed=observed)

subprocess.run(['git', 'init', '--quiet', str(source)], check=True)
git('remote', 'add', 'origin', OFFICIAL)
subprocess.run(['git', 'fetch', '--quiet', '--depth=1', 'origin', OFFICIAL_BASE], cwd=source, check=True)
subprocess.run(['git', 'fetch', '--quiet', str(bundle), PACKAGE_A_BRANCH + ':refs/heads/package-a'], cwd=source, check=True)
base = git('rev-parse', 'refs/heads/package-a')
identity = {
    'repository': OFFICIAL,
    'officialBase': OFFICIAL_BASE,
    'officialBaseTree': git('rev-parse', OFFICIAL_BASE + '^{tree}'),
    'packageABase': base,
    'packageATree': git('rev-parse', base + '^{tree}'),
    'packageAParents': git('show', '-s', '--format=%P', base).split(),
    'packageABundle': {'file': '../package-a-pr-2026-11-14/' + bundle.name, 'sha256': sha(bundle.read_bytes()),
                       'ref': PACKAGE_A_BRANCH},
    'readOnlyRemoteObservation': {'observedUtc': now(), 'refs': observed,
                                  'pullRequestHeadMatchesBase': observed[PACKAGE_A_PR_REF] == PACKAGE_A_HEAD},
    'commitMetadata': git('show', '-s', '--format=%H%n%T%n%cI%n%s', base),
    'sourceUrl': 'https://github.com/dapr/components-contrib/pull/4767/commits/' + PACKAGE_A_HEAD,
}
write_json('source-identity.json', identity)
if (base, identity['packageATree'], identity['packageAParents']) != (PACKAGE_A_HEAD, PACKAGE_A_TREE, [OFFICIAL_BASE]):
    halt('Package A identity mismatch', identity=identity)

subprocess.run(['git', '-c', 'advice.detachedHead=false', 'checkout', '--quiet', PACKAGE_A_HEAD], cwd=source, check=True)
subprocess.run(['git', 'switch', '--quiet', '--create', BRANCH], cwd=source, check=True)
patch_bytes = patch.read_bytes()
subprocess.run(['git', 'apply', '--check', '--whitespace=error-all', str(patch)], cwd=source, check=True)
subprocess.run(['git', 'apply', '--whitespace=error-all', str(patch)], cwd=source, check=True)
changed = sorted(git('diff', '--name-only').splitlines() + git('ls-files', '--others', '--exclude-standard').splitlines())
if changed != FILES:
    halt('patched file set differs', changed=changed)
subprocess.run(['git', 'add', '--', *FILES], cwd=source, check=True)
subprocess.run(['git', 'commit', '--quiet', '--signoff', '--no-verify', '--cleanup=whitespace',
                '-F', str(out / 'commit-message-input.txt'), '--trailer', TRAILER], cwd=source, check=True)

head = git('rev-parse', 'HEAD')
raw = subprocess.check_output(['git', 'cat-file', 'commit', head], cwd=source)
message = raw.split(b'\n\n', 1)[1]
lines = message.decode().rstrip('\n').split('\n')
signoff = 'Signed-off-by: ' + git('var', 'GIT_AUTHOR_IDENT').rsplit(' ', 2)[0]

# Expected tree: Package A's tree plus the patch, built in a scratch index.
index = Path(os.environ['TMPDIR']) / 'expected.index'
env = dict(os.environ, GIT_INDEX_FILE=str(index))
subprocess.run(['git', 'read-tree', PACKAGE_A_HEAD], cwd=source, env=env, check=True)
subprocess.run(['git', 'apply', '--cached', str(patch)], cwd=source, env=env, check=True)
expected_tree = subprocess.check_output(['git', 'write-tree'], cwd=source, env=env, text=True).strip()
delta = subprocess.check_output(['git', 'diff', '--binary', '--full-index', PACKAGE_A_HEAD, 'HEAD'], cwd=source)

commit = {
    'recordedUtc': now(),
    'purpose': 'disposable local test subject; not pushed and not an approved submission',
    'branch': BRANCH,
    'base': PACKAGE_A_HEAD,
    'officialBase': OFFICIAL_BASE,
    'head': head,
    'tree': git('rev-parse', 'HEAD^{tree}'),
    'expectedTreeBasePlusPatch': expected_tree,
    'treeEqualsBasePlusPatch': git('rev-parse', 'HEAD^{tree}') == expected_tree,
    'deltaEqualsContributionPatch': delta == patch_bytes,
    'parents': git('show', '-s', '--format=%P', 'HEAD').split(),
    'author': git('show', '-s', '--format=%an <%ae>', 'HEAD'),
    'commitCount': int(git('rev-list', '--count', PACKAGE_A_HEAD + '..HEAD')),
    'messageSha256': sha(message),
    'subject': lines[0],
    'signedOffByPresent': signoff in lines,
    'endsWithAttribution': lines[-1] == TRAILER,
    'patchSha256': sha(patch_bytes),
    'patchBytes': len(patch_bytes),
    'diffStat': subprocess.check_output(['git', 'diff', '--stat', PACKAGE_A_HEAD, 'HEAD'], cwd=source, text=True),
    'changedFiles': git('diff', '--name-only', PACKAGE_A_HEAD, 'HEAD').splitlines(),
    'statusAfterCommit': git('status', '--porcelain', '--untracked-files=all'),
}
write_json('commit-identity.json', commit)
if not (commit['treeEqualsBasePlusPatch'] and commit['deltaEqualsContributionPatch'] and commit['signedOffByPresent']
        and commit['endsWithAttribution'] and commit['parents'] == [PACKAGE_A_HEAD] and commit['commitCount'] == 1
        and commit['changedFiles'] == FILES and not commit['statusAfterCommit']):
    halt('commit identity check failed', commit=commit)
(out / 'workspace-path.txt').write_text(str(source) + '\n')
print('WORKSPACE_READY ' + str(source), flush=True)

for line in sys.stdin:
    if line.strip() == 'cleanup':
        break
print('WORKSPACE_RELEASED', flush=True)
