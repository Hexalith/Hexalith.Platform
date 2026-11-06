"""Authenticate retained hosted results without executing application code."""
import datetime
import hashlib
import io
import json
import subprocess
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path

EV = Path(__file__).resolve().parent
CACHE = Path('/home/administrator/.cache/hexalith/story-2-8/parent-platform153-published-hosted-20261106')
RAW = Path('/home/administrator/.cache/hexalith/story-2-8/parent-newer-platform153-parties-fb66-read-20261106')
SOURCE = 'd1da347db92ef3272beda90005be5239878ff901'
LEGACY = '638070cbbb35bd9bd11caf5198a41bb7c511d780'
SHA = lambda b: hashlib.sha256(b).hexdigest()

def git(*args, data=None):
    return subprocess.check_output(['git', '--git-dir', str(CACHE / 'source.git'), *args], input=data)

def body(revision, path):
    return git('show', revision + ':' + path)

def api(endpoint, filename):
    result = subprocess.run(['gh', 'api', endpoint], capture_output=True)
    (CACHE / (filename + '.json')).write_bytes(result.stdout)
    (CACHE / (filename + '.stderr')).write_bytes(result.stderr)
    assert result.returncode == 0, result.stderr.decode()
    return json.loads(result.stdout)

run = api('repos/Hexalith/Hexalith.Platform/actions/runs/42157212231', 'actual-run')
jobs = json.loads((RAW / 'platform153-tuple-jobs.json').read_bytes())['jobs']
assert run['head_sha'] == SOURCE and run['run_attempt'] == 1
assert run['status'] == 'completed' and run['conclusion'] == 'success'
assert all(job['conclusion'] == 'success' for job in jobs) and len(jobs) == 2
tag = json.loads((RAW / 'platform153-tag.json').read_bytes())
release = json.loads((RAW / 'platform153-release.json').read_bytes())
assert tag['object'] == {'sha': SOURCE, 'type': 'commit', 'url': tag['object']['url']}
assert release['id'] == 436080860 and release['tag_name'] == 'v1.5.3'
assert release['target_commitish'] == SOURCE and not release['draft'] and not release['prerelease']
artifacts = json.loads((RAW / 'platform153-tuple-artifacts.json').read_bytes())['artifacts']
assert len(artifacts) == 2
reports = []
for artifact in artifacts:
    assert artifact['workflow_run']['id'] == run['id']
    assert artifact['workflow_run']['head_sha'] == SOURCE and not artifact['expired']
    archive = CACHE / (artifact['name'] + '.zip')
    assert SHA(archive.read_bytes()) == artifact['digest'].removeprefix('sha256:')
    historical = 'legacy-public' in artifact['name']
    revision, expected_count = (LEGACY, 349) if historical else (SOURCE, 259)
    with zipfile.ZipFile(archive) as z:
        names = z.namelist()
        assert len(names) == len(set(names))
        result_name = next(n for n in names if n.endswith('/tuple-ci-result.json'))
        prefix = result_name.rsplit('/', 1)[0]
        result = json.loads(z.read(result_name))
        assert result['platformRevision'] == revision and result['status'] == 'passed'
        assert result['candidateAcceptance'] is False and result['platformSourceState']['dirty'] is False
        assert all(command['exitCode'] == 0 for command in result['commands'])
        profile = body(revision, 'eng/tuple-ci-suites.json')
        assert result['requiredProfile'] == json.loads(profile)
        if historical:
            assert SHA(profile) == '61689f1598dac43a9fa28dcf5319008059e0fea0f1018209b1a8e79e4f63d7b0'
            comparison = json.loads(z.read('legacy-source/source-comparison.json'))
            assert comparison['currentSource'] == SOURCE and comparison['legacySource'] == LEGACY
            assert comparison['qualificationCredit'] is False and comparison['requiredCount'] == 349
            differences = git('diff', '--name-only', LEGACY, SOURCE).decode().splitlines()
            assert set(differences) == set(comparison['allowedMetadataDifferences']) and len(differences) == 8
        hashes = result['platformSourceState']['inputHashes']
        tracked_blobs = {entry.split(b'\t', 1)[1].decode() for entry in git('ls-tree', '-r', '-z', revision).split(b'\0')
                         if entry and entry.split(b' ', 2)[1] == b'blob'}
        assert set(hashes) == tracked_blobs
        request = ''.join(revision + ':' + path + '\n' for path in hashes).encode()
        stream = io.BytesIO(git('cat-file', '--batch', data=request))
        for path, digest in hashes.items():
            header = stream.readline().decode().split()
            assert len(header) == 3 and header[1] == 'blob', path
            assert SHA(stream.read(int(header[2]))) == digest, path
            assert stream.read(1) == b'\n'
        assert not stream.read()
        groups, cases = [], []
        for index, group in enumerate(result['requiredProfile']['groups']):
            xml_body = z.read(prefix + '/required-' + str(index) + '.xml')
            xml = ET.fromstring(xml_body)
            assembly = xml.find('assembly')
            count = group['requiredCount']
            assert int(assembly.attrib['passed']) == int(assembly.attrib['total']) == count
            assert all(int(assembly.attrib[key]) == 0 for key in ['failed', 'errors', 'skipped', 'not-run'])
            tests = xml.findall('.//test')
            assert len(tests) == count and all(test.attrib['result'] == 'Pass' for test in tests)
            assert int(result['tests'][index]['counts']['passed']) == count
            names_in_group = [test.attrib['name'] for test in tests]
            cases.extend(names_in_group)
            groups.append({'index': index, 'passed': count, 'xmlSha256': SHA(xml_body), 'cases': names_in_group})
        assert len(cases) == len(set(cases)) == expected_count
        if historical:
            original = json.loads((EV / 'parent-platform-894-same-source-original349-hosted-audit-20261105.json').read_bytes())
            assert set(cases) == {name for group in original['groups'] for name in group['cases']}
        else:
            assert any('PreparationFailuresExposeOnlyFinitePhaseStatusAndDiagnosticCodes' in name for name in cases)
        identity = json.loads(z.read(prefix + '/builds/artifact-identity.json'))
        assert identity['platformRevision'] == revision
        assert identity['platformSourceState'] == result['platformSourceState']
        if historical:
            binding = json.loads((EV / 'parent-builds-4443-qualified-tuple-binding-20261105.json').read_bytes())['binding']
        else:
            binding = json.loads((EV / 'parent-builds4446-actual-qualified-producer-and-tuple-binding-20261106.json').read_bytes())['binding']
        assert result['tupleBinding'] == identity['tupleBinding'] == binding
        version = binding['publication']['version']
        assert identity['version'] == version and identity['buildsRevision'] == binding['producer']['sourceRevision']
        package_name = next(n for n in names if n.endswith('/hexalith.builds.module.cli.' + version + '.nupkg'))
        package = z.read(package_name)
        assert SHA(package) == identity['archiveSha256'] == binding['publication']['archiveSha256']
        extracted_count = 0
        with zipfile.ZipFile(io.BytesIO(package)) as p:
            assert set(p.namelist()) == set(identity['files'])
            for name, digest in identity['files'].items():
                assert SHA(p.read(name)) == digest
                retained = prefix + '/builds/published/' + name
                if retained in names:
                    assert SHA(z.read(retained)) == digest
                    extracted_count += 1
        observed = result['compositionTestOutputs']
        assert observed['unchanged'] is True and observed['before'] == observed['after']
        reports.append({'source': revision, 'artifactId': artifact['id'], 'artifactSha256': SHA(archive.read_bytes()),
                        'actualPassed': len(cases), 'groups': groups, 'sourceInputsMatchedGit': len(hashes),
                        'signedPackageMembersMatched': len(identity['files']), 'extractedMembersMatched': extracted_count,
                        'tupleBinding': binding, 'compositionOutputsBeforeAfterUnchanged': True})

