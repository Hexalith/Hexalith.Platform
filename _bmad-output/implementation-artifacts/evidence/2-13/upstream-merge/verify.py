from pathlib import Path
import datetime, gzip, hashlib, json, os, signal, subprocess, tarfile

root = Path.cwd()
scratch = Path(os.environ['TMPDIR'])
clone = scratch / 'merged-platform'
output = root / '_bmad-output/implementation-artifacts/evidence/2-13/upstream-merge'
output.mkdir(exist_ok=False)
baseline = 'ddb1c0b141372a66d4a9d9c231d0041afbb7cd0c'
def git(directory, *args, check=True, data=None):
    result = subprocess.run(['git', '-c', 'submodule.recurse=false', *args], cwd=directory,
                            input=data, capture_output=True)
    if check and result.returncode:
        raise RuntimeError(result.stderr.decode(errors='replace'))
    return result
def value(directory, *args):
    return git(directory, *args).stdout.decode().strip()
def sha(raw): return hashlib.sha256(raw).hexdigest()
def dump(name, data): (output / name).write_text(json.dumps(data, indent=2) + '\n')
upstream = value(root, 'rev-parse', 'origin/main')
assert value(root, 'rev-parse', 'HEAD') == baseline
git(root, 'clone', '--shared', '--no-checkout', str(root), str(clone))
git(clone, 'config', 'user.name', 'Isolated merge verification')
git(clone, 'config', 'user.email', 'fixture@invalid')
git(clone, 'read-tree', baseline)
patch = git(root, 'diff', '--binary', baseline).stdout
git(clone, 'apply', '--cached', '--binary', '--whitespace=nowarn', '-', data=patch)
tree = value(clone, 'write-tree')
candidate = git(clone, '-c', 'commit.gpgsign=false', 'commit-tree', tree, '-p', baseline,
                '-m', 'refactor(ci): verify candidate-only tuple retirement').stdout.decode().strip()
git(clone, 'update-ref', 'refs/heads/story-candidate', candidate)
git(clone, 'symbolic-ref', 'HEAD', 'refs/heads/story-candidate')
git(clone, 'sparse-checkout', 'init', '--no-cone')
git(clone, 'sparse-checkout', 'set', '--no-cone', '/*', '!/*/', '/.github/', '/eng/', '/tests/source-release/',
    '/docs/implementation/story-4-17-broker-candidate-02.json')
git(clone, 'checkout', '--detach', upstream)
git(clone, 'reset', '--hard', upstream)
assert (clone / 'release.config.mjs').is_file()
assert (clone / 'docs/implementation/story-4-17-broker-candidate-02.json').is_file()
assert not value(clone, 'status', '--porcelain')
merge = git(clone, 'merge', '--no-ff', '--no-commit', candidate, check=False)
(output / 'merge.log').write_bytes(merge.stdout + merge.stderr)
conflicts = value(clone, 'diff', '--name-only', '--diff-filter=U').splitlines()
expected = ['eng/legacy-tuple-source.json', 'eng/pre-extension-tuple-source.json']
assert sorted(conflicts) == expected, conflicts
git(clone, 'rm', '--', *conflicts)
git(clone, '-c', 'commit.gpgsign=false', 'commit', '-qm', 'test(ci): prove retirement merge against current main')
merged = value(clone, 'rev-parse', 'HEAD')
merged_tree = value(clone, 'rev-parse', 'HEAD^{tree}')
before = json.loads((root / '_bmad-output/implementation-artifacts/evidence/2-13/before-inventory.json').read_text())
assert value(clone, 'ls-tree', merged + ':references').splitlines() == before['allGitlinks']
for path, digest in before['files'].items():
    assert sha(git(clone, 'show', merged + ':' + path).stdout) == digest
    assert sha(git(clone, 'show', upstream + ':' + path).stdout) == digest
post = json.loads((root / '_bmad-output/implementation-artifacts/evidence/2-13/post-review/source-and-output-before.json').read_text())
production = json.loads((root / '_bmad-output/implementation-artifacts/evidence/2-13/post-review/production-source-comparison.json').read_text())['sourceFiles']
for path, digest in dict(production, **post['sourceFiles']).items():
    assert sha(git(clone, 'show', merged + ':' + path).stdout) == digest, path
