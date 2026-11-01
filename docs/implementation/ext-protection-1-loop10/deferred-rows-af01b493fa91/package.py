"""Bundle deferred-row verification run af01b493fa91 into a new unique evidence directory without changing any existing evidence byte.

Usage: python3 -I package.py
"""
import difflib
import hashlib
import json
from datetime import datetime, timezone
from pathlib import Path
import shutil
import subprocess
import xml.etree.ElementTree as ET

RUN_ID = 'af01b493fa91'
HEX = Path('/home/administrator/projects/hexalith')
PLATFORM = HEX / 'platform'
EVENTSTORE = HEX / 'eventstore'
SCRATCH = Path('/tmp/claude-1000/-home-administrator-projects-hexalith-platform/b949fb33-be78-47f5-84d2-3974588e5fdf/scratchpad')
EXTERNAL = SCRATCH / ('ext-protection-loop10-deferred-rows-' + RUN_ID)
SCRIPTS = SCRATCH / ('scripts-' + RUN_ID)
LOOP10 = PLATFORM / 'docs/implementation/ext-protection-1-loop10'
BUNDLE = LOOP10 / ('deferred-rows-' + RUN_ID)
SPEC = PLATFORM / '_bmad-output/implementation-artifacts/spec-ext-protection-1-production-engine.md'
PHASES = ['phase-a-attempt1-compile-error', 'phase-a', 'phase-b', 'final']


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def tree(directory, exclude=None):
    return {str(p.relative_to(directory)): digest(p) for p in sorted(directory.rglob('*'))
            if p.is_file() and (exclude is None or not p.is_relative_to(exclude))}


assert not BUNDLE.exists(), 'unique bundle directory already exists'
existing_before = tree(LOOP10)
BUNDLE.mkdir()

# Phase evidence: commands with byte-identical raw logs, xUnit XML and manifests.
phase_results = {}
for phase in PHASES:
    source, target = EXTERNAL / phase, BUNDLE / phase
    target.mkdir()
    commands = json.loads((source / 'commands.json').read_text())
    for command in commands:
        # The first attempt directory was renamed after it stopped, so resolve each log inside its own phase directory.
        raw = source / Path(command['log']).name
        command['externalLog'] = str(raw)
        assert digest(raw) == command['logSha256'], command['name']
        bundled = target / (command['name'] + '.log.txt')
        shutil.copyfile(raw, bundled)
        command['bundledLog'] = phase + '/' + bundled.name
        command['bundledLogSha256'] = digest(bundled)
    (target / 'commands.json').write_text(json.dumps(commands, indent=2) + '\n')
    for path in sorted(source.iterdir()):
        if path.is_file() and path.suffix in ('.json', '.xml') and path.name != 'commands.json':
            shutil.copyfile(path, target / path.name)
    if (source / 'driver.log').is_file():
        shutil.copyfile(source / 'driver.log', target / 'driver.log')
    elif (EXTERNAL / (phase + '-driver.log')).is_file():
        shutil.copyfile(EXTERNAL / (phase + '-driver.log'), target / 'driver.log')
    tests = {}
    for repository in ('platform', 'eventstore'):
        xml = target / (repository + '-tests.xml')
        if xml.is_file():
            assembly = ET.parse(xml).getroot().find('assembly')
            tests[repository] = {key: int(assembly.attrib[key]) for key in ('total', 'passed', 'failed', 'errors', 'skipped')}
            tests[repository]['results'] = {test.attrib['name']: test.attrib['result'] for test in assembly.iter('test')}
    phase_results[phase] = {'commands': [{'name': c['name'], 'exitCode': c['exitCode'], 'startedUtc': c['startedUtc'], 'finishedUtc': c['finishedUtc']} for c in commands],
                            'tests': tests}

