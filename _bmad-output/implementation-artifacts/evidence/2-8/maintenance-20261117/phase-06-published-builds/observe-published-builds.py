"""Read genuine publication evidence and verify the public signed package; no services."""
import datetime
import hashlib
import io
import json
import os
from pathlib import Path, PurePosixPath
import re
import shutil
import subprocess
import urllib.request
import zipfile

ROOT = Path('/home/administrator/projects/hexalith/platform')
AREA = ROOT / 'artifacts/2-8-maintenance-20261117'
OUT = AREA / 'builds-published-observation-01'
SCRATCH = Path(os.environ['TMPDIR'])
SOURCE = '979cfd3e2c4145be516fbd833dacfc6b8e8fdce5'
RUN = 44133307523
JOB = 134250485020
ATTEMPT = 2
REPOSITORY = 'Hexalith/Hexalith.Builds'
OUT.mkdir(exist_ok=False)
record = {'source': SOURCE, 'run': RUN, 'attempt': ATTEMPT, 'job': JOB,
          'remoteMutations': False, 'classification': 'publication and artifact identity only',
          'candidateAcceptance': False, 'commands': []}


def command(label, argv, cwd=ROOT, timeout=90, binary=False):
    result = subprocess.run(argv, cwd=cwd, capture_output=True, timeout=timeout)
    (OUT / (label + ('.bin' if binary else '.stdout.log'))).write_bytes(result.stdout)
    (OUT / (label + '.stderr.log')).write_bytes(result.stderr)
    record['commands'].append({'label': label, 'exitCode': result.returncode})
    print(label, result.returncode, flush=True)
    if result.returncode:
        raise RuntimeError(label + ' failed; inspect retained output')
    return result.stdout if binary else result.stdout.decode().strip()


def api(label, path):
    result = json.loads(command(label, ['gh', 'api', path]))
    (OUT / (label + '.json')).write_text(json.dumps(result, indent=2) + '\n')
    return result


def checked_zip(payload):
    package = zipfile.ZipFile(io.BytesIO(payload))
    names = set()
    for item in package.infolist():
        name = PurePosixPath(item.filename)
        if name.is_absolute() or '..' in name.parts or '\\' in item.filename or item.filename in names:
            raise ValueError('Unsafe or duplicate archive member')
        if ((item.external_attr >> 16) & 0o170000) == 0o120000:
            raise ValueError('Archive contains a symbolic link')
        names.add(item.filename)
    if package.testzip() is not None:
        raise ValueError('Archive CRC verification failed')
    return package


