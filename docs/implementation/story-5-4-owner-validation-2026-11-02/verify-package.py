"""Validate retained preparation evidence without executing a dependency seam."""
from pathlib import Path, PurePosixPath
from collections import Counter
import argparse
import hashlib
import json
import re
import xml.etree.ElementTree as ET


class VerificationError(ValueError):
    """A required package invariant failed."""


def require(condition, message):
    if not condition:
        raise VerificationError(message)


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def load(path):
    def unique_pairs(pairs):
        result = {}
        for key, value in pairs:
            require(key not in result, f'Duplicate JSON key {key} in {path}')
            result[key] = value
        return result
    return json.loads(path.read_text(), object_pairs_hook=unique_pairs)


def member_path(value):
    path = PurePosixPath(value)
    require(isinstance(value, str) and value and not path.is_absolute()
            and '..' not in path.parts and str(path) == value,
            f'Invalid artifact member: {value}')
    return value


def manifest_files(document, identical_source_aliases=False):
    entries = document['files']
    require(isinstance(entries, list) and entries, 'Empty or invalid file manifest')
    result = {}
    for item in entries:
        name = item['path']
        require(isinstance(name, str) and name, 'Empty manifest file')
        value = (item['sha256'], item['bytes'])
        require(name not in result or identical_source_aliases and result[name] == value,
                'Duplicate manifest file or conflicting source alias')
        require(re.fullmatch('[0-9a-f]{64}', item['sha256']) is not None, f'Invalid digest: {name}')
        require(type(item['bytes']) is int and item['bytes'] >= 0, f'Invalid size: {name}')
        result[name] = (item['sha256'], item['bytes'])
    require(document['fileCount'] == len(entries), 'Manifest count differs from complete file list')
    return result


def verify_xunit(bundle, xml_name, command, expected_methods):
    document = ET.parse(bundle / xml_name)
    root = document.getroot()
    require(root.tag == 'assemblies', f'Wrong xUnit root: {xml_name}')
    assemblies = root.findall('./assembly')
    require(len(assemblies) == len(root.findall('.//assembly')) == 1,
            f'Exactly one xUnit assembly required: {xml_name}')
    assembly = assemblies[0]
    require(not root.findall('.//error') and not root.findall('.//failure'),
            f'Error/failure evidence present: {xml_name}')
    expected_total = sum(expected_methods.values())
    for key, value in {'total': expected_total, 'passed': expected_total,
                       'failed': 0, 'errors': 0, 'skipped': 0, 'not-run': 0}.items():
        require(assembly.get(key) == str(value), f'Assembly {key} mismatch: {xml_name}')
    tests = assembly.findall('./collection/test')
    require(len(tests) == len(assembly.findall('.//test')) == expected_total,
            f'Incomplete or extra case list: {xml_name}')
    seen_ids = set()
    observed_methods = Counter()
    for case in tests:
        identity = case.get('id')
        require(identity and identity not in seen_ids, f'Duplicate/missing case id: {xml_name}')
        seen_ids.add(identity)
        kind, method = case.get('type'), case.get('method')
        require((kind, method) in expected_methods, f'Unexpected class/method: {kind}.{method}')
        require(case.get('name', '').startswith(kind + '.' + method), f'Case identity mismatch: {xml_name}')
        require(case.get('result') == 'Pass' and not list(case), f'Case did not pass cleanly: {xml_name}')
        require(case.get('start-rtf') and case.get('finish-rtf'), f'Case execution interval missing: {xml_name}')
        observed_methods[(kind, method)] += 1
    require(observed_methods == expected_methods, f'Per-method case counts differ: {xml_name}')
    for collection in assembly.findall('./collection'):
        count = len(collection.findall('./test'))
        for key, value in {'total': count, 'passed': count, 'failed': 0, 'skipped': 0, 'not-run': 0}.items():
            require(collection.get(key) == str(value), f'Collection {key} mismatch: {xml_name}')
    argv = command['argv']
    require(command['exitCode'] == 0 and argv[0] == 'dotnet', f'Unsuccessful test command: {xml_name}')
    require(assembly.get('name') == argv[1], f'Command/assembly mismatch: {xml_name}')
    selected = [argv[index + 1] for index, value in enumerate(argv[:-1]) if value == '-class']
    expected_classes = {kind for kind, _ in expected_methods}
    require(len(selected) == len(expected_classes) and set(selected) == expected_classes,
            f'Incomplete, duplicate or foreign class selection: {xml_name}')
    allowed = {'-class', '-xml', '-result-xml'}
    require(len(argv[2:]) % 2 == 0 and all(argv[index] in allowed for index in range(2, len(argv), 2)),
            f'Unexpected test selection/arguments: {xml_name}')
    outputs = [argv[index + 1] for index in range(2, len(argv), 2) if argv[index] in {'-xml', '-result-xml'}]
    require(len(outputs) == 1 and Path(outputs[0]).name == Path(xml_name).name,
            f'XML output selection mismatch: {xml_name}')
    return expected_total


