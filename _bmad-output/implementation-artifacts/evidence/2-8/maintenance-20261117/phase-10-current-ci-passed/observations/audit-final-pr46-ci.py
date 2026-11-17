"""Audit authentic raw receipts against exact PR profiles and individual XML cases."""
import hashlib
import json
from pathlib import Path
import xml.etree.ElementTree as ET

AREA = Path('/home/administrator/projects/hexalith/platform/artifacts/2-8-current-20261117')
OUT = AREA / 'pr46-final-ci-verified-02'
HEAD = '408481da06128ae50ad274d5dddcd4234d8b677d'
BASE = '11719c5cf51c2b2b080b89e0e301c2ef8dbacc46'
EXECUTED = 'e6869d11be7e636ae2717ea77e504225eb83f5dd'
BUILDS = '7afc4981e21bc2fcce687199a3fd2c61fe87336e'

def unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError('Duplicate JSON member: ' + key)
        result[key] = value
    return result

def read(path):
    return json.loads(path.read_bytes(), object_pairs_hook=unique_object)

archive = read(OUT / 'result.json')
assert archive['status'] == 'archive-authenticated; primary receipt audit pending'
assert archive['artifact']['officialDigestAndCrcVerified'] is True
for item in archive['artifact']['retained']:
    raw = (OUT / 'receipts' / item['name']).read_bytes()
    assert len(raw) == item['size'] and hashlib.sha256(raw).hexdigest() == item['sha256']
merge = read(OUT / 'executed-merge-commit.json')
head = read(OUT / 'head-commit.json')
assert merge['sha'] == EXECUTED and head['sha'] == HEAD
assert [p['sha'] for p in merge['parents']] == [BASE, HEAD]
assert merge['tree']['sha'] == head['tree']['sha'] == 'f8370e59ff5b20b75a681a581a21869f9e8b32c3'
binding = read(OUT / 'source-profiles/builds-tuple.json')
assert binding['producer']['sourceRevision'] == BUILDS and binding['publication']['version'] == '6.3.0'
report = {'status': 'passed', 'classification': 'PR preflight only', 'candidateAcceptance': False,
          'nativeQualificationCredit': False, 'headSource': HEAD, 'baseSource': BASE,
          'actualExecutedSource': EXECUTED, 'executedMergeTreeEqualsPrHeadTree': True,
          'tree': head['tree']['sha'], 'buildsVersion': '6.3.0', 'buildsSource': BUILDS,
          'tupleRun': 44166185074, 'sdkRun': 44166185115, 'attempt': 1,
          'artifactId': archive['artifact']['id'], 'artifactSha256': archive['artifact']['sha256'],
          'suites': [], 'remoteMutations': False}
