#!/usr/bin/env python3
"""Prepare a public, bounded expired-HMAC recovery scope; never execute it."""
import argparse
from copy import deepcopy
from datetime import datetime, timedelta, timezone
import hashlib
import json
import os
from pathlib import Path
import re
import sys
import uuid

SCHEMA = 'EXT-SECRETS-1.expired-hmac-recovery-scope.v1'
FIELDS = {'PublishProfile': 'profile', 'SetInventoryHost': 'host', 'SetCaller': 'caller', 'SetKeyStatus': 'keyStatus'}
PROTOCOL_FILES = ('src/Hexalith.Platform.Secrets.Authority/AuthorityRecords.cs',
    'src/Hexalith.Platform.Secrets.Authority/AuthorityRepository.cs',
    'src/Hexalith.Platform.Secrets.Authority/AuthorityHttp.cs',
    'src/Hexalith.Platform.Secrets.Authority/AuthorityEvaluator.cs',
    'eng/ext-secrets-1/maintain_isolated_custody.py', 'eng/ext-secrets-1/bind_isolated_custody.py')


def canonical(value):
    return json.dumps(value, sort_keys=True, separators=(',', ':'), ensure_ascii=True).encode()


def digest(value):
    return hashlib.sha256(value).hexdigest()


def require(value, reason):
    if not value:
        raise ValueError(reason)


def utc(value):
    require(isinstance(value, str) and re.fullmatch(r'\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d(?:\.\d{1,9})?(?:Z|\+00:00)', value), 'utc-timestamp-required')
    return datetime.fromisoformat(value.replace('Z', '+00:00'))


def read_public(path, pins):
    path = Path(path).absolute()
    require(path.is_file() and not path.is_symlink() and path.stat().st_size <= 16 * 1024 * 1024, 'public-input-file-required')
    raw = path.read_bytes()
    def unique(pairs):
        result = {}
        for key, value in pairs:
            require(key not in result, 'duplicate-public-json-field')
            result[key] = value
        return result
    value = json.loads(raw, object_pairs_hook=unique, parse_constant=lambda _: require(False, 'invalid-public-json-number'))
    pins[str(path)] = digest(raw)
    return value


def records(state):
    return [('SetInventoryHost', item) for item in state['hosts']] + [('SetCaller', item) for item in state['callers']] + [
        ('SetKeyStatus', item) for item in state['keyStatuses']] + [('PublishProfile', state['profile'])]


def operations(state, run_id, deadline):
    """Keep the expired profile as the deny gate until the final operation."""
    result = []
    for index, (kind, record) in enumerate(records(state)):
        body = {'operationId': uuid.uuid5(uuid.UUID(run_id), 'activate:' + str(index)).hex,
                'expectedRevision': state['instanceId'] + ':' + str(state['generation'] + index), 'kind': kind,
                'profile': None, 'caller': None, 'host': None, 'keyStatus': None}
        body[FIELDS[kind]] = {**deepcopy(record), 'validUntil': deadline}
        result.append(body)
    require(len(result) == 9, 'exact-nine-records-required')
    return result


