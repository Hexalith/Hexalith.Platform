"""Audit the already-landed current EventStore source and focused execution."""
import datetime
import hashlib
import io
import json
import os
import subprocess
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path

p = Path('/home/administrator/.cache/hexalith/story-2-8/eventstore-current-pin-repair-20261105/repair-evidence')
root = p.parent / 'Hexalith.EventStore'
read = lambda name: json.loads((p / name).read_bytes())
sha = lambda body: hashlib.sha256(body).hexdigest()

def git(*args, data=None):
    return subprocess.check_output(['git', *args], cwd=root, input=data, stderr=subprocess.DEVNULL)

idx = read('current0245-published-pin-verification-index.json')
cp = read('current0245-published-pin-verification-checkpoint.json')
d = read('current0245-focused-first-result.json')
head, base = cp['currentVerifiedHead'], cp['originalPreparedBase']
assert git('rev-parse', 'HEAD').decode().strip() == head == '0245c39c2884d4859cb4bce75aeeb209ef70ec74'
assert not git('status', '--porcelain')
assert len(idx['items']) == len({item['path'] for item in idx['items']}) == idx['files'] == 55
size = 0
for item in idx['items']:
    path = p / item['path']
    assert path.resolve().is_relative_to(p.resolve()) and not path.is_symlink()
    body = path.read_bytes()
    assert len(body) == item['bytes'] and sha(body) == item['sha256']
    size += len(body)
assert size == idx['bytes'] == 5653525
patch = git('diff', '--binary', '--full-index', '--submodule=short', base, head)
assert patch == (p / cp['actualExternalFix']['path']).read_bytes()
assert len(patch) == 734 and sha(patch) == cp['actualExternalFix']['sha256']
assert git('diff', '--name-only', base, head).decode().splitlines() == ['references/Hexalith.Builds', 'references/Hexalith.Platform']
assert git('rev-parse', head + ':references/Hexalith.Builds').decode().strip() == 'fc2122240a39f03628c04fa8433e5eece90b8b4c'
assert git('rev-parse', head + ':references/Hexalith.Platform').decode().strip() == '4afa0421794f7e6f3fcbd5d84664eae206e9daa5'
assert d['passed'] and d['source'] == head and d['sourceStatusBefore'] == d['sourceStatusAfter'] == ''
assert all(value is None for value in d['actualCiContext'].values())
assert d['sourceBefore'] == d['sourceAfter'] and len(d['sourceBefore']) == 10248
stream = io.BytesIO(git('cat-file', '--batch', data=''.join(head + ':' + name + '\n' for name in d['sourceBefore']).encode()))
source_projections = []
for name, expected in d['sourceBefore'].items():
    header = stream.readline().split()
    assert header[1] == b'blob'
    body = stream.read(int(header[2]))
    assert stream.read(1) == b'\n'
    if sha(body) == expected:
        continue
    working = root / name
    assert sha(working.read_bytes()) == expected
    if name == '_bmad-output/planning-artifacts/architecture/architecture-eventstore-2026-07-05/ARCHITECTURE-SPINE.md':
        # The observation hashes the existing link's resolved document, whereas Git stores its target text.
        assert working.is_symlink() and working.resolve().is_relative_to(root.resolve())
        assert os.readlink(working).encode() == body
        assert git('ls-tree', head, '--', name).startswith(b'120000 blob ')
        source_projections.append({'path': name, 'projection': 'existing tracked symlink to independently committed in-repository document',
                                   'gitLinkSha256': sha(body), 'observedResolvedBodySha256': expected})
    else:
        assert name == 'global.json'
        assert body.replace(b'\r\n', b'\n') == working.read_bytes().replace(b'\r\n', b'\n')
        assert json.loads(body) == json.loads(working.read_bytes())
        source_projections.append({'path': name, 'projection': 'checkout CRLF versus committed LF only',
                                   'gitSha256': sha(body), 'observedWorkingSha256': expected})
assert len(source_projections) == 2
assert d['outputBefore'] == d['outputAfter'] and len(d['outputBefore']) == 1353
for name, expected in d['outputBefore'].items():
    assert sha((root / name).read_bytes()) == expected, name
