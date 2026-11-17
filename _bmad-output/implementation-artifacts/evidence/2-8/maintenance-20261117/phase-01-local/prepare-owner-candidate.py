"""Prepare and verify the committed CiCd proposal in a disposable standalone clone."""
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import time

ROOT = Path('/home/administrator/projects/hexalith/platform')
AREA = ROOT / 'artifacts/2-8-maintenance-20261117'
OUT = AREA / 'owner-candidate-01'
OUT.mkdir(exist_ok=False)
SCRATCH = Path(os.environ['TMPDIR']) / 'maintenance-owner'
SCRATCH.mkdir()
OWNER = SCRATCH / 'Hexalith.CiCd'
BUILDS = OWNER / 'references/Hexalith.Builds'
RESULTS = []


def run(label, arguments, cwd=ROOT, environment=None, timeout=300):
    start = time.monotonic()
    with (OUT / (label + '.log')).open('w') as log:
        result = subprocess.run(arguments, cwd=cwd, env=environment, stdout=log,
                                stderr=subprocess.STDOUT, timeout=timeout, check=False)
    RESULTS.append({'label': label, 'exitCode': result.returncode, 'seconds': round(time.monotonic()-start, 3)})
    print(json.dumps(RESULTS[-1]), flush=True)
    if result.returncode:
        raise RuntimeError(f'{label} failed; see its retained log')


def git(repository, *arguments):
    return subprocess.check_output(['git', '-c', 'submodule.recurse=false', '-C', str(repository), *arguments]).decode().strip()


record = {'classification': 'unpublished owner proposal; no release/runtime acceptance', 'remoteMutations': False}
try:
    run('clone-owner', ['git', 'clone', '--shared', '--no-checkout', str(ROOT/'references/Hexalith.CiCd'), str(OWNER)])
    git(OWNER, 'remote', 'set-url', 'origin', 'https://github.com/Hexalith/Hexalith.CiCd.git')
    run('fetch-owner-main', ['git', 'fetch', '--no-recurse-submodules', 'origin', 'main'], OWNER)
    base = git(OWNER, 'rev-parse', 'refs/remotes/origin/main')
    run('owner-branch', ['git', '-c', 'submodule.recurse=false', 'checkout', '-b', 'fix/story-2-8-maintenance-lanes', 'origin/main'], OWNER)
    record['base'] = base
    run('clone-direct-builds', ['git', 'clone', '--shared', '--no-checkout', str(ROOT/'references/Hexalith.Builds'), str(BUILDS)])
    git(BUILDS, 'remote', 'set-url', 'origin', 'https://github.com/Hexalith/Hexalith.Builds.git')
    pin = git(OWNER, 'rev-parse', 'HEAD:references/Hexalith.Builds')
    run('checkout-direct-builds', ['git', '-c', 'submodule.recurse=false', 'checkout', '--detach', pin], BUILDS)
    record['pinnedBuilds'] = pin
    released = git(BUILDS, 'rev-parse', 'v6.1.0^{commit}')
    paths = ['Github/guard/package.json', 'Github/guard/package-lock.json',
             'Github/guard/commitlint.config.mjs', 'Github/guard/commitlint_bridge.mjs']
    source_proof = {}
    for relative in paths:
        current = subprocess.check_output(['git', '-C', str(BUILDS), 'show', pin + ':' + relative])
        previous = subprocess.check_output(['git', '-C', str(BUILDS), 'show', released + ':' + relative])
        if current != previous:
            raise RuntimeError('Pinned commitlint policy differs from the selected released validator')
        source_proof[relative] = hashlib.sha256(current).hexdigest()
    record['validator'] = {'releasedTag': 'v6.1.0', 'releasedSource': released, 'pinnedSource': pin,
                           'identicalPolicyFiles': source_proof}
    for source in sorted((AREA/'cicd-projection').rglob('*')):
        if not source.is_file():
            continue
        relative = source.relative_to(AREA/'cicd-projection')
        target = OWNER / relative
        if target.exists():
            raise RuntimeError('New proposal path already exists in fetched owner main: ' + str(relative))
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, target)
    sys.dont_write_bytecode = True
    sys.path.insert(0, str(BUILDS/'Github/lib'))
    from guard_tools import Tools
    tools_directory = SCRATCH/'guard-tools'
    tools_directory.mkdir()
    tools = Tools(tools_directory)
    message = 'fix(ci): support bounded story 2.8 maintenance releases\n'
    (OUT/'commit-message.txt').write_text(message)
    tools.lint_message(message, 'CiCd maintenance proposal')
    record['validator']['lockedInstallAndSignaturesPassed'] = True
    record['validator']['exactMessageSha256'] = hashlib.sha256(message.encode()).hexdigest()
    (OUT/'commitlint.json').write_text(json.dumps(record['validator'], indent=2)+'\n')
    run('stage-owner', ['git', 'add', '--', 'tools/maintenance.py', 'tools/tests/test_maintenance.py', 'policies/maintenance'], OWNER)
    run('owner-diff-check', ['git', 'diff', '--cached', '--check'], OWNER)
    run('commit-owner', ['git', 'commit', '-F', str(OUT/'commit-message.txt')], OWNER)
    head = git(OWNER, 'rev-parse', 'HEAD')
    record.update(head=head, tree=git(OWNER, 'rev-parse', 'HEAD^{tree}'))
    with (OUT/'owner.diff').open('wb') as stream:
        subprocess.run(['git', '-C', str(OWNER), 'diff', '--binary', base, head, '--'], stdout=stream, check=True)
    environment = dict(os.environ, PYTHONDONTWRITEBYTECODE='1', MAINTENANCE_OWNER_ROOT=str(OWNER),
                       MAINTENANCE_BUILDS_SOURCE=str(ROOT/'references/Hexalith.Builds'))
    run('owner-write-controls', ['python3', '-B', str(OWNER/'tools/tests/test_maintenance.py')], OWNER, environment, 900)
    run('bundle-owner', ['git', 'bundle', 'create', str(OUT/'cicd-candidate.bundle'), 'refs/heads/fix/story-2-8-maintenance-lanes', '^'+base], OWNER)
    record['status'] = 'verified-local-owner-proposal'
except BaseException as error:
    record.update(status='failed', reason=type(error).__name__+': '+str(error))
    raise
finally:
    record['commands'] = RESULTS
    (OUT/'result.json').write_text(json.dumps(record, indent=2)+'\n')