# Source: pre-change baseline, after snapshot and unified delta.
baseline = json.loads((EXTERNAL / 'baseline/baseline.json').read_text())
patch = []
changed = []
for entry in baseline:
    repo, relative = entry['repository'], entry['path']
    before_path = EXTERNAL / 'baseline' / repo / relative
    current_path = (PLATFORM if repo == 'platform' else EVENTSTORE) / relative
    (BUNDLE / 'baseline' / repo / relative).parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(before_path, BUNDLE / 'baseline' / repo / relative)
    (BUNDLE / 'after' / repo / relative).parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(current_path, BUNDLE / 'after' / repo / relative)
    before_text = before_path.read_bytes().decode().replace('\r\n', '\n').splitlines(keepends=True)
    after_text = current_path.read_bytes().decode().replace('\r\n', '\n').splitlines(keepends=True)
    patch.extend(difflib.unified_diff(before_text, after_text, 'a/' + repo + '/' + relative, 'b/' + repo + '/' + relative))
    entry['afterSha256'] = digest(current_path)
    entry['changed'] = entry['afterSha256'] != entry['baselineSha256']
    changed.append(entry)
shutil.copyfile(EXTERNAL / 'baseline/baseline.json', BUNDLE / 'baseline/baseline.json')
(BUNDLE / 'source-delta.patch').write_text(''.join(patch))
(BUNDLE / 'source-delta.json').write_text(json.dumps(changed, indent=2) + '\n')

final = json.loads((BUNDLE / 'final/task-sources-before.json').read_text())
assert all(digest(Path(item['path'])) == item['sha256'] for item in final), 'task sources changed after the final run'


def result(phase, repository, fragment):
    matches = {name: value for name, value in phase_results[phase]['tests'].get(repository, {}).get('results', {}).items() if fragment in name}
    assert matches, (phase, repository, fragment)
    return matches


def bootstrap_status(phase):
    text = (BUNDLE / phase / 'bootstrap-tests.log.txt').read_text()
    return 'Fail (3 subtests)' if 'PreexistingHeadConfigReadbackTests' in text and 'FAILED' in text else ('Pass' if '\nOK' in text else 'Unknown')


