"""Offline verification of the prepared PR subject; invoke with run-with-temp.

Clones official main read-only into TMPDIR, verifies package-a-pr.bundle
against it, fetches the bundled branch and checks head, parent, tree (official
base plus the unchanged reviewed patch plus pr-delta.patch (lint fixes and restored logging)),
the exact source delta bytes, changed files, commit message bytes and sign-off. Also checks the PR title/body texts against the spec rules.
Writes subject-verification.json. No fork, push, comment or PR is created.
"""
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys
from datetime import datetime, timezone

OFFICIAL = 'https://github.com/dapr/components-contrib.git'
ATTRIBUTION = '\U0001F916 Generated with [Claude Code](https://claude.com/claude-code)'
TRAILER = 'Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>'
CLOSING = re.compile(r'(?i)\b(close[sd]?|fix(?:e[sd])?|resolve[sd]?)\b\s*:?\s*'
                     r'(#\d+|https://github\.com/\S+/(?:issues|pull)/\d+|[\w.-]+/[\w.-]+#\d+)')
PRIVATE = ['EventStore', 'Platform', 'Hexalith', 'qualification', 'candidate', '20/24', 'Package A', 'Package B',
           'jpiquot', 'Administrator']

out = Path(__file__).resolve().parent
local = out.parent / 'package-a-local-2026-11-14'
patch = local / 'contribution.patch'
pr_delta = out / 'pr-delta.patch'
scratch = Path(os.environ['TMPDIR'])
clone = scratch / 'fresh-official'
commit = json.loads((out / 'commit-identity.json').read_text())
bundle = json.loads((out / 'bundle.json').read_text())


def sha(data):
    return hashlib.sha256(data).hexdigest()


def git(*args, env=None):
    return subprocess.check_output(['git', *args], cwd=clone, text=True, env=env).strip()


checks = {}
subprocess.run(['git', 'clone', '--quiet', '--depth=1', '--branch=main', '--single-branch', OFFICIAL, str(clone)], check=True)
main = git('rev-parse', 'HEAD')
checks['officialMainObserved'] = main
sys.dont_write_bytecode = True
from preflight import check_upstream
preflight = check_upstream(clone)
checks['upstreamPreflightPasses'] = preflight['status'] == 'PASS'
if not checks['upstreamPreflightPasses']:
    raise SystemExit('HALT: upstream preflight failed: ' + json.dumps(preflight))
# The shared preflight explicitly fetches the base SHA before bundle use.
bundle_file = out / bundle['file']
checks['bundleSha256Matches'] = sha(bundle_file.read_bytes()) == bundle['sha256']
verify = subprocess.run(['git', 'bundle', 'verify', str(bundle_file)], cwd=clone, capture_output=True, text=True)
checks['bundleVerifiesAgainstFreshClone'] = verify.returncode == 0
subprocess.run(['git', 'fetch', '--quiet', str(bundle_file), bundle['ref'] + ':' + bundle['ref']], cwd=clone, check=True)
head = git('rev-parse', bundle['ref'])
checks['headMatches'] = head == commit['head'] == bundle['head']
checks['treeMatches'] = git('rev-parse', head + '^{tree}') == commit['tree'] == bundle['tree']
checks['singleParentIsBase'] = git('show', '-s', '--format=%P', head) == commit['base']
index = scratch / 'expected.index'
env = dict(os.environ, GIT_INDEX_FILE=str(index))
subprocess.run(['git', 'read-tree', commit['base']], cwd=clone, env=env, check=True)
subprocess.run(['git', 'apply', '--cached', str(patch)], cwd=clone, env=env, check=True)
reviewed_tree = git('write-tree', env=env)
checks['reviewedTreeMatchesWorkspace'] = reviewed_tree == commit['reviewedTreeBasePlusPatch']
subprocess.run(['git', 'apply', '--cached', str(pr_delta)], cwd=clone, env=env, check=True)
checks['treeEqualsFreshBasePlusPatchPlusPrDelta'] = git('write-tree', env=env) == commit['tree']
delta = subprocess.check_output(['git', 'diff', '--binary', '--full-index', reviewed_tree, head + '^{tree}'], cwd=clone)
checks['headMinusReviewedEqualsPrDeltaPatch'] = delta == pr_delta.read_bytes()
# Independently split the delta into the retained lint fix and logging restoration.
lint_fix = out / 'lint-fix.patch'
logging_patch = out / 'logging-restoration.patch'
subprocess.run(['git', 'read-tree', reviewed_tree], cwd=clone, env=env, check=True)
subprocess.run(['git', 'apply', '--cached', str(lint_fix)], cwd=clone, env=env, check=True)
linted_tree = git('write-tree', env=env)
subprocess.run(['git', 'apply', '--cached', str(logging_patch)], cwd=clone, env=env, check=True)
checks['retainedLintThenLoggingEqualsHead'] = git('write-tree', env=env) == commit['tree']
logging_delta = subprocess.check_output(['git', 'diff', '--binary', '--full-index', linted_tree, head + '^{tree}'], cwd=clone)
checks['headMinusLintedEqualsLoggingRestoration'] = logging_delta == logging_patch.read_bytes()
checks['lintFixUnchangedSincePriorRound'] = lint_fix.read_bytes() == (out / 'history/round-2-unchanged-patch/lint-fix.patch').read_bytes()
checks['changedFiles'] = git('diff', '--name-only', commit['base'], head).splitlines()
checks['onlyFivePatchedFiles'] = checks['changedFiles'] == commit['changedFiles'] and len(checks['changedFiles']) == 5
raw = subprocess.check_output(['git', 'cat-file', 'commit', head], cwd=clone)
message = raw.split(b'\n\n', 1)[1]
checks['commitMessageEqualsFile'] = message == (out / 'commit-message.txt').read_bytes()
lines = message.decode().rstrip('\n').split('\n')
author = git('show', '-s', '--format=%an <%ae>', head)
checks['signedOffByAuthor'] = ('Signed-off-by: ' + author) in lines
checks['endsWithCoAuthoredBy'] = lines[-1] == TRAILER
body_lines = [line for line in lines if not re.match(r'^(Signed-off-by|Co-Authored-By): ', line)]
commit_text = '\n'.join(body_lines)