def validate_inputs(state, runtime, keys, prepared):
    require(state.get('generation') == 19 and len(state.get('audit', [])) == 19, 'nineteen-original-receipts-required')
    require(state['revision'] == state['instanceId'] + ':19' and uuid.UUID(state['instanceId']).hex == state['instanceId'], 'exact-authority-revision-required')
    require(len({item['operationId'] for item in state['audit']}) == 19, 'unique-original-receipts-required')
    for index, receipt in enumerate(state['audit']):
        require(receipt['previousRevision'] == state['instanceId'] + ':' + str(index)
            and receipt['revision'] == state['instanceId'] + ':' + str(index + 1)
            and receipt['outcome'] == 'Applied' and receipt['administratorId'] == 'ext-secrets-1-hmac-owner', 'original-receipt-chain-mismatch')
    require(state['profile']['revoked'] is False and utc(state['profile']['validUntil']) <= prepared, 'expired-unrevoked-original-profile-required')
    require(runtime['runtime']['profileValidUntil'] == state['profile']['validUntil']
        and runtime['runtime']['profileVersion'] == state['profile']['version'], 'installed-profile-runtime-mismatch')
    require(runtime['runtime']['timing'] == {'L': 300, 'S': 30, 'O': 600, 'H': 86400, 'R': 604800}
        and runtime['runtime']['overlappingExportDisposition'] == 'Open'
        and runtime['runtime']['decisionKeysOutsideCustody'] is True, 'settled-configuration-mismatch')
    require(len(state['hosts']) == 2 and len(state['callers']) == 2 and len(state['keyStatuses']) == len(keys) == 4, 'exact-installed-record-counts-required')
    require({item['callerId'] for item in state['callers']} == {'ext-secrets-1-hmac-inventory', 'ext-secrets-1-hmac-custody'}
        and all(item['enabled'] is True for item in state['callers']) and all(item['enrolled'] is True for item in state['hosts']), 'original-enrollment-shape-mismatch')
    expected = {(runtime['runtime'][tenant], purpose) for tenant in ('tenantA', 'tenantB') for purpose in ('TrustedEnvelope', 'ContentDigest')}
    require({(item['original']['tenantId'], item['original']['purpose']) for item in keys} == expected, 'exact-four-native-key-tuples-required')
    key_map = {(item['original']['tenantId'], item['original']['purpose'], item['opaqueVersion']): item for item in keys}
    require(len(key_map) == 4, 'distinct-opaque-key-versions-required')
    for status in state['keyStatuses']:
        key = key_map.get((status['tenantId'], status['purpose'], status['version']))
        require(key is not None and key['phase'] == 'MetadataVerified' and key['secretVersion'] == key['mapping']['secretVersion'] == '2'
            and key['mapping']['state'] == 'Active' and key['mapping']['retiredAt'] is None and status['revoked'] is False
            and status['profileVersion'] == state['profile']['version'] and utc(status['notBefore']) == utc(key['mapping']['notBefore']), 'version-two-native-mapping-mismatch')


