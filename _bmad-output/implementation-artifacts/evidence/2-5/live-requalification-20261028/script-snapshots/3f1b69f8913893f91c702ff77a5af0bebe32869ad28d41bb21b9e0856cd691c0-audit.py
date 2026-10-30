"""Audit fresh retained observations; optionally reobserve original runtime files."""
from pathlib import Path
import gzip
import hashlib
import json
import sys
import xml.etree.ElementTree as ET

out = Path(__file__).resolve().parent
live = out / 'live-final'
runtime_checks = '--check-runtime' in sys.argv

def read(path):
    return json.loads(path.read_text())

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def empty(resources):
    assert resources['IsEmpty'] is True and resources['Unverified'] is False
    for field in ['ContainerIds', 'ProcessIds', 'WorkspaceIds', 'ClaimIds', 'DiscoveryFailures', 'ResourceIds']:
        assert resources[field] == [], (field, resources[field])

command_records = []
for path in sorted(out.glob('*.command.json')):
    record = read(path)
    log = out / record['log']
    archive = out / record['archivedLog']
    assert digest(log) == record['logSha256'], path.name
    assert digest(archive) == record['archiveSha256'], path.name
    assert gzip.decompress(archive.read_bytes()) == log.read_bytes(), path.name
    for script in record.get('scriptSnapshots', []):
        assert digest(out / script['snapshot']) == script['sha256'], script['snapshot']
    command_records.append({'record': path.name, 'exitCode': record['exitCode'], 'logHashesVerified': True})
for name in ['setup-dapr', 'apphost-build', 'platform-build-review-final', 'cleanup-helper-review-final', 'live-qualification-final', 'host-before-review-final', 'host-after-review-final']:
    assert read(out / (name + '.command.json'))['exitCode'] == 0, name
assembly = ET.parse(out / 'platform-live-tests-final.xml').getroot().find('assembly')
assert assembly is not None and int(assembly.attrib['total']) == 1
assert int(assembly.attrib['passed']) == 1
for field in ['failed', 'errors', 'skipped', 'not-run']:
    assert int(assembly.attrib[field]) == 0
helper = ET.parse(out / 'cleanup-helper-tests-final.xml').getroot().find('assembly')
assert helper is not None and int(helper.attrib['total']) == int(helper.attrib['passed']) == 1
for field in ['failed', 'errors', 'skipped', 'not-run']:
    assert int(helper.attrib[field]) == 0
before = read(out / 'source-before-final.json')
after = read(out / 'source-after-final.json')
assert before == after and len(before) >= 261
prior = read(out / 'prior-offline-verification.json')
assert prior['sourceFilesVerified'] == 184 and prior['executedPassingCases'] == 950
assert prior['reusedSourceHashesMatched'] is True
prior_hashes = read(out / 'prior-source-fingerprints.json')
assert len(prior_hashes) == 184
assert all(before[path] == identity for path, identity in prior_hashes.items())
schema_parity = read(out / 'schema-parity.json')
for record in schema_parity:
    assert record['platformSha256'] == record['buildsSha256'] == digest(out / 'schemas' / record['schema'])

