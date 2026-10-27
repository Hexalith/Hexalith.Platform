"""Bind the W1-W9 corrections to unchanged source and passing owning-project evidence."""
from pathlib import Path
import hashlib
import json
import xml.etree.ElementTree as ET

out = Path(__file__).resolve().parent
root = out.parents[4]
snapshot = json.loads((out / 'implementation-source-snapshot.json').read_text())
for name, expected in snapshot['sha256'].items():
    assert hashlib.sha256((root / name).read_bytes()).hexdigest() == expected, name
lanes = {}
tests = []
for name in ['module-focused', 'module-tests', 'evidence-tests']:
    report = ET.parse(out / (name + '.xml')).getroot()
    assembly = report.find('assembly')
    assert assembly is not None, name
    attributes = assembly.attrib
    assert int(attributes['total']) > 0, name
    assert all(attributes[field] == '0' for field in ['errors', 'failed', 'skipped', 'not-run']), attributes
    assert attributes['passed'] == attributes['total'], attributes
    lanes[name] = attributes
    if name != 'module-focused':
        tests.extend(report.findall('.//test'))
for name in ['module-build-final', 'evidence-build']:
    command = json.loads((out / (name + '.command.json')).read_text())
    assert command['exitCode'] == 0, command
    log = (out / command['log']).read_text()
    assert '0 Warning(s)' in log and '0 Error(s)' in log, name


def passing(method):
    matches = [test.attrib['name'] for test in tests if method in test.attrib['name'] and test.attrib['result'] == 'Pass']
    assert matches, method
    return matches


findings = {
    'W1': passing('SupportedTerminalMetadataRestoresAndListsAsync'),
    'W2': passing('UnverifiableObservationNamesOwnerAsync'),
    'W3': passing('SessionlessStartupRejectsMalformedCommittedOutcomeAsync'),
    'W4': [name for method in ['NullCommittedResourceEntryReturnsStructuredUncertaintyAsync', 'NullTrackedReadinessReturnsStructuredUncertaintyAsync', 'DiscoveryFailuresPreventCompleteCleanupAsync', 'MalformedTerminalMetadataRefusesRestorationAsync'] for name in passing(method)],
    'W5': passing('UnavailableResourcesRequireUnavailableReadiness'),
    'W6': ['README.md explicitly states that --evidence requires --manifest.'],
    'W7': passing('CompletedSelectedEvidenceRequiresVerifiedCleanupAsync'),
    'W8': passing('NativeOutcomeControlsLocalCleanupAsync'),
    'W9': passing('ExhaustedExecutionLifetimeBeforeNativeReturnRecordsTimeoutCauseAsync'),
}
builds = root / 'references/Hexalith.Builds'
service = (builds / 'src/libraries/Hexalith.Builds.Tooling/Runtime/ModuleCommandExecutionService.cs').read_text()
start = service.index('bool ownerCancelled = cancellationToken.IsCancellationRequested;')
end = service.index('catch (Exception exception)', start)
assert service[start:end].count('cancellationToken.IsCancellationRequested') == 1
assert 'fallbackTerminalCause = ownerCancelled' in service[start:end]
assert 'result = ownerCancelled' in service[start:end]
assert '`--evidence` requires an explicit `--manifest`.' in (builds / 'README.md').read_text()
contract = (root / 'docs/contracts/test-environment-v1.md').read_text()
assert '`environment.readiness` must also be `not-yet-populated`' in contract
assert 'including an empty readiness array' in contract
summary = {
    'lanes': lanes,
    'builds': 'Module.Tests and Evidence.Tests Debug builds: zero warnings and errors',
    'verifiedChangedSourceFiles': len(snapshot['sha256']),
    'findings': findings,
    'W9SourceAudit': 'One owner-cancellation snapshot selects both fallback cause and command result.',
    'freshLiveQualification': False,
    'packageRuntimeQualification': False,
    'nativeWindowsMacOSQualification': False,
    'carriedDeferrals': ['W10', 'D1', 'D2', 'D3', 'CB3', 'CE1', 'Q3'],
}
(out / 'implementation-verification.json').write_text(json.dumps(summary, indent=2) + '\n')
print(json.dumps({name: lane['passed'] for name, lane in lanes.items()}))
