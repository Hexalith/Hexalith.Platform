"""Read-only audit of the mandatory loop-10, Review 11 and source-gap corrections in the current working trees.

Usage: python3 -I audit.py <output.json>
Exit 0 only when every correction and its regression coverage is present.
"""
import hashlib
import json
import re
import sys
from datetime import datetime, timezone
from pathlib import Path

HEX = Path('/home/administrator/projects/hexalith')
PLATFORM = HEX / 'platform'
EVENTSTORE = HEX / 'eventstore'
LOOP10 = PLATFORM / 'docs/implementation/ext-protection-1-loop10'

F = {
    'bootstrap': PLATFORM / 'deploy/erasure-root/bootstrap.py',
    'bootstrap_tests': PLATFORM / 'eng/ext-protection-1/test_bootstrap.py',
    'statefulset': PLATFORM / 'deploy/erasure-root/statefulset.yaml',
    'kubelet': PLATFORM / 'deploy/erasure-root/kubelet-noswap.fragment.yaml',
    'store': PLATFORM / 'src/Hexalith.Platform.Protection/OpenBaoErasureRootStore.cs',
    'store_tests': PLATFORM / 'tests/Hexalith.Platform.Protection.Tests/OpenBaoErasureRootStoreTests.cs',
    'unissued_tests': PLATFORM / 'tests/Hexalith.Platform.Protection.Tests/ErasureRootUnissuedReplacementTests.cs',
    'registry': EVENTSTORE / 'src/Hexalith.EventStore.Server/Security/InteractionOccurrenceRegistryActor.cs',
    'registry_tests': EVENTSTORE / 'tests/Hexalith.EventStore.Server.Tests/Security/InteractionOccurrenceRegistryActorTests.cs',
    'replacement_tests': EVENTSTORE / 'tests/Hexalith.EventStore.Server.Tests/Security/PublishedTrustReplacementAuthorityTests.cs',
    'owner_tests': EVENTSTORE / 'tests/Hexalith.EventStore.Server.Tests/Security/PublishedTrustOwnerStateAuthorityTests.cs',
    'reducer': EVENTSTORE / 'src/Hexalith.EventStore.Server/Security/GovernanceScopeGuardReducer.cs',
}
TEXT = {name: path.read_text() for name, path in F.items()}


def block(name, start, end_markers):
    """Return the text from start up to the first following end marker (method-scoped evidence)."""
    text = TEXT[name]
    index = text.index(start)
    stop = min((text.find(marker, index + len(start)) for marker in end_markers if text.find(marker, index + len(start)) > 0), default=len(text))
    return text[index:stop]


def python_test(name, test):
    """Return the body of one unittest method."""
    text = TEXT[name]
    index = text.index('    def ' + test + '(')
    following = re.search(r'\n    def |\nclass ', text[index + 1:])
    return text[index:index + 1 + following.start()] if following else text[index:]


def csharp_test(name, test):
    text = TEXT[name]
    index = text.index(' ' + test + '(')
    following = re.search(r'\n    \[(?:Fact|Theory)', text[index:])
    return text[index:index + following.start()] if following else text[index:]


results = []


def check(correction, description, evidence):
    """evidence: list of (label, text, required substrings or regexes prefixed by 're:')."""
    findings = []
    passed = True
    for label, text, requirements in evidence:
        for requirement in requirements:
            if requirement.startswith('re:'):
                ok = re.search(requirement[3:], text, re.S) is not None
            else:
                ok = requirement in text
            findings.append({'location': label, 'requires': requirement, 'present': ok})
            passed &= ok
    results.append({'correction': correction, 'description': description, 'present': passed, 'findings': findings})


decrypt = block('store', 'public async Task<byte[]> DecryptAsync', ['/// <inheritdoc />'])
trim = block('store', 'public async Task TrimAsync', ['/// <inheritdoc />'])
read_key = block('store', 'private async Task<JsonDocument> ReadKeyAsync', ['private string Key('])
apply_test = python_test('bootstrap_tests', 'test_apply_installs_non_deletable_keys_policies_and_reads_heads_back')
policy_test = python_test('bootstrap_tests', 'test_rendered_policy_is_one_tenant_and_manifests_pin_the_client_boundary')
render = block('bootstrap', 'def render_policy', ['def policy_text'])
config = block('bootstrap', 'def ensure_config', ["step = 'health'"])
unissued = csharp_test('replacement_tests', 'UnissuedProofAcceptsMatchingGuardAndRejectsMismatchedGuard')

