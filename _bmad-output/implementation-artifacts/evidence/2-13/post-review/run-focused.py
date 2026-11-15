"""Build one clean candidate and execute only the review-affected controls."""
from pathlib import Path
import datetime, hashlib, json, os, signal, subprocess, sys, xml.etree.ElementTree as ET

out = Path(sys.argv[1]).resolve()
source = Path(sys.argv[2]).resolve()
scratch = Path(os.environ['TMPDIR'])
tool = Path(os.environ['TASK_REVIEW_TOOL'])
sys.path.insert(0, str(Path.cwd() / 'eng'))
from run_tuple_ci import require_collector_source, require_collector_execution, snapshot_composition_test_outputs, verify_composition_test_outputs_unchanged

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()
def dump(path, value):
    path.write_text(json.dumps(value, indent=2) + '\n')
def git(*args):
    return subprocess.check_output(['git', '-c', 'submodule.recurse=false', *args], stderr=subprocess.PIPE).decode().strip()
def run(command, log, timeout=1200):
    with log.open('x') as stream:
        child = subprocess.Popen(command, stdout=stream, stderr=subprocess.STDOUT, start_new_session=True)
        try:
            return child.wait(timeout=timeout)
        except subprocess.TimeoutExpired:
            os.killpg(child.pid, signal.SIGTERM)
            try:
                child.wait(timeout=10)
            except subprocess.TimeoutExpired:
                os.killpg(child.pid, signal.SIGKILL)
                child.wait()
            return 'timeout'

revision = git('rev-parse', 'HEAD')
require_collector_source(Path.cwd(), revision)
paths = ['tests/Hexalith.Platform.Composition.Tests/' + name + '.cs' for name in [
    'PlatformPartiesPackageEntryTests', 'PlatformPartiesCatalogHandoffTests', 'PlatformCurrentOwnerHandoffTests',
    'PlatformCurrentOwnerPackageTests', 'PlatformQualificationPackageFixture']]
paths += ['eng/test_current_tuple_source.py', 'docs/source-releases.md', 'docs/package-catalog-qualification.md']
inputs = {p: sha(Path(p)) for p in paths}
assert all(sha(source / p) == h for p, h in inputs.items())
(out / 'review-fix.patch').write_bytes(subprocess.check_output(['git', 'diff', '--binary', '--', *paths], cwd=source, stderr=subprocess.PIPE))
original_archive = source / '_bmad-output/implementation-artifacts/evidence/2-13/verification-artifacts.tar.gz'
original_archive_hash = sha(original_archive)
command = ['dotnet', 'build', 'tests/Hexalith.Platform.Composition.Tests/Hexalith.Platform.Composition.Tests.csproj', '-c', 'Release', '-m:1',
    '--artifacts-path', str(scratch / 'build'), '-p:UseHexalithProjectReferences=false', '-p:HexalithBuildsRoot=' + str(Path.cwd() / 'references/Hexalith.Builds'),
    '-p:HexalithBuildsPublishedToolRoot=' + str(tool), '-p:HexalithEventStoreVersion=4.2.0', '-p:HexalithTenantsVersion=7.0.7',
    '-p:HexalithMemoriesVersion=3.0.27', '-p:HexalithPartiesVersion=3.1.1', '-p:HexalithMcpCliVersion=2.0.1']
code = run(command, out / 'composition-build.log')
dump(out / 'build-command.json', {'candidateRevision': revision, 'cwd': str(Path.cwd()), 'command': command, 'exitCode': code})
print('build', code, flush=True)
assert code == 0
assembly = scratch / 'build/bin/Hexalith.Platform.Composition.Tests/release/Hexalith.Platform.Composition.Tests.dll'
outputs = {'before': snapshot_composition_test_outputs(assembly)}
production = assembly.parent / 'Hexalith.Platform.Composition.dll'
production_hash = sha(production)
dump(out / 'source-and-output-before.json', {'candidateRevision': revision, 'candidateTree': git('rev-parse', 'HEAD^{tree}'),
    'sourceFiles': inputs, 'output': outputs['before'], 'productionAssemblySha256': production_hash,
    'publishedAppHostSha256': sha(tool / 'g4-host/Hexalith.Builds.Module.AppHost.dll'),
    'publishedToolingSha256': sha(tool / 'g4-host/Hexalith.Builds.Tooling.dll'), 'originalArchiveSha256': original_archive_hash})