scenarios = []
baseline = read(live / 'peer-before.json')
assert baseline['observedValue'] == 'unchanged'
assert baseline['ContainerIds'] and baseline['stablePeerPids']
assert set(baseline['stablePeerPids']).issubset(baseline['ProcessIds'])
peer_baseline = (baseline['RunId'], baseline['ContainerIds'], baseline['stablePeerPids'])
archive_manifest = read(out / 'final-runtime-archive.json')
attempts_by_scenario = {}
run_ids = set()
for scenario, outcome, cause in [
    ('success', 'passed', 'tests-completed'),
    ('failure', 'failed', 'test-failure'),
    ('timeout', 'failed', 'execution-timeout'),
    ('partial-startup', 'failed', 'startup-failure'),
]:
    directory = live / 'diagnostics' / scenario
    result = read(directory / 'test-result.json')
    initial_directory = live / 'initial-diagnostics' / scenario
    initial = read(initial_directory / 'test-result.json')
    assert initial['terminal'] == result['terminal'], scenario
    assert initial['retention']['failureRunIds'] == result['retention']['failureRunIds'], scenario
    if scenario != 'success':
        assert initial['retention']['disposition'] == 'failure-retained'
    assert result['schema'] == 'hexalith.test-result.v1'
    assert result['policyId'] == 'hexalith.platform.candidate-and-attachment-policy.v1'
    assert result['terminal']['outcome'] == outcome and result['terminal']['cause'] == cause
    assert result['bindings']['environment']['mode'] == 'source'
    assert result['cleanup']['disposition'] == 'complete'
    assert result['cleanup']['leftoverResourceIds'] == []
    owner = result['bindings']['run']['runId']
    run_ids.add(owner)
    command = read(live / ('command-' + scenario + '.json'))
    assert command['runId'] == owner
    observed = read(live / ('resources-' + scenario + '.json'))
    assert observed['Unverified'] is False
    if scenario == 'success':
        empty(observed)
    else:
        assert observed['ContainerIds'] and observed['ProcessIds'] and observed['IsEmpty'] is False
        listing = (live / ('list-' + scenario + '.json')).read_text()
        assert owner in listing and 'failure-retained' in listing
    attempts = sorted(live.glob('cleanup-' + scenario + '-[12].json'))
    assert attempts and [int(path.stem.rsplit('-', 1)[1]) for path in attempts] == list(range(1, len(attempts) + 1))
    attempted = []
    for index, path in enumerate(attempts):
        receipt = read(path)
        exit_code = receipt['Result']['Outcome']['ExitCode']
        rule = receipt['Result']['Outcome'].get('RuleId')
        attempted.append({'attempt': index + 1, 'exitCode': exit_code, 'ruleId': rule})
        if index + 1 < len(attempts):
            assert exit_code != 0 or receipt['Remaining']['IsEmpty'] is False
            if rule == 'HXR028':
                empty(receipt['Remaining'])
    attempts_by_scenario[scenario] = attempted
    for path in [live / ('cleanup-' + scenario + '-idempotent.json'), attempts[-1]]:
        cleanup = read(path)
        assert cleanup['Result']['Outcome']['ExitCode'] == 0, path.name
        empty(cleanup['Remaining'])
    for document, location in [(initial, initial_directory), (result, directory)]:
        for reference in document['diagnostics']:
            relative = Path(reference['reference'])
            assert not relative.is_absolute() and '..' not in relative.parts
            assert reference['classification'] == 'internal' and reference['redacted'] is True
            assert reference['contentIdentity'] == 'sha256:' + digest(location / relative)
    peer = read(live / ('peer-audit-' + scenario + '.json'))
    assert peer['observedValue'] == 'unchanged'
    assert peer['ContainerIds'] and peer['stablePeerPids']
    assert set(peer['stablePeerPids']).issubset(peer['ProcessIds'])
    identity = (peer['RunId'], peer['ContainerIds'], peer['stablePeerPids'])
    assert identity == peer_baseline, scenario
    run_ids.add(peer['RunId'])
    final_directory = live / 'final-diagnostics' / scenario
    final_result = read(final_directory / 'test-result.json')
    def immutable(document):
        result = {key: value for key, value in document.items() if key not in ['cleanup', 'retention', 'diagnostics']}
        mutable_retention = {'disposition', 'retainedResourceIds', 'reason', 'recordedAt'}
        mutable_cleanup = {'state', 'disposition', 'observedAt', 'ownedResourceIds', 'leftoverResourceIds', 'failures', 'diagnosticIds', 'attemptId'}
        result['retention'] = {key: value for key, value in document['retention'].items() if key not in mutable_retention}
        result['cleanup'] = {key: value for key, value in document['cleanup'].items() if key not in mutable_cleanup}
        return result
    assert immutable(initial) == immutable(result), scenario
    assert immutable(final_result) == immutable(result), scenario
    inventory = archive_manifest[scenario]['filesSha256']
    assert set(inventory) == {path.name for path in final_directory.iterdir() if path.is_file()}
    for name, identity in inventory.items():
        assert digest(final_directory / name) == identity, (scenario, name)
    assert final_result['cleanup']['disposition'] == 'complete' and not final_result['cleanup']['leftoverResourceIds']
    for reference in final_result['diagnostics']:
        relative = Path(reference['reference'])
        assert not relative.is_absolute() and '..' not in relative.parts
        assert reference['classification'] == 'internal' and reference['redacted'] is True
        assert reference['contentIdentity'] == 'sha256:' + digest(final_directory / relative)
    if runtime_checks:
        original = Path(command['testResultPath']).parent
        assert set(inventory) == {path.name for path in original.iterdir() if path.is_file() and path.suffix in ['.json', '.trx']}
        for path in final_directory.iterdir():
            assert path.read_bytes() == (original / path.name).read_bytes(), str(path)
        state = original.parents[1]
        assert not list(state.glob('*.json'))
        assert not list((state / 'claims').glob('*.json'))
        for path in (state / 'claims' / 'owners').glob('*.json'):
            assert read(path) == [], path.name
        assert not list((state.parent / 'runs').glob('*'))
    scenarios.append({'scenario': scenario, 'runId': owner, 'outcome': outcome, 'cause': cause, 'cleanupComplete': True, 'diagnosticHashesVerified': len(result['diagnostics']), 'peerContainersAndStableProcessesPreserved': True, 'peerRedisValue': peer['observedValue']})