for path in ['package.json', 'package-lock.json']:
    assert git(clone, 'show', merged + ':' + path).stdout == git(clone, 'show', upstream + ':' + path).stdout
assert not value(clone, 'ls-files', '--', *expected)
tags = value(clone, 'for-each-ref', '--format=%(objectname) %(refname)', 'refs/tags').splitlines()
assert set(before['tags']).issubset(tags)
assert not value(clone, 'for-each-ref', 'refs/heads/qualification/')
commands = []
for label, command in [('npm-ci', ['npm', 'ci', '--ignore-scripts', '--no-audit', '--no-fund', '--cache', str(scratch / 'npm-cache')]),
                       ('source-release', ['npm', 'run', 'test:source-release'])]:
    with (output / (label + '.log')).open('w') as log:
        process = subprocess.Popen(command, cwd=clone, stdout=log, stderr=subprocess.STDOUT, start_new_session=True)
        try:
            code = process.wait(timeout=600)
        except subprocess.TimeoutExpired:
            os.killpg(process.pid, signal.SIGTERM)
            try: process.wait(timeout=10)
            except subprocess.TimeoutExpired:
                os.killpg(process.pid, signal.SIGKILL)
                process.wait()
            code = 'timeout'
    commands.append({'label': label, 'command': command, 'cwd': str(clone), 'exitCode': code})
    dump('commands.json', commands)
    print(label, code, flush=True)
    assert code == 0, label
import re
text = (output / 'source-release.log').read_text()
counts = {key:int(number) for key,number in re.findall(r'^(?:#|ℹ) (tests|pass|fail|cancelled|skipped|todo) (\d+)$', text, re.M)}
assert counts['tests'] == counts['pass'] == 83 and all(counts[key] == 0 for key in ['fail','cancelled','skipped','todo']), counts
assert not value(clone, 'status', '--porcelain')
code_patch = git(clone, 'diff', '--binary', upstream, merged, '--', '.', ':(exclude)_bmad-output/implementation-artifacts/evidence/**').stdout
(output / 'merge-code.patch.gz').write_bytes(gzip.compress(code_patch, mtime=0))
dump('patch-receipt.json', {'file': 'merge-code.patch.gz', 'sha256': sha((output/'merge-code.patch.gz').read_bytes()),
                          'uncompressedSha256': sha(code_patch), 'uncompressedBytes': len(code_patch), 'excludesEvidence': True})
logs = {path.name: sha(path.read_bytes()) for path in output.glob('*.log')}
with tarfile.open(output / 'raw-logs.tar.gz', 'w:gz') as archive:
    for path in sorted(output.glob('*.log')): archive.add(path, arcname=path.name, recursive=False)
dump('summary.json', {'recordedUtc': datetime.datetime.now(datetime.timezone.utc).isoformat(), 'passed': True,
    'baselineRevision': baseline, 'upstreamRevision': upstream, 'candidateRevision': candidate,
    'mergedRevision': merged, 'mergedTree': merged_tree, 'resolvedModifyDeleteConflicts': conflicts,
    'resolution': 'Retire the two descriptors; preserve upstream release tooling, lockfile and new assertions.',
    'productionAndReviewSourcesMatch': len(dict(production, **post['sourceFiles'])), 'allOriginalPinsGitlinksTagsPreserved': True,
    'originalTags': len(before['tags']), 'currentTags': len(tags), 'sourceReleaseNode': counts,
    'rawLogsArchive': {'file':'raw-logs.tar.gz', 'sha256':sha((output/'raw-logs.tar.gz').read_bytes()), 'members':logs},
    'patchScope': 'Merged code/spec only; evidence remains in the primary worktree.', 'remoteMutations': [],
    'sharedWorktreeRebased': False, 'hostedMainAcceptance': False})
print(json.dumps({'mergedRevision': merged, 'sourceReleasePassed':83, 'preservedPinsAndTags':True}), flush=True)
