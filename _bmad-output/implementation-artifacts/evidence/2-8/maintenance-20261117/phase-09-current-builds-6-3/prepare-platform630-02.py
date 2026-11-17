"""Recover the prepared current source and fix exact-producer acquisition in the normal path."""
import json
import os
from pathlib import Path
import subprocess

ROOT = Path('/home/administrator/projects/hexalith/platform')
AREA = ROOT / 'artifacts/2-8-current-20261117'
OUT = AREA / 'platform630-source-02'
OUT.mkdir(exist_ok=False)
REPO = Path(os.environ['TMPDIR']) / 'Hexalith.Platform'
BUILDS = REPO.parent / 'Hexalith.Builds'
BASE = '11719c5cf51c2b2b080b89e0e301c2ef8dbacc46'
FIRST = '1ef875d775293d71de96e21126259bd89d390285'
SOURCE = '7afc4981e21bc2fcce687199a3fd2c61fe87336e'
BRANCH = 'fix/story-2-8-builds-6-3'
NODE = Path('/home/administrator/.nvm/versions/node/v24.22.1/bin/node')
ENV = dict(os.environ, PATH=str(NODE.parent) + ':' + os.environ['PATH'], PYTHONDONTWRITEBYTECODE='1')
RECORD = {'base': BASE, 'buildsSource': SOURCE, 'buildsVersion': '6.3.0', 'remoteMutations': False, 'commands': []}


def run(label, argv, cwd=REPO, payload=None, timeout=300, required=True):
    result = subprocess.run(argv, cwd=cwd, env=ENV, input=payload, capture_output=True, text=True, timeout=timeout)
    (OUT / (label + '.log')).write_text(result.stdout + result.stderr)
    RECORD['commands'].append({'label': label, 'exitCode': result.returncode})
    print(label, result.returncode, flush=True)
    if required and result.returncode:
        raise RuntimeError(label)
    return result


def git(label, *args):
    return run(label, ['git', '-c', 'submodule.recurse=false', *args]).stdout.strip()


def replace(path, before, after, count=1):
    text = path.read_text()
    assert text.count(before) == count, (path, before, text.count(before))
    path.write_text(text.replace(before, after))