check('R10-BH-1/R10-EC-7', 'apply resumes exact matches, refuses mismatches before change, and names the stopped step; default no-apply is zero calls', [
    ('deploy/erasure-root/bootstrap.py', TEXT['bootstrap'], [
        'class BootstrapStop(Exception)', "'step': step", "print(json.dumps({'status': 'PreparedOnly', 'tenants': len(value['nativeKeys']), 'effects': 0}))",
        "re:if not args\\.apply:.*?return 0.*?step = 'credential'", "step = 'transit-key:' + key", "step = 'policy:' + name", "step = 'head:' + tenant",
        "elif any(data.get(field) != key_body[field] for field in key_body):\n                raise BootstrapStop(step)",
        "elif policy_text(existing) != text:\n                raise BootstrapStop(step)", "elif current != head:\n                raise BootstrapStop(step)",
        "if not same_mount(existing, expected_type, options):\n                raise BootstrapStop(step)"]),
    ('eng/ext-protection-1/test_bootstrap.py::test_apply_installs_non_deletable_keys_policies_and_reads_heads_back', apply_test, [
        "for stage in ('mount', 'key', 'policy', 'head'):", "'stop-after-' + stage", 'committed_paths.isdisjoint(rewritten)',
        "self.assertEqual([method for method, _, _ in Native.records if method == 'POST'], [])", "run(True, 'mount-mismatch')", "run(True, 'head-mismatch')",
        "run(True, 'policy-mismatch')"]),
    ('eng/ext-protection-1/test_bootstrap.py::test_actual_cli_defaults_to_prepared_only_with_available_credential_and_zero_native_requests',
     python_test('bootstrap_tests', 'test_actual_cli_defaults_to_prepared_only_with_available_credential_and_zero_native_requests'), [
        "{'status': 'PreparedOnly', 'tenants': 2, 'effects': 0}", 'self.assertEqual(requests, [])']),
])

check('R10-BH-2', 'bootstrap verifies the ERC with the explicit pinned CA before any bearer; a different CA refuses before the token is written', [
    ('deploy/erasure-root/bootstrap.py', TEXT['bootstrap'], [
        'context.verify_mode = ssl.CERT_REQUIRED', 'context.check_hostname = True', 'context.load_verify_locations(cafile=str(ca_file))',
        "re:step = 'ca-pin'.*?raise BootstrapStop\\('ca-pin'\\).*?opener = pinned_opener\\(args\\.ca_certificate\\).*?step = 'health'",
        'urllib.request.HTTPSHandler(context=context), NoRedirect()', 'return None']),
    ('eng/ext-protection-1/test_bootstrap.py::test_apply_installs_non_deletable_keys_policies_and_reads_heads_back', apply_test, [
        "unpinned = run(True, 'ok', pin=False)", "self.assertEqual(json.loads(unpinned.stdout)['step'], 'ca-pin')",
        "rejected = run(True, 'ok', ca=other)", 'self.assertEqual(Native.tokens, [])', "SSL_CERT_FILE=str(other)"]),
    ('src/Hexalith.Platform.Protection/OpenBaoErasureRootStore.cs (production pin)', TEXT['store'], [
        'chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;', 'chain.ChainPolicy.CustomTrustStore.Add(pinnedCa);',
        'Dedicated erasure-root TLS CA pin required.']),
    ('tests/Hexalith.Platform.Protection.Tests/OpenBaoErasureRootStoreTests.cs', TEXT['store_tests'], [
        'public void MissingCaPinRefusesBeforeASocket()', 'public async Task MatchingPrivateCaReceivesTheTokenAndADifferentCaDoesNot()']),
])

