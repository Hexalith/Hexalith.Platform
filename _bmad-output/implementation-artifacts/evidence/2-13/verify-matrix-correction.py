"""Execute affected guard groups and retain honest per-snapshot reuse observations."""
from pathlib import Path
import datetime, hashlib, json, os, shutil, subprocess, sys, xml.etree.ElementTree as ET

evidence = Path(sys.argv[1]).resolve()
assembly = Path(sys.argv[2]).resolve()
target = evidence / (sys.argv[3] if len(sys.argv) > 3 else 'matrix-correction')
target.mkdir(exist_ok=False)
sys.path.insert(0, str(Path.cwd() / 'eng'))
from run_tuple_ci import snapshot_composition_test_outputs, verify_composition_test_outputs_unchanged, require_collector_source, require_collector_execution
def git(*args):
    return subprocess.check_output(['git', '-c', 'submodule.recurse=false', *args], stderr=subprocess.PIPE).decode().strip()
def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()
def dump(path, value):
    path.write_text(json.dumps(value, indent=2) + '\n')

prior_file = evidence / 'continuing-inventories.json'
prior = json.loads(prior_file.read_text())
if not (evidence / 'continuing-inventories-after-transport.json').exists():
    shutil.copyfile(prior_file, evidence / 'continuing-inventories-after-transport.json')
previous = prior['finalCandidateRevision']
revision = git('rev-parse', 'HEAD')
allowed = ['tests/Hexalith.Platform.Composition.Tests/' + p + '.cs' for p in [
    'PlatformEventStorePackageTestCohortTests', 'PlatformPackageCatalogQualificationTests', 'PlatformSdk42EventStorePackageTestCohortTests']]
changed = git('diff', '--name-only', previous, revision).splitlines()
assert sorted(changed) == sorted(allowed), changed
require_collector_source(Path.cwd(), revision)
(target / 'test-source-delta.patch').write_text(git('diff', previous, revision) + '\n')
production = assembly.parent / 'Hexalith.Platform.Composition.dll'
before = snapshot_composition_test_outputs(assembly)
shutil.copyfile(production, target / 'prior-production.dll')
command = json.loads((evidence / 'transport-correction/build-command.json').read_text())['command']
with (target / 'build.log').open('w') as log:
    code = subprocess.run(command, stdout=log, stderr=subprocess.STDOUT, timeout=1200).returncode
dump(target / 'build-command.json', {'candidateRevision': revision, 'command': command, 'cwd': str(Path.cwd()), 'exitCode': code})
assert code == 0
after = snapshot_composition_test_outputs(assembly)
shutil.copyfile(production, target / 'final-production.dll')
helper = Path(os.environ['TMPDIR']) / 'method-fingerprint/bin/Release/net11.0/Fingerprint.dll'
fingerprints = {label: json.loads(subprocess.check_output(['dotnet', str(helper), str(target / (label + '-production.dll'))], timeout=30)) for label in ['prior', 'final']}
assert fingerprints['prior']['methodAndReferenceSha256'] == fingerprints['final']['methodAndReferenceSha256']
assert all(before['dependencies'][n]['sha256'] == after['dependencies'][n]['sha256'] for n in before['dependencies'])
record = {'before': after}
rows = []
for suite, filename, number in [('current42', 'tuple-ci-sdk42-current-suites.json', 6), ('current42', 'tuple-ci-sdk42-current-suites.json', 20), ('base42', 'tuple-ci-sdk42-base-suites.json', 0)]:
    group = json.loads((Path('eng') / filename).read_text())['groups'][number]
    xml = target / (suite + '-' + str(number) + '.xml')
    command = ['dotnet', str(assembly), *[a for s in group['selectors'] for a in [group['filter'], s]], '-result-xml', str(xml)]
    started = datetime.datetime.now(datetime.timezone.utc).isoformat()
    with xml.with_suffix('.log').open('w') as log:
        code = subprocess.run(command, stdout=log, stderr=subprocess.STDOUT, timeout=1200).returncode
    doc = ET.parse(xml).getroot().find('assembly')
    counts = doc.attrib
    require_collector_execution(counts, 'net11.0')
    passed = code == 0 and int(counts['total']) == int(counts['passed']) == group['requiredCount'] and all(int(counts.get(k, 0)) == 0 for k in ['failed', 'errors', 'skipped', 'not-run'])
    row = {'suite': suite, 'profile': filename, 'group': number, 'selectors': group['selectors'], 'required': group['requiredCount'],
           'candidateRevision': revision, 'startedUtc': started, 'command': command, 'exitCode': code, 'counts': counts, 'passed': passed,
           'failures': [{'name': t.attrib['name'], 'message': t.find('failure/message').text} for t in doc.iter('test') if t.attrib.get('result') == 'Fail'],
           'evidenceDisposition': 'executed-final-snapshot'}
    rows.append(row)
    dump(target / 'results.json', rows)
    print(suite, number, counts['total'], passed, flush=True)
verify_composition_test_outputs_unchanged(record)
require_collector_source(Path.cwd(), revision)
binding = {'priorRevision': previous, 'finalRevision': revision, 'onlyChangedSources': changed, 'completeProductionSourceUnchanged': True,
           'testSourceHashes': {p: digest(Path(p)) for p in changed}, 'priorOutput': before, 'finalOutput': record,
           'priorProductionSha256': digest(target / 'prior-production.dll'), 'finalProductionSha256': digest(target / 'final-production.dll'),
           'fingerprints': fingerprints, 'productionMethodsAndReferencesEqual': True, 'allFiveDependencyHashesEqual': True, 'revisionMetadataOverridden': False}
dump(target / 'source-and-output-binding.json', binding)
assert all(r['passed'] for r in rows)
replacements = {(r['suite'], r['group']): r for r in rows}
combined = [replacements.get((r['suite'], r['group']), dict(r, evidenceDisposition='reused-unchanged-control', reason='Only the three guard-test classes changed; all production source and method/reference fingerprints and five dependency hashes are equal.')) for r in prior['results']]
dump(prior_file, {'passed': all(r['passed'] for r in combined), 'finalCandidateRevision': revision, 'priorRunRevision': previous, 'groups': len(combined),
    'freshFinalSnapshotGroups': len(rows), 'reusedUnchangedGroups': len(combined) - len(rows),
    'totals': {s: sum(int(r['counts']['total']) for r in combined if r['suite'] == s) for s in ['current42', 'base42', 'extensions']},
    'skips': sum(int(r['counts']['skipped']) for r in combined), 'candidateAcceptance': False, 'hostedMainAcceptance': False, 'results': combined})
print('guard matrices verified; unchanged controls explicitly reconciled', flush=True)
