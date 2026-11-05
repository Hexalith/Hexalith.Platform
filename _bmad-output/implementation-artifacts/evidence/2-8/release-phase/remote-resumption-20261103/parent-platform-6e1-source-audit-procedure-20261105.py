"""Independently audit the test-only catalog handoff proposal and actual execution."""
import datetime
import hashlib
import io
import json
import subprocess
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path

EVIDENCE = Path('/home/administrator/.cache/hexalith/story-2-8/platform-catalog-handoff-20261105/diagnosis-evidence')
ROOT = EVIDENCE.parent / 'Hexalith.Platform'
OUTPUT = Path(__file__).with_name('parent-platform-6e1-source-and-full349-audit-20261105.json')

def sha(body):
    return hashlib.sha256(body).hexdigest()

def read(name):
    return json.loads((EVIDENCE / name).read_bytes())

def git(*args, data=None):
    return subprocess.check_output(['git', *args], cwd=ROOT, input=data)

def tree(revision):
    return {record.split(b'\t', 1)[1].decode(): record.split(b'\t', 1)[0].decode()
            for record in git('ls-tree', '-rz', revision).split(b'\0') if record}

def main():
    cp = read('two-phase-required349-checkpoint.json')
    source, base = cp['head'], cp['base']
    assert source == '6e1a04d3a24ebe7a93fb6794b1ceec29d57aa445'
    assert git('rev-parse', 'HEAD').decode().strip() == source and not git('status', '--porcelain')
    index = read('two-phase-required349-evidence-index.json')
    assert len(index['items']) == len({item['path'] for item in index['items']}) == index['files'] == 433
    total = 0
    for item in index['items']:
        path = EVIDENCE / item['path']
        assert path.resolve().is_relative_to(EVIDENCE.resolve()) and not path.is_symlink()
        body = path.read_bytes()
        assert len(body) == item['bytes'] and sha(body) == item['sha256'], item['path']
        total += len(body)
    assert total == index['bytes'] == 181051801
    before, after = tree(base), tree(source)
    changed = sorted(name for name in before.keys() | after.keys() if before.get(name) != after.get(name))
    assert changed == sorted(cp['authoredPaths']) and len(changed) == 3
    patch = git('diff', '--binary', '--full-index', base, source)
    assert patch == (EVIDENCE / cp['completePatch']['path']).read_bytes()
    assert len(patch) == 11656 and sha(patch) == cp['completePatch']['sha256']
    old_profile = json.loads(git('show', base + ':eng/tuple-ci-suites.json'))
    profile = json.loads(git('show', source + ':eng/tuple-ci-suites.json'))
    assert profile['groups'][:15] == old_profile['groups'] and len(profile['groups']) == 16
    result = read('final-required349-first-result.json')
    assert result['source'] == result['sourceAfter'] == source
    assert result['gitStatus'] == result['gitStatusAfter'] == '' and result['passed']
    assert all(value is None for value in result['actualCiContext'].values())
    assert result['sourceBefore'] == result['sourceAfterHashes'] and result['sourceBytesUnchanged']
    hashes = result['sourceBefore']
    assert len(hashes) == 21429
    batch = ''.join(source + ':' + path + '\n' for path in hashes).encode()
    stream = io.BytesIO(git('cat-file', '--batch', data=batch))
    for path, expected in hashes.items():
        header = stream.readline().split()
        assert len(header) == 3 and header[1] == b'blob'
        body = stream.read(int(header[2]))
        assert stream.read(1) == b'\n' and sha(body) == expected, path
    assert result['outputBefore'] == result['outputAfter'] and result['outputsUnchanged']
    assert len(result['outputBefore']) == 631
    for path, expected in result['outputBefore'].items():
        assert sha((ROOT / path).read_bytes()) == expected, path
    assert result['publicToolBefore'] == result['publicToolAfter'] and result['publicToolUnchanged']
    tool = result['publicToolBefore']
    archive = Path('/home/administrator/.cache/hexalith/story-2-8/parent-builds-4443-fc212-official-20261105/hexalith.builds.module.cli.4.44.3.nupkg')
    assert sha(archive.read_bytes()) == tool['archiveSha256'] == '32c2f517791bfce975bf775e73bbbf626cc710da83c68b89c5e305ef55acea32'
    with zipfile.ZipFile(archive) as content:
        assert len(tool['members']) == 317
        for name, expected in tool['members'].items():
            assert sha(content.read(name)) == expected
    actual, groups = [], []
    for group, expected in zip(result['groups'], profile['groups'], strict=True):
        xml = ET.parse(EVIDENCE / group['xml']).getroot()
        tests = list(xml.iter('test'))
        assert len(tests) == expected['requiredCount'] == group['actualCount']
        assert group['exitCode'] == group['errors'] == 0
        assert group['selectors'] == expected['selectors']
        assert all(test.attrib['result'] == 'Pass' for test in tests)
        assert all(int(assembly.attrib[key]) == 0 for assembly in xml.iter('assembly')
                   for key in ['errors', 'failed', 'skipped', 'not-run'])
        actual.extend(tests)
        groups.append({'xml': group['xml'], 'passed': len(tests), 'sha256': sha((EVIDENCE / group['xml']).read_bytes())})
    names = {test.attrib['name'] for test in actual}
    assert len(names) == len(actual) == result['actualCount'] == result['passedCount'] == 349
    assert all(command['exitCode'] == 0 for command in result['commands'])
    prior_root = Path('/home/administrator/.cache/hexalith/story-2-8/platform-4afa-hosted-20261105/platform-tuple-42030769896-1')
    prior = next(prior_root.rglob('tuple-ci-result.json'))
    old_names = {test.attrib['name'] for i in range(15)
                 for test in ET.parse(prior.parent / f'required-{i}.xml').getroot().iter('test')}
    assert len(old_names) == 347 and old_names.issubset(names)
    observations = []
    for test in actual:
        if test.attrib['method'] != 'PublishedPreflightAndOrdinaryCatalogRespectVerifiedDerivationAsync':
            continue
        output = test.findtext('output') or ''
        observed = [json.loads(line) for line in output.splitlines() if line.startswith('{"phase":')]
        assert [item['phase'] for item in observed] == ['command-cold', 'published-preflight', 'ordinary-proof', 'ordinary-materialized']
        cold, warm, proof, materialized = observed
        assert not cold['readerConsumerCached'] and warm['readerConsumerCached']
        assert cold['snapshotSha256'] != warm['snapshotSha256'] == proof['snapshotSha256']
        assert proof['proofMatches'] is True
        legacy = 'releasedMaterialization: True' in test.attrib['name']
        assert materialized['proofMatches'] is (not legacy)
        assert materialized['snapshotSha256'] == (cold if legacy else warm)['snapshotSha256']
        assert ('ordinaryGenerateExceptionType=' + ('InvalidDataException' if legacy else 'none')) in output
        observations.append({'name': test.attrib['name'], 'phaseObservations': observed,
                             'actualOrdinaryGenerateExceptionType': 'InvalidDataException' if legacy else 'none',
                             'xmlOutputSha256': sha(output.encode())})
    assert len(observations) == 2
    preserve = read('final-test-only349-source-and-public-body-preservation.json')
    for item in preserve['verifiedSelectedPublicSdkOutputBodies']:
        assert sha(Path(item['genuinePublicBody']).read_bytes()) == item['actualOutputSha256']
        name = Path(item['genuinePublicBody']).name
        assert result['outputBefore']['tests/Hexalith.Platform.Composition.Tests/bin/Release/net10.0/' + name] == item['actualOutputSha256']
    assert json.loads((ROOT / 'node_modules/@commitlint/cli/package.json').read_text())['version'] == '21.2.4'
    validations = []
    for commit in git('rev-list', '--reverse', base + '..' + source).decode().splitlines():
        message = git('show', '-s', '--format=%B', commit)
        message_path = OUTPUT.with_name('parent-platform-6e1-commit-message-' + commit[:8] + '-20261105.txt')
        assert not message_path.exists()
        message_path.write_bytes(message)
        checked = subprocess.run(['node', 'node_modules/@commitlint/cli/cli.js', '--edit', str(message_path),
                                  '--config', 'commitlint.config.mjs', '--verbose'], cwd=ROOT, capture_output=True, text=True)
        assert checked.returncode == 0
        validations.append({'commit': commit, 'messageSha256': sha(message), 'owning21_2_4Exit': checked.returncode})
    audit = {'utc': datetime.datetime.now(datetime.timezone.utc).isoformat(), 'base': base, 'head': source,
             'changedPaths': changed, 'completePatchBytes': len(patch), 'completePatchSha256': sha(patch),
             'evidenceFilesRehashed': len(index['items']), 'evidenceBytesRehashed': total,
             'indexSha256': sha((EVIDENCE / 'two-phase-required349-evidence-index.json').read_bytes()),
             'allSourceHashesComparedToImmutableGit': len(hashes), 'outputDllsRehashedUnchanged': 631,
             'signedArchiveMembersRehashed': 317, 'allActualUniqueCasesPassed': 349,
             'all347PriorExecutedNamesPreserved': True, 'groups': groups, 'causalPhaseObservations': observations,
             'commitMessageValidations': validations,
             'sourceAssessment': 'Only tests, the existing test fixture opt-in, and profile registration change. The signed preflight and actual ordinary catalog execute before resource effects. Derived cache comparison permits only the actual reader entry and recomputed hash. The paired former call demonstrates the real proof/Generate refusal. Production source is unchanged from genuine v1.4.1.',
             'limitations': ['The exact inert image probe supplies no real image or service qualification.',
                             'Actual native callers and manual lifecycle gates remain required.'],
             'candidateAcceptance': False, 'storyComplete': False}
    assert not OUTPUT.exists()
    OUTPUT.write_text(json.dumps(audit, indent=2) + '\n')
    print(json.dumps({key: audit[key] for key in ['head', 'evidenceFilesRehashed', 'allSourceHashesComparedToImmutableGit', 'allActualUniqueCasesPassed', 'outputDllsRehashedUnchanged']}, indent=2))

if __name__ == '__main__':
    main()