actual, groups = [], []
for group in d['groups']:
    xml = ET.parse(p / group['xml']).getroot()
    tests = list(xml.iter('test'))
    assert len(tests) == group['actual'] == group['expected'] and group['exitCode'] == group['errors'] == 0
    assert all(test.attrib['result'] == 'Pass' for test in tests)
    assert all(int(assembly.attrib[key]) == 0 for assembly in xml.iter('assembly') for key in ['errors', 'failed', 'skipped', 'not-run'])
    actual.extend(test.attrib for test in tests)
    groups.append({'name': group['group'], 'actualPassed': len(tests), 'xml': group['xml'], 'sha256': sha((p / group['xml']).read_bytes())})
assert len(actual) == len({test['id'] for test in actual}) == len({test['name'] for test in actual}) == 36
assert all(command['exitCode'] == 0 for command in d['commands'])
binding = read('actual-genuine141-tool-and-prepared-cohort-binding.json')
archive = Path(binding['genuineSignedArchive'])
assert sha(archive.read_bytes()) == binding['archiveSha256'] == '32c2f517791bfce975bf775e73bbbf626cc710da83c68b89c5e305ef55acea32'
with zipfile.ZipFile(archive) as content:
    assert len(binding['actualRestoredToolPayloads']) == 310
    for item in binding['actualRestoredToolPayloads']:
        assert sha(content.read(item['path'])) == item['sha256']
for consumer in binding['preparedEffectiveConsumers']:
    props = json.loads((p / consumer['output']).read_text())['Properties']
    assert consumer['exitCode'] == 0 and props == consumer['properties']
    assert props['HexalithEventStoreVersion'] == '4.1.0' and props['HexalithTenantsVersion'] == '7.0.2'
    assert props['UseHexalithProjectReferences'] == 'false' and props['HexalithMemoriesVersion'] == '3.0.23' and props['HexalithPartiesVersion'] == '3.0.0'
prep = read('normal-shared-preparation-first.json')
assert prep['passed'] and len(prep['commands']) == 6 and all(command['exitCode'] == 0 for command in prep['commands'])
assert binding['actualPreparedPlatformIdentity']['ReleaseTag'] == 'v1.4.1'
assert binding['actualPreparedPlatformIdentity']['Commit'] == '4afa0421794f7e6f3fcbd5d84664eae206e9daa5'
result = {'utc': datetime.datetime.now(datetime.timezone.utc).isoformat(), 'base': base, 'head': head,
          'agentAuthoredChanges': False, 'evidenceFilesRehashed': 55, 'evidenceBytesRehashed': size,
          'indexSha256': sha((p / 'current0245-published-pin-verification-index.json').read_bytes()),
          'actualExternalPatchBytes': len(patch), 'actualExternalPatchSha256': sha(patch),
          'sourceInputsIndependentlyVerified': 10248, 'sourceHashesExactlyComparedToImmutableGit': 10246,
          'explicitExistingCheckoutProjections': source_projections, 'outputDllsRehashedUnchanged': 1353,
          'actualPassedUniqueCases': 36, 'groups': groups,
          'genuineRecordedToolPayloadsComparedToSignedArchive': 310,
          'realPreparedConsumers': binding['preparedEffectiveConsumers'], 'realPlatformIdentity': binding['actualPreparedPlatformIdentity'],
          'normalSixCommandPreparationFirstPassed': True, 'mappingHash': binding['actualMappingHash'],
          'auditRenderingCorrection': 'Initial parent default diff inherited submodule log rendering (716 bytes). Explicit short/full-index rendering matches the retained complete 734-byte patch. The first source audit treated the existing documentation symlink resolved body and global.json checkout CRLF as raw Git blobs; both exact existing projections are now verified separately. Source, evidence and product assertions are unchanged.',
          'currentHostedNative9StillRequired': True, 'candidateAcceptance': False, 'storyComplete': False}
output = Path(__file__).with_name('parent-eventstore-0245-source-and-control-audit-20261105.json')
assert not output.exists()
output.write_text(json.dumps(result, indent=2) + '\n')
print(json.dumps({key: result[key] for key in ['head', 'evidenceFilesRehashed', 'sourceInputsIndependentlyVerified', 'outputDllsRehashedUnchanged', 'actualPassedUniqueCases', 'mappingHash']}, indent=2))