legacy_ancestry = subprocess.run(['git', '--git-dir', str(CACHE / 'source.git'), 'merge-base', '--is-ancestor', LEGACY, SOURCE], capture_output=True)
assert legacy_ancestry.returncode in (0, 1)
receipt = {'at': datetime.datetime.now(datetime.timezone.utc).isoformat(), 'currentSource': SOURCE,
           'actualLegacySource': LEGACY, 'runId': run['id'], 'attempt': 1, 'releaseId': release['id'],
           'actualTagSourceMatched': True, 'legacyIsAncestorOfPublishedCurrent': legacy_ancestry.returncode == 0,
           'onlyEightMetadataDifferences': differences, 'reports': reports,
           'nativeAcceptance': False, 'candidateAcceptance': False,
           'limits': ['Authenticates incoming Platform1.5.3 source and retained hosted results only.',
                      'This published source includes its seed and receipt repairs but lacks separate sealing/key/deadline controls; no merged-source qualification.',
                      'Signed module archives match independently authenticated genuine public4443/4446 bytes.',
                      'Output before/after equality is verified; no additional runtime body observation is inferred.']}
output = EV / 'parent-platform153-current259-and-original349-hosted-audit-20261106.json'
assert not output.exists()
output.write_text(json.dumps(receipt, indent=2) + '\n')
print(json.dumps({'output': str(output), 'actualPassed': [r['actualPassed'] for r in reports],
                  'sourceInputs': [r['sourceInputsMatchedGit'] for r in reports], 'metadataDifferences': differences}))
