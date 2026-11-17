"""Complete exact consumer and signed validation-host pairing for Builds 6.3.0."""
import hashlib
import json
import os
from pathlib import Path
import subprocess
import xml.etree.ElementTree as ET

ROOT = Path('/home/administrator/projects/hexalith/platform')
AREA = ROOT / 'artifacts/2-8-current-20261117'
OUT = AREA / 'platform630-source-06'
OUT.mkdir(exist_ok=False)
REPO = Path(os.environ['TMPDIR']) / 'Hexalith.Platform'
BUILDS = REPO.parent / 'Hexalith.Builds'
BASE = '11719c5cf51c2b2b080b89e0e301c2ef8dbacc46'
SOURCE = '7afc4981e21bc2fcce687199a3fd2c61fe87336e'
BRANCH = 'fix/story-2-8-builds-6-3'
PREVIOUS = json.loads((AREA / 'platform630-source-05/result.json').read_text())
assert PREVIOUS['status'] == 'incomplete' and PREVIOUS['reason'] == 'RuntimeError: current-artifact-controls'
NODE = Path('/home/administrator/.nvm/versions/node/v24.22.1/bin/node')
ENV = dict(os.environ, PATH=str(NODE.parent) + ':' + os.environ['PATH'], PYTHONDONTWRITEBYTECODE='1')
RECORD = {'base': BASE, 'buildsVersion': '6.3.0', 'remoteMutations': [], 'commands': []}
IDENTITY = json.loads((AREA / 'platform630-source-03/artifact-identity.json').read_text())
HOST = IDENTITY['files']['tools/net11.0/any/g4-host/Hexalith.Builds.Module.AppHost.dll']
EVENTSTORE = IDENTITY['files']['tools/net11.0/any/g4-host/bin/EventStoreHost/Hexalith.Builds.Module.EventStoreHost.dll']
TOOLING = IDENTITY['files']['tools/net11.0/any/g4-host/Hexalith.Builds.Tooling.dll']
assert HOST == '3050b20b0037ab62470e4bb42a7e08512181b6abbbc02bb12de17b0f1e75f0a1'
assert TOOLING == '48448ba24ceb2d265ad4b641e6587c79547167c0563954c662807824fdd6be27'
(OUT / 'signed-host-identities.json').write_text(json.dumps({'appHostSha256': HOST, 'eventStoreHostSha256': EVENTSTORE,
    'toolingSha256': TOOLING, 'archiveSha256': IDENTITY['archiveSha256'], 'qualification': 'artifact identity only',
    'correction': 'The earlier collector apphostSha256 label contained the distinct EventStoreHost member; this record binds the actual AppHost member.'}, indent=2) + '\n')


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


def replace(name, before, after, count=1):
    path = REPO / name
    raw = path.read_bytes()
    assert raw.count(before.encode()) == count, (name, before, raw.count(before.encode()))
    path.write_bytes(raw.replace(before.encode(), after.encode()))


