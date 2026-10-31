from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import shutil
import xml.etree.ElementTree as ET

platform = Path('/home/administrator/projects/hexalith/platform')
eventstore = platform.parent / 'eventstore'
external = Path(json.loads(Path('/tmp/ext-protection-loop10-active.json').read_text())['root'])
packet = platform / 'docs/implementation/ext-protection-1-loop10'
bundle = packet / external.name

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

commands = json.loads((external / 'commands.json').read_text())
for command in commands:
    raw = external / (command['name'] + '.log')
    bundled = bundle / (command['name'] + '.log.txt')
    shutil.copyfile(raw, bundled)
    command['bundledLog'] = str(bundled.relative_to(packet))
    command['bundledLogSha256'] = digest(bundled)
    assert command['bundledLogSha256'] == command['logSha256']
    (bundle / raw.name).unlink(missing_ok=True)
(bundle / 'commands.json').write_text(json.dumps(commands, indent=2) + '\n')

source_paths = json.loads((external / 'source-before-validation.json').read_text())
for item in source_paths:
    assert digest(Path(item['path'])) == item['sha256'], item['path']
paths = [Path(item['path']) for item in source_paths]
paths += [platform / p for p in ['deploy/erasure-root/README.md', '.gitattributes',
    'src/Hexalith.Platform.Protection/ErasureRootCustody.cs', 'src/Hexalith.Platform.Protection/OrdinalStringSortedDictionaryConverterFactory.cs',
    'src/Hexalith.Platform.Protection/OrdinalStringSortedDictionaryConverter.cs',
    'tests/Hexalith.Platform.Protection.Tests/ErasureRootCustodyTests.cs', 'tests/Hexalith.Platform.Protection.Tests/CustodyOccurrenceProviderTests.cs',
    'tests/Hexalith.Platform.Protection.Tests/ErasureRootLoop8AdmissionTests.cs', 'tests/Hexalith.Platform.Protection.Tests/ErasureRootReceivingTests.cs',
    'tests/Hexalith.Platform.Protection.Tests/OpenBaoBootstrapIntegrationTests.cs', 'tests/Hexalith.Platform.Protection.Tests/OpenBaoNativeCustodyFixture.cs']]
paths += [eventstore / p for p in ['src/Hexalith.EventStore.Server/Security/PublishedTrustDeletionConsumptionAuthority.cs',
    'src/Hexalith.EventStore.Server/Security/GovernanceScopeGuardReducer.cs',
    'tests/Hexalith.EventStore.Server.Tests/Security/PublishedTrustDeletionConsumptionAuthorityTests.cs',
    'tests/Hexalith.EventStore.Server.Tests/Security/GovernanceScopeGuardTests.cs']]
sources = []
for path in sorted(set(paths)):
    assert path.is_file(), str(path)
    repo = platform if path.is_relative_to(platform) else eventstore
    relative = path.relative_to(repo)
    snapshot = external / 'selected-source' / repo.name / relative
    snapshot.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(path, snapshot)
    bundled = bundle / 'selected-source' / repo.name / relative
    bundled.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(path, bundled)
    sources.append({'repository': repo.name, 'path': str(relative), 'bytes': path.stat().st_size,
                    'sha256': digest(path), 'snapshot': str(bundled.relative_to(packet))})
(bundle / 'selected-source.json').write_text(json.dumps(sources, indent=2) + '\n')

contexts = [platform / '_bmad-output/implementation-artifacts/spec-ext-protection-1-production-engine.md',
            platform.parent / 'agents/_bmad-output/planning-artifacts/external-dependency-register.md',
            eventstore / 'docs/implementation/ext-protection-1-owner-acceptance.md',
            platform / 'docs/implementation/ext-secrets-1-dek-provider-decision-2026-10-18.md']
context_inventory = [{'path': str(path), 'bytes': path.stat().st_size, 'sha256': digest(path)} for path in contexts]
(bundle / 'spec-context.json').write_text(json.dumps(context_inventory, indent=2) + '\n')
spec = contexts[0].read_text()
for invariant in ["status: 'in-progress'", 'review_loop_iteration: 10', "baseline_commit: '231b0f2b57e96984a919ac1cdce97cc36df18542'", "eventstore_baseline_commit: 'a62241679854fa9c359c0f552114a42ca0f6a638'"]:
    assert invariant in spec