check('R10-BH-3 + source gap 2 (policy literals)', 'per-tenant key config allow-lists are exactly the quoted strings "1" through "64" for both minimum versions', [
    ('deploy/erasure-root/bootstrap.py::render_policy', render, [
        "versions = ', '.join(json.dumps(str(version)) for version in range(1, 65))",
        '\'  allowed_parameters = {\\n    "min_decryption_version" = [\' + versions + \']\\n\'',
        '\'    "min_encryption_version" = [\' + versions + \']\\n  }\\n}\\n\'']),
    ('eng/ext-protection-1/test_bootstrap.py::test_rendered_policy_is_one_tenant_and_manifests_pin_the_client_boundary', policy_test, [
        'self.assertEqual(json.loads(versions[1]), [str(version) for version in range(1, 65)])', "self.assertNotIn('= []', text)",
        "self.assertNotIn(', \"65\"]', text)"]),
])

check('Source gap 2 (trim request values)', 'TrimAsync sends invariant string minimum versions matching the quoted ACL literals; trim body keeps its numeric form', [
    ('src/Hexalith.Platform.Protection/OpenBaoErasureRootStore.cs::TrimAsync', trim, [
        'string version = epoch.ToString(CultureInfo.InvariantCulture);',
        'JsonSerializer.SerializeToUtf8Bytes(new { min_decryption_version = version, min_encryption_version = version });',
        'JsonSerializer.SerializeToUtf8Bytes(new { min_available_version = epoch });']),
    ('tests/Hexalith.Platform.Protection.Tests/OpenBaoErasureRootStoreTests.cs::TrimMinimumVersionsUseExactStringValuesAcceptedByTheBoundedNativePolicy',
     csharp_test('store_tests', 'TrimMinimumVersionsUseExactStringValuesAcceptedByTheBoundedNativePolicy'), [
        'GetProperty("min_decryption_version").GetString()', 'GetProperty("min_encryption_version").GetString()',
        'GetProperty("min_available_version").GetInt32()']),
    ('tests/Hexalith.Platform.Protection.Tests/OpenBaoErasureRootStoreTests.cs (theory data)', TEXT['store_tests'], [
        're:\\[InlineData\\(2\\)\\]\\s*\\[InlineData\\(64\\)\\]\\s*public async Task TrimMinimumVersionsUseExactStringValuesAcceptedByTheBoundedNativePolicy']),
])

check('Source gap 1 (KV-v2 new-mount defaults)', 'only a mount created by this invocation moves from native KV-v2 defaults; readback is mandatory; existing mismatches refuse', [
    ('deploy/erasure-root/bootstrap.py::ensure_config', config, [
        "native_defaults = isinstance(data, dict) and type(data.get('max_versions')) is int", "data['max_versions'] == 0 and data.get('cas_required') is False",
        'if data is None or newly_created and native_defaults:', "response = call('GET', path)", 'raise BootstrapStop(step)']),
    ('deploy/erasure-root/bootstrap.py (caller)', TEXT['bootstrap'], [
        "head_mount_created = ensure_mount(value['headMount'], 'kv', {'version': '2'})",
        "ensure_config(value['headMount'] + '/config', {'max_versions': 1, 'cas_required': True}, head_mount_created)"]),
    ('eng/ext-protection-1/test_bootstrap.py::test_apply_installs_non_deletable_keys_policies_and_reads_heads_back', apply_test, [
        "Native.configs['config'] = {'max_versions': 0, 'cas_required': False, 'delete_version_after': '0s'}",
        "self.assertEqual([method for method, path, _ in Native.records if path.endswith('/erc-heads/config')], ['GET', 'POST', 'GET'])",
        "fresh_conflict = run(True, 'fresh-config-conflict')", "unreadback = run(True, 'config-readback-mismatch')",
        "for configuration in ({'max_versions': 0, 'cas_required': False}, {'max_versions': 2, 'cas_required': True}):"]),
])

check('R10-BH-5', 'TLS Secret defaultMode 0400; startup refuses unless memory.max=1Gi and memory.swap.max=0 (kubelet NoSwap fragment)', [
    ('deploy/erasure-root/statefulset.yaml', TEXT['statefulset'], [
        'secretName: erasure-root-tls\n            defaultMode: 0400', '[ "$(cat /sys/fs/cgroup/memory.max 2>/dev/null)" != "1073741824" ]',
        '[ "$(cat /sys/fs/cgroup/memory.swap.max 2>/dev/null)" != "0" ]', 'ERC_MEMORY_AND_SWAP_LIMIT_UNVERIFIED',
        'limits:\n              memory: 1Gi', 'command: [/bin/sh, /bao/config/start-erasure-root.sh]']),
    ('deploy/erasure-root/kubelet-noswap.fragment.yaml', TEXT['kubelet'], ['swapBehavior: NoSwap']),
    ('eng/ext-protection-1/test_bootstrap.py::test_prepared_startup_requires_exact_memory_and_zero_swap_before_openbao',
     python_test('bootstrap_tests', 'test_prepared_startup_requires_exact_memory_and_zero_swap_before_openbao'), [
        "('1073741824', '0', True)", "('1073741824', 'max', False)", "(None, '0', False)", "('1073741824', None, False)"]),
])

