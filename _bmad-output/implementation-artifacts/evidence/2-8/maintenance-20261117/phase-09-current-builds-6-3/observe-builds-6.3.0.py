"""Verify the released 6.x handoff, inventory and public signed package without starting services."""
import hashlib
import io
import json
import os
from pathlib import Path, PurePosixPath
import subprocess
import urllib.request
import zipfile

ROOT = Path('/home/administrator/projects/hexalith/platform')
AREA = ROOT / 'artifacts/2-8-current-20261117'
OUT = AREA / 'builds630-observation-01'
OUT.mkdir(exist_ok=False)
TMP = Path(os.environ['TMPDIR'])
SOURCE = '7afc4981e21bc2fcce687199a3fd2c61fe87336e'
VERSION = '6.3.0'
RECORD = {'source': SOURCE, 'version': VERSION, 'classification': 'published artifact identity only',
          'candidateAcceptance': False, 'runtimeAcceptance': False, 'commands': []}


def command(label, argv, cwd=ROOT):
    result = subprocess.run(argv, cwd=cwd, capture_output=True, timeout=120)
    (OUT / (label + '.stdout.log')).write_bytes(result.stdout)
    (OUT / (label + '.stderr.log')).write_bytes(result.stderr)
    RECORD['commands'].append({'label': label, 'exitCode': result.returncode})
    print(label, result.returncode, flush=True)
    if result.returncode:
        raise RuntimeError(label)
    return result.stdout


def checked_zip(raw):
    archive = zipfile.ZipFile(io.BytesIO(raw))
    names = archive.namelist()
    assert len(names) == len(set(names)) and archive.testzip() is None
    for name in names:
        path = PurePosixPath(name)
        assert not path.is_absolute() and '..' not in path.parts and '\\' not in name
        assert ((archive.getinfo(name).external_attr >> 16) & 0o170000) != 0o120000
    return archive