for suite, directory, profile_file, expected in [
        ('current42', 'tuple-ci-current', 'tuple-ci-sdk42-current-suites.json', 486),
        ('base42', 'tuple-ci', 'tuple-ci-sdk42-base-suites.json', 93),
        ('extensions', 'tuple-ci-extensions', 'tuple-ci-extension-suites.json', 9)]:
    root = OUT / 'receipts/Hexalith.Platform/Hexalith.Platform/artifacts' / directory
    result = read(root / 'tuple-ci-result.json')
    profile_path = OUT / 'source-profiles' / profile_file
    profile = read(profile_path)
    assert result['kind'] == 'tuple-ci-preflight' and result['candidateAcceptance'] is False
    assert result['suite'] == suite and result['status'] == 'passed' and result['eventStoreSdk'] == '4.2.0'
    assert result['platformRevision'] == EXECUTED and result['tupleBinding'] == binding
    assert result['platformSourceState']['dirty'] is False
    assert result['platformSourceState']['hiddenTrackedInputs'] is False
    assert result['runner']['GITHUB_RUN_ID'] == '44166185074'
    assert result['runner']['GITHUB_RUN_ATTEMPT'] == '1'
    assert result['runner']['RUNNER_OS'] == 'Linux' and result['runner']['RUNNER_ARCH'] == 'X64'
    context = result['runtimeBuildContext']
    assert context['targetFramework'] == 'net11.0' and context['toolVersion'] == '6.3.0'
    assert context['buildsSource'] == BUILDS and context['nativeAcceptance'] is False
    assert context['profileSha256'] == hashlib.sha256(profile_path.read_bytes()).hexdigest()
    assert result['requiredProfile'] == profile
    assert set(result['collectors']) == {'current'}
    collector = result['collectors']['current']
    assert collector['source'] == EXECUTED and collector['targetFramework'] == 'net11.0'
    assert collector['toolVersion'] == '6.3.0' and collector['buildsSource'] == BUILDS
    assert collector['nativeAcceptance'] is False and collector['tupleBinding'] == binding
    assert all(c['exitCode'] == 0 and not c.get('timedOut') for c in result['commands'])
    commands = {c['name']: c for c in result['commands']}
    for name in ['published-artifacts', 'restore', 'source-sdk-propagation-controls', 'build']:
        assert commands[name]['exitCode'] == 0
    outputs = result['compositionTestOutputs']
    assert outputs['unchanged'] is True and outputs['before'] == outputs['after']
    assert len(result['tests']) == len(profile['groups'])
    assert len(list(root.glob('required-*.xml'))) == len(profile['groups'])
    cases_total = 0
    for index, (group, test) in enumerate(zip(profile['groups'], result['tests'])):
        assert test['selectors'] == group['selectors'] and test['project'] == group['project']
        assert test['platformSource'] == test['currentSource'] == EXECUTED
        assert test['collector'] == group['collector'] == 'current'
        assert test['targetFramework'] == 'net11.0' and test['toolVersion'] == '6.3.0'
        document = ET.fromstring((root / ('required-' + str(index) + '.xml')).read_bytes())
        assemblies = document.findall('assembly')
        assert len(assemblies) == 1
        assembly = assemblies[0]
        assert assembly.attrib == test['counts']
        counts = assembly.attrib
        assert int(counts['total']) == int(counts['passed']) == group['requiredCount'] > 0
        assert all(int(counts.get(key, '0')) == 0 for key in ['failed', 'errors', 'skipped', 'not-run'])
        assert counts['target-framework'] == '.NETCoreApp,Version=v11.0'
        assert ' .NET 11.0.0 ' in counts['environment']
        cases = list(assembly.iter('test'))
        assert len(cases) == group['requiredCount']
        assert len({c.attrib['id'] for c in cases}) == len(cases)
        for case in cases:
            assert case.attrib['result'] == 'Pass'
            selector = case.attrib['type']
            if group['filter'] == '-method':
                selector += '.' + case.attrib['method']
            assert selector in group['selectors'], selector
        assert commands['test-' + str(index)]['exitCode'] == 0
        cases_total += len(cases)
    assert cases_total == expected
    report['suites'].append({'suite': suite, 'groups': len(profile['groups']), 'passed': cases_total,
                             'failed': 0, 'skipped': 0, 'errors': 0, 'notRun': 0,
                             'collector': 'current', 'targetFramework': 'net11.0',
                             'profileSha256': context['profileSha256']})
assert sum(row['passed'] for row in report['suites']) == 588
sdk_jobs = archive['sdk']['jobs']
expected_sdks = ['3.130.0', '3.130.1', '3.130.2', '3.132.1', '4.1.0', '4.2.0']
assert sorted(j['name'] for j in sdk_jobs) == sorted('consumer-binding (' + v + ')' for v in expected_sdks)
assert all(j['conclusion'] == 'success' for j in sdk_jobs)
report['sdkVersionsPassed'] = expected_sdks
report['requiredPassingCases'] = 588
report['requiredOwnerReviewExceptionApproved'] = False
report['mainMergeOrPublication'] = False
report['mainPushChecksStillRequired'] = True
report['originalStoryCancellationProofStillOpen'] = True
target = OUT / 'primary-receipt-audit.json'
assert not target.exists()
target.write_text(json.dumps(report, indent=2) + '\n')
print(json.dumps(report, indent=2))