check('R10-BH-7', 'readiness fails unless health JSON reports version 2.6.2, initialized true and sealed false', [
    ('deploy/erasure-root/statefulset.yaml', TEXT['statefulset'], [
        '''grep -Eq '"version"[[:space:]]*:[[:space:]]*"2\\.6\\.2"' || exit 1''', '''grep -Eq '"initialized"[[:space:]]*:[[:space:]]*true' || exit 1''',
        '''grep -Eq '"sealed"[[:space:]]*:[[:space:]]*false' || exit 1''', '-ca-cert=/bao/tls/ca.crt']),
    ('eng/ext-protection-1/test_bootstrap.py::test_actual_readiness_shell_refuses_wrong_native_health',
     python_test('bootstrap_tests', 'test_actual_readiness_shell_refuses_wrong_native_health'), [
        "({**good, 'version': '2.6.1'}, '0', False)", "({**good, 'initialized': False}, '0', False)", "({**good, 'sealed': True}, '0', False)",
        "({'version': '2.6.2', 'initialized': True}, '0', False)", "(good, '1', False)"]),
])

check('R10-BH-8', 'listener max_request_size 67108864 and max_request_json_memory 134217728', [
    ('deploy/erasure-root/statefulset.yaml', TEXT['statefulset'], ['max_request_size = 67108864', 'max_request_json_memory = 134217728']),
    ('eng/ext-protection-1/test_bootstrap.py::test_rendered_policy_is_one_tenant_and_manifests_pin_the_client_boundary', policy_test, [
        "self.assertIn('max_request_size = 67108864', stateful)", "self.assertIn('max_request_json_memory = 134217728', stateful)"]),
])

check('R10-BH-13/R10-EC-3', 'registry recovery returns null for a null snapshot (ArgumentNullException) and for JsonException', [
    ('src/Hexalith.EventStore.Server/Security/InteractionOccurrenceRegistryActor.cs', TEXT['registry'], [
        'ArgumentNullException.ThrowIfNull(recovered);',
        'catch (Exception exception) when (exception is InvalidOperationException or JsonException or ArgumentNullException) { return null; }']),
    ('tests/Hexalith.EventStore.Server.Tests/Security/InteractionOccurrenceRegistryActorTests.cs', TEXT['registry_tests'], [
        'public async Task NullRecoveredSnapshotReturnsUnavailable()', 'byte[] bytes = "null"u8.ToArray();',
        'public async Task InvalidRecoveredSnapshotJsonReturnsUnavailable()']),
])

check('R10-BH-17', 'DecryptAsync zeroes its request body in finally', [
    ('src/Hexalith.Platform.Protection/OpenBaoErasureRootStore.cs::DecryptAsync', decrypt, ['finally { CryptographicOperations.ZeroMemory(body); }',
                                                                                         'finally { CryptographicOperations.ZeroMemory(response); }']),
    ('tests/Hexalith.Platform.Protection.Tests/OpenBaoErasureRootStoreTests.cs', TEXT['store_tests'], [
        '[InlineData("success")]', '[InlineData("native-refusal")]', '[InlineData("invalid-json")]',
        'public async Task DecryptRequestBodyIsZeroedAfterSuccessAndFailure(string outcome)', 'retainedBody.ShouldNotBeNull().ToArray().ShouldAllBe(value => value == 0);']),
])

check('R10-VG-1', 'newer current-original registry restore', [
    ('InteractionOccurrenceRegistryActorTests', TEXT['registry_tests'], ['public async Task NewerCurrentOriginalRestoresExactRegistryState()',
                                                                        'lookup.Record.Sealed.SnapshotReplaySkeleton.ShouldBe(skeleton);'])])
