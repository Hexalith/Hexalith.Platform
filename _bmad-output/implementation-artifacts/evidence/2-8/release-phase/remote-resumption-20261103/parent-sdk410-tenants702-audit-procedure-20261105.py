from pathlib import Path
import concurrent.futures
import datetime
import hashlib
import json
import subprocess
import xml.etree.ElementTree as ET
import zipfile

cache = Path('/home/administrator/.cache/hexalith/story-2-8/platform-sdk410-migration-20261105')
evidence = Path(__file__).parent
sha = lambda data: hashlib.sha256(data).hexdigest()
index = json.loads((cache / 'sdk410-tenants702-preparation-index.json').read_text())
by_hash = {}
for row in index['files']:
    path = cache / row['path']
    data = path.read_bytes()
    assert len(data) == row['bytes'] and sha(data) == row['sha256'], path
    by_hash.setdefault(row['sha256'], []).append(path)

packages = []
for stem, version, source, count in [
    ('sdk410', '4.1.0', 'dbc6e7e08d605ba6c6c790e3e08a15dc8bbdec8a', 14),
    ('tenants702', '7.0.2', '143cec60a3f5bb1831fa57f35c510880ceb788d8', 5),
]:
    bindings = json.loads((cache / (stem + '-signed-payload-bindings.json')).read_text())
    assert len(bindings) == count
    for row in bindings:
        name = row['package']
        signed = cache / (stem + '-signed-packages') / name
        official = cache / (stem + '-release-packages') / name
        assert sha(signed.read_bytes()) == row['archiveSha256']
        with zipfile.ZipFile(signed) as signed_zip, zipfile.ZipFile(official) as official_zip:
            assert '.signature.p7s' in signed_zip.namelist()
            names = lambda archive: {n for n in archive.namelist() if not n.endswith('/') and n != '.signature.p7s'}
            members = names(signed_zip)
            assert members == names(official_zip)
            for member in members:
                assert signed_zip.read(member) == official_zip.read(member), (name, member)
            nuspec = ET.fromstring(signed_zip.read(next(n for n in members if n.endswith('.nuspec'))))
            assert nuspec.find('.//{*}repository').get('commit') == source
            assert nuspec.find('.//{*}version').text == version
        log_name = name if stem == 'tenants702' else name.removesuffix('.nupkg')
        log = cache / (log_name + '-signature.log')
        assert 'Successfully verified package' in log.read_text()
        packages.append({'name': name, 'source': source, 'signedSha256': sha(signed.read_bytes()),
                         'officialUnsignedSha256': sha(official.read_bytes()),
                         'payloadMembersCompared': len(members), 'signatureSuccessLogSha256': sha(log.read_bytes())})

images = json.loads((cache / 'sdk410-tenants702-release-image-bindings.json').read_text())
image_checks = []
for row in images:
    image_index = json.loads(by_hash[row['indexDigest'].removeprefix('sha256:')][0].read_bytes())
    assert len(image_index['manifests']) == len(row['children']) == 2
    for child in row['children']:
        manifest = json.loads(by_hash[child['manifestSha256']][0].read_bytes())
        config = json.loads(by_hash[child['configSha256']][0].read_bytes())
        operating_system, architecture = child['platform'].split('/')
        assert config['os'] == operating_system and config['architecture'] == architecture
        entry = next(x for x in image_index['manifests'] if x['platform']['os'] == operating_system and x['platform']['architecture'] == architecture)
        assert entry['digest'] == 'sha256:' + child['manifestSha256']
        assert manifest['config']['digest'] == 'sha256:' + child['configSha256']
        assert [x['digest'] for x in manifest['layers']] == child['layers']
        assert config['config']['Labels']['org.opencontainers.image.revision'] == child['revision']
        assert config['config']['Labels']['org.opencontainers.image.version'] == row['image'].rsplit(':', 1)[1]
    image_checks.append({'image': row['image'], 'indexDigest': row['indexDigest'],
                         'bothChildConfigSourceVersionArchitectureChainsVerified': True,
                         'releaseSmoke': row['retainedReleaseSmoke']})

gates = json.loads((cache / 'sdk-gates-green.json').read_text())
assert gates['status'] == 'passed' and len(gates['cases']) == 236
assert all(row['status'] == 'passed' for row in gates['cases'])
result = json.loads((cache / 'catalog410-tenants702-bound/result.json').read_text())
assert result['status'] == 'passed' and result['candidateAcceptance'] is False
assert result['consumerBindingVerified'] is True and result['authorityRegistered'] is False
assert len(result['cases']) == 15

urls = {
    'eventstore-release': 'repos/Hexalith/Hexalith.EventStore/releases/433830750',
    'tenants-release': 'repos/Hexalith/Hexalith.Tenants/releases/433517592',
    'eventstore-image-artifact': 'repos/Hexalith/Hexalith.EventStore/actions/artifacts/13317896664',
    'tenants-image-artifact': 'repos/Hexalith/Hexalith.Tenants/actions/artifacts/13295723413',
}
def request(item):
    key, url = item
    return key, json.loads(subprocess.check_output(['gh', 'api', url]))
with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
    remote = dict(pool.map(request, urls.items()))
for package in packages:
    key = 'eventstore-release' if package['name'].startswith('Hexalith.EventStore.') else 'tenants-release'
    asset = next(x for x in remote[key]['assets'] if x['name'] == package['name'])
    assert asset['digest'] == 'sha256:' + package['officialUnsignedSha256']
for key in ['eventstore-image-artifact', 'tenants-image-artifact']:
    assert remote[key]['digest'].startswith('sha256:')
    assert remote[key]['digest'][7:] in by_hash

output = {
    'at': datetime.datetime.now(datetime.timezone.utc).isoformat(),
    'retainedFilesRehashed': len(index['files']), 'retainedBytes': index['bytes'],
    'packages': packages, 'images': image_checks,
    'actualFiniteGateEvaluationsPassed': 236, 'actualCatalogControlsPassed': 15,
    'remoteMetadata': remote,
    'firstAuditSetupFailure': 'Used SDK signature-log naming for Tenants; corrected to actual indexed .nupkg-signature.log files. No product/source failure.',
    'scope': 'Public artifact and SDK component verification only; no present registry/layer availability, new producer binding, native or candidate acceptance.',
}
destination = evidence / 'parent-sdk410-tenants702-artifact-and-control-audit-20261105.json'
assert not destination.exists()
destination.write_text(json.dumps(output, indent=2) + '\n')
print(json.dumps({'audit': str(destination), 'rehashed': len(index['files']),
                  'packages': len(packages), 'imageIndexes': len(images),
                  'gateEvaluations': 236, 'catalogCases': 15}))
