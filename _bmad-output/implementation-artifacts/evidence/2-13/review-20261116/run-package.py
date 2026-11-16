"""Verify the 2026-11-16 review-patch candidate in a disposable copy, with signed packages and no Dapr CLI on PATH."""
from pathlib import Path
import datetime, hashlib, json, os, shutil, signal, subprocess, sys, urllib.request, xml.etree.ElementTree as ET, zipfile

source = Path(__file__).resolve().parents[5]
out = Path(__file__).resolve().parent / 'package'
out.mkdir(exist_ok=False)
scratch = Path(os.environ['TMPDIR'])
candidate = scratch / 'platform'
records = []

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def dump(name, value):
    (out / name).write_text(json.dumps(value, indent=2) + '\n')

def git(root, *arguments):
    return subprocess.check_output(['git', '-c', 'core.commitGraph=false', '-c', 'submodule.recurse=false', *arguments], cwd=root, stderr=subprocess.PIPE).decode().strip()

def run(label, command, cwd=candidate, timeout=1200, required=True):
    log = out / (label + '.log')
    started = datetime.datetime.now(datetime.timezone.utc).isoformat()
    with log.open('x') as stream:
        child = subprocess.Popen(command, cwd=cwd, stdout=stream, stderr=subprocess.STDOUT, start_new_session=True)
        try:
            code = child.wait(timeout=timeout)
        except subprocess.TimeoutExpired:
            os.killpg(child.pid, signal.SIGTERM)
            try:
                child.wait(timeout=10)
            except subprocess.TimeoutExpired:
                os.killpg(child.pid, signal.SIGKILL)
                child.wait()
            code = 'timeout'
    row = {'label': label, 'command': command, 'cwd': str(cwd), 'startedUtc': started,
           'exitCode': code, 'logSha256': sha(log)}
    records.append(row)
    dump('package-commands.json', records)
    print(label, code, flush=True)
    if required:
        assert code == 0, label
    return row

os.environ.update(NUGET_PACKAGES=str(scratch / 'packages'), MSBUILDDISABLENODEREUSE='1', DOTNET_CLI_USE_MSBUILD_SERVER='0', DOTNET_CLI_TELEMETRY_OPTOUT='1')
# No /usr/local/bin: the original test accidentally found /usr/local/bin/dapr.
os.environ['PATH'] = str(Path(shutil.which('dotnet')).parent) + ':/usr/bin:/bin'
assert shutil.which('dapr') is None, 'Verification must reproduce a host with no installed Dapr CLI.'
revision = git(source, 'rev-parse', 'HEAD')
patch = subprocess.check_output(['git', '-c', 'core.commitGraph=false', 'diff', '--binary', 'HEAD', '--', '.',
    ':(exclude)_bmad-output/implementation-artifacts/evidence/2-13/review-20261116'], cwd=source)
(out / 'candidate.patch').write_bytes(patch)
git(source, 'clone', '--shared', '--no-checkout', str(source), str(candidate))
git(candidate, 'checkout', '--detach', revision)
subprocess.run(['git', 'apply', '--binary', '--whitespace=nowarn', '-'], cwd=candidate, input=patch, capture_output=True, check=True)
for line in git(source, 'ls-tree', 'HEAD:references').splitlines():
    metadata, name = line.split('\t')
    mode, kind, selected = metadata.split()
    if mode != '160000':
        continue
    owner = candidate / 'references' / name
    git(source, 'clone', '--shared', '--no-checkout', str(source / 'references' / name), str(owner))
    git(owner, 'checkout', '--detach', selected)
git(candidate, 'add', '-A')
git(candidate, '-c', 'user.name=Candidate verification', '-c', 'user.email=fixture@invalid', '-c', 'commit.gpgsign=false', 'commit', '-qm', 'test(ci): verify 2.13 review patches of 2026-11-16')
private_revision = git(candidate, 'rev-parse', 'HEAD')
assert not git(candidate, 'status', '--porcelain')
inputs = {path: sha(candidate / path) for path in git(candidate, 'ls-files').splitlines()
          if (candidate / path).is_file() and (path.startswith(('src/', 'eng/', 'tests/Hexalith.Platform.Composition.Tests/')) or path in ['Directory.Build.props', 'Directory.Packages.props', 'global.json'])}