def verify(bundle, root):
    for path in bundle.rglob('*.json'):
        load(path)
    inventory = load(bundle / 'artifact-inventory.json')
    require(inventory.get('excludedFiles') == ['artifact-inventory.json'], 'Only documented inventory self-exclusion is allowed')
    require(inventory.get('inventoryExcludesItself') is True, 'Inventory self-exclusion must be explicit')
    actual = set()
    for path in bundle.rglob('*'):
        require(not path.is_symlink(), f'Symbolic artifact member: {path}')
        if path.is_file():
            actual.add(str(path.relative_to(bundle)))
    actual.remove('artifact-inventory.json')
    listed = [member_path(item['path']) for item in inventory['files']]
    require(len(listed) == len(set(listed)) and set(listed) == actual,
            'Artifact inventory differs from exact on-disk membership')
    for item in inventory['files']:
        path = bundle / item['path']
        require(path.stat().st_size == item['bytes'] and sha(path) == item['sha256'], f'Changed artifact: {item["path"]}')
    sources = load(bundle / 'source-and-prior-evidence.json')
    for item in sources['files']:
        path = root / item['path']
        require(path.stat().st_size == item['bytes'] and sha(path) == item['sha256'], f'Changed source/evidence: {item["path"]}')
    for target in re.findall(r'\[[^\]]+\]\(([^)]+)\)', (bundle / 'README.md').read_text()):
        if '://' in target or target.startswith('#'):
            continue
        relative = target.split('#')[0]
        # Copies of the evidence bundle may still validate repository-relative documentation links.
        path = bundle / relative
        if not path.exists():
            path = root / 'docs/implementation/story-5-4-owner-validation-2026-11-02' / relative
        require(path.exists(), f'Missing local link: {target}')
    baseline = load(bundle / 'baseline.json')['relativeSha256']
    for name in ['../agents/_bmad-output/planning-artifacts/external-dependency-register.md',
                 '../agents/_bmad-output/implementation-artifacts/sprint-status.yaml']:
        require(name in baseline and sha(root / name) == baseline[name], f'Agents gate changed: {name}')
    require(sha(bundle / 'external-dependency-register.snapshot.md') == baseline['../agents/_bmad-output/planning-artifacts/external-dependency-register.md'],
            'Register snapshot does not preserve baseline bytes')
    before_raw = (bundle / 'owner-decisions.before.json').read_bytes()
    owner = root / 'docs/implementation/story-5-4-coupled-delivery-2026-10-25/owner-decisions.json'
    current, original = load(owner), json.loads(before_raw)
    nomination = current['mvpOperationalInputs']['independentDecisionAuthority'].pop('businessApproverNomination')
    require(current == original, 'Owner-form values changed outside nomination')
    lines = json.dumps(nomination, indent=2).splitlines()
    insertion = '      "businessApproverNomination": ' + lines[0] + '\n' + '\n'.join('      ' + line for line in lines[1:]) + '\n'
    needle = b'      "custodianMayMintApprovals": false\n'
    require(before_raw.count(needle) == 1, 'Nomination insertion anchor is ambiguous')
    expected = before_raw.replace(needle, b'      "custodianMayMintApprovals": false,\n' + insertion.encode())
    require(expected == owner.read_bytes(), 'Unrelated owner-form bytes changed')
    audit = load(bundle / 'owner-form-delta-audit.json')
    require(audit['beforeSha256'] == hashlib.sha256(before_raw).hexdigest() and audit['afterSha256'] == sha(owner), 'Owner-form delta audit hashes differ')
    authority = current['mvpOperationalInputs']['independentDecisionAuthority']
    require(authority['installedIssuerAndPublicTrustEvidenceReference'] is None
            and authority['currentRolesAndRevocationEvidenceReference'] is None
            and nomination['businessApprovalEvidenceReference'] is None
            and nomination['humanPartyBindingEvidenceReference'] is None
            and nomination['grantsAuthority'] is False, 'Nomination established unsupported authority')
    observation = nomination['readOnlyIdentityObservation']
    receipt = bundle / 'identity-evidence/known-subject-identity-observation.json'
    script = bundle / 'identity-evidence/capture-known-subject.py.txt'
    identity = load(receipt)
    require(sha(receipt) == observation['evidenceSha256'] and sha(script) == observation['captureScriptSha256']
            == identity['captureScriptSha256'], 'Scoped identity source/receipt hash mismatch')
    require(identity['outcome'] == 'ObservedIdentityCandidateOnly' and all(step['httpStatus'] == 200 for step in identity['steps']), 'Scoped identity stages not successful')
    methods = Counter({('Hexalith.Platform.Composition.Tests.ConversationsEnrollmentTests', name): count for name, count in {
        'RealDeclarationResolvesOnlyItsOrdinaryServerWithCompleteExplicitConfiguration': 1,
        'OrdinaryCompositionRefusesTheAbsentOwnerCatalogAdapterBeforeAddingResources': 1,
        'MissingOrNonSourceOwnerMappingRefusesBeforeComposition': 2,
        'MissingProjectBindingOrFileRefusesBeforeComposition': 1,
        'PhysicallyForeignProjectRefusesBeforeComposition': 2,
        'EveryMissingDeclaredSettingRefusesBeforeComposition': 28,
        'ForeignScopedConfigurationRefusesBeforeComposition': 1,
        'PackageImageAndConformanceReceiverRemainUnavailable': 1}.items()})
    commands = load(bundle / 'review-fix-verification/commands.json')
    command = next(item for item in commands if item['name'] == 'focused-tests')
    total = verify_xunit(bundle, 'review-fix-verification/conversations-tests.xml', command, methods)
    broad_methods = Counter({('Hexalith.Platform.Composition.Tests.' + kind, name): count for kind, name, count in [
        ('DomainEnrollmentBindingTests', 'StaticValidityRequiresExplicitProjectAndScopedConfigurationBeforeComposition', 1),
        ('PartiesAdoptionTests', 'ActualPartiesDeclarationValidatesAndSelectsEightMappedServers', 1),
        ('PartiesAdoptionTests', 'CompositionWiresSingularPartyGatewayAndDeclarativeTenantSubscription', 1),
        ('PartiesAdoptionTests', 'NativeSourceSuiteRequiresTheExactMappedRunnerSeed', 1),
        ('PartiesAdoptionTests', 'SignedPartiesRunUsesTheRunnerJwtSettingsAsync', 1),
        ('PartiesAdoptionTests', 'MissingOrOverriddenPartiesSourceRefusesBeforeComposition', 1),
        ('PartiesAdoptionTests', 'ResolvedStandaloneRootsBindTheirActualPartiesProjectAsync', 2),
        ('AcceptedLaunchBoundaryTests', 'ActualAcceptedAspireCompositionChecksCompleteImmutableSiblingBytesBeforeAddingAnyResource', 22)]})
    verify_xunit(bundle, 'enrollment-relocated-tests.xml', load(bundle / 'relocated-test-command.json'), broad_methods)
    runtime_names = ['original-runtime-before', 'relocated-runtime-before', 'relocated-runtime-after', 'original-runtime-after']
    runtimes = [manifest_files(load(bundle / f'review-fix-verification/{name}.json')) for name in runtime_names]
    require(all(value == runtimes[0] for value in runtimes[1:]), 'Original/relocated before/after runtime manifests differ')
    before = load(bundle / 'review-fix-verification/before-source-build-inputs.json')
    after = load(bundle / 'review-fix-verification/after-source-build-inputs.json')
    # The raw evaluated capture observes one identical source path through two aliases.
    # Retain both records and compare the entire raw lists; conflicting duplicates still fail.
    inputs = manifest_files(before, identical_source_aliases=True)
    require(before['files'] == after['files'] and inputs == manifest_files(after, identical_source_aliases=True), 'Build source/input closure changed during verification')
    changed_test = 'tests/Hexalith.Platform.Composition.Tests/ConversationsEnrollmentTests.cs'
    require(inputs[changed_test][0] == sha(root / changed_test), 'Fresh run does not bind final test source')
    build = next(item for item in commands if item['name'] == 'build')
    require(build['exitCode'] == 0 and before['completedAtUtc'] <= build['startedAtUtc']
            <= build['finishedAtUtc'] <= command['startedAtUtc'] <= command['finishedAtUtc'] <= after['capturedAtUtc'],
            'Source snapshots do not bracket final build and test')
    require('-p:UseHexalithProjectReferences=true' in build['argv'] and '--no-incremental' in build['argv'], 'Fresh source build selection missing')
    require(not any(arg.startswith(('-p:NoWarn', '-p:RunAnalyzers=false', '-p:NuGetAudit=false')) for arg in build['argv']), 'Warning/source verification bypass present')
    status = load(bundle / 'delivery-status.json')
    require(not status['replacement']['submitted'] and not status['replacement']['completePacketPrepared'], 'Incomplete replacement submitted')
    require(all(status['replacement'][key] is None for key in ['platformTarget', 'protectionTarget', 'integrationDate', 'installedProfileSha256', 'exactNewLiveCommand']), 'Invented replacement input')
    require(not any(status['preservedGates'][key] for key in ['qualificationAuthorized', 'attemptReserved', 'dependencyAvailable', 'consumerSeamsExecuted', 'businessApprovalsIssued']), 'Unavailable dependency gate advanced')
    return {'status': 'Passed', 'focusedPassed': total, 'retainedBroadPassed': 30,
            'freshRuntimeManifestFiles': len(runtimes[0]), 'bracketedSourceBuildInputFiles': len(inputs),
            'exactArtifactMembership': len(actual), 'nominationOnlyOwnerDelta': True,
            'historicalBroadRuntimeEquality': 'Recorder booleans only; distinct historical before/after manifests were not retained and are not re-derived.'}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--bundle', type=Path, default=Path(__file__).resolve().parent)
    parser.add_argument('--root', type=Path, default=Path(__file__).resolve().parents[3])
    args = parser.parse_args()
    try:
        print(json.dumps(verify(args.bundle.resolve(), args.root.resolve()), indent=2))
    except (VerificationError, OSError, ValueError, KeyError, TypeError, StopIteration, ET.ParseError) as error:
        parser.exit(1, f'Package verification failed: {error}\n')