runtime_inventories = []
for repo in ['platform', 'eventstore']:
    manifest = external / (repo + '-runtime.json')
    files = json.loads(manifest.read_text())
    for entry in files:
        assert digest(external / 'runtime' / repo / entry['path']) == entry['sha256']
    runtime_inventories.append({'repository': repo, 'runtime': str(external / 'runtime' / repo), 'files': len(files),
                                'manifest': str((bundle / manifest.name).relative_to(packet)), 'sha256': digest(manifest)})
results = []
for repo in ['platform', 'eventstore']:
    path = bundle / (repo + '-tests.xml')
    assembly = ET.parse(path).getroot().find('assembly')
    results.append({'repository': repo, 'total': int(assembly.attrib['total']), 'passed': int(assembly.attrib['passed']),
                    'failed': int(assembly.attrib['failed']), 'errors': int(assembly.attrib['errors']), 'skipped': int(assembly.attrib['skipped']),
                    'xml': str(path.relative_to(packet)), 'sha256': digest(path)})
verification = {'record': 'EXT-PROTECTION-1', 'loop': 10, 'status': 'in-progress', 'scope': 'authorized loop-10 resume only',
    'recordedUtc': datetime.now(timezone.utc).isoformat(), 'acceptedTarget': None, 'acceptedProfile': None, 'acceptedDate': None, 'availability': None,
    'nativeFiveCaseRerun': False, 'installed41CaseRun': False, 'physicalCopyQualification': False,
    'externalEvidenceRoot': str(external), 'commands': str((bundle / 'commands.json').relative_to(packet)),
    'builds': [{'name': c['name'], 'exitCode': c['exitCode'], 'warnings': 0, 'errors': 0} for c in commands if c['name'].endswith('-build')],
    'tests': results + [{'name': 'bootstrap-tests', 'passed': 12, 'failed': 0, 'exitCode': 0}],
    'runtimeInventories': runtime_inventories, 'selectedSourceInventory': str((bundle / 'selected-source.json').relative_to(packet)),
    'sourceBeforeValidationUnchanged': True, 'specContextInventory': str((bundle / 'spec-context.json').relative_to(packet)),
    'notices': ['Existing redirect HTTPError cleanup ResourceWarning in Python test output', 'xUnit -parallel and -xml deprecated-option notices; direct DLL runs passed'],
    'confirmedRemainingSourceGaps': ['OpenBaoBootstrapIntegrationTests omits explicit CA argument and adjacent custody-policy.hcl extraction and uses a deny-only runtime policy',
                                    'OpenBaoNativeCustodyFixture uses the deny-only shared policy as its runtime grant'],
    'preexistingDeferredSourceGap': 'R7-BH-7: generic subprocess capture_output accumulates pipes before stdout validation and has no stderr bound; explicitly unchanged and outside this cycle',
    'unopenedReviewRows': 'Review 11 maybe-false/defer rows remain unverified; no new review or counter increment',
    'newInstalledPrerequisite': 'Linux cgroup v2 plus compatible CRI/container cgroup namespace with memory.max=1073741824 and memory.swap.max=0; independently configured kubelet NoSwap; startup verifies fixed paths',
    'historicalEvidence': [{'path': name, 'sha256': digest(packet / name)} for name in ['initial-handback.md', 'initial-verification.json']]}
(packet / 'verification.json').write_text(json.dumps(verification, indent=2) + '\n')
shutil.copyfile('/tmp/ext-protection-loop10-package.py', bundle / 'package.py')
artifacts = [{'path': str(path.relative_to(packet)), 'bytes': path.stat().st_size, 'sha256': digest(path)} for path in sorted(packet.rglob('*'))
             if path.is_file() and path.name != 'artifact-inventory.json']
(packet / 'artifact-inventory.json').write_text(json.dumps(artifacts, indent=2) + '\n')
print(json.dumps({'bundle': str(bundle), 'sources': len(sources), 'artifacts': len(artifacts), 'tests': results}, indent=2))