cleanup = read(live / 'cleanup-audit.json')
assert cleanup['completed'] is True and not cleanup['claims'] and not cleanup['runs'] and not cleanup['cleanupFailures']
final_receipts = list(live.glob('final-cleanup-*.json'))
assert final_receipts
final_by_owner = {}
for path in sorted(final_receipts):
    final_by_owner[path.stem.removeprefix('final-cleanup-').rsplit('-', 1)[0]] = path
assert set(final_by_owner) == run_ids
for path in final_by_owner.values():
    receipt = read(path)
    assert receipt['Result']['Outcome']['ExitCode'] == 0
    empty(receipt['Remaining'])
host = read(out / 'host-after.json')
assert set(host['qualificationOwners']) == run_ids and len(run_ids) == 5
assert host['ownedResourcesAbsent'] is True
assert not host['newUninspectableProcesses']
for observation in host['qualificationOwners'].values():
    assert not observation['containerIds'] and not observation['processIds']
receipts = read(out / 'claim-receipts.json')
assert set(receipts) == run_ids
for owner, record in receipts.items():
    assert record['identities'] == []
    assert digest(out / record['snapshot']) == record['sha256']
    assert read(out / record['snapshot']) == []
if (out / 'verification.json').exists():
    verification = read(out / 'verification.json')
    if verification.get('liveEvidenceDirectory') == live.name:
        assert verification['cleanupAttempts'] == attempts_by_scenario
summary = {
    'freshLiveSourceQualification': 'passed',
    'liveTest': assembly.attrib, 'scenarios': scenarios,
    'sourceFilesUnchanged': len(before), 'priorOfflineCasesReused': 950,
    'priorOfflineCasesRerun': False, 'schemaByteParityVerified': True,
    'exactOwnerCleanupVerified': True, 'independentPeerIsolationVerified': True,
    'preexistingContainersStillPresent': len(host['preexistingContainersStillPresent']),
    'ambientContainerChangesAttribution': host['ambientContainerChangesAttribution'],
    'runtimeArchiveBytesReobserved': runtime_checks,
    'cleanupAttempts': attempts_by_scenario,
    'peerBaselineRecordedBeforeScenarios': True,
    'initialFailureRetentionAndTerminalHistoryVerified': True,
    'archiveCompleteInventoryVerified': True,
    'durableOwnerReceiptsVerifiedEmpty': len(receipts),
    'logsVerified': command_records,
    'packageQualification': False, 'windowsMacOSNativeQualification': False,
}
(out / ('runtime-audit.json' if runtime_checks else 'retained-audit.json')).write_text(json.dumps(summary, indent=2) + '\n')
print(json.dumps({'freshLiveSourceQualification': 'passed', 'scenariosAudited': len(scenarios), 'ownersCleaned': len(run_ids), 'sourceFilesUnchanged': len(before), 'runtimeArchiveBytesReobserved': runtime_checks}))