check('R10-VG-2', 'non-JSON registry target bytes', [
    ('InteractionOccurrenceRegistryActorTests', TEXT['registry_tests'], ['byte[] bytes = "{not-json"u8.ToArray();',
                                                                        'Status.ShouldBe(InteractionOccurrenceReservationStatus.Unavailable)'])])
check('R10-VG-3', 'sealed skeleton detachment and oversize rejection', [
    ('InteractionOccurrenceRegistryActorTests', TEXT['registry_tests'], ['public async Task SealedSkeletonIsDetachedAndOversizeSkeletonIsRejected()',
                                                                        'SnapshotReplaySkeleton.ShouldNotBeSameAs(skeleton);', 'new byte[16777217]'])])
check('R10-VG-4', 'published-trust ReadCurrentOriginalAsync vectors', [
    ('PublishedTrustOwnerStateAuthorityTests', TEXT['owner_tests'], [
        're:\\[InlineData\\("exact"\\)\\]\\[InlineData\\("foreign-scope"\\)\\]\\[InlineData\\("foreign-tenant"\\)\\]\\[InlineData\\("duplicate"\\)\\]\\[InlineData\\("absent"\\)\\]\\s*'
        'public async Task CurrentOriginalReadForwardsOnlyToExactSingleEnrolledOwner\\(string vector\\)']),
    ('PublishedTrustOwnerStateAuthorityTests::CurrentOriginalReadForwardsOnlyToExactSingleEnrolledOwner',
     csharp_test('owner_tests', 'CurrentOriginalReadForwardsOnlyToExactSingleEnrolledOwner'), [
        'var original = await authority.ReadCurrentOriginalAsync(requested, TestContext.Current.CancellationToken);',
        'if (vector == "exact") { original.ShouldBeSameAs(transition); }',
        'await owner.DidNotReceiveWithAnyArgs().ReadCurrentOriginalAsync(default!, default);'])])
check('R10-VG-5', 'real-authority unissued accept and mismatch', [
    ('PublishedTrustReplacementAuthorityTests::UnissuedProofAcceptsMatchingGuardAndRejectsMismatchedGuard', unissued, [
        '(await f.Authority.VerifyBlockedReplacementAsync(request, token)).ShouldBeTrue();',
        'f.State = f.State with { Revision = f.State.Revision + 1 };', 'EpochId = "epoch-mutated"'])])
check('R10-VG-6', 'RejectedUnissuedReplacement is null after the healthy issue', [
    ('ErasureRootUnissuedReplacementTests', TEXT['unissued_tests'], [
        're:var healthyIssue = await guard\\.Apply\\(guard\\.Command\\(GovernanceGuardOperation\\.ReplaceAttestation, batch: healthy\\)\\);.*?'
        'guard\\.State\\.Deletions\\.Single\\(\\)\\.Batches\\.Single\\(\\)\\.RejectedUnissuedReplacement\\.ShouldBeNull\\(\\);',
        'unchanged.RejectedUnissuedReplacement.ShouldNotBeNull();'])])

check('R11-VG-1', 'a newer matching original replaces an older stored registry snapshot that is still present', [
    ('InteractionOccurrenceRegistryActorTests', TEXT['registry_tests'], [
        'public async Task NewerCurrentOriginalReplacesOlderStoredRegistry()',
        '((InteractionOccurrenceRegistrySnapshot)f.Backend.CommittedState.Single().Value).Revision.ShouldBe(olderRevision);',
        '((InteractionOccurrenceRegistrySnapshot)f.Backend.CommittedState.Single().Value).Revision.ShouldBe(newer.Revision);'])])
check('R11-VG-2', 'the unissued accept test makes the independent journal reject the proof', [
    ('PublishedTrustReplacementAuthorityTests::UnissuedProofAcceptsMatchingGuardAndRejectsMismatchedGuard', unissued, [
        'return blocked && acceptBlockedProof;',
        're:acceptBlockedProof = false;\\s*\\(await f\\.Authority\\.VerifyBlockedReplacementAsync\\(request, token\\)\\)\\.ShouldBeFalse\\(\\);'])])
