"""Recheck the recorded candidate using disposable local sources and signed artifacts."""
from pathlib import Path
import hashlib
import json
import os
import shutil
import subprocess
import sys
import tarfile
import urllib.request
import xml.etree.ElementTree as ET
import zipfile

root = Path(__file__).resolve().parents[5]
out = Path(__file__).resolve().parent
scratch = Path(os.environ['TMPDIR'])
prior = out.parent / 'review-patches'


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def git(directory, *arguments):
    return subprocess.check_output(['git', '-c', 'core.commitGraph=false', '-c', 'submodule.recurse=false', *arguments], cwd=directory, stderr=subprocess.PIPE).decode().strip()


def inventory():
    return {'revision': git(root, 'rev-parse', 'HEAD'),
            'pins': {name: sha((root / name).read_bytes()) for name in json.loads((out.parent / 'before-inventory.json').read_bytes())['files']},
            'gitlinks': git(root, 'ls-tree', 'HEAD:references').splitlines(),
            'refs': git(root, 'for-each-ref', '--format=%(objectname) %(refname)').splitlines(),
            'tags': git(root, 'for-each-ref', '--format=%(objectname) %(refname)', 'refs/tags/').splitlines(),
            'qualificationBranches': git(root, 'for-each-ref', '--format=%(objectname) %(refname)', 'refs/heads/qualification/').splitlines()}


before = inventory()
original = json.loads((out.parent / 'before-inventory.json').read_bytes())
assert before['pins'] == original['files']
assert before['gitlinks'] == original['allGitlinks']
assert set(original['tags']).issubset(before['tags'])
assert not before['qualificationBranches']
sources = json.loads((prior / 'candidate.json').read_bytes())
changed = {name: {'recordedSha256': expected, 'currentSha256': sha((root / name).read_bytes())}
           for name, expected in sources['sourceFiles'].items() if sha((root / name).read_bytes()) != expected}
assert set(changed) == {'eng/test_current_tuple_source.py'}
assert changed['eng/test_current_tuple_source.py']['currentSha256'] == json.loads((prior / 'final-source-summary.json').read_bytes())['sourceSha256']

manifest = json.loads((prior / 'raw-observations.json').read_bytes())
archive = prior / manifest['file']
assert sha(archive.read_bytes()) == manifest['sha256']
totals = {}
groups = 0
with tarfile.open(archive, 'r:gz') as bundle:
    assert {member.name for member in bundle.getmembers()} == set(manifest['members'])
    for member in bundle.getmembers():
        raw = bundle.extractfile(member).read()
        assert len(raw) == manifest['members'][member.name]['bytes']
        assert sha(raw) == manifest['members'][member.name]['sha256']
    for suite, profile in [('current42', 'tuple-ci-sdk42-current-suites.json'), ('base42', 'tuple-ci-sdk42-base-suites.json'), ('extensions', 'tuple-ci-extension-suites.json')]:
        totals[suite] = 0
        for number, group in enumerate(json.loads((root / 'eng' / profile).read_bytes())['groups']):
            assembly = ET.fromstring(bundle.extractfile(suite + '-' + str(number) + '.xml').read()).find('assembly')
            assert int(assembly.attrib['total']) == int(assembly.attrib['passed']) == group['requiredCount'] > 0
            assert all(int(assembly.attrib.get(key, '0')) == 0 for key in ['failed', 'errors', 'skipped', 'not-run'])
            totals[suite] += group['requiredCount']
            groups += 1
assert totals == {'current42': 463, 'base42': 93, 'extensions': 9}
print('Reused original Composition observations:', totals, 'groups:', groups, flush=True)

candidate = scratch / 'platform'
git(root, 'clone', '--no-local', '--depth=1', '--no-checkout', str(root), str(candidate))
git(candidate, 'checkout', '--detach', before['revision'])
assert git(candidate, 'rev-list', '--count', '--all') == '1'
for entry in before['gitlinks']:
    metadata, name = entry.split('\t')
    selected = metadata.split()[2]
    owner = candidate / 'references' / name
    git(root, 'clone', '--shared', '--no-checkout', str(root / 'references' / name), str(owner))
    git(owner, 'checkout', '--detach', selected)

binding = json.loads((candidate / 'eng/builds-tuple.json').read_bytes())
version = binding['publication']['version']
artifacts = scratch / 'artifacts'
artifacts.mkdir()
package = artifacts / ('hexalith.builds.module.cli.' + version + '.nupkg')
with urllib.request.urlopen('https://api.nuget.org/v3-flatcontainer/hexalith.builds.module.cli/' + version + '/' + package.name, timeout=90) as response:
    package.write_bytes(response.read())
assert sha(package.read_bytes()) == binding['publication']['archiveSha256']
with (out / 'archive-signature.log').open('x') as stream:
    subprocess.run(['dotnet', 'nuget', 'verify', str(package), '--all'], stdout=stream, stderr=subprocess.STDOUT, check=True, timeout=180)
extracted = artifacts / 'published'
with zipfile.ZipFile(package) as bundle:
    for member in bundle.infolist():
        if member.is_dir():
            continue
        destination = (extracted / member.filename).resolve()
        assert destination.is_relative_to(extracted)
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_bytes(bundle.read(member))
tool = extracted / 'tools/net11.0/any'
assert sha((tool / 'Hexalith.Builds.Tooling.dll').read_bytes()) == binding['publication']['toolingSha256']
(artifacts / 'artifact-identity.json').write_text(json.dumps({'toolRoot': str(tool), 'version': version, 'buildsRevision': binding['producer']['sourceRevision'], 'tupleBinding': binding}))
command = [sys.executable, '-B', 'eng/run_tuple_ci.py', '--audit-only', '--suite', 'current42', '--eventstore-sdk', '4.2.0', '--artifacts', str(artifacts), '--evidence', str(out / 'tuple-audit')]
with (out / 'tuple-audit.log').open('x') as stream:
    subprocess.run(command, cwd=candidate, stdout=stream, stderr=subprocess.STDOUT, check=True, timeout=600)
assert json.loads((out / 'tuple-audit/tuple-ci-result.json').read_bytes())['status'] == 'graph-verified'
after = inventory()
assert before == after
summary = {'passed': True, 'sourceRevision': before['revision'], 'sourceFilesCompared': len(sources['sourceFiles']),
           'sourceDeltaFromOriginalCompositionRun': changed, 'originalCompositionCandidate': sources['candidateRevision'],
           'originalCompositionGroups': groups, 'originalCompositionExecutions': totals, 'originalCompositionSkipped': 0,
           'archivedMemberHashesVerified': len(manifest['members']), 'candidatePlatformCommitsAvailable': 1,
           'auditCommand': command, 'auditStatus': 'graph-verified', 'pinsAndGitlinksUnchanged': True,
           'rootRefInventoryUnchanged': True, 'originalTagsPreserved': len(original['tags']),
           'archivedQualificationTags': sum(' refs/tags/archive/qualification' in tag for tag in before['tags']),
           'brokerTags': sum(' refs/tags/qualification/broker-' in tag for tag in before['tags']),
           'localQualificationBranches': [], 'remoteRepositoryOperations': [], 'pushallInvoked': False,
           'hostedMainAcceptance': False, 'remainingAcceptance': 'AC3: successful hosted main resolve-sources, tuple and tuple-verdict.'}
(out / 'candidate-audit-summary.json').write_text(json.dumps(summary, indent=2) + '\n')
print(json.dumps(summary, indent=2), flush=True)
