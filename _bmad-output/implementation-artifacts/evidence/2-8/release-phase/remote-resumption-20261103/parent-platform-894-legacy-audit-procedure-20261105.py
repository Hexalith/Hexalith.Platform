from pathlib import Path
import datetime
import hashlib
import io
import json
import subprocess
import xml.etree.ElementTree as ET
import zipfile

ev = Path(__file__).resolve().parent
cache = Path.home() / '.cache/hexalith/story-2-8/platform-894-hosted-20261105'
root = Path.home() / '.cache/hexalith/story-2-8/platform-current-owner-repair-20261105/Hexalith.Platform'
current = '8946209702a6c1c48239f206dd452ae7d729a307'
legacy = '0b6635d97c4959b09ba90b8c2df8c48f40525691'
receipt = json.loads((ev / 'parent-platform-894-terminal-tuple-and-legacy-api-20261105.json').read_bytes())
assert receipt['run']['head_sha'] == current and receipt['run']['conclusion'] == 'success'
assert all(j['conclusion'] == 'success' for j in receipt['jobs']['jobs'])
archive = cache / 'platform-legacy-13447376185.zip'
sha = lambda body: hashlib.sha256(body).hexdigest()
assert sha(archive.read_bytes()) == '07aeff98a0bbcb00b60af0e596495a14cea6a180619db4892ffd4bb97f5f7236'
with zipfile.ZipFile(archive) as z:
    assert len(z.namelist()) == len(set(z.namelist())) == 513
    comparison = json.loads(z.read('legacy-source/source-comparison.json'))
    assert comparison['currentSource'] == current and comparison['legacySource'] == legacy
    assert comparison['qualificationCredit'] is False and comparison['requiredCount'] == 349
    differences = subprocess.check_output(['git', '-C', str(root), 'diff', '--name-only', legacy, current]).decode().splitlines()
    assert set(differences) == set(comparison['allowedMetadataDifferences']) and len(differences) == 8
    result = json.loads(z.read('legacy-public-cohort/tuple-ci-result.json'))
    assert result['platformRevision'] == legacy and result['status'] == 'passed' and result['candidateAcceptance'] is False
    assert result['platformSourceState']['dirty'] is False and all(c['exitCode'] == 0 for c in result['commands'])
    profile = subprocess.check_output(['git', '-C', str(root), 'show', legacy + ':eng/tuple-ci-suites.json'])
    assert sha(profile) == '61689f1598dac43a9fa28dcf5319008059e0fea0f1018209b1a8e79e4f63d7b0'
    assert result['requiredProfile'] == json.loads(profile)
    hashes = result['platformSourceState']['inputHashes']
    requests = ''.join(legacy + ':' + p + '\n' for p in hashes).encode()
    stream = io.BytesIO(subprocess.check_output(['git', '-C', str(root), 'cat-file', '--batch'], input=requests))
    for path, expected in hashes.items():
        header = stream.readline().decode().strip().split()
        assert len(header) == 3 and header[1] == 'blob', path
        body = stream.read(int(header[2]))
        assert stream.read(1) == b'\n' and sha(body) == expected, path
    assert stream.read() == b''
    groups = []
    cases = []
    for i, group in enumerate(result['requiredProfile']['groups']):
        body = z.read('legacy-public-cohort/required-' + str(i) + '.xml')
        xml = ET.fromstring(body)
        assembly = xml.find('assembly')
        expected = group['requiredCount']
        assert int(assembly.attrib['passed']) == int(assembly.attrib['total']) == expected
        assert all(int(assembly.attrib[k]) == 0 for k in ['failed', 'errors', 'not-run', 'skipped'])
        tests = xml.findall('.//test')
        assert len(tests) == expected and all(t.attrib['result'] == 'Pass' for t in tests)
        cases.extend(t.attrib['name'] for t in tests)
        groups.append({'index': i, 'passed': expected, 'xmlSHA256': sha(body), 'cases': [t.attrib['name'] for t in tests]})
    assert len(cases) == len(set(cases)) == 349
    original_root = Path.home() / '.cache/hexalith/story-2-8/platform-349-main-hosted-20261105/platform-tuple-42042311595-1'
    original_result = next(original_root.rglob('tuple-ci-result.json'))
    original_names = {t.attrib['name'] for i in range(16) for t in ET.parse(original_result.parent / ('required-' + str(i) + '.xml')).getroot().findall('.//test')}
    assert set(cases) == original_names and len(original_names) == 349
    ident = json.loads(z.read('legacy-public-cohort/builds/artifact-identity.json'))
    assert ident['platformRevision'] == legacy and ident['platformSourceState'] == result['platformSourceState']
    assert result['tupleBinding'] == ident['tupleBinding'] == json.loads((ev / 'parent-builds-4443-qualified-tuple-binding-20261105.json').read_bytes())['binding']
    package = z.read('legacy-public-cohort/builds/hexalith.builds.module.cli.4.44.3.nupkg')
    assert sha(package) == ident['archiveSha256'] == '32c2f517791bfce975bf775e73bbbf626cc710da83c68b89c5e305ef55acea32'
    with zipfile.ZipFile(io.BytesIO(package)) as p:
        assert set(p.namelist()) == set(ident['files'])
        for path, expected in ident['files'].items():
            assert sha(p.read(path)) == expected
            retained = 'legacy-public-cohort/builds/published/' + path
            if retained in z.namelist():
                assert sha(z.read(retained)) == expected
    assert len(ident['files']) == 317
    observations = result['compositionTestOutputs']
    assert observations['unchanged'] is True and observations['before'] == observations['after']

record = {
    'utc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
    'run': 42091454297, 'attempt': 1, 'conclusion': 'success',
    'currentSource': current, 'actualLegacySource': legacy,
    'onlyEightPermittedMetadataDifferences': differences,
    'actualPassed': 349, 'exactOriginal349NamesRetained': True,
    'sourceBodyHashesMatchedImmutableLegacyGit': len(hashes),
    'signed4443ArchiveMembersVerified': len(ident['files']),
    'artifactId': 13447376185, 'artifactSHA256': sha(archive.read_bytes()),
    'groups': groups, 'tupleBinding': result['tupleBinding'],
    'qualificationCredit': False,
    'limits': ['Same changed source under the genuine historical public tuple. No native, final fixed-producer, candidate or whole-story acceptance.'],
}
output = ev / 'parent-platform-894-same-source-original349-hosted-audit-20261105.json'
assert not output.exists()
output.write_text(json.dumps(record, indent=2) + '\n')
print(json.dumps({'passed': len(cases), 'sourceBodyHashes': len(hashes), 'exactOriginal349Names': True, 'metadataDifferences': len(differences)}))
