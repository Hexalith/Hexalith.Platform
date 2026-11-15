"""Verify the sole test-only correction and explicitly reconcile unchanged controls."""
from pathlib import Path
import datetime, hashlib, json, os, shutil, subprocess, sys, xml.etree.ElementTree as ET

evidence = Path(sys.argv[1]).resolve()
assembly = Path(sys.argv[2]).resolve()
sys.path.insert(0, str(Path.cwd() / 'eng'))
from run_tuple_ci import snapshot_composition_test_outputs, verify_composition_test_outputs_unchanged, require_collector_source, require_collector_execution

def git(*args):
    return subprocess.check_output(['git', '-c', 'submodule.recurse=false', *args], stderr=subprocess.PIPE).decode().strip()
def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()
def dump(path, value):
    path.write_text(json.dumps(value, indent=2) + '\n')

prior = json.loads((evidence / 'composition-final/summary.json').read_text())
rows = json.loads((evidence / 'composition-final/results.json').read_text())
assert len(rows) == prior['groups'] == 36
failed = [r for r in rows if not r['passed']]
assert len(failed) == 1 and (failed[0]['suite'], failed[0]['group']) == ('current42', 0)
previous = prior['candidateRevision']
revision = git('rev-parse', 'HEAD')
require_collector_source(Path.cwd(), revision)
changed = git('diff', '--name-only', previous, revision).splitlines()
fixture = 'tests/Hexalith.Platform.Composition.Tests/PlatformMemoriesContainerTransportTests.cs'
assert changed == [fixture], changed
source_before = json.loads((evidence / 'composition-final/source-and-output-before.json').read_text())
source_equality = {p: digest(Path(p)) == value for p, value in source_before['sourceFiles'].items() if p != fixture}
assert all(source_equality.values())
target = evidence / 'transport-correction'
target.mkdir(exist_ok=False)
(target / 'test-source-delta.patch').write_text(git('diff', previous, revision) + '\n')
production = assembly.parent / 'Hexalith.Platform.Composition.dll'
for name in [assembly.name, production.name]:
    shutil.copyfile(assembly.parent / name, target / ('prior-' + name))
before = snapshot_composition_test_outputs(assembly)
production_before = digest(production)
tool = json.loads((evidence / 'published-artifact-identity.json').read_text())['toolRoot']
command = ['dotnet', 'build', 'tests/Hexalith.Platform.Composition.Tests/Hexalith.Platform.Composition.Tests.csproj',
    '-c', 'Release', '-m:1', '--artifacts-path', str(assembly.parents[3]),
    '-p:UseHexalithProjectReferences=false', '-p:HexalithBuildsRoot=' + str(Path.cwd() / 'references/Hexalith.Builds'),
    '-p:HexalithBuildsPublishedToolRoot=' + tool, '-p:HexalithEventStoreVersion=4.2.0', '-p:HexalithTenantsVersion=7.0.7',
    '-p:HexalithMemoriesVersion=3.0.27', '-p:HexalithPartiesVersion=3.1.1', '-p:HexalithMcpCliVersion=2.0.1']
with (target / 'build.log').open('w') as log:
    result = subprocess.run(command, stdout=log, stderr=subprocess.STDOUT, timeout=1200)
dump(target / 'build-command.json', {'candidateRevision': revision, 'cwd': str(Path.cwd()), 'command': command, 'exitCode': result.returncode})
assert result.returncode == 0
after = snapshot_composition_test_outputs(assembly)
production_after = digest(production)
for name in [assembly.name, production.name]:
    shutil.copyfile(assembly.parent / name, target / ('final-' + name))

# Compare actual method IL without overriding authentic SourceLink/revision metadata.
helper = Path(os.environ['TMPDIR']) / 'method-fingerprint'
helper.mkdir(exist_ok=False)
shutil.copyfile(evidence / 'assembly-method-fingerprint.cs', helper / 'Program.cs')
(helper / 'Fingerprint.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net11.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup></Project>')
with (target / 'fingerprint-build.log').open('w') as log:
    subprocess.run(['dotnet', 'build', str(helper / 'Fingerprint.csproj'), '-c', 'Release', '-m:1', '--nologo'], stdout=log, stderr=subprocess.STDOUT, check=True, timeout=120)