prefix = 'Hexalith.Platform.Composition.Tests.'
jobs = [
    ('fixture-factories', 3, [
        'PlatformPackageCatalogQualificationTests.LoadedPublicSdkBytesDetermineAdmissionAndImmutableTenantImages',
        'PlatformEventStorePackageTestCohortTests.OrdinaryAndSerializedOptionsCannotSelectThePrivatePurpose',
        'PlatformSdk42EventStorePackageTestCohortTests.OrdinaryAndSerializedOptionsCannotSelectThePrivatePurpose']),
    ('entry', 11, ['PlatformPartiesPackageEntryTests.ChangedFullEntryRefusesBeforeEffectsAsync']),
    ('reader-reuse', 1, ['PlatformCurrentOwnerPackageTests.CurrentOwnerBuildAndReaderUseIdenticalMeasuredReleaseContractsAsync']),
    ('catalog-handoff', 2, ['PlatformPartiesCatalogHandoffTests.PublishedPreflightAndOrdinaryCatalogRespectVerifiedDerivationAsync']),
    ('signed-handoff', 2, ['PlatformCurrentOwnerHandoffTests.PublishedCurrentValidationThenFreshOrdinaryGeneratePreservesMeasuredAuthorityAsync',
                          'PlatformCurrentOwnerHandoffTests.PublishedCurrentValidationAcceptsFreshWellFormedInitialKeyWithoutPriorAnchorAsync']),
]
results = []
for label, required, methods in jobs:
    xml = out / (label + '.xml')
    command = ['dotnet', str(assembly), *[a for method in methods for a in ['-method', prefix + method]], '-result-xml', str(xml)]
    row = {'control': label, 'required': required, 'candidateRevision': revision, 'startedUtc': datetime.datetime.now(datetime.timezone.utc).isoformat(), 'command': command}
    row['exitCode'] = run(command, out / (label + '.log'))
    if xml.exists():
        doc = ET.parse(xml).getroot().find('assembly')
        row['counts'] = doc.attrib
        row['failures'] = [{'name': t.attrib['name'], 'message': t.find('failure/message').text} for t in doc.iter('test') if t.attrib.get('result') == 'Fail']
        require_collector_execution(row['counts'], 'net11.0')
        row['passed'] = row['exitCode'] == 0 and int(row['counts']['total']) == int(row['counts']['passed']) == required and all(int(row['counts'].get(k, 0)) == 0 for k in ['failed', 'errors', 'skipped', 'not-run'])
    else:
        row['passed'] = False
    results.append(row)
    dump(out / 'results.json', results)
    print(label, row.get('counts', {}).get('total'), row['passed'], flush=True)
verify_composition_test_outputs_unchanged(outputs)
assert sha(production) == production_hash and sha(original_archive) == original_archive_hash
require_collector_source(Path.cwd(), revision)
assert all(sha(Path(p)) == h for p, h in inputs.items())
dump(out / 'source-and-output-after.json', {'candidateRevision': revision, 'unchanged': True, 'output': outputs,
    'productionAssemblySha256': production_hash, 'originalArchiveSha256': original_archive_hash})
summary = {'scope': 'post-review focused controls only', 'candidateRevision': revision, 'candidateTree': git('rev-parse', 'HEAD^{tree}'),
    'passed': len(results) == len(jobs) and all(r['passed'] for r in results), 'executed': sum(int(r.get('counts', {}).get('total', 0)) for r in results),
    'required': 19, 'skipped': sum(int(r.get('counts', {}).get('skipped', 0)) for r in results), 'sourceAndOutputUnchanged': True,
    'originalArchiveUnchanged': True, 'candidateAcceptance': False, 'hostedMainAcceptance': False}
dump(out / 'summary.json', summary)
raise SystemExit(not summary['passed'])