rows = [
    ('R11-BH-1', 'false', 'platform', 'RejectedUnissuedCandidateRetainsIssuedOrdinal',
     'ReadBlockedReplacementAsync returns the retained operation outcome (ActivationBlockedByReplacementKeyCompromise, receipt over the exact phase), not the durable ConsumptionBlocked batch outcome; that stored outcome satisfies UnissuedReplacementProof.',
     ['eventstore src/Hexalith.EventStore.Server/Security/DeletionConsumptionActor.cs:314-317 (returned/retained vs durable outcome)',
      'eventstore src/Hexalith.EventStore.Server/Security/DeletionConsumptionActor.cs:332 (ReadBlockedReplacementAsync returns new(phase, operation.Outcome))',
      'eventstore src/Hexalith.EventStore.Server/Security/GovernanceScopeGuardReducer.cs:443-463 (UnissuedReplacementProof)'],
     'Existing test: the guard mirror UnissuedReplacementBlocked commits with the retained stored outcome, and the owner returns the identical outcome after restore.'),
    ('R11-BH-2', 'real', 'eventstore', 'UnissuedProofRequiresIndependentCurrentnessAndARefusalStatus',
     'Refusal-status half is false: Capture refuses any refusal status other than Blocked/Stale. Currentness half is real: the unissued branch accepted on repeated guard reads plus the journal without the independent current blocked-replacement evidence the issued branch requires. In the shipped Platform composition the evidence journal performs the equivalent check, so the gap was at the component contract.',
     ['eventstore src/Hexalith.EventStore.Server/Security/DeletionConsumptionIdentity.cs:90 (refusal status)',
      'baseline eventstore PublishedTrustDeletionConsumptionAuthority.cs:316-328 (unissued branch without VerifyCurrentBlockedReplacementAsync) vs :343-344 (issued branch)',
      'platform src/Hexalith.Platform.Protection/ErasureRootAuthorityOperationHandler.cs:436-437 (shipped journal blocked-replacement evidence)',
      'fix: eventstore src/Hexalith.EventStore.Server/Security/PublishedTrustDeletionConsumptionAuthority.cs:327-329'],
     'New test fails before the fix (independent currentness never consulted) and passes after.'),
    ('R11-BH-3', 'false', 'eventstore', 'RecoveredOriginalThatFailsPostPersistValidationStaysExactAndIsNeverServedUnvalidated',
     'The snapshot persisted before the failed re-validation is the exact independently validated committed original, and every later read revalidates it before use; no unvalidated state is served or derived.',
     ['eventstore src/Hexalith.EventStore.Server/Security/InteractionOccurrenceRegistryActor.cs:103-114'], 'New passing test.'),
    ('R11-BH-5', 'false', 'eventstore', 'ForeignScopeOwnerOriginalsAreRefusedByEveryConsumerBeforePersistence',
     'The forwarder routes only to the single owner enrolled for the exact scope and passes its result through; every consumer rejects a foreign ScopeId before deserializing or persisting.',
     ['eventstore src/Hexalith.EventStore.Server/Security/PublishedTrustDeletionConsumptionAuthority.cs:367-377',
      'eventstore src/Hexalith.EventStore.Contracts/Security/RecoverableAnchoredState.cs:98',
      'eventstore src/Hexalith.EventStore.Server/Security/DeletionConsumptionActor.cs:360,541',
      'eventstore src/Hexalith.EventStore.Server/Security/InteractionOccurrenceRegistryActor.cs:103'], 'New passing test.'),
    ('R11-BH-6', 'false', 'platform', 'ActualPublishedTrustProtocolScopeAuthenticatesItsExactTenantWithoutAcceptingALabelOverride',
     'The journal client ignores the response TenantId field, but the response is bound to the exact transition by Identity == ScopeId and RequestDigest, and the receiver derives the single tenant from the transition, requires ScopeId to equal published-trust|kind|tenant|identity and requires the caller grant for that tenant.',
     ['platform src/Hexalith.Platform.Protection/DaprErasureRootEvidenceJournal.cs:14',
      'platform src/Hexalith.Platform.Protection/ErasureRootAuthorityOperationHandler.cs:37,467'], 'Existing passing test (label override and ScopeId tenant substitution refused).'),
    ('R11-BH-7', 'false', 'platform', 'ErasedReadsRefuseEverySubstitutedSourceIdentity',
     'Erased-original reads apply the owner/occurrence/key-reference/store identity checks at the binding layer, and the native skeleton read binds the caller exact binding before reading writer proof.',
     ['platform src/Hexalith.Platform.Protection/ErasureRootOccurrenceBindings.cs:28-29',
      'platform src/Hexalith.Platform.Protection/ErasureRootNativeOccurrenceSource.cs:28',
      'platform src/Hexalith.Platform.Protection/ErasureRootAuthorityOperationHandler.cs:113-115'], 'New passing test.'),
    ('R11-BH-8', 'false', 'platform', 'ConcurrentUnwrapsOfOneSharedWriterHolderCommitExactlyOneNativeHolder',
     'UnwrapAsync refuses a holder id already live, completed or archived and commits it through head CAS, so at most one unwrap of a shared erc-write id can return a key; the TryAdd loser branch is unreachable, and no loser commits a native holder.',
     ['platform src/Hexalith.Platform.Protection/ErasureRootOccurrenceKeyProvider.cs:55,64',
      'platform src/Hexalith.Platform.Protection/ErasureRootCustody.cs:177-181'], 'New passing test.'),
    ('R11-EC-1', 'real', 'platform', 'AbsentQualificationBindingsDenyWithoutThrowing',
     'With QualificationBindings null, VerifyLifecycle and VerifyReservedManifestAndCopyPolicy dereferenced the bindings through a non-short-circuit &= and threw NullReferenceException instead of returning a denial.',
     ['baseline platform ErasureRootAuthorityOperationHandler.cs:250,254',
      'fix: platform src/Hexalith.Platform.Protection/ErasureRootAuthorityOperationHandler.cs:250,254'],
     'New test fails before the fix (NullReferenceException, both operations) and passes after.'),
    ('R11-EC-3', 'real', 'bootstrap', 'PreexistingHeadConfigReadbackTests',
     'For a head mount that already existed, a configuration read without data (empty object, null data or 404) was treated as absent and the enrolled configuration was written over the unverified existing object.',
     ['baseline platform deploy/erasure-root/bootstrap.py:215', 'fix: platform deploy/erasure-root/bootstrap.py:216'],
     'New test fails before the fix (a POST to erc-heads/config in all three modes) and passes after.'),
    ('R11-EC-6', 'false', 'eventstore', 'UnissuedProofWithNonzeroIssuedRevisionIsRefusedBeforeTheJournal',
     'Capture rejects an unissued proof with a nonzero CommittedIssuedGuardRevision, so the authority returns false before the journal, the owner actor throws on capture, and the reducer also requires zero.',
     ['eventstore src/Hexalith.EventStore.Server/Security/DeletionConsumptionIdentity.cs:65',
      'eventstore src/Hexalith.EventStore.Server/Security/GovernanceScopeGuardReducer.cs:447'], 'New passing test.'),
    ('R11-EC-7', 'false', 'platform', 'SequentialAndConcurrentStoresKeepOnlyTheirOwnPinnedCa',
     'The AsyncLocal handoff is set by the synchronous production factory and consumed and cleared by the first statement of the constructor in the same flow; concurrent flows copy-on-write their own value. Validation callbacks capture the factory local certificate, not the handoff.',
     ['platform src/Hexalith.Platform.Protection/OpenBaoErasureRootStore.cs:21,53,55,61,105-106'], 'New passing test.'),
]
verdicts = []
for row, verdict, repository, fragment, finding, evidence, testing in rows:
    if repository == 'bootstrap':
        before, after, final_status = bootstrap_status('phase-a'), bootstrap_status('phase-b'), bootstrap_status('final')
        names = ['test_bootstrap.PreexistingHeadConfigReadbackTests.test_preexisting_mount_config_without_data_refuses_before_any_write']
    else:
        before, after, final_status = result('phase-a', repository, fragment), result('phase-b', repository, fragment), result('final', repository, fragment)
        names = sorted(after)
    verdicts.append({'row': row, 'verdict': verdict, 'finding': finding, 'evidence': evidence, 'testing': testing, 'tests': names,
                     'beforeFix': before, 'afterFix': after, 'finalRun': final_status})