fingerprints = {}
for label in ['prior', 'final']:
    body = target / (label + '-Hexalith.Platform.Composition.dll')
    fingerprints[label] = json.loads(subprocess.check_output(['dotnet', str(helper / 'bin/Release/net11.0/Fingerprint.dll'), str(body)], timeout=30))
assert fingerprints['prior']['methodAndReferenceSha256'] == fingerprints['final']['methodAndReferenceSha256']
dump(target / 'production-method-equality.json', fingerprints)

xml = target / 'transport.xml'
command = ['dotnet', str(assembly), '-class', 'Hexalith.Platform.Composition.Tests.PlatformMemoriesContainerTransportTests', '-result-xml', str(xml)]
record = {'before': after}
started = datetime.datetime.now(datetime.timezone.utc).isoformat()
with (target / 'transport.log').open('w') as log:
    code = subprocess.run(command, stdout=log, stderr=subprocess.STDOUT, timeout=1200).returncode
counts = ET.parse(xml).getroot().find('assembly').attrib
require_collector_execution(counts, 'net11.0')
passed = code == 0 and int(counts['total']) == int(counts['passed']) == 1 and all(int(counts.get(k, 0)) == 0 for k in ['failed', 'errors', 'skipped', 'not-run'])
verify_composition_test_outputs_unchanged(record)
assert digest(production) == production_after
require_collector_source(Path.cwd(), revision)
dump(target / 'result.json', {'candidateRevision': revision, 'startedUtc': started, 'command': command, 'exitCode': code, 'counts': counts, 'passed': passed})
dump(target / 'source-and-output-binding.json', {
    'priorRevision': previous, 'finalRevision': revision, 'onlyChangedSource': fixture,
    'priorTestSourceSha256': source_before['sourceFiles'][fixture], 'finalTestSourceSha256': digest(Path(fixture)),
    'allOtherRecordedSourcesEqual': all(source_equality.values()), 'sourceEquality': source_equality,
    'priorOutput': before, 'finalOutput': record, 'priorProductionSha256': production_before,
    'finalProductionSha256': production_after, 'productionFileHashEqual': production_before == production_after,
    'productionMethodsAndReferencesEqual': True,
    'dependencyHashesEqual': {name: before['dependencies'][name]['sha256'] == after['dependencies'][name]['sha256'] for name in before['dependencies']},
    'revisionMetadataOverridden': False})
assert passed
assert all(before['dependencies'][name]['sha256'] == after['dependencies'][name]['sha256'] for name in before['dependencies'])
reused = [dict(r, evidenceDisposition='reused-unchanged-control', reason='Only the separate transport test source changed; all recorded production and other test sources and production method/reference fingerprint are equal.') for r in rows if (r['suite'], r['group']) != ('current42', 0)]
replacement = dict(failed[0], candidateRevision=revision, startedUtc=started, command=command, exitCode=code, counts=counts, failures=[], passed=passed, evidenceDisposition='executed-final-snapshot')
combined = [replacement, *reused]
dump(evidence / 'continuing-inventories.json', {'passed': all(r['passed'] for r in combined), 'finalCandidateRevision': revision,
    'priorRunRevision': previous, 'groups': len(combined), 'freshFinalSnapshotGroups': 1, 'reusedUnchangedGroups': len(reused),
    'totals': {s: sum(int(r['counts']['total']) for r in combined if r['suite'] == s) for s in ['current42', 'base42', 'extensions']},
    'skips': sum(int(r['counts']['skipped']) for r in combined), 'candidateAcceptance': False, 'hostedMainAcceptance': False, 'results': combined})
print('transport correction verified; unchanged controls explicitly reconciled', flush=True)
