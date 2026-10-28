"""Audit fresh retained observations; optionally reobserve original runtime files."""
from pathlib import Path
import gzip
import hashlib
import json
import sys
import xml.etree.ElementTree as ET

out = Path(__file__).resolve().parent
live = out / 'live'
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
    command_records.append({'record': path.name, 'exitCode': record['exitCode'], 'logHashesVerified': True})
for name in ['setup-dapr', 'apphost-build', 'platform-build', 'live-qualification', 'host-before-final', 'host-after']:
    assert read(out / (name + '.command.json'))['exitCode'] == 0, name
assembly = ET.parse(out / 'platform-live-tests.xml').getroot().find('assembly')
assert assembly is not None and int(assembly.attrib['total']) == 1
assert int(assembly.attrib['passed']) == 1
for field in ['failed', 'errors', 'skipped', 'not-run']:
    assert int(assembly.attrib[field]) == 0
before = read(out / 'source-before.json')
after = read(out / 'source-after.json')
assert before == after and len(before) >= 234
prior = read(out / 'prior-offline-verification.json')
assert prior['sourceFilesVerified'] == 184 and prior['executedPassingCases'] == 950
assert prior['reusedSourceHashesMatched'] is True
schema_parity = read(out / 'schema-parity.json')
for record in schema_parity:
    assert record['platformSha256'] == record['buildsSha256'] == digest(out / 'schemas' / record['schema'])

scenarios = []
peer_baseline = None
run_ids = set()
for scenario, outcome, cause in [
    ('success', 'passed', 'tests-completed'),
    ('failure', 'failed', 'test-failure'),
    ('timeout', 'failed', 'execution-timeout'),
    ('partial-startup', 'failed', 'startup-failure'),
]:
    directory = live / 'diagnostics' / scenario
    result = read(directory / 'test-result.json')
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
    for path in [live / ('cleanup-' + scenario + '-idempotent.json'), *sorted(live.glob('cleanup-' + scenario + '-[12].json'))[-1:]]:
        cleanup = read(path)
        assert cleanup['Result']['Outcome']['ExitCode'] == 0, path.name
        empty(cleanup['Remaining'])
    for reference in result['diagnostics']:
        relative = Path(reference['reference'])
        assert not relative.is_absolute() and '..' not in relative.parts
        assert reference['classification'] == 'internal' and reference['redacted'] is True
        assert reference['contentIdentity'] == 'sha256:' + digest(directory / relative)
    peer = read(live / ('peer-audit-' + scenario + '.json'))
    assert peer['observedValue'] == 'unchanged'
    assert peer['ContainerIds'] and peer['stablePeerPids']
    assert set(peer['stablePeerPids']).issubset(peer['ProcessIds'])
    identity = (peer['RunId'], peer['ContainerIds'], peer['stablePeerPids'])
    if peer_baseline is None:
        peer_baseline = identity
    assert identity == peer_baseline, scenario
    run_ids.add(peer['RunId'])
    if runtime_checks:
        original = Path(command['testResultPath']).parent
        for path in directory.iterdir():
            assert path.read_bytes() == (original / path.name).read_bytes(), str(path)
        state = original.parents[1]
        assert not list(state.glob('*.json'))
        assert not list((state / 'claims').glob('*.json'))
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
summary = {
    'freshLiveSourceQualification': 'passed',
    'liveTest': assembly.attrib, 'scenarios': scenarios,
    'sourceFilesUnchanged': len(before), 'priorOfflineCasesReused': 950,
    'priorOfflineCasesRerun': False, 'schemaByteParityVerified': True,
    'exactOwnerCleanupVerified': True, 'independentPeerIsolationVerified': True,
    'preexistingContainersStillPresent': len(host['preexistingContainersStillPresent']),
    'ambientContainerChangesAttribution': host['ambientContainerChangesAttribution'],
    'runtimeArchiveBytesReobserved': runtime_checks,
    'logsVerified': command_records,
    'packageQualification': False, 'windowsMacOSNativeQualification': False,
}
(out / ('runtime-audit.json' if runtime_checks else 'retained-audit.json')).write_text(json.dumps(summary, indent=2) + '\n')
print(json.dumps({'freshLiveSourceQualification': 'passed', 'scenariosAudited': len(scenarios), 'ownersCleaned': len(run_ids), 'sourceFilesUnchanged': len(before), 'runtimeArchiveBytesReobserved': runtime_checks}))