bh9 = {'row': 'R11-BH-9 observation point', 'verdict': 'implemented; completion defect found and fixed',
       'finding': 'Internal ResponseBufferObserver (null by default) observes TrimAsync and key-metadata response buffers. Before the fix, ReadKeyAsync returned a JsonDocument over a retained copy of the metadata response that was never zeroed on success. The read now projects inside ReadKeyAsync, so no copy outlives it and every buffer is zeroed in finally.',
       'evidence': ['baseline platform OpenBaoErasureRootStore.cs:379-397 (retained copy zeroed only on failure)',
                    'platform src/Hexalith.Platform.Protection/OpenBaoErasureRootStore.cs:148-153,347,350,378,386 (observation point)'],
       'tests': sorted(result('phase-b', 'platform', 'ResponseBuffersAreNonzeroInUseAndZeroAfterward')),
       'beforeFix': result('phase-a', 'platform', 'ResponseBuffersAreNonzeroInUseAndZeroAfterward'),
       'afterFix': result('phase-b', 'platform', 'ResponseBuffersAreNonzeroInUseAndZeroAfterward'),
       'finalRun': result('final', 'platform', 'ResponseBuffersAreNonzeroInUseAndZeroAfterward')}
verdicts.append(bh9)
(BUNDLE / 'verdicts.json').write_text(json.dumps(verdicts, indent=2) + '\n')

for name in ['phase.py', 'package.py']:
    shutil.copyfile(SCRIPTS / name, BUNDLE / name)


def git_state(repository):
    def git(*arguments):
        return subprocess.run(['git', '--no-optional-locks', '-C', str(repository), *arguments], capture_output=True, text=True).stdout
    return {'repository': str(repository), 'head': git('rev-parse', 'HEAD').strip(),
            'statusPorcelain': [line for line in git('status', '--porcelain=v1').splitlines() if line]}


