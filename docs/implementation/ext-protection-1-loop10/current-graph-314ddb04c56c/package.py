"""Bundle run 314ddb04c56c into a new unique evidence directory without changing any existing evidence byte.

Usage: python3 -I package.py
"""
import hashlib
import json
from datetime import datetime, timezone
from pathlib import Path
import re
import shutil
import subprocess
import xml.etree.ElementTree as ET

RUN_ID = '314ddb04c56c'
HEX = Path('/home/administrator/projects/hexalith')
PLATFORM = HEX / 'platform'
EVENTSTORE = HEX / 'eventstore'
SCRATCH = Path('/tmp/claude-1000/-home-administrator-projects-hexalith-platform/b949fb33-be78-47f5-84d2-3974588e5fdf/scratchpad')
EXTERNAL = SCRATCH / ('ext-protection-loop10-current-graph-' + RUN_ID)
SCRIPTS = SCRATCH / ('scripts-' + RUN_ID)
LOOP10 = PLATFORM / 'docs/implementation/ext-protection-1-loop10'
BUNDLE = LOOP10 / ('current-graph-' + RUN_ID)
SOURCE_GAPS = LOOP10 / 'source-gaps-7b5a4e938648'
RESUME = LOOP10 / 'ext-protection-loop10-resume-27fe4f343c1a'
SPEC = PLATFORM / '_bmad-output/implementation-artifacts/spec-ext-protection-1-production-engine.md'
CONTEXTS = [SPEC, HEX / 'agents/_bmad-output/planning-artifacts/external-dependency-register.md',
            EVENTSTORE / 'docs/implementation/ext-protection-1-owner-acceptance.md',
            PLATFORM / 'docs/implementation/ext-secrets-1-dek-provider-decision-2026-10-18.md']


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def tree(directory, exclude=None):
    return {str(p.relative_to(directory)): digest(p) for p in sorted(directory.rglob('*'))
            if p.is_file() and (exclude is None or not p.is_relative_to(exclude))}


def load(name):
    return json.loads((EXTERNAL / name).read_text())


status = load('run-status.json')
assert status['completed'], status
assert not BUNDLE.exists(), 'unique bundle directory already exists'
existing_before = tree(LOOP10)
BUNDLE.mkdir()

# Commands and raw logs (byte-identical copies, hash-checked).
commands = load('commands.json')
for command in commands:
    raw = Path(command['log'])
    assert digest(raw) == command['logSha256'], command['name']
    bundled = BUNDLE / (command['name'] + '.log.txt')
    shutil.copyfile(raw, bundled)
    command['bundledLog'] = bundled.name
    command['bundledLogSha256'] = digest(bundled)
    assert command['bundledLogSha256'] == command['logSha256']
(BUNDLE / 'commands.json').write_text(json.dumps(commands, indent=2) + '\n')

copied = ['audit.json', 'build-freshness.json', 'build-closure.json', 'platform-runtime.json', 'eventstore-runtime.json', 'source-before.json',
          'post-run-integrity.json', 'git-before.json', 'git-after.json', 'run-status.json', 'run-marker.json', 'platform-tests.xml', 'eventstore-tests.xml',
          'run-driver.log']
for name in copied:
    shutil.copyfile(EXTERNAL / name, BUNDLE / name)
for name in ['audit.py', 'run.py', 'package.py']:
    shutil.copyfile(SCRIPTS / name, BUNDLE / name)

# Concurrent build processes: summarized only, because other sessions' command lines are not this run's evidence.
processes = load('concurrent-processes-before.json')
shared = [line for line in processes if re.search(r'/home/administrator/projects/hexalith/(platform|eventstore)\b', line)]
process_summary = {'dotnetBuildOrMsbuildProcessesObservedBeforeRun': len(processes), 'referencingSharedPlatformOrEventStoreCheckout': len(shared),
                   'note': 'Raw process command lines belong to other sessions and are not bundled.'}

# Test results.
tests = []
for repository in ['platform', 'eventstore']:
    assembly = ET.parse(BUNDLE / (repository + '-tests.xml')).getroot().find('assembly')
    failures = [test.attrib['name'] for test in assembly.iter('test') if test.attrib.get('result') not in ('Pass',)]
    tests.append({'repository': repository, 'total': int(assembly.attrib['total']), 'passed': int(assembly.attrib['passed']),
                  'failed': int(assembly.attrib['failed']), 'errors': int(assembly.attrib['errors']), 'skipped': int(assembly.attrib['skipped']),
                  'notPassed': failures, 'xml': repository + '-tests.xml', 'sha256': digest(BUNDLE / (repository + '-tests.xml'))})
