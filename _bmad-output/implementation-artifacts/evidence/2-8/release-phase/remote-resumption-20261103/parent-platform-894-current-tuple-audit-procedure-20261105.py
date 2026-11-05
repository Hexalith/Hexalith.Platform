import datetime
import hashlib
import io
import json
import subprocess
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path

ev = Path(__file__).resolve().parent
cache = Path.home() / '.cache/hexalith/story-2-8/platform-894-hosted-20261105'
source_root = Path.home() / '.cache/hexalith/story-2-8/platform-current-owner-repair-20261105/Hexalith.Platform'
source = '8946209702a6c1c48239f206dd452ae7d729a307'
acquisition = json.loads((ev / 'parent-platform-894-current-tuple-artifact-acquisition-20261105.json').read_bytes())
archive = cache / 'platform-tuple-13445558624.zip'
sha = lambda body: hashlib.sha256(body).hexdigest()
assert sha(archive.read_bytes()) == acquisition['localArchive']['sha256'] == '4ad716f73373c5382867752212476d107acb8ac596929a3ed4423ea2fa1ef5ef'
assert acquisition['run']['head_sha'] == acquisition['commit']['sha'] == source
assert acquisition['run']['run_attempt'] == 1
job = next(j for j in acquisition['jobs']['jobs'] if j['id'] == 127470257852)
assert job['conclusion'] == 'success'

with zipfile.ZipFile(archive) as z:
    names = z.namelist()
    assert len(names) == len(set(names))
    result_name = next(n for n in names if n.endswith('/tuple-ci-result.json'))
    prefix = result_name.rsplit('/', 1)[0]
    result = json.loads(z.read(result_name))
    bootstrap = json.loads(z.read(next(n for n in names if n.endswith('/bootstrap.json'))))
    assert bootstrap['context']['PLATFORM_CANDIDATE_SHA'] == result['platformRevision'] == source
    assert bootstrap['context']['GITHUB_RUN_ID'] == '42091454297'
    assert bootstrap['context']['GITHUB_RUN_ATTEMPT'] == '1'
    assert all(v == 'success' for v in bootstrap['outcomes'].values())
    assert result['status'] == 'passed' and result['candidateAcceptance'] is False
    assert result['platformSourceState']['dirty'] is False
    assert all(c['exitCode'] == 0 for c in result['commands'])
    profile = subprocess.check_output(['git', '-C', str(source_root), 'show', source + ':eng/tuple-ci-suites.json'])
    assert result['requiredProfile'] == json.loads(profile)
    hashes = result['platformSourceState']['inputHashes']
    requests = ''.join(source + ':' + p + '\n' for p in hashes)
    raw = subprocess.check_output(['git', '-C', str(source_root), 'cat-file', '--batch'], input=requests.encode())
    stream = io.BytesIO(raw)
    for path, expected in hashes.items():
        header = stream.readline().decode().strip().split()
        assert len(header) == 3 and header[1] == 'blob', (path, header)
        body = stream.read(int(header[2]))
        assert stream.read(1) == b'\n' and sha(body) == expected, path
    assert stream.read() == b''
    groups = []
    all_cases = []
    for i, group in enumerate(result['requiredProfile']['groups']):
        body = z.read(prefix + '/required-' + str(i) + '.xml')
        xml = ET.fromstring(body)
        assembly = xml.find('assembly')
        expected = group['requiredCount']
        assert int(assembly.attrib['passed']) == int(assembly.attrib['total']) == expected
        assert all(int(assembly.attrib[k]) == 0 for k in ['failed', 'errors', 'skipped', 'not-run'])
        tests = xml.findall('.//test')
        assert len(tests) == expected and all(t.attrib['result'] == 'Pass' for t in tests)
        assert int(result['tests'][i]['counts']['passed']) == expected
        cases = [{'name': t.attrib['name'], 'method': t.attrib['method'], 'outputSHA256': sha((t.findtext('output') or '').encode())} for t in tests]
        all_cases.extend(cases)
        groups.append({'index': i, 'passed': expected, 'xmlSHA256': sha(body), 'cases': cases})
    assert len(all_cases) == len({c['name'] for c in all_cases}) == 258
    assert [g['passed'] for g in groups[9:]] == [8, 2, 2, 4, 4, 4]
    ident = json.loads(z.read(prefix + '/builds/artifact-identity.json'))
    assert ident['platformRevision'] == source and ident['platformSourceState'] == result['platformSourceState']
    assert ident['version'] == '4.44.4' and ident['buildsRevision'] == '5fa2b97c8b22047c2c33e2c9f7017fd5d604c9ef'
    public = json.loads((ev / 'parent-builds-4444-genuine-intermediate-publication-binding-20261105.json').read_bytes())
    assert result['tupleBinding'] == ident['tupleBinding'] == public['binding']
    package_name = next(n for n in names if n.endswith('hexalith.builds.module.cli.4.44.4.nupkg'))
    package = z.read(package_name)
    assert sha(package) == ident['archiveSha256'] == 'd375583dba2800fcc76e4d013a102b59937470af87487f890fbd0926a37af294'
    with zipfile.ZipFile(io.BytesIO(package)) as p:
        assert set(p.namelist()) == set(ident['files'])
        retained = []
        archive_only = []
        for path, digest in ident['files'].items():
            assert sha(p.read(path)) == digest
            extracted = prefix + '/builds/published/' + path
            if extracted in names:
                assert sha(z.read(extracted)) == digest
                retained.append(path)
            else:
                archive_only.append(path)
        assert len(ident['files']) == 317 and len(retained) == 315
    observations = result['compositionTestOutputs']
    assert observations['unchanged'] is True and observations['before'] == observations['after']
    official = Path.home() / '.cache/hexalith/story-2-8/parent-builds-4444-5fa-official-20261105/hexalith.builds.module.cli-extracted/tools/net10.0/any'
    matched_dependencies = {}
    for name, entry in observations['before']['dependencies'].items():
        if name == 'Hexalith.Tenants.Contracts.dll':
            tenants = Path.home() / '.cache/hexalith/story-2-8/newer-builds-tenants704-prerequisite-20261105/signed/Hexalith.Tenants.Contracts.7.0.4.nupkg'
            receipt = json.loads((ev / 'parent-tenants704-public-body-and-producer-audit-20261105.json').read_bytes())
            expected = next(p for p in receipt['packages'] if p['name'] == tenants.name)
            assert sha(tenants.read_bytes()) == expected['signedSha256'] == '04989ef3b9755c8af217517c7964a75796903d2c21c517ff76d7d46c574861a7'
            member = 'lib/net10.0/Hexalith.Tenants.Contracts.dll'
            with zipfile.ZipFile(tenants) as t:
                assert sha(t.read(member)) == entry['sha256']
            matched_dependencies[name] = {'sha256': entry['sha256'], 'signedArchive': str(tenants), 'signedMember': member}
            continue
        candidates = sorted(official.rglob(name))
        matches = [p for p in candidates if sha(p.read_bytes()) == entry['sha256']]
        assert matches, name
        matched_dependencies[name] = {'sha256': entry['sha256'], 'signedPayloadPaths': [str(p.relative_to(official)) for p in matches]}