(BUNDLE / 'git-after.json').write_text(json.dumps([git_state(PLATFORM), git_state(EVENTSTORE)], indent=2) + '\n')
spec = SPEC.read_text()
frozen = spec[spec.index('## Intent'):spec.index('## Code Map')].strip()
verification = {
    'record': 'EXT-PROTECTION-1', 'loop': 10, 'runId': RUN_ID, 'status': 'in-progress', 'reviewLoopIteration': 10,
    'scope': 'Authorized loop-10 deferred-row verification (2026-10-31) only; not a new review cycle',
    'recordedUtc': datetime.now(timezone.utc).isoformat(),
    'acceptedTarget': None, 'acceptedProfile': None, 'acceptedDate': None, 'availability': None,
    'nativeBootstrapIntegrationRerun': False, 'nativeFiveCaseRerun': False, 'installed41CaseRun': False, 'physicalCopyQualification': False,
    'nativeCoverageNote': 'Native results (source-gaps-7b5a4e938648) do not cover source changed here: OpenBaoErasureRootStore.cs, ErasureRootAuthorityOperationHandler.cs, bootstrap.py and PublishedTrustDeletionConsumptionAuthority.cs.',
    'verdictSummary': {row['row']: row['verdict'] for row in verdicts},
    'phases': {phase: {'commands': value['commands'], 'tests': {repo: {k: v for k, v in data.items() if k != 'results'} for repo, data in value['tests'].items()}}
               for phase, value in phase_results.items()},
    'productionFilesChanged': [e['repository'] + '/' + e['path'] for e in changed if e['changed'] and ('/src/' in '/' + e['path'] or e['path'].startswith('deploy/'))],
    'testFilesChanged': [e['repository'] + '/' + e['path'] for e in changed if e['changed'] and not ('/src/' in '/' + e['path'] or e['path'].startswith('deploy/'))],
    'specInvariantsHold': all(i in spec for i in ["status: 'in-progress'", 'review_loop_iteration: 10', "baseline_commit: '231b0f2b57e96984a919ac1cdce97cc36df18542'",
                                                  "eventstore_baseline_commit: 'a62241679854fa9c359c0f552114a42ca0f6a638'"]),
    'frozenIntentSha256': hashlib.sha256(frozen.encode()).hexdigest(),
    'frozenIntentMatchesParentAudit': hashlib.sha256(frozen.encode()).hexdigest() == json.loads((LOOP10 / 'parent-audit.json').read_text())['frozenIntentSha256'],
    'agentsRegisterSha256': digest(HEX / 'agents/_bmad-output/planning-artifacts/external-dependency-register.md'),
    'privateOwnerOperationDeadlineUnchanged': subprocess.run(['git', '--no-optional-locks', '-C', str(EVENTSTORE), 'diff', '--quiet', 'HEAD', '--',
                                                              'src/Hexalith.EventStore.Server/Security/PrivateOwnerOperationDeadline.cs']).returncode == 0,
    'deferred': ['R7-BH-7 generic subprocess capture remains deferred and unchanged'],
    'externalRuntimeRoot': str(EXTERNAL),
}
(BUNDLE / 'verification.json').write_text(json.dumps(verification, indent=2) + '\n')

existing_after = tree(LOOP10, exclude=BUNDLE)
assert existing_after == existing_before, 'existing evidence bytes changed during packaging'
tracked = subprocess.run(['git', '--no-optional-locks', '-C', str(PLATFORM), 'diff', '--quiet', 'HEAD', '--', str(LOOP10.relative_to(PLATFORM))])
(BUNDLE / 'evidence-preservation.json').write_text(json.dumps({'existingFilesVerifiedUnchanged': len(existing_before),
                                                               'existingTrackedEvidenceMatchesHead': tracked.returncode == 0}, indent=2) + '\n')
print(json.dumps({'bundle': str(BUNDLE), 'verdicts': verification['verdictSummary'], 'productionFilesChanged': verification['productionFilesChanged'],
                  'phases': verification['phases'], 'frozen': verification['frozenIntentMatchesParentAudit'],
                  'deadlineUnchanged': verification['privateOwnerOperationDeadlineUnchanged']}, indent=1))