python_results = []
for name in ['bootstrap-tests', 'harness-unit-tests']:
    text = (BUNDLE / (name + '.log.txt')).read_text()
    ran = re.search(r'^Ran (\d+) tests? in', text, re.M)
    python_results.append({'name': name, 'ran': int(ran[1]) if ran else None, 'ok': bool(re.search(r'^OK\s*$', text, re.M)),
                           'exitCode': next(c['exitCode'] for c in commands if c['name'] == name),
                           'okLines': len(re.findall(r' \.\.\. ok$', text, re.M)), 'resourceWarnings': text.count('ResourceWarning')})

# Runtime comparison with the native source-gap freeze and the previous resume graph (observations only, no equality claim).
def runtime_delta(current, prior):
    current_map = {f['path']: f['sha256'] for f in current}
    prior_map = {f['path']: f['sha256'] for f in prior}
    return {'currentFiles': len(current_map), 'priorFiles': len(prior_map),
            'identical': sorted(p for p in current_map if prior_map.get(p) == current_map[p]).__len__(),
            'changed': sorted(p for p in current_map if p in prior_map and prior_map[p] != current_map[p]),
            'added': sorted(p for p in current_map if p not in prior_map), 'removed': sorted(p for p in prior_map if p not in current_map)}

platform_runtime = load('platform-runtime.json')['files']
eventstore_runtime = load('eventstore-runtime.json')['files']
runtime_comparison = {
    'platformVersusSourceGapsNativeFreeze': runtime_delta(platform_runtime, json.loads((SOURCE_GAPS / 'runtime-manifest.json').read_text())['files']),
    'platformVersusResume27fe': runtime_delta(platform_runtime, json.loads((RESUME / 'platform-runtime.json').read_text())),
    'eventstoreVersusResume27fe': runtime_delta(eventstore_runtime, json.loads((RESUME / 'eventstore-runtime.json').read_text())),
    'note': 'Observations only. This graph is a fresh rebuild; its passes are not attributed to the source-gaps native graph, and no whole-binary equality is claimed.'}
(BUNDLE / 'runtime-comparison.json').write_text(json.dumps(runtime_comparison, indent=2) + '\n')

# Source changes since the source-gaps native freeze (concurrent unrelated work is recorded, not adopted or reverted).
manifest = json.loads((SOURCE_GAPS / 'source-manifest.json').read_text())
roots = {name: Path(path) for name, path in manifest['roots'].items()}
changed, missing = [], []
for entry in manifest['files']:
    path = roots[entry['repository']] / entry['path']
    if not path.is_file():
        missing.append(entry['repository'] + '/' + entry['path'])
    elif digest(path) != entry['sha256']:
        changed.append(entry['repository'] + '/' + entry['path'])
before = {Path(f['path']) for f in load('source-before.json')['files']}
frozen = {roots[e['repository']] / e['path'] for e in manifest['files']}
added_in_closure = sorted(str(p) for p in before - frozen if any(p.is_relative_to(r) for r in roots.values()))
source_delta = {'sourceGapsFreezeFiles': len(manifest['files']), 'changedSinceFreeze': changed, 'missingSinceFreeze': missing,
                'inThisClosureButNotInFreeze': added_in_closure,
                'note': 'The freeze manifest covered the native Protection graph; this run additionally builds the EventStore Server.Tests closure, so many added paths are scope differences rather than new files.'}
(BUNDLE / 'source-delta-since-native-freeze.json').write_text(json.dumps(source_delta, indent=2) + '\n')

# Snapshot exact audited and previously selected task source bytes.
audit = load('audit.json')
selected = {(a['repository'], a['path']) for a in audit['auditedFiles']}
selected |= {(c['repository'], c['path']) for c in audit['byteComparisonWithLatestLoop10Graphs']}
snapshots = []
for repository, relative in sorted(selected):
    source = (PLATFORM if repository == 'platform' else EVENTSTORE) / relative
    target = BUNDLE / 'selected-source' / repository / relative
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(source, target)
    before_hash = next((f['sha256'] for f in load('source-before.json')['files'] if f['path'] == str(source)), None)
    # None means the file is outside the build-closure inventory (for example .gitattributes); the audit still byte-compares it.
    snapshots.append({'repository': repository, 'path': relative, 'sha256': digest(target),
                      'matchesSourceBeforeBuild': None if before_hash is None else before_hash == digest(target)})