check('R11-VG-3', 'owner reads include a foreign-tenant scope case', [
    ('PublishedTrustOwnerStateAuthorityTests', TEXT['owner_tests'], [
        'owner.TenantId.Returns(vector == "foreign-tenant" ? "tenant-b" : "tenant-a");',
        're:\\[InlineData\\("foreign-tenant"\\)\\].*?public async Task AdmittedOriginalReadForwardsOnlyToExactSingleEnrolledOwner',
        're:\\[InlineData\\("foreign-tenant"\\)\\].*?public async Task CurrentOriginalReadForwardsOnlyToExactSingleEnrolledOwner'])])
check('R11-BH-9', 'TrimAsync and ReadKeyAsync zero their native response buffers in finally', [
    ('src/Hexalith.Platform.Protection/OpenBaoErasureRootStore.cs::TrimAsync', trim, [
        'if (configured is not null) { CryptographicOperations.ZeroMemory(configured); }', 'if (trimmed is not null) { CryptographicOperations.ZeroMemory(trimmed); }',
        'CryptographicOperations.ZeroMemory(config);', 'if (trim is not null) { CryptographicOperations.ZeroMemory(trim); }']),
    ('src/Hexalith.Platform.Protection/OpenBaoErasureRootStore.cs::ReadKeyAsync', read_key, [
        'if (health is not null) { CryptographicOperations.ZeroMemory(health); }', 'if (response is not null) { CryptographicOperations.ZeroMemory(response); }',
        'CryptographicOperations.ZeroMemory(retained);']),
])


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


# Byte-level regression comparison with the two latest authoritative loop-10 graphs.
resume = LOOP10 / 'ext-protection-loop10-resume-27fe4f343c1a'
source_gaps = LOOP10 / 'source-gaps-7b5a4e938648'
after = {str(p.relative_to(source_gaps / 'implementation-after')): digest(p) for p in sorted((source_gaps / 'implementation-after').rglob('*')) if p.is_file()}
comparisons = []
for entry in json.loads((resume / 'selected-source.json').read_text()):
    path = HEX / entry['repository'] / entry['path']
    current = digest(path) if path.is_file() else None
    key = entry['path'] if entry['repository'] == 'platform' else None
    expected_source = 'source-gaps-7b5a4e938648/implementation-after' if key in after else 'ext-protection-loop10-resume-27fe4f343c1a/selected-source'
    expected = after[key] if key in after else entry['sha256']
    comparisons.append({'repository': entry['repository'], 'path': entry['path'], 'currentSha256': current, 'expectedFrom': expected_source,
                        'expectedSha256': expected, 'matches': current == expected})
for key, value in after.items():
    if not any(item['repository'] == 'platform' and item['path'] == key for item in comparisons):
        path = PLATFORM / key
        current = digest(path) if path.is_file() else None
        comparisons.append({'repository': 'platform', 'path': key, 'currentSha256': current, 'expectedFrom': 'source-gaps-7b5a4e938648/implementation-after',
                            'expectedSha256': value, 'matches': current == value})

all_present = all(item['present'] for item in results)
all_match = all(item['matches'] for item in comparisons)
report = {
    'record': 'EXT-PROTECTION-1', 'loop': 10, 'status': 'in-progress', 'auditedUtc': datetime.now(timezone.utc).isoformat(),
    'scope': 'read-only audit of mandatory loop-10 corrections, Review 11 patches and both source-gap corrections in the current working trees',
    'auditedFiles': [{'repository': 'platform' if path.is_relative_to(PLATFORM) else 'eventstore',
                      'path': str(path.relative_to(PLATFORM if path.is_relative_to(PLATFORM) else EVENTSTORE)), 'sha256': digest(path)}
                     for path in sorted(F.values())],
    'corrections': results, 'allCorrectionsPresent': all_present,
    'byteComparisonWithLatestLoop10Graphs': comparisons, 'allBytesMatchLatestLoop10Graphs': all_match,
    'sourceChangedByThisRun': False,
}
Path(sys.argv[1]).write_text(json.dumps(report, indent=2) + '\n')
for item in results:
    print(('PRESENT ' if item['present'] else 'MISSING ') + item['correction'])
for item in comparisons:
    if not item['matches']:
        print('BYTES-DIFFER ' + item['repository'] + ' ' + item['path'])
print(json.dumps({'allCorrectionsPresent': all_present, 'comparedFiles': len(comparisons), 'allBytesMatchLatestLoop10Graphs': all_match}))
raise SystemExit(0 if all_present and all_match else 1)