try:
    run = api('run', f'repos/{REPOSITORY}/actions/runs/{RUN}')
    assert run['head_sha'] == SOURCE and run['head_branch'] == '4.44.x'
    assert run['run_attempt'] == ATTEMPT and run['workflow_id'] == 152656352
    assert run['event'] == 'workflow_dispatch' and run['path'] == '.github/workflows/build-release.yml'
    assert run['status'] == 'completed' and run['conclusion'] == 'success'
    jobs = api('jobs', f'repos/{REPOSITORY}/actions/runs/{RUN}/attempts/{ATTEMPT}/jobs')
    assert len(jobs['jobs']) == 2 and all(j['conclusion'] == 'success' for j in jobs['jobs'])
    job = next(j for j in jobs['jobs'] if j['id'] == JOB)
    assert job['head_sha'] == SOURCE and job['run_attempt'] == ATTEMPT and job['name'] == 'Release'
    steps = {s['name']: s for s in job['steps']}
    for name in ['Recheck approved release source and push CI',
                 'Authenticate to NuGet.org with Trusted Publishing', 'Create Release']:
        assert steps[name]['status'] == 'completed' and steps[name]['conclusion'] == 'success'
    command('release-job', ['gh', 'api', f'repos/{REPOSITORY}/actions/jobs/{JOB}/logs'], timeout=120)
    artifacts = api('artifacts', f'repos/{REPOSITORY}/actions/runs/{RUN}/artifacts?per_page=100')
    expected_name = f'g4-release-qualification-{SOURCE}-{RUN}-{ATTEMPT}'
    matches = [a for a in artifacts['artifacts'] if a['name'] == expected_name]
    assert len(matches) == 1 and not matches[0]['expired']
    artifact = matches[0]
    payload = command('qualification', ['gh', 'api', f'repos/{REPOSITORY}/actions/artifacts/{artifact["id"]}/zip'], binary=True, timeout=120)
    assert artifact['digest'] == 'sha256:' + hashlib.sha256(payload).hexdigest()
    outer = checked_zip(payload)
    inner_names = [n for n in outer.namelist() if PurePosixPath(n).name == 'g4-tool-qualification-evidence.zip']
    assert len(inner_names) == 1
    inner = checked_zip(outer.read(inner_names[0]))
    producers = [n for n in inner.namelist() if n == 'producer-run.json']
    inventories = [n for n in inner.namelist() if n.endswith('g4-tool-package-inventory.json')]
    assert len(producers) == len(inventories) == 1
    producer = json.loads(inner.read(producers[0]))
    expected_context = {'GITHUB_REPOSITORY': REPOSITORY, 'GITHUB_RUN_ID': str(RUN),
                        'GITHUB_RUN_ATTEMPT': str(ATTEMPT), 'GITHUB_JOB': 'release',
                        'GITHUB_WORKFLOW_REF': REPOSITORY + '/.github/workflows/build-release.yml@refs/heads/4.44.x',
                        'GITHUB_WORKFLOW_SHA': SOURCE, 'GITHUB_EVENT_NAME': 'workflow_dispatch',
                        'kind': 'release', 'sourceRevision': SOURCE}
    for key, value in expected_context.items():
        assert producer[key] == value, key
    inventory_bytes = inner.read(inventories[0])
    inventory = json.loads(inventory_bytes)
    assert producer['schema'] == 'hexalith.g4-qualification-carrier.v1'
    assert inventory['schema'] == 'hexalith.g4-tool-package-inventory.v1' and inventory['configuration'] == 'Release'
    rows = inventory['qualificationEvidence']
    assert rows and producer['qualificationEvidenceFiles'] == len(rows)
    assert set(inner.namelist()) == {'producer-run.json', 'g4-tool-package-inventory.json', *[r['file'] for r in rows]}
    for row in rows:
        data = inner.read(row['file'])
        assert type(row['sizeBytes']) is int and row['sizeBytes'] == len(data)
        assert hashlib.sha256(data).hexdigest() == row['sha256'].lower()
    outer_inventories = [n for n in outer.namelist() if PurePosixPath(n).name == 'g4-tool-package-inventory.json']
    assert len(outer_inventories) == 1 and outer.read(outer_inventories[0]) == inventory_bytes
    version = inventory['version']
    assert re.fullmatch(r'4\.44\.(0|[1-9][0-9]*)', version) and producer['version'] == version
    assert producer['inventorySha256'] == hashlib.sha256(inventory_bytes).hexdigest()
    assert inventory['qualification']['sourceTree'] == {'clean': True, 'revision': SOURCE}
    assert inventory['qualification']['releaseEligible'] is True
    assert inventory['qualification']['ineligibilityReasons'] == []
    (OUT / 'actual-producer.json').write_text(json.dumps(producer, indent=2) + '\n')
    (OUT / 'g4-tool-package-inventory.json').write_bytes(inventory_bytes)
    release = api('release', f'repos/{REPOSITORY}/releases/tags/v{version}')
    assert release['tag_name'] == 'v' + version and not release['draft'] and not release['prerelease']
    tag = api('release-tag', f'repos/{REPOSITORY}/git/ref/tags/v{version}')
    target = tag['object']
    if target['type'] == 'tag':
        target = api('annotated-tag', f'repos/{REPOSITORY}/git/tags/{target["sha"]}')['object']
    assert target['type'] == 'commit' and target['sha'] == SOURCE
    asset = [a for a in release['assets'] if a['name'] == 'g4-tool-package-inventory.json']
    assert len(asset) == 1
    inventory_url = f'https://github.com/{REPOSITORY}/releases/download/v{version}/g4-tool-package-inventory.json'
    assert asset[0]['browser_download_url'] == inventory_url
    with urllib.request.urlopen(inventory_url, timeout=90) as response:
        public_inventory = response.read()
    assert public_inventory == inventory_bytes
    if asset[0].get('digest'):
        assert asset[0]['digest'] == 'sha256:' + hashlib.sha256(public_inventory).hexdigest()
    repository = SCRATCH / 'Hexalith.Builds'
    command('clone', ['git', 'clone', '--shared', '--no-checkout', str(ROOT / 'references/Hexalith.Builds'), str(repository)])
    command('origin', ['git', 'remote', 'set-url', 'origin', 'https://github.com/Hexalith/Hexalith.Builds.git'], repository)
    command('fetch-source', ['git', 'fetch', '--no-recurse-submodules', 'origin', SOURCE], repository)
    command('checkout', ['git', '-c', 'submodule.recurse=false', 'checkout', '--detach', SOURCE], repository)
    assert command('source', ['git', 'rev-parse', 'HEAD'], repository) == SOURCE
    catalog = command('catalog-bytes', ['git', 'show', 'HEAD:Props/Directory.Packages.props'], repository, binary=True)
    blob = command('catalog-blob', ['git', 'rev-parse', 'HEAD:Props/Directory.Packages.props'], repository)
    origin_revision = command('catalog-origin', ['git', 'log', '-1', '--format=%H', '--', 'Props/Directory.Packages.props'], repository)
    catalog_sha = hashlib.sha256(catalog).hexdigest()
    assert catalog_sha == '908d6acf84f3fac00c14942652b7b6d35fc93094e5ce41f87061db44bdaf3295'
    package_name = f'hexalith.builds.module.cli.{version}.nupkg'
    package_url = f'https://api.nuget.org/v3-flatcontainer/hexalith.builds.module.cli/{version}/{package_name}'
    with urllib.request.urlopen(package_url, timeout=120) as response:
        signed_bytes = response.read()
    archive = SCRATCH / package_name
    archive.write_bytes(signed_bytes)
    signed = checked_zip(signed_bytes)
    assert '.signature.p7s' in signed.namelist()
    command('nuget-signature', ['dotnet', 'nuget', 'verify', str(archive), '--all'], SCRATCH, timeout=120)
    tool_name = 'tools/net10.0/any/Hexalith.Builds.Tooling.dll'
    app_name = 'tools/net10.0/any/g4-host/bin/EventStoreHost/Hexalith.Builds.Module.EventStoreHost.dll'
    tooling_sha = hashlib.sha256(signed.read(tool_name)).hexdigest()
    apphost_sha = hashlib.sha256(signed.read(app_name)).hexdigest()
    extracted = SCRATCH / 'published'
    signed.extractall(extracted)
    observed_version = command('cli-version', ['dotnet', str(extracted / 'tools/net10.0/any/Hexalith.Builds.Module.Cli.dll'), '--version'], SCRATCH)
    assert observed_version == version + '+' + SOURCE
    binding = {'schemaVersion': 1, 'candidateAcceptance': False,
               'producer': {'repository': 'https://github.com/Hexalith/Hexalith.Builds.git', 'sourceRevision': SOURCE,
                            'catalog': {'path': 'Props/Directory.Packages.props', 'blob': blob, 'sha256': catalog_sha}},
               'publication': {'version': version, 'sourceRevision': SOURCE, 'releaseTag': 'v' + version,
                               'catalogSha256': catalog_sha, 'archiveSha256': hashlib.sha256(signed_bytes).hexdigest(),
                               'toolingSha256': tooling_sha, 'inventorySha256': hashlib.sha256(public_inventory).hexdigest(),
                               'inventoryUrl': inventory_url}}
    record.update(status='verified-publication-and-public-signed-artifact', version=version, binding=binding,
                  apphostSha256=apphost_sha, catalogOriginRevision=origin_revision, archiveUrl=package_url,
                  nugetSignatureVerified=True, officialArtifactDigestAndCrcVerified=True,
                  verifiedAt=datetime.datetime.now(datetime.timezone.utc).isoformat())
    (OUT / 'binding.json').write_text(json.dumps(binding, indent=2) + '\n')
    (AREA / 'builds-published-observation.json').write_text(json.dumps(record, indent=2) + '\n')
except BaseException as error:
    record.update(status='observation-incomplete', reason=type(error).__name__ + ': ' + str(error))
    raise
finally:
    (OUT / 'result.json').write_text(json.dumps(record, indent=2) + '\n')