title_bytes = (out / 'pr-title.txt').read_bytes()
body_bytes = (out / 'pr-body.md').read_bytes()
title, body = title_bytes.decode(), body_bytes.decode()
checks['titleSingleLine'] = '\n' not in title and 0 < len(title) <= 72
checks['bodyEndsWithAttribution'] = body.rstrip('\n').endswith(ATTRIBUTION)
checks['bodyPartiallyAddresses4764'] = 'Partially addresses #4764' in body
checks['commitPartiallyAddresses4764'] = 'Partially addresses #4764' in commit_text
checks['noClosingKeyword'] = not any(CLOSING.search(text) for text in (title, body, commit_text))
checks['noAtMention'] = all('@' not in text for text in (title, body, commit_text))
checks['references4766ByNumber'] = '#4766' in body
checks['listsExclusions'] = all(term in body for term in ('integration', 'certification', 'conformance', 'Dapr runtime', '#4765'))
checks['mentionsSdkLimits'] = all(term in body for term in ('ErrCredentials', 'close-ok'))
checks['disclosesTransportAbort'] = all(term in body for term in ('confirmation', 'subscriptions reconnect', 'unacknowledged deliveries are redelivered', 'dedicated publisher connection', 'drained listener'))
checks['noPrivatePaths'] = not any(re.search(r'(?i)/(?:tmp|home)/', text) for text in (title, body, commit_text))
checks['offersRebaseOrSplit'] = 'rebase' in body and 'split' in body
checks['noPrivateMarkers'] = [term for term in PRIVATE if any(term.casefold() in text.casefold() for text in (title, body, commit_text))] == []

passed = all(value for key, value in checks.items() if isinstance(value, bool))
record = {
    'checkedUtc': datetime.now(timezone.utc).isoformat(),
    'status': 'PASS' if passed else 'FAIL',
    'base': commit['base'],
    'head': head,
    'tree': commit['tree'],
    'patchSha256': sha(patch.read_bytes()),
    'prDeltaPatchSha256': sha(pr_delta.read_bytes()),
    'reviewedTree': reviewed_tree,
    'texts': {
        'pr-title.txt': {'sha256': sha(title_bytes), 'bytes': len(title_bytes)},
        'pr-body.md': {'sha256': sha(body_bytes), 'bytes': len(body_bytes)},
        'commit-message.txt': {'sha256': sha(message), 'bytes': len(message)},
    },
    'checks': checks,
    'method': 'fresh read-only official clone, bundle fetch, scratch-index tree rebuild and static text rules',
}
(out / 'subject-verification.json').write_text(json.dumps(record, indent=2, ensure_ascii=False) + '\n')
print(json.dumps(record, indent=2, ensure_ascii=False))
raise SystemExit(0 if passed else 1)