record = {
    'utc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
    'source': source, 'runId': 42091454297, 'attempt': 1,
    'jobId': 127470257852, 'artifactId': 13445558624,
    'artifactSHA256': sha(archive.read_bytes()),
    'actualPassed': len(all_cases), 'actualGroups': groups,
    'sourceInputBodiesComparedToImmutableGit': len(hashes),
    'all317SignedArchiveMembersMatched': True,
    'retained315ExtractedMembersMatched': True,
    'archiveOnlyMembers': archive_only,
    'unchangedBeforeAfterOutputObservationsMatchedSignedBodies': True,
    'observedDependenciesMatchedSignedBodies': matched_dependencies,
    'tupleBinding': result['tupleBinding'],
    'limits': [
        'This audits the current258 job only. The separate legacy349 job was still running at acquisition.',
        'Current package two cases executed; empty test output provides no additional retained private-reader-body diagnostic identity.',
        'The original signed4.44.4 package was independently authenticated in the public producer audit; this archive matches it byte-for-byte.',
        'No final fixed-producer, native, candidate or whole-story qualification credit.',
    ],
}
output = ev / 'parent-platform-894-current-tuple-hosted-audit-20261105.json'
assert not output.exists()
output.write_text(json.dumps(record, indent=2) + '\n')
print(json.dumps({'output': str(output), 'passed': len(all_cases), 'sourceBodies': len(hashes), 'packageMembers': len(ident['files'])}))