(BUNDLE / 'selected-source.json').write_text(json.dumps(snapshots, indent=2) + '\n')

spec = SPEC.read_text()
invariants = ["status: 'in-progress'", 'review_loop_iteration: 10', "baseline_commit: '231b0f2b57e96984a919ac1cdce97cc36df18542'",
              "eventstore_baseline_commit: 'a62241679854fa9c359c0f552114a42ca0f6a638'"]
# Same span as parent-audit.json frozenIntentSha256: '## Intent' up to '## Code Map', stripped.
frozen_intent = spec[spec.index('## Intent'):spec.index('## Code Map')].strip()
freshness = load('build-freshness.json')
integrity = load('post-run-integrity.json')
verification = {
    'record': 'EXT-PROTECTION-1', 'loop': 10, 'runId': RUN_ID, 'status': 'in-progress', 'reviewLoopIteration': 10,
    'scope': 'Authorized loop-10 current-graph regression resume (2026-10-31) only',
    'recordedUtc': datetime.now(timezone.utc).isoformat(),
    'acceptedTarget': None, 'acceptedProfile': None, 'acceptedDate': None, 'availability': None,
    'nativeBootstrapIntegrationRerun': False, 'nativeFiveCaseRerun': False, 'installed41CaseRun': False, 'physicalCopyQualification': False,
    'liveOqAgentsClusterProductionOrGitMutation': False,
    'auditAllCorrectionsPresent': audit['allCorrectionsPresent'], 'auditBytesMatchLatestLoop10Graphs': audit['allBytesMatchLatestLoop10Graphs'],
    'productionSourceChanged': False, 'testSourceChanged': False,
    'builds': [{'name': f['build'], 'warnings': f['warnings'], 'errors': f['errors'], 'allOutputsFresh': f['allFresh'], 'buildStartUtc': f['buildStartUtc'],
                'projectOutputs': len(f['projectOutputs'])} for f in freshness],
    'tests': tests, 'pythonTests': python_results,
    'runtimeInventories': [{'repository': r['repository'], 'files': r['files'], 'unchangedAfterExecution': r['unchanged'],
                            'manifest': r['repository'] + '-runtime.json', 'sha256': digest(BUNDLE / (r['repository'] + '-runtime.json')),
                            'externalRuntime': str(EXTERNAL / 'runtime' / r['repository'])} for r in integrity['runtime']],
    'sourceChangedDuringRun': integrity['sourceChangedDuringRun'], 'sourceAddedDuringRun': integrity['sourceAddedDuringRunInPredictedScope'],
    'sourceAfterManifest': {'path': str(EXTERNAL / 'source-after.json'), 'sha256': digest(EXTERNAL / 'source-after.json')},
    'concurrentProcesses': process_summary,
    'specInvariantsHold': all(i in spec for i in invariants), 'frozenIntentSha256': hashlib.sha256(frozen_intent.encode()).hexdigest(),
    'frozenIntentMatchesParentAudit': hashlib.sha256(frozen_intent.encode()).hexdigest() == json.loads((LOOP10 / 'parent-audit.json').read_text())['frozenIntentSha256'],
    'contextInventory': [{'path': str(p), 'bytes': p.stat().st_size, 'sha256': digest(p)} for p in CONTEXTS],
    'deferred': ['R7-BH-7 generic subprocess capture remains deferred and unchanged', 'Unopened Review 11 maybe-false rows remain deferred and unverified'],
    'notAttributed': 'Bootstrap native integration and the five-case native suite passes belong to source-gaps-7b5a4e938648 and are not attributed to this graph.',
}
(BUNDLE / 'verification.json').write_text(json.dumps(verification, indent=2) + '\n')

existing_after = tree(LOOP10, exclude=BUNDLE)
assert existing_after == existing_before, 'existing evidence bytes changed during packaging'
committed = subprocess.run(['git', '--no-optional-locks', '-C', str(PLATFORM), 'diff', '--quiet', 'HEAD', '--', str(LOOP10.relative_to(PLATFORM))])
preservation = {'existingFilesVerifiedUnchanged': len(existing_before), 'existingTrackedEvidenceMatchesHead': committed.returncode == 0}
(BUNDLE / 'evidence-preservation.json').write_text(json.dumps(preservation, indent=2) + '\n')
print(json.dumps({'bundle': str(BUNDLE), 'tests': tests, 'python': python_results, 'preservation': preservation,
                  'changedSinceFreeze': len(changed), 'missingSinceFreeze': len(missing)}, indent=2))