def build_scope(state, runtime, keys, identities, prepared_at, run_id):
    prepared = utc(prepared_at)
    validate_inputs(state, runtime, keys, prepared)
    deadline = min(prepared + timedelta(days=1), *(utc(item['mapping']['verifyUntil']) for item in keys))
    require(deadline > prepared and deadline - prepared == timedelta(days=1), 'existing-key-windows-must-cover-proposed-day')
    until = deadline.isoformat()
    owner_ops = operations(state, run_id, until)
    proposed_runtime = deepcopy(runtime)
    proposed_runtime['runtime']['profileValidUntil'] = until
    generation = state['generation'] + len(owner_ops)
    deny = []
    for index, caller in enumerate(state['callers']):
        body = {'operationId': uuid.uuid5(uuid.UUID(run_id), 'deny:' + str(index)).hex,
                'expectedRevision': state['instanceId'] + ':' + str(generation + index), 'kind': 'SetCaller',
                'profile': None, 'host': None, 'keyStatus': None,
                'caller': {**deepcopy(caller), 'enabled': False}}
        deny.append(body)
    credential_plans = {}
    for role in ('data', 'metadata'):
        identity = identities[role]
        require(identity['policy'] == ('ext-secrets-1-hmac-data-read' if role == 'data' else 'ext-secrets-1-inventory-metadata-read'), 'original-policy-mismatch')
        successor = 'ext-secrets-1-recovery-' + role + '-' + run_id[:12]
        credential_plans[role] = {'original': deepcopy(identity), 'freshLeaseState': None, 'selectedBranch': None,
            'reuse': {'condition': 'Fresh exact-accessor lease, policy and effective scopes independently verified; exclusive expiry covers proposed deadline.', 'mutations': []},
            'renew': {'condition': 'Fresh original is currently valid/renewable, original absolute cap covers deadline, exact self-renew capability verified, and explicit renewal branch approved.',
                'nativeRequest': {'method': 'POST', 'path': '/v1/auth/token/renew-self', 'body': {'increment': 86400}},
                'recovery': 'Journal before request; uncertain renewal permits exact current lease lookup only, never blind renewal replay. Stop if lookup cannot establish the original outcome.'},
            'replace': {'condition': 'Fresh original is absent/expired/revoked/nonrenewable/capped, or separately approved replacement is selected; exact create-orphan and revoke-accessor capabilities verified.',
                'newSecretName': successor, 'newCredentialReference': 'ext-secrets-1-isolated/Secret/' + successor + '#token',
                'nativeRequest': {'method': 'POST', 'path': '/v1/auth/token/create-orphan', 'body': {'policies': [identity['policy']],
                    'no_default_policy': True, 'ttl': '24h', 'explicit_max_ttl': '168h', 'renewable': True,
                    'display_name': 'ext-secrets-1-recovery-' + run_id + '-' + role}},
                'destination': 'Require name absent, create intended Secret via stdin, retain UID/resourceVersion from creation response; never overwrite old Secret.',
                'issuanceRecovery': 'Persist IssuanceStarted before POST. Lost response freezes this original; authenticated provider audit recovers only its exact accessor. Never issue a second credential for an uncertain original.',
                'cutover': 'Conditionally update only exact inventoried Secret volume references. Restart existing inventory/custody consumers at pinned Deployment UID/resourceVersion with unchanged images; custody Dapr must reload its data-token mount. Inventory never receives data token.',
                'revokeOriginal': {'method': 'POST', 'path': '/v1/auth/token/revoke-accessor', 'body': {'accessor': identity['accessor']},
                    'condition': 'All selected successor consumers and metadata-only readiness independently verified; preserve original Secret and public receipt unless separate cleanup is approved.'},
                'failure': 'Before cutover, revoke only a demonstrably issued successor and conditionally delete only its exact creation UID/resourceVersion. After cutover, deny callers and preserve exact originals; never roll back to an expired/revoked credential.'}}
    return {'schema': SCHEMA, 'runId': run_id, 'status': 'PreparedScopeOnly',
        'confirmedOwnership': {'confirmedByUser': True, 'scope': 'Authority to approve expired HMAC profile and restricted provider credential recovery.', 'operationApproval': False, 'independentEvidenceCreated': False},
        'proposedWindow': {'executionNotBefore': prepared_at, 'exclusiveDeadline': until, 'maximumSeconds': 86400,
            'recordNotBeforeValuesPreserved': True, 'automaticRebaseOrExtensionAllowed': False},
        'originalAuthorityRevision': state['revision'], 'originalAuthorityStateSha256': digest(canonical(state)),
        'originalReceipts': deepcopy(state['audit']), 'nativeOwnerOperations': owner_ops,
        'authorityRequests': {'transport': 'Existing authenticated owner administrative channel; HTTPS issuer, exact administrator JWT audience/azp/hmac:administer and separate authority-admin-token required.',
            'read': {'method': 'GET', 'path': '/admin/state'}, 'apply': {'method': 'POST', 'path': '/admin/operations'},
            'originalRecovery': 'GET exact durable state/audit; only identical administrator, operationId, body and expectedRevision may use native exact-original idempotency. A RevisionConflict is durable: stop; never change its payload or revision.'},
        'activationOrdering': ['Verify complete fresh provider/native tenant facts, authentic preservation dispositions, actual workload UIDs/specs, source pins and exact scope approval; retain all 19 old receipts.',
            'Resolve and pin one credential branch per role. Stage approved successors if necessary while old profile remains expired; no policy, key, mapping or key-window changes.',
            'Stage both runtime ConfigMaps with only proposed profileValidUntil and, for data replacement only, hmacCredentialReference; retain every null input and settled value.',
            'Perform approved exact consumer Secret-reference cutover/restart with image/UID/resourceVersion preconditions; verify liveness and intended mounted references. Readiness stays denied while profile is expired.',
            'Apply eight exact revision-checked host/caller/key-status operations; publish profile last as the ninth activation gate. Retain each durable native receipt before the next operation.',
            'Verify metadata-only current enrollment/profile/allowlist/permissions and all required health; only then revoke a replaced original accessor. No key-value read or EXT-SECRETS-1 contract case is authorized.'],
        'failureContainment': {'beforeFinalProfile': 'Stop with old profile still expired; retain journals, uncertain originals and staged resources. Do not publish profile or roll back retained records.',
            'afterFinalProfile': 'When live state exactly equals the planned final generation 28, the separately approved two deny operations may disable the existing callers at revisions 28/29. Foreign/unknown revisions stop for exact original reconciliation.',
            'proposedDenyOperations': deny, 'automaticRollbackToOldProfileOrCredential': False},
        'keyPlan': deepcopy(keys), 'keyMutationAllowed': False, 'credentialBranches': credential_plans,
        'runtimeBeforeSha256': digest(canonical(runtime)), 'proposedRuntimeForReuseOrRenew': proposed_runtime,
        'independentRequiredFacts': ['Fresh owner-controlled exact-accessor lease/capability observations; current complete four-key metadata plus unchanged mount/auth/policy configuration.',
            'Current complete original native tenant enrollment streams and exact original hashes; not inferred from Kubernetes identity.',
            'Authentic original-to-current system Secret write-scope evidence and explicit exact-transition acceptance; separate register/Memories preservation disposition.',
            'Current actual UID/spec/resourceVersion and intended credential mount/caller/host identities before every conditional effect.'],
        'implementationStillRequired': {'owner': 'Platform implementation agent', 'liveRecoveryExecutorAvailable': False,
            'action': 'Implement and verify a separate journaled expired-state recovery executor against this approved exact scope and fresh evidence. Existing maintenance and one-time installers must not run. Owner approval does not supply missing implementation or authentic facts.'},
        'liveMutationCommand': None, 'qualificationPassed': False, 'dependencyAvailable': False, 'mutationsExecuted': 0}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--observations', type=Path, required=True)
    parser.add_argument('--maintenance', type=Path, required=True)
    parser.add_argument('--binding', type=Path, required=True)
    parser.add_argument('--protocol-source', type=Path, required=True)
    parser.add_argument('--prepared-at', required=True)
    parser.add_argument('--run-id', required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    try:
        require(uuid.UUID(args.run_id).hex == args.run_id, 'canonical-run-id-required')
        pins = {}
        state = read_public(args.observations / 'authority-state.current.json', pins)
        runtime = read_public(args.observations / 'runtime-reference.json', pins)
        retained_state = read_public(args.maintenance / 'authority-state.after.json', pins)
        require(state == retained_state, 'observed-retained-authority-state-drift')
        keys = read_public(args.binding / 'successor-key-plan.json', pins)
        metadata = read_public(args.maintenance / 'key-metadata.after.json', pins)
        provider = read_public(args.maintenance / 'preserved-openbao-before.json', pins)
        snapshot = read_public(args.observations / 'preservation-after.json', pins)
        preservation = read_public(args.observations / 'preservation-result.json', pins)
        discovery = read_public(args.observations / 'discovery.json', pins)
        for key in keys:
            native = metadata[key['original']['secretReference']]
            mapping = json.loads(native['custom_metadata']['ext-secrets-1.versionMappings'])
            require(native['current_version'] == 2 and native['versions']['2']['destroyed'] is False
                and native['versions']['2']['deletion_time'] == '' and mapping[key['opaqueVersion']] == key['mapping'], 'retained-native-version-two-metadata-mismatch')
        maintenance = read_public(args.maintenance / 'operations/maintenance-plan.json', pins)
        sources = read_public(args.maintenance / 'original-owner-receipts.json', pins)
        journals = [read_public(item['source'], pins) for item in sources]
        journals += [read_public(args.maintenance / ('operations/owner-operation-' + str(index) + '.json'), pins) for index in range(9)]
        require(len(journals) == 19, 'nineteen-original-journals-required')
        by_id = {item['operation']['operationId']: item for item in journals}
        require(len(by_id) == 19 and all(by_id.get(receipt['operationId'], {}).get('receipt') == receipt for receipt in state['audit']), 'original-native-receipt-journal-mismatch')
        identities = {role: maintenance['credentials'][role]['identity'] for role in ('data', 'metadata')}
        for role, identity in identities.items():
            issuance = read_public(args.binding / (role + '-credential-issuance.json'), pins)
            require(issuance['phase'] == 'Stored' and issuance['accessor'] == identity['accessor']
                and issuance['credentialReference'] == identity['credentialReference'], 'original-issuance-reference-mismatch')
        for relative in PROTOCOL_FILES:
            path = (args.protocol_source / relative).absolute()
            require(path.is_file() and not path.is_symlink(), 'native-protocol-source-missing')
            pins[str(path)] = digest(path.read_bytes())
        result = build_scope(state, runtime, keys, identities, args.prepared_at, args.run_id)
        references = ['ConfigMap/ext-secrets-1-isolated/' + name for name in
                      ('ext-secrets-1-inventory-runtime', 'ext-secrets-1-custody-runtime-29a4ace57e04')]
        references += ['Deployment/ext-secrets-1-isolated/' + name for name in ('inventory', 'ext-secrets-1-custody')]
        references += ['StatefulSet/ext-secrets-1-isolated/ext-secrets-1-hmac-authority',
                       'Component/ext-secrets-1-isolated/ext-secrets-1-hmac']
        references += ['Secret/ext-secrets-1-isolated/' + value['secretName'] for value in identities.values()]
        require(all(reference in snapshot for reference in references), 'exact-installed-target-identities-required')
        result['installedResourcePreconditions'] = {reference: snapshot[reference] for reference in references}
        result['installedWorkloadImages'] = {item['name']: item['images'] for item in discovery['workloads']
            if item['namespace'] == 'ext-secrets-1-isolated' and item['name'] in ('inventory', 'ext-secrets-1-custody', 'ext-secrets-1-hmac-authority')}
        result['retainedNativeMetadataSha256'] = digest(canonical(metadata))
        result['retainedProviderConfigurationSha256'] = digest(canonical(provider))
        result['freshMetadataRequirement'] = 'Fresh complete metadata and public provider configuration must equal these retained originals, including every old version/window and exact version-2 mapping. Any difference stops for a new scoped decision; no key write/rotation/window change is proposed.'
        result['unresolvedPreservationDifferences'] = preservation['differencesSinceRetainedOwnerObservation']
        result['unresolvedRegisterPreservation'] = preservation['registerAfter']
        result['separatePreservationAcceptanceStillRequired'] = True
        result['inputSha256'] = pins
        result['preparationCommand'] = [sys.executable, str(Path(__file__).resolve()), *sys.argv[1:]]
        result['preparerSha256'] = digest(Path(__file__).read_bytes())
        result['scopeSha256'] = digest(canonical(result))
        # Fresh output only; no token/store/network/cluster operations exist in this tool.
        os.umask(0o077)
        args.output.mkdir(parents=False, exist_ok=False)
        with (args.output / 'recovery-scope.json').open('xb') as output:
            output.write(json.dumps(result, sort_keys=True, indent=2).encode() + b'\n')
            output.flush()
            os.fsync(output.fileno())
        print(json.dumps({'status': result['status'], 'scopeSha256': result['scopeSha256'],
            'scope': str(args.output / 'recovery-scope.json'), 'operationApproval': False,
            'liveRecoveryExecutorAvailable': False, 'mutationsExecuted': 0, 'dependencyAvailable': False}))
        return 0
    except (ValueError, OSError, KeyError, TypeError):
        print('recovery-scope-preparation-blocked; public inputs require reconciliation', file=sys.stderr)
        return 2


if __name__ == '__main__':
    raise SystemExit(main())