dump('candidate.json', {'sourceRevision': revision, 'candidateRevision': private_revision, 'candidateTree': git(candidate, 'rev-parse', 'HEAD^{tree}'),
    'sourceFiles': inputs, 'patchSha256': sha(out / 'candidate.patch'), 'historicalPlatformInputs': [], 'directOwnersOnly': True,
    'daprCliOnPath': shutil.which('dapr'), 'path': os.environ['PATH'], 'privatePackageCache': os.environ['NUGET_PACKAGES']})
print('candidate', private_revision, flush=True)

binding = json.loads((candidate / 'eng/builds-tuple.json').read_bytes())
publication = binding['publication']
version = publication['version']
artifacts = scratch / 'builds'
artifacts.mkdir()
archive = artifacts / ('hexalith.builds.module.cli.' + version + '.nupkg')
with urllib.request.urlopen('https://api.nuget.org/v3-flatcontainer/hexalith.builds.module.cli/' + version + '/' + archive.name, timeout=90) as response:
    archive.write_bytes(response.read())
assert sha(archive) == publication['archiveSha256']
run('published-signature', ['dotnet', 'nuget', 'verify', str(archive), '--all'])
extracted = artifacts / 'published'
files = {}
with zipfile.ZipFile(archive) as package:
    for entry in package.infolist():
        if entry.is_dir():
            continue
        destination = (extracted / entry.filename).resolve()
        assert destination.is_relative_to(extracted) and entry.filename not in files
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_bytes(package.read(entry))
        files[entry.filename] = sha(destination)
tool = extracted / 'tools/net11.0/any'
assert sha(tool / 'Hexalith.Builds.Tooling.dll') == publication['toolingSha256']
assert subprocess.check_output(['dotnet', str(tool / 'Hexalith.Builds.Module.Cli.dll'), '--version'], cwd=candidate).decode().strip() == version + '+' + publication['sourceRevision']
identity = {'toolRoot': str(tool), 'version': version, 'buildsRevision': publication['sourceRevision'],
            'archiveSha256': sha(archive), 'tupleBinding': binding, 'files': files, 'candidateAcceptance': False}
(artifacts / 'artifact-identity.json').write_text(json.dumps(identity, indent=2) + '\n')
dump('published-artifact.json', identity)

properties = ['-p:UseHexalithProjectReferences=false', '-p:HexalithBuildsRoot=' + str(candidate / 'references/Hexalith.Builds'),
              '-p:HexalithBuildsPublishedToolRoot=' + str(tool), '-p:HexalithEventStoreVersion=4.2.0',
              '-p:HexalithTenantsVersion=7.0.7', '-p:HexalithMemoriesVersion=3.0.27', '-p:HexalithPartiesVersion=3.1.1', '-p:HexalithMcpCliVersion=2.0.1']
run('solution-build', ['dotnet', 'build', 'Hexalith.Platform.slnx', '-c', 'Release', '-m:1', '--disable-build-servers', '--artifacts-path', str(scratch / 'build'), *properties])
assembly = scratch / 'build/bin/Hexalith.Platform.Composition.Tests/release/Hexalith.Platform.Composition.Tests.dll'
sys.path.insert(0, str(candidate / 'eng'))
from run_tuple_ci import snapshot_composition_test_outputs, verify_composition_test_outputs_unchanged, require_collector_source, require_collector_execution, verify_test_sdk, verify_collecting_cohort, sdk42_collecting_cohort, REQUIRED_TOTALS, validate_profile
require_collector_source(candidate, private_revision)
verify_test_sdk(assembly, '4.2.0', '3.0.27')
assets = scratch / 'build/obj/Hexalith.Platform.Composition.Tests/project.assets.json'
dump('restored-cohort.json', verify_collecting_cohort(json.loads(assets.read_bytes()), sdk42_collecting_cohort(binding, 'current42')))
outputs = {'before': snapshot_composition_test_outputs(assembly)}
dump('source-and-output-before.json', {'candidateRevision': private_revision, 'sourceFiles': inputs, 'outputs': outputs['before']})
rows = []
jobs = []
for filename, suite in [('tuple-ci-sdk42-current-suites.json', 'current42'), ('tuple-ci-sdk42-base-suites.json', 'base42'), ('tuple-ci-extension-suites.json', 'extensions')]:
    profile = json.loads((candidate / 'eng' / filename).read_bytes())
    assert validate_profile(profile) == REQUIRED_TOTALS[suite]
    jobs.extend((suite, number, group) for number, group in enumerate(profile['groups']))