try:
    release = json.loads((AREA / 'builds-release-6.3.0.json').read_text())
    tag = json.loads((AREA / 'builds-tag-6.3.0.json').read_text())
    run = json.loads((AREA / 'builds-publisher-6.3.0.json').read_text())
    assert release['id'] == 449037811 and release['tag_name'] == 'v' + VERSION and not release['draft'] and not release['prerelease']
    assert tag['object'] == {'sha': SOURCE, 'type': 'commit', 'url': 'https://api.github.com/repos/Hexalith/Hexalith.Builds/git/commits/' + SOURCE}
    assert run['id'] == 44147684659 and run['head_sha'] == SOURCE and run['conclusion'] == 'success'
    assert run['status'] == 'completed' and run['path'] == '.github/workflows/builds-release.yml'
    body = release['body']
    markers = {}
    for name in ['hexalith-release', 'hexalith-release-run', 'hexalith-release-artifact', 'hexalith-release-packages', 'hexalith-release-containers']:
        values = [line.partition(':')[2].strip() for line in body.splitlines() if line.startswith(name + ':')]
        assert len(values) == 1
        markers[name] = values[0]
    assert markers['hexalith-release'] == SOURCE and markers['hexalith-release-run'] == str(run['id'])
    assert markers['hexalith-release-packages'] == 'Hexalith.Builds.Evidence.Cli,Hexalith.Builds.Module.Cli'
    assert markers['hexalith-release-containers'] == ''
    artifact_id, artifact_digest = markers['hexalith-release-artifact'].split(' ')
    assert int(artifact_id) == 14357353124
    inventory_raw = (AREA / 'hexalith-package-publication-6.3.0.v1.json').read_bytes()
    inventory = json.loads(inventory_raw)
    assert inventory['schema'] == 'hexalith.package-publication.v1'
    assert inventory['repository'] == 'Hexalith/Hexalith.Builds' and inventory['sha'] == SOURCE and inventory['version'] == VERSION
    assert inventory['artifactId'] == int(artifact_id) and inventory['artifactRun'] == run['id'] and inventory['artifactDigest'] == artifact_digest
    asset = next(a for a in release['assets'] if a['name'] == 'hexalith-package-publication.v1.json')
    assert asset['digest'] == 'sha256:' + hashlib.sha256(inventory_raw).hexdigest()
    artifact = json.loads(command('artifact-metadata', ['gh', 'api', 'repos/Hexalith/Hexalith.Builds/actions/artifacts/' + artifact_id]))
    assert artifact['workflow_run']['id'] == run['id'] and artifact['workflow_run']['head_sha'] == SOURCE
    assert not artifact['expired'] and artifact['digest'] == artifact_digest
    artifact_path = TMP / 'publication.zip'
    with artifact_path.open('wb') as stream:
        result = subprocess.run(['gh', 'api', 'repos/Hexalith/Hexalith.Builds/actions/artifacts/' + artifact_id + '/zip'],
                                stdout=stream, stderr=subprocess.PIPE, timeout=180)
    assert result.returncode == 0, result.stderr
    artifact_bytes = artifact_path.read_bytes()
    assert 'sha256:' + hashlib.sha256(artifact_bytes).hexdigest() == artifact_digest
    archive = checked_zip(artifact_bytes)
    expected = {'sha': SOURCE, 'version': VERSION, 'packages': [
        {'id': name, 'path': 'packages/' + name + '.' + VERSION + '.nupkg'}
        for name in ['Hexalith.Builds.Evidence.Cli', 'Hexalith.Builds.Module.Cli']], 'containers': []}
    manifests = []
    for name in archive.namelist():
        if name.endswith('.json'):
            try:
                raw = archive.read(name)
                if json.loads(raw) == expected:
                    manifests.append((name, raw))
            except (ValueError, UnicodeDecodeError):
                pass
    assert len(manifests) == 1
    manifest_name, manifest_bytes = manifests[0]
    for package in inventory['packages']:
        name = next(row['path'] for row in expected['packages'] if row['id'] == package['id'])
        package_bytes = archive.read(name)
        assert hashlib.sha256(package_bytes).hexdigest() == package['archiveSha256']
        package_zip = checked_zip(package_bytes)
        actual_files = {n: hashlib.sha256(package_zip.read(n)).hexdigest() for n in package_zip.namelist() if not n.endswith('/')}
        assert actual_files == package['files']
    (OUT / 'original-release-manifest.json').write_bytes(manifest_bytes)
    repo = TMP / 'Hexalith.Builds'
    command('clone', ['git', 'clone', '--shared', '--no-checkout', str(ROOT / 'references/Hexalith.Builds'), str(repo)])
    command('fetch', ['git', 'fetch', '--no-recurse-submodules', 'https://github.com/Hexalith/Hexalith.Builds.git', SOURCE], repo)
    command('checkout', ['git', 'checkout', '--detach', SOURCE], repo)
    catalog = command('catalog', ['git', 'show', 'HEAD:Props/Directory.Packages.props'], repo)
    blob = command('catalog-blob', ['git', 'rev-parse', 'HEAD:Props/Directory.Packages.props'], repo).decode().strip()
    url = 'https://api.nuget.org/v3-flatcontainer/hexalith.builds.module.cli/' + VERSION + '/hexalith.builds.module.cli.' + VERSION + '.nupkg'
    with urllib.request.urlopen(url, timeout=120) as response:
        signed_bytes = response.read()
    signed_path = TMP / ('hexalith.builds.module.cli.' + VERSION + '.nupkg')
    signed_path.write_bytes(signed_bytes)
    signed = checked_zip(signed_bytes)
    module = next(p for p in inventory['packages'] if p['id'] == 'Hexalith.Builds.Module.Cli')
    assert '.signature.p7s' in signed.namelist()
    public_files = {n: hashlib.sha256(signed.read(n)).hexdigest() for n in signed.namelist() if not n.endswith('/') and n != '.signature.p7s'}
    assert public_files == module['files']
    command('nuget-signature', ['dotnet', 'nuget', 'verify', str(signed_path), '--all'], TMP)
    extracted = TMP / 'published'
    signed.extractall(extracted)
    tool = extracted / 'tools/net11.0/any'
    actual_version = command('cli-version', ['dotnet', str(tool / 'Hexalith.Builds.Module.Cli.dll'), '--version'], TMP).decode().strip()
    assert actual_version == VERSION + '+' + SOURCE
    tooling = public_files['tools/net11.0/any/Hexalith.Builds.Tooling.dll']
    host = public_files['tools/net11.0/any/g4-host/bin/EventStoreHost/Hexalith.Builds.Module.EventStoreHost.dll']
    catalog_sha = hashlib.sha256(catalog).hexdigest()
    binding = {'schemaVersion': 1, 'candidateAcceptance': False,
        'producer': {'repository': 'https://github.com/Hexalith/Hexalith.Builds.git', 'sourceRevision': SOURCE,
            'catalog': {'path': 'Props/Directory.Packages.props', 'blob': blob, 'sha256': catalog_sha}},
        'publication': {'kind': 'shared-release-handoff/v1', 'version': VERSION, 'sourceRevision': SOURCE,
            'releaseTag': 'v' + VERSION, 'catalogSha256': catalog_sha,
            'archiveSha256': hashlib.sha256(signed_bytes).hexdigest(), 'toolingSha256': tooling,
            'releaseId': release['id'], 'runId': run['id'], 'artifactId': int(artifact_id),
            'artifactSha256': artifact_digest.removeprefix('sha256:'), 'releaseBodySha256': hashlib.sha256(body.encode()).hexdigest(),
            'manifestJson': manifest_bytes.decode(), 'manifestSha256': hashlib.sha256(manifest_bytes).hexdigest()}}
    RECORD.update(status='verified-official-shared-release-and-signed-package', binding=binding,
        apphostSha256=host, inventoryAssetDigest=asset['digest'], officialArtifactDigestAndCrcVerified=True,
        sourcePayloadMatchesInventoryAndSignedPackage=True, nugetSignatureVerified=True, nativeQualificationCredit=False)
    (AREA / 'builds630-binding.json').write_text(json.dumps(binding, indent=2) + '\n')
    (AREA / 'builds630-observation.json').write_text(json.dumps(RECORD, indent=2) + '\n')
except BaseException as error:
    RECORD.update(status='incomplete', reason=type(error).__name__ + ': ' + str(error))
    raise
finally:
    (OUT / 'result.json').write_text(json.dumps(RECORD, indent=2) + '\n')