try:
    run('clone', ['git', 'clone', '--shared', '--no-checkout', str(ROOT), str(REPO)], ROOT)
    git('origin', 'remote', 'set-url', 'origin', 'https://github.com/Hexalith/Hexalith.Platform.git')
    git('recover', 'fetch', str(AREA / 'platform630-source-05/Platform.bundle'), 'refs/heads/' + BRANCH)
    git('checkout', 'checkout', '-b', BRANCH, PREVIOUS['source'])
    run('clone-builds', ['git', 'clone', '--shared', '--no-checkout', str(ROOT / 'references/Hexalith.Builds'), str(BUILDS)])
    run('fetch-builds', ['git', 'fetch', '--no-recurse-submodules', 'https://github.com/Hexalith/Hexalith.Builds.git', SOURCE], BUILDS)
    run('checkout-builds', ['git', 'checkout', '--detach', SOURCE], BUILDS)
    run('clone-direct-builds', ['git', 'clone', '--shared', '--no-checkout', str(BUILDS), str(REPO / 'references/Hexalith.Builds')])
    run('checkout-direct-builds', ['git', 'checkout', '--detach', SOURCE], REPO / 'references/Hexalith.Builds')
    for owner in ('EventStore', 'Parties', 'Tenants', 'Memories'):
        name = 'Hexalith.' + owner
        selected = git('owner-pin-' + owner, 'rev-parse', 'HEAD:references/' + name)
        target = REPO / 'references' / name
        run('owner-clone-' + owner, ['git', 'clone', '--shared', '--no-checkout', str(ROOT / 'references' / name), str(target)])
        run('owner-checkout-' + owner, ['git', 'checkout', '--detach', selected], target)
    RECORD.update(source=git('source', 'rev-parse', 'HEAD'), tree=git('tree', 'rev-parse', 'HEAD^{tree}'), branch=BRANCH)
    git('bundle', 'bundle', 'create', str(OUT / 'Platform.bundle'), 'refs/heads/' + BRANCH, '^' + BASE)
    git('source-diff', 'diff', '--binary', BASE, 'HEAD')
    run('actual-source-closure', ['python3', '-B', '-c', 'import sys; from pathlib import Path; sys.path.insert(0,"eng"); '
        'from current_tuple_source import verify_checkout; import json; print(json.dumps(verify_checkout(Path.cwd(),"' + RECORD['source'] + '"),indent=2))'])
    run('normal-published-artifact-preparation', ['python3', '-B', 'eng/prepare_builds_artifacts.py', '--output',
        str(REPO.parent / 'verified-publication'), '--require-recorded'], timeout=300)
    observed = json.loads((REPO.parent / 'verified-publication/artifact-identity.json').read_bytes())
    (OUT / 'artifact-identity.json').write_text(json.dumps(observed, indent=2) + '\n')
    assert observed['files']['tools/net11.0/any/g4-host/Hexalith.Builds.Module.AppHost.dll'] == HOST
    assert not Path(observed['boundProducerContext']['buildsRoot']).exists()
    tool = Path(observed['toolRoot'])
    properties = ['-p:Configuration=Release', '-p:UseHexalithProjectReferences=false', '-p:HexalithEventStoreVersion=4.2.0',
        '-p:HexalithTenantsVersion=7.0.7', '-p:HexalithMemoriesVersion=3.0.27', '-p:HexalithPartiesVersion=3.1.1',
        '-p:HexalithMcpCliVersion=2.0.1', '-p:HexalithBuildsRoot=' + str(BUILDS), '-p:HexalithBuildsPublishedToolRoot=' + str(tool)]
    project = 'tests/Hexalith.Platform.Composition.Tests/Hexalith.Platform.Composition.Tests.csproj'
    run('build-owning-controls', ['dotnet', 'build', project, '--configuration', 'Release', '--disable-build-servers', '-m:1', '-warnaserror', *properties], timeout=900)
    assembly = run('owning-control-path', ['dotnet', 'msbuild', project, '-nologo', *properties, '-getProperty:TargetPath']).stdout.strip()
    for label, name, count in [('current-artifact-controls', 'PlatformCurrentPackageArtifactCohortTests', 17),
                               ('current-signed-handoff-controls', 'PlatformCurrentOwnerHandoffTests', 8)]:
        xml = OUT / (label + '.xml')
        run(label, ['dotnet', assembly, '-class', 'Hexalith.Platform.Composition.Tests.' + name, '-result-xml', str(xml)], timeout=600)
        counts = ET.parse(xml).getroot().find('assembly').attrib
        assert int(counts['total']) == int(counts['passed']) == count, counts
        assert not any(int(counts.get(key, '0')) for key in ('failed', 'errors', 'skipped', 'not-run')), counts
    old = git('remote-before-push', 'ls-remote', '--heads', 'origin', BRANCH).split()[0]
    assert old == 'b5c3114a5a9b42adfd42668bcf1fcd2964d625fc'
    git('push', 'push', '--no-recurse-submodules', 'origin', 'HEAD:refs/heads/' + BRANCH)
    RECORD['remoteMutations'].append({'action': 'normal-fast-forward-update-pr46', 'sha': RECORD['source']})
    RECORD.update(status='fully-prepared-pr-awaiting-hosted-ci', prUrl=PREVIOUS['prUrl'])
except BaseException as error:
    RECORD.update(status='incomplete', reason=type(error).__name__ + ': ' + str(error))
    raise
finally:
    if REPO.exists():
        subprocess.run(['git', 'bundle', 'create', str(OUT / 'recoverable.bundle'), 'refs/heads/' + BRANCH, '^' + BASE], cwd=REPO, capture_output=True)
        with (OUT / 'recoverable.patch').open('w') as stream:
            subprocess.run(['git', 'diff', '--binary', PREVIOUS['source']], cwd=REPO, stdout=stream, stderr=subprocess.DEVNULL)
    (OUT / 'result.json').write_text(json.dumps(RECORD, indent=2) + '\n')
