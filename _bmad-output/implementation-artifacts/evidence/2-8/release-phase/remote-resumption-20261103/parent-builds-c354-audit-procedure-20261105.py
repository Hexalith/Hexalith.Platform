from pathlib import Path, PurePosixPath
import datetime
import hashlib
import json
import subprocess
import xml.etree.ElementTree as ET
import zipfile

base = Path.home() / '.cache/hexalith/story-2-8/builds-prerequisite-launch-diagnostic-20261105'
ev = Path(__file__).resolve().parent
d = base / 'evidence'
index = d / 'safe-launch-diagnostic-evidence-index.json'
checkpoint = d / 'safe-launch-diagnostic-checkpoint.json'
r = json.loads(index.read_bytes())
c = json.loads(checkpoint.read_bytes())
sha = lambda b: hashlib.sha256(b).hexdigest()
assert sha(index.read_bytes()) == c['indexSha256'] == '7ff8d107610e84c9c5d898589111045238a9cb6af46bff3ed9fd2335a9c26628'
assert r['fileCount'] == len(r['files']) == len({x['path'] for x in r['files']}) == 18151
total = 0
for entry in r['files']:
    path = PurePosixPath(entry['path'])
    assert not path.is_absolute() and '..' not in path.parts
    b = (base / entry['path']).read_bytes()
    assert len(b) == entry['bytes'] and sha(b) == entry['sha256'], entry['path']
    total += len(b)
assert total == r['totalBytes'] == 2252589951

root = Path(c['owningProposal']['owningRoot'])
head = 'c354a044e40d70f494732180be0046611bae44cf'
parent = '43adaa5d30437bec89f9485d414b1f92342b617c'
git = lambda *args: subprocess.check_output(['git', '-C', str(root), *args])
assert git('status', '--porcelain').strip() == b''
assert git('rev-parse', 'HEAD').decode().strip() == head
assert git('rev-parse', head + '^').decode().strip() == parent
patch = git('diff', '--binary', '--full-index', parent, head)
assert len(patch) == 17068
assert sha(patch) == c['owningProposal']['patchSha256'] == 'f5a1784bd4c9af660e0b3603c190d53d3d3794623e1737fc95fd70a19b64e3d6'
assert patch == (d / 'complete-owning-base-proposal.patch').read_bytes()
message = (d / 'commit-message.txt').read_bytes()
assert sha(message) == c['owningProposal']['commitMessageSha256']
assert git('show', '-s', '--format=%B', head).rstrip(b'\n') == message.rstrip(b'\n')

xmls = {}
names = {}
for name, passed, failed in [('causal-red.xml', 0, 3), ('omission-mutant.xml', 0, 3), ('final-clean-narrow-groups.xml', 27, 0)]:
    path = d / name
    xml = ET.parse(path).getroot()
    assembly = xml.find('assembly')
    tests = xml.findall('.//test')
    assert int(assembly.attrib['passed']) == passed and int(assembly.attrib['failed']) == failed
    assert int(assembly.attrib['total']) == passed + failed
    assert int(assembly.attrib['skipped']) == int(assembly.attrib['errors']) == 0
    assert len(tests) == len({t.attrib['name'] for t in tests}) == passed + failed
    assert all(t.attrib['result'] == ('Pass' if passed else 'Fail') for t in tests)
    xmls[name] = {'bytes': path.stat().st_size, 'sha256': sha(path.read_bytes()), 'actualPassed': passed, 'actualFailed': failed}
    names[name] = [t.attrib['name'] for t in tests]
assert set(names['causal-red.xml']) == set(names['omission-mutant.xml'])
assert set(names['causal-red.xml']).issubset(names['final-clean-narrow-groups.xml'])

packages = json.loads((d / 'public-hexalith-package-inputs.json').read_bytes())
assert len(packages) == 12
member_count = 0
for package in packages:
    b = Path(package['path']).read_bytes()
    assert len(b) == package['bytes'] and sha(b) == package['sha256'] and package['signaturePresent']
    with zipfile.ZipFile(package['path']) as z:
        assert '.signature.p7s' in z.namelist()
        for entry in package['bodies']:
            b = z.read(entry['path'])
            assert len(b) == entry['bytes'] and sha(b) == entry['sha256']
            member_count += 1
ledger = json.loads((d / 'actual-verification-ledger.json').read_bytes())
signatures = [x for x in ledger['completedRecordedCommands'] if x['argv'][:3] == ['dotnet', 'nuget', 'verify']]
assert len(signatures) == 1 and signatures[0]['exitCode'] == 0
assert signatures[0]['argv'][3] == '--all'
assert set(signatures[0]['argv'][4:]) == {p['path'] for p in packages}
signature_output = (d / 'verify-public-hexalith-signatures.stdout').read_text()
verified_names = [line.removeprefix('Verifying ') for line in signature_output.splitlines() if line.startswith('Verifying ')]
expected_names = []
for package in packages:
    nuspec = ET.fromstring(package['nuspec'])
    expected_names.append(nuspec.findtext('.//{*}id') + '.' + nuspec.findtext('.//{*}version'))
assert len(verified_names) == len(set(verified_names)) == 12
assert set(verified_names) == set(expected_names)
assert signature_output.count('Signature type: Repository') == 12
restore = json.loads((d / 'actual-public-only-restore-proof.json').read_bytes())
assert restore['onlyNewPrivateCache'] and restore['onlyPublicFeed'] and len(restore['assets']) == 13
for entry in restore['assets']:
    assert entry['sources'] == ['https://api.nuget.org/v3/index.json']
    assert Path(entry['packagesPath']) == base / 'nuget-packages'

record = {
    'utc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
    'closedRoot': str(base), 'indexSHA256': sha(index.read_bytes()),
    'checkpointSHA256': sha(checkpoint.read_bytes()),
    'allClosedFilesRehashed': r['fileCount'], 'allClosedBytesRehashed': total,
    'parentReadCompleteSevenPathPatch': True, 'patchSHA256': sha(patch), 'patchBytes': len(patch),
    'head': head, 'parent': parent, 'tree': c['owningProposal']['tree'],
    'cleanRealHeadAndExactValidatedMessageVerified': True,
    'actualXml': xmls, 'final27Names': names['final-clean-narrow-groups.xml'],
    'actualPublicHexalithArchives': 12, 'actualSignatureCommandsAllExit0': 1,
    'actualSignatureSuccessesInBatch': 12,
    'actualPackageMembersRehashed': member_count,
    'all13RestoreSourcesPublicOnlyAndCachePrivate': True,
    'originalHostedNativeCause': 'Unproved; new safe diagnostic distinctions only',
    'hostedPublicationNativeOrStoryAcceptance': False,
}
output = ev / 'parent-builds-c354-safe-launch-complete-source-and-control-audit-20261105.json'
assert not output.exists()
output.write_text(json.dumps(record, indent=2) + '\n')
print(json.dumps({k: v for k, v in record.items() if k != 'final27Names'}, indent=2))