try:
    run('clone', ['git', 'clone', '--shared', '--no-checkout', str(ROOT), str(REPO)], ROOT)
    git('origin', 'remote', 'set-url', 'origin', 'https://github.com/Hexalith/Hexalith.Platform.git')
    git('recover', 'fetch', str(AREA / 'platform630-source-01/Platform.bundle'), 'refs/heads/' + BRANCH)
    git('checkout', 'checkout', '-b', BRANCH, FIRST)
    artifact = REPO / 'eng/prepare_builds_artifacts.py'
    replace(artifact, 'import argparse\n', 'import argparse\nfrom contextlib import ExitStack\n')
    replace(artifact, 'import subprocess\n', 'import subprocess\nimport tempfile\n')
    replace(artifact, 'from bound_builds import inspection_environment, verify_bound_checkout',
            'from bound_builds import inspection_environment, materialize, verify_bound_checkout')
    replace(artifact, 'def recorded_preparation_context(binding=None):',
            'def recorded_preparation_context(binding=None, *, builds_root=None):')
    replace(artifact, 'verify_bound_checkout(ROOT, resolve_builds_root(ROOT))}',
            'verify_bound_checkout(ROOT, resolve_builds_root(ROOT) if builds_root is None else builds_root)}')
    replace(artifact, 'if recorded_preparation_context(binding) != original:',
            'builds_root = original["boundProducerContext"].get("buildsRoot")\n'
            '    if recorded_preparation_context(binding, builds_root=builds_root) != original:')
    text = artifact.read_text()
    start = text.index('    recorded = recorded_preparation_context(binding) if args.require_recorded else None\n')
    end = text.index('\n\n\nif __name__', start)
    body = text[start:end]
    body = body.replace('    recorded = recorded_preparation_context(binding) if args.require_recorded else None\n',
        '    builds_root = None\n'
        '    if args.require_recorded:\n'
        '        temporary = cleanup.enter_context(tempfile.TemporaryDirectory(prefix="platform-builds-"))\n'
        '        builds_root = materialize(ROOT, Path(temporary) / "Hexalith.Builds")["buildsRoot"]\n'
        '    recorded = recorded_preparation_context(binding, builds_root=builds_root) if args.require_recorded else None\n')
    text = text[:start] + '    # Own the exact producer checkout for the entire verification, including the final recheck.\n    with ExitStack() as cleanup:\n' + ''.join('    ' + line if line.strip() else line for line in body.splitlines(keepends=True)) + text[end:]
    artifact.write_text(text)
    tests = REPO / 'eng/test_builds_tuple.py'
    replace(tests, 'def context(_binding):', 'def context(_binding, *, builds_root=None):')
    # Existing synthetic archive/refusal controls do not acquire a real producer; real Git coverage is below.
    text = tests.read_text()
    anchor = 'stack.enter_context(patch.object(prepare_builds_artifacts, "recorded_preparation_context",'
    lines = text.splitlines(keepends=True)
    for index in reversed(range(len(lines))):
        if anchor in lines[index]:
            indent = lines[index][:len(lines[index]) - len(lines[index].lstrip())]
            # The first control assigns the context mock; its leading assignment must stay on its original line.
            lines.insert(index, indent + 'stack.enter_context(patch.object(prepare_builds_artifacts, "materialize", return_value={"buildsRoot": str(self.root / "synthetic-producer")}))\n')
    tests.write_text(''.join(lines))
    bound = REPO / 'eng/test_bound_builds.py'
    replace(bound, '    def test_recorded_artifacts_refuse_wrong_dirty_or_hidden_bound_inputs(self):\n',
        '''    def test_normal_recorded_preparation_materializes_raw_bytes_and_rechecks_that_exact_producer(self):
        owner, producer, _ = self.recorded_owner()
        # Restore the normal CRLF checkout, which cannot itself supply exact catalog bytes.
        (producer / ".git/info/attributes").unlink()
        (producer / "Props/Directory.Packages.props").unlink()
        git(producer, "checkout", "--", "Props/Directory.Packages.props")
        before = (producer / "Props/Directory.Packages.props").read_bytes()
        self.assertIn(b"\\r\\n", before)
        with patch.object(prepare_builds_artifacts, "ROOT", owner):
            with self.assertRaisesRegex(ValueError, "working bytes differ"):
                prepare_builds_artifacts.recorded_preparation_context()
            with tempfile.TemporaryDirectory() as temporary:
                exact = materialize(owner, Path(temporary) / "producer", producer)
                context = prepare_builds_artifacts.recorded_preparation_context(builds_root=exact["buildsRoot"])
                prepare_builds_artifacts.require_unchanged_recorded_context(context)
                # The end guard must inspect the same private source, not resolve the CRLF sibling again.
                (Path(exact["buildsRoot"]) / "Hexalith.Build.props").write_text("changed producer")
                with self.assertRaisesRegex(ValueError, "modified or untracked"):
                    prepare_builds_artifacts.require_unchanged_recorded_context(context)
            self.assertFalse(Path(exact["buildsRoot"]).exists())
        self.assertEqual(before, (producer / "Props/Directory.Packages.props").read_bytes())
        self.assertEqual("", git(producer, "status", "--porcelain"))

    def test_recorded_artifacts_refuse_wrong_dirty_or_hidden_bound_inputs(self):
''')
    run('clone-builds', ['git', 'clone', '--shared', '--no-checkout', str(ROOT / 'references/Hexalith.Builds'), str(BUILDS)])
    run('fetch-builds', ['git', 'fetch', '--no-recurse-submodules', 'https://github.com/Hexalith/Hexalith.Builds.git', SOURCE], BUILDS)
    run('checkout-builds', ['git', 'checkout', '--detach', SOURCE], BUILDS)
    for label, script in [('tuple-controls', 'test_builds_tuple.py'), ('publication-controls', 'test_builds_publication.py'),
                          ('bound-producer-controls', 'test_bound_builds.py'), ('collector-controls', 'test_tuple_collectors.py'),
                          ('runtime-controls', 'test_tuple_runtime_context.py'), ('source-controls', 'test_current_tuple_source.py')]:
        run(label, ['python3', '-B', 'eng/' + script])
    run('npm-ci', ['npm', 'ci', '--ignore-scripts', '--no-fund'])
    run('signatures', ['npm', 'audit', 'signatures'])
    run('source-release-controls', ['npm', 'run', 'test:source-release'])
    message = 'fix(ci): acquire the exact bound producer for artifact verification\n'
    (OUT / 'commit-message.txt').write_text(message)
    run('commitlint', [str(NODE), str(REPO / 'node_modules/@commitlint/cli/cli.js'), '--config', str(REPO / 'commitlint.config.mjs'), '--color=false'], payload=message)
    git('stage', 'add', '--', 'eng/prepare_builds_artifacts.py', 'eng/test_builds_tuple.py', 'eng/test_bound_builds.py')
    git('diff-check', 'diff', '--cached', '--check')
    git('commit', 'commit', '-F', str(OUT / 'commit-message.txt'))
    RECORD.update(source=git('source', 'rev-parse', 'HEAD'), tree=git('tree', 'rev-parse', 'HEAD^{tree}'), branch=BRANCH)
    git('source-diff', 'diff', '--binary', BASE, 'HEAD')
    git('bundle', 'bundle', 'create', str(OUT / 'Platform.bundle'), 'refs/heads/' + BRANCH, '^' + BASE)
    # Real public metadata, signed archive, exact executable identity; no AppHost or native runtime starts.
    preparation = run('normal-published-artifact-preparation', ['python3', '-B', 'eng/prepare_builds_artifacts.py', '--output',
        str(REPO.parent / 'verified-publication'), '--require-recorded'], timeout=300, required=False)
    RECORD['normalArtifactPreparationExitCode'] = preparation.returncode
    identity = REPO.parent / 'verified-publication/artifact-identity.json'
    if identity.exists():
        (OUT / 'artifact-identity.json').write_bytes(identity.read_bytes())
    RECORD['status'] = 'prepared-and-normal-artifact-path-passed' if preparation.returncode == 0 else 'prepared; normal-artifact-path-needs-correction'
except BaseException as error:
    RECORD.update(status='incomplete', reason=type(error).__name__ + ': ' + str(error))
    raise
finally:
    if REPO.exists():
        subprocess.run(['git', 'bundle', 'create', str(OUT / 'recoverable.bundle'), 'refs/heads/' + BRANCH, '^' + BASE], cwd=REPO, capture_output=True)
        subprocess.run(['git', 'diff', '--binary', FIRST, '--', 'eng/prepare_builds_artifacts.py', 'eng/test_builds_tuple.py', 'eng/test_bound_builds.py'], cwd=REPO, stdout=(OUT / 'recoverable.patch').open('w'), stderr=subprocess.DEVNULL)
    (OUT / 'result.json').write_text(json.dumps(RECORD, indent=2) + '\n')