# The changed transport, listener and restored groups run first; all declared groups still run exactly once.
priority = {('current42', 0): 0, ('current42', 7): 1, ('current42', 32): 2, ('current42', 33): 3}
jobs.sort(key=lambda item: priority.get((item[0], item[1]), 100))
for suite, number, group in jobs:
    label = suite + '-' + str(number)
    xml = out / (label + '.xml')
    command = ['dotnet', str(assembly), *[arg for selector in group['selectors'] for arg in [group['filter'], selector]], '-result-xml', str(xml)]
    row = run(label, command, required=False)
    row.update(suite=suite, group=number, selectors=group['selectors'], required=group['requiredCount'], candidateRevision=private_revision)
    if xml.exists():
        doc = ET.parse(xml).getroot().find('assembly')
        row['counts'] = doc.attrib
        row['failures'] = [{'name': test.attrib['name'], 'message': test.find('failure/message').text} for test in doc.iter('test') if test.attrib.get('result') == 'Fail']
        require_collector_execution(row['counts'], 'net11.0')
        row['passed'] = row['exitCode'] == 0 and int(row['counts']['total']) == int(row['counts']['passed']) == group['requiredCount'] and all(int(row['counts'].get(key, 0)) == 0 for key in ['failed', 'errors', 'skipped', 'not-run'])
    else:
        row['passed'] = False
    rows.append(row)
    dump('composition-results.json', rows)
    print(label, row.get('counts', {}).get('total'), row['passed'], flush=True)
    assert row['passed'], label
verify_composition_test_outputs_unchanged(outputs)
require_collector_source(candidate, private_revision)
assert all(sha(candidate / path) == digest for path, digest in inputs.items())
dump('source-and-output-after.json', {'candidateRevision': private_revision, 'unchanged': True, 'outputs': outputs})
dump('composition-summary.json', {'candidateRevision': private_revision, 'passed': True, 'groups': len(rows),
    'totals': {suite: sum(int(row['counts']['total']) for row in rows if row['suite'] == suite) for suite in REQUIRED_TOTALS},
    'executions': sum(int(row['counts']['total']) for row in rows), 'skipped': 0, 'daprCliOnPath': None,
    'candidateAcceptance': False, 'hostedMainAcceptance': False})
run('tuple-audit', [sys.executable, '-B', 'eng/run_tuple_ci.py', '--audit-only', '--suite', 'current42', '--eventstore-sdk', '4.2.0', '--artifacts', str(artifacts), '--evidence', str(out / 'tuple-audit')])
run('collection-graph', [sys.executable, '-B', 'eng/test_tuple_collection_graph.py', '--evidence', str(out / 'collection-graph')])
run('source-sdk', [sys.executable, '-B', 'eng/test_source_sdk_propagation.py', '--evidence', str(out / 'source-sdk'), '--artifacts', str(scratch / 'build'), '--published-tool-root', str(tool)])
require_collector_source(candidate, private_revision)
assert all(sha(candidate / path) == digest for path, digest in inputs.items())
dump('package-summary.json', {'passed': True, 'candidateRevision': private_revision, 'candidateTree': git(candidate, 'rev-parse', 'HEAD^{tree}'),
    'compositionExecutions': sum(int(row['counts']['total']) for row in rows), 'skipped': 0, 'daprCliOnPath': None, 'sourceAndOutputUnchanged': True,
    'candidateAcceptance': False, 'hostedMainAcceptance': False, 'allChildrenFinished': True})
