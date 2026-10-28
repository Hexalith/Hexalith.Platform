#!/usr/bin/env python3
"""Inspect or maintain the exact completed isolated custody binding.

Public plans and per-effect journals precede owner operations, lease renewals and
conditional runtime updates. No installer, issuance, key write or contract case
is called. Unknown native outcomes are observed, never replayed.
"""
import argparse
import base64
import hashlib
from copy import deepcopy
from datetime import datetime, timedelta, timezone
import json
import os
from pathlib import Path
import sys
import uuid

from bind_isolated_custody import (APP, AUTHORITY, CUSTODY_CLIENT, DATA_POLICY, EVIDENCE_ROOT,
    INVENTORY, METADATA_POLICY, MOUNT, ORIGINAL_AUTHORITY, QUERY_CLIENT, instant, public_object,
    http_health)
from install_current_inventory import application_request, read_pinned_credential_secret
from install_hmac_authority import ADMIN_CLIENT, RUNTIME, administration_request, forward, timestamp
from install_isolated_registry import Installer, NS, REGISTER, canonical, kjson, kube, require_creation_identity, sha, snapshot
from native_journal import durable_write, journal_lock

BASELINE = EVIDENCE_ROOT / 'custody-maintenance-20261027T234210095124Z'
BINDING = EVIDENCE_ROOT / 'custody-binding-20261027T182648692778Z'
HORIZON_SECONDS = 24 * 60 * 60
ABSOLUTE_LIFETIME_SECONDS = 7 * HORIZON_SECONDS
FLAGS = {'dependencyAvailable': False, 'liveRun': False, 'contractOperations': 0,
         'providerDataReads': 0, 'providerWrites': 0}
LEASE_FIELDS = ('accessor', 'creation_time', 'issue_time', 'expire_time', 'explicit_max_ttl', 'policies', 'renewable', 'ttl')
PUBLIC_METADATA_TRANSITION = {
    'reference': 'Secret/kubesphere-system/kubesphere-sa-token',
    'originalIdentity': {'uid': 'a97fa037-798b-46b6-9232-76e600480142', 'resourceVersion': '36774577'},
    'observedIdentity': {'uid': 'a97fa037-798b-46b6-9232-76e600480142', 'resourceVersion': '37223806'},
    'label': {'name': 'kubernetes.io/legacy-token-last-used', 'value': '2026-10-28'},
    'managedField': {'manager': 'kube-apiserver', 'operation': 'Update', 'apiVersion': 'v1',
        'fieldsType': 'FieldsV1', 'time': '2026-10-28T00:00:02Z',
        'fieldsV1': {'f:metadata': {'f:labels': {'f:kubernetes.io/legacy-token-last-used': {}}}}}}
OWNER_SCOPE_ATTESTATION = {'ownerAccepted': True, 'historicalEvidenceEstablishesOnlyLabelRefresh': True,
                         'noOtherSecretFieldChanges': True, 'noInterveningWrites': True}
OWNER_EVIDENCE_TRUST = ('Owner-accepted historical scope is a trusted human input. SHA256 fingerprints '
                        'establish file integrity, never truth or authenticity; current managed fields '
                        'do not establish historical scope or credential-value equality.')


def read(path):
    return json.loads(Path(path).read_text())


def wire_time(value):
    # System.Text.Json trims trailing fractional zeros on DateTimeOffset output.
    date, offset = value.isoformat(timespec='microseconds').rsplit('+', 1)
    return date.rstrip('0').rstrip('.') + '+' + offset


def null_paths(value, prefix=''):
    if isinstance(value, dict):
        return sorted(path for key, item in value.items() for path in null_paths(item, prefix + '.' + key if prefix else key))
    if value is None:
        return [prefix]
    return []


def fail(reason):
    raise RuntimeError(reason)


def fingerprinted_public_file(source):
    if (not isinstance(source, dict) or set(source) != {'path', 'sha256'}
        or not isinstance(source['path'], str) or not Path(source['path']).is_absolute()
        or not isinstance(source['sha256'], str) or len(source['sha256']) != 64
        or any(char not in '0123456789abcdef' for char in source['sha256'])):
        fail('public-metadata-acceptance-invalid-public-file-fingerprint')
    path = Path(source['path'])
    if not path.is_file() or sha(path.read_bytes()) != source['sha256']:
        fail('public-metadata-acceptance-evidence-changed')
    return path


def accepted_public_metadata(path, *, retained_blocker=None):
    """Validate owner-supplied public evidence, without asserting its authenticity."""
    path = Path(path)
    if not path.is_absolute() or not path.is_file():
        fail('public-metadata-acceptance-requires-absolute-owner-record-file')
    record = read(path)
    fields = {'schema', 'ownerIdentity', 'historicalEvidence', 'retainedBlocker',
              'currentPublicMetadata', 'transition', 'attestation'}
    if (not isinstance(record, dict) or set(record) != fields
        or record['schema'] != 'EXT-SECRETS-1.public-metadata-acceptance.v1'
        or not isinstance(record['ownerIdentity'], str) or not record['ownerIdentity'].strip()
        or record['transition'] != PUBLIC_METADATA_TRANSITION
        or record['attestation'] != OWNER_SCOPE_ATTESTATION
        or any(value is not True for value in record['attestation'].values())):
        fail('public-metadata-acceptance-requires-exact-owner-historical-scope-attestation')
    historical = record['historicalEvidence']
    if (not isinstance(historical, dict) or set(historical) != {'sourceDescription', 'files'}
        or not isinstance(historical['sourceDescription'], str) or not historical['sourceDescription'].strip()
        or not isinstance(historical['files'], list) or not historical['files']):
        fail('public-metadata-acceptance-missing-historical-evidence')
    sources = [record['retainedBlocker'], record['currentPublicMetadata'], *historical['files']]
    paths = [fingerprinted_public_file(source) for source in sources]
    if (retained_blocker is not None and paths[0].resolve() != retained_blocker.resolve()
        or len({str(item.resolve()) for item in [path, *paths]}) != len(paths) + 1):
        fail('public-metadata-acceptance-requires-distinct-original-blocker-and-historical-evidence')
    blocker, current = read(paths[0]), read(paths[1])
    transition = PUBLIC_METADATA_TRANSITION
    if (not isinstance(blocker, dict) or blocker.get('phase') != 'NextMaintenanceBaselineBlocked' or blocker.get('baselineIssued') is not False
        or any(blocker.get(key) != transition[key] for key in ('reference', 'originalIdentity', 'observedIdentity'))):
        fail('public-metadata-acceptance-retained-blocker-transition-mismatch')
    public = blocker.get('publicMetadata')
    permitted = {'annotations', 'labels', 'managedFields', 'name', 'namespace', 'resourceVersion', 'uid', 'creationTimestamp'}
    if (not isinstance(public, dict) or not isinstance(current, dict)
        or set(public) - permitted or set(current) - permitted):
        fail('public-metadata-acceptance-requires-metadata-only-observations')
    comparable = dict(current)
    if 'creationTimestamp' not in public:
        comparable.pop('creationTimestamp', None)
    if comparable != public:
        fail('public-metadata-acceptance-current-metadata-differs-from-retained-blocker')
    if (current.get('name') != 'kubesphere-sa-token' or current.get('namespace') != 'kubesphere-system'
        or {key: current.get(key) for key in ('uid', 'resourceVersion')} != transition['observedIdentity']
        or current.get('labels', {}).get(transition['label']['name']) != transition['label']['value']
        or [item for item in current.get('managedFields', []) if item.get('manager') == 'kube-apiserver'] != [transition['managedField']]):
        fail('public-metadata-acceptance-label-manager-time-or-fields-mismatch')
    pins = {str(item): source['sha256'] for item, source in zip(paths, sources)}
    pins[str(path)] = sha(path.read_bytes())
    return {'record': record, 'recordSource': {'path': str(path), 'sha256': pins[str(path)]},
            'sourceFingerprints': pins, 'acceptedDifference': deepcopy(transition), 'trustBoundary': OWNER_EVIDENCE_TRUST}


def preserved_originals(baseline):
    verification = baseline / 'baseline-verification.json'
    if verification.exists():
        manifest = read(verification)
        if manifest.get('phase') != 'VerifiedNextMaintenanceBaseline':
            fail('maintenance-baseline-not-verified')
        pins = dict(manifest['sourceFingerprints'])
        pins.update({str(baseline / name): expected for name, expected in manifest['files'].items()})
        if any(sha(Path(name).read_bytes()) != expected for name, expected in pins.items()):
            fail('verified-maintenance-baseline-provenance-changed')
        accepted_name = 'accepted-public-metadata-difference.json'
        if manifest.get('acceptedPublicMetadataDifference') is not None:
            accepted = read(baseline / accepted_name)
            if (manifest['acceptedPublicMetadataDifference'] != accepted
                or accepted_public_metadata(accepted['recordSource']['path']) != accepted
                or any(manifest['sourceFingerprints'].get(name) != expected
                       for name, expected in accepted['sourceFingerprints'].items())
                or manifest['files'].get(accepted_name) != sha((baseline / accepted_name).read_bytes())):
                fail('verified-maintenance-baseline-accepted-evidence-changed')
    expanded = baseline / 'original-public-evidence-recursive-fingerprints.json'
    fingerprints = read(expanded if expanded.exists() else baseline / 'original-public-evidence-fingerprints.json')
    if not fingerprints or any(sha(Path(name).read_bytes()) != expected for name, expected in fingerprints.items()):
        fail('original-public-evidence-changed')
    if sha(REGISTER.read_bytes()) != read(baseline / 'agents-register-before.json')['sha256']:
        fail('agents-register-changed')
    return fingerprints


def runtime_targets():
    custody_runtime = APP + '-runtime-' + read(BINDING / 'run.json')['runId'][:12]
    return [('ConfigMap', 'ext-secrets-1-inventory-runtime'), ('ConfigMap', custody_runtime),
            ('Deployment', 'inventory'), ('Deployment', APP)]


def object_identity(obj):
    """Use the same public specification material as install_isolated_registry.snapshot."""
    material = {key: value for key, value in obj.items() if key not in ('metadata', 'status', 'kind', 'apiVersion')}
    material['metadata'] = {key: obj['metadata'][key] for key in ('labels', 'annotations') if key in obj['metadata']}
    return {'uid': obj['metadata']['uid'], 'specSha256': sha(canonical(material))}


def baseline_runtime(baseline):
    original = read(BINDING / 'runtime-reference.json')
    profile = read(baseline / 'authority-state.before.json')['profile']
    expected = runtime_after(original, profile)
    path = baseline / 'runtime-reference.json'
    if path.exists() and read(path) != expected:
        fail('baseline-runtime-outside-verified-deadline-change')
    return expected


def successor_plan(baseline):
    plan = read(BINDING / 'successor-key-plan.json')
    metadata = read(baseline / 'key-metadata.before.json')
    references = [item['original']['secretReference'] for item in plan]
    if len(plan) != 4 or len(set(references)) != 4 or set(references) != set(metadata):
        fail('exact-four-original-successor-metadata-windows-required')
    for item in plan:
        native = metadata[item['original']['secretReference']]
        mappings = json.loads(native['custom_metadata']['ext-secrets-1.versionMappings'])
        if (item.get('phase') != 'MetadataVerified' or item.get('secretVersion') != '2'
            or native.get('current_version') != 2 or mappings.get(item['opaqueVersion']) != item['mapping']
            or item['mapping'].get('secretVersion') != '2'):
            fail('original-successor-technical-window-or-mapping-changed')
    return plan


def retained_receipts(baseline, state):
    sources = read(baseline / 'original-owner-receipts.json')
    if len(sources) != len(state['audit']) or len(sources) < 10:
        fail('all-original-and-later-verified-receipts-required')
    by_id = {source['operationId']: source for source in sources}
    if len(by_id) != len(sources):
        fail('duplicate-retained-receipt-provenance')
    ordered = []
    for receipt in state['audit']:
        source = by_id.get(receipt['operationId'])
        if source is None:
            fail('retained-owner-receipt-provenance-missing')
        path = Path(source['source'])
        journal = read(path)
        if (journal.get('phase') != 'Committed' or journal['operation']['operationId'] != receipt['operationId']
            or journal['receipt'] != receipt or source.get('receipt', receipt) != receipt
            or source.get('sourceSha256', sha(path.read_bytes())) != sha(path.read_bytes())):
            fail('original-owner-receipt-not-retained')
        ordered.append(dict(source, receipt=receipt, sourceSha256=sha(path.read_bytes())))
    return ordered


def validate_baseline(baseline):
    preserved_originals(baseline)
    state = read(baseline / 'authority-state.before.json')
    original = read(BINDING / 'owner-administered-state.json')
    if not (baseline / 'baseline-verification.json').exists() and state != original:
        fail('unverified-maintenance-baseline-state')
    if state['audit'][:10] != original['audit'][:10]:
        fail('ten-original-receipts-changed-or-removed')
    # Every baseline preserves the original record identities and non-deadline fields.
    for field, identities in (('callers', ('callerId',)), ('hosts', ('inventoryAuthorityReference',)),
                              ('keyStatuses', ('tenantId', 'purpose', 'version'))):
        normalize = lambda items: sorted(({key: value for key, value in item.items() if key != 'validUntil'} for item in items),
                                         key=lambda item: tuple(item[key] for key in identities))
        if normalize(state[field]) != normalize(original[field]):
            fail('baseline-record-identity-or-nondeadline-fields-changed')
    if ({key: value for key, value in state['profile'].items() if key != 'validUntil'}
        != {key: value for key, value in original['profile'].items() if key != 'validUntil'}):
        fail('baseline-profile-identity-changed')
    sources = retained_receipts(baseline, state)
    expected = deepcopy(original)
    if (len(state['audit']) - 10) % 9:
        fail('baseline-requires-complete-verified-maintenance-cycles')
    for source, receipt in zip(sources[10:], state['audit'][10:]):
        operation = read(source['source'])['operation']
        if operation.get('expectedRevision') != expected['revision'] or not receipt_matches(receipt, operation):
            fail('baseline-maintenance-receipt-revision-discontinuity')
        field, collection, keys = {'PublishProfile': ('profile', None, ()),
            'SetCaller': ('caller', 'callers', ('callerId',)), 'SetInventoryHost': ('host', 'hosts', ('inventoryAuthorityReference',)),
            'SetKeyStatus': ('keyStatus', 'keyStatuses', ('tenantId', 'purpose', 'version'))}[operation['kind']]
        record = operation[field]
        candidates = [expected['profile']] if collection is None else [item for item in expected[collection] if all(item[key] == record[key] for key in keys)]
        if len(candidates) != 1 or {key: value for key, value in record.items() if key != 'validUntil'} != {key: value for key, value in candidates[0].items() if key != 'validUntil'}:
            fail('baseline-receipt-operation-outside-deadline-only-scope')
        expected = expected_records({'beforeState': expected, 'operations': [operation]}, 1)
        expected.update(generation=expected['generation'] + 1, revision=receipt['revision'], audit=expected['audit'] + [receipt])
    if expected != state:
        fail('baseline-state-not-derived-from-retained-receipts')
    baseline_runtime(baseline)
    return state


def operations_for(state, run_id, deadline):
    desired = deepcopy(state)
    desired['profile']['validUntil'] = wire_time(deadline)
    records = [('profile', 'PublishProfile', desired['profile'])]
    for collection, field, kind in (('hosts', 'host', 'SetInventoryHost'), ('callers', 'caller', 'SetCaller'), ('keyStatuses', 'keyStatus', 'SetKeyStatus')):
        for item in desired[collection]:
            item['validUntil'] = wire_time(deadline)
            records.append((field, kind, item))
    operations = []
    for index, (field, kind, value) in enumerate(records):
        operation = {'operationId': uuid.uuid5(uuid.UUID(run_id), str(index)).hex,
                     'expectedRevision': state['instanceId'] + ':' + str(state['generation'] + index), 'kind': kind,
                     'profile': None, 'caller': None, 'host': None, 'keyStatus': None}
        operation[field] = deepcopy(value)
        operations.append(operation)
    if len(operations) != 9:
        fail('exact-nine-deadline-only-operations-required')
    return operations


def intended_object(before, public, run_id):
    intended = deepcopy(before)
    intended.pop('status', None)
    intended['metadata'].pop('managedFields', None)
    if before['kind'] == 'ConfigMap':
        intended['data']['runtime-reference.json'] = json.dumps(public, sort_keys=True)
    else:
        intended['spec']['template']['metadata'].setdefault('annotations', {})['hexalith.io/custody-maintenance'] = run_id
    return intended


def validate_plan(instance, plan):
    """Re-derive authorized intent; a persisted plan grants no extra authority."""
    state = validate_baseline(instance.baseline)
    successors = successor_plan(instance.baseline)
    prepared = instant(plan['preparedAt'])
    deadline = min(prepared + timedelta(seconds=HORIZON_SECONDS), *(instant(item['mapping']['verifyUntil']) for item in successors))
    if (plan.get('schema') != 'EXT-SECRETS-1.custody-maintenance.v1'
        or plan.get('baseline') != str(instance.baseline) or plan.get('binding') != str(BINDING)
        or uuid.UUID(plan['runId']).hex != plan['runId'] or prepared > datetime.now(timezone.utc)
        or plan.get('deadline') != wire_time(deadline) or deadline <= instant(state['profile']['validUntil'])
        or plan.get('beforeState') != state or plan.get('operations') != operations_for(state, plan['runId'], deadline)
        or any(plan.get(key) != value for key, value in FLAGS.items())):
        fail('persisted-maintenance-plan-intent-mismatch')
    before_public = baseline_runtime(instance.baseline)
    expected_public = runtime_after(before_public, plan['operations'][0]['profile'])
    if (plan.get('beforePublicRuntime') != before_public or plan.get('publicRuntime') != expected_public
        or plan.get('keyMetadata') != read(instance.baseline / 'key-metadata.before.json')
        or plan.get('beforeKubernetes') != read(instance.baseline / 'preserved-kubernetes-before.json')
        or plan.get('beforeOpenbao') != read(instance.baseline / 'preserved-openbao-before.json')):
        fail('persisted-maintenance-plan-preservation-or-runtime-intent-mismatch')
    expected_refs = [kind + '/' + NS + '/' + name for kind, name in runtime_targets()]
    if [item['reference'] for item in plan.get('objects', [])] != expected_refs:
        fail('persisted-maintenance-plan-fixed-object-scope-mismatch')
    for item, (kind, name) in zip(plan['objects'], runtime_targets()):
        before = item['before']
        if (before.get('kind') != kind or before['metadata'].get('name') != name or before['metadata'].get('namespace') != NS
            or item['uid'] != before['metadata']['uid'] or object_identity(before) != plan['beforeKubernetes'][item['reference']]
            or item['intended'] != intended_object(before, expected_public, plan['runId'])):
            fail('persisted-maintenance-plan-object-intent-mismatch')
        if kind == 'ConfigMap' and json.loads(before['data']['runtime-reference.json']) != before_public:
            fail('persisted-maintenance-plan-original-runtime-copy-mismatch')
    if set(plan.get('credentials', {})) != {'data', 'metadata'}:
        fail('persisted-maintenance-plan-credential-scope-mismatch')
    for role, entry in plan['credentials'].items():
        identity = credential_identity(role, instance.baseline)
        prior = read(instance.baseline / 'credentials.before.json')[role]['lease']
        cap = validate_lease(role, identity, entry['before'], prepared)
        if (entry['identity'] != identity or entry['absoluteDeadline'] != cap.isoformat() or cap < deadline
            or any(entry['before'].get(key) != prior.get(key) for key in LEASE_FIELDS if key != 'ttl')):
            fail('persisted-maintenance-plan-original-credential-facts-mismatch')
    return plan


def credential_identity(role, baseline):
    """Require the original issuance accessor and original recorded Secret identity.

    Legacy resource journals pin UID/resourceVersion; they do not establish that
    those fields were captured directly from a creation response.
    """
    policy = DATA_POLICY if role == 'data' else METADATA_POLICY
    original = read(BINDING / (role + '-credential-issuance.json'))
    observed_original = read(baseline / 'credentials.before.json')[role]
    if (original.get('phase') != 'Stored' or original.get('policy') != policy or not original.get('accessor')
        or original.get('revocationPhase') is not None or original.get('secretCreationPhase') not in (None, 'Created')):
        fail(role + '-original-issuance-identity-or-policy-invalid')
    name = original['secretName']
    reference = NS + '/Secret/' + name + '#token'
    resource = [item for item in read(BINDING / 'resource-journal.json')
                if item.get('reference') == 'Secret/' + NS + '/' + name]
    if len(resource) != 1 or resource[0].get('phase') != 'Created':
        fail(role + '-original-recorded-secret-identity-missing')
    identity = require_creation_identity(resource[0])
    if (original.get('secretIdentity', identity) != identity or original.get('credentialReference') != reference
        or observed_original['credentialReference'] != reference
        or observed_original['originalRecordedSecretIdentity'] != identity
        or observed_original['lease']['accessor'] != original['accessor']):
        fail(role + '-original-credential-identities-disagree')
    return {'secretName': name, 'credentialReference': reference, 'secretIdentity': identity,
            'accessor': original['accessor'], 'policy': policy,
            'creationTime': observed_original['lease']['creation_time'],
            'issueTime': observed_original['lease']['issue_time'],
            'explicitMaximumTtlSeconds': observed_original['lease']['explicit_max_ttl']}


def validate_lease(role, identity, lease, now):
    if (lease.get('accessor') != identity['accessor'] or lease.get('policies') != [identity['policy']]
        or lease.get('creation_time') != identity['creationTime']
        or lease.get('issue_time') != identity['issueTime']
        or lease.get('explicit_max_ttl') != identity['explicitMaximumTtlSeconds']
        or type(lease.get('explicit_max_ttl')) is not int or not 0 < lease['explicit_max_ttl'] <= ABSOLUTE_LIFETIME_SECONDS):
        fail(role + '-credential-accessor-policy-or-absolute-limit-mismatch')
    if (type(lease.get('ttl')) is not int or lease['ttl'] <= 0 or lease.get('renewable') is not True
        or instant(lease['expire_time']) <= now):
        fail(role + '-credential-expired-or-nonrenewable-successor-secret-cutover-revocation-required')
    cap = instant(identity['issueTime']) + timedelta(seconds=lease['explicit_max_ttl'])
    if instant(lease['expire_time']) > cap:
        fail(role + '-credential-expiry-exceeds-seven-day-absolute-cap')
    return cap


class Maintenance(Installer):
    def __init__(self, evidence, baseline=BASELINE):
        self.baseline = Path(baseline)
        self.tokens = {}
        self.private_values = []
        Installer.__init__(self, evidence)
        self.private_values.append(bytes(self.token))

    def write(self, name, value):
        durable_write(self.evidence / name, value)

    def secret_field(self, name, field):
        value = Installer.secret_field(self, name, field)
        if field not in ('ca\\.crt', 'client-id') and value:
            self.private_values.append(value.encode())
        return value

    def close(self):
        for token in self.tokens.values():
            token[:] = b'\0' * len(token)
        self.tokens.clear()
        self.private_values.clear()
        Installer.close(self)

    def credential(self, role):
        identity = credential_identity(role, self.baseline)
        token = read_pinned_credential_secret(identity['secretName'], identity['secretIdentity'])
        previous = self.tokens.get(role)
        if previous is not None:
            previous[:] = b'\0' * len(previous)
        self.tokens[role] = token
        self.private_values.append(bytes(token))
        lease = self.lookup(role)
        validate_lease(role, identity, lease, datetime.now(timezone.utc))
        # Capabilities are checked without reading any provider data or metadata.
        denied = ('secret/data/*', 'sys/policies/acl/*', MOUNT + ('/metadata/*' if role == 'data' else '/data/*'))
        for path in denied:
            if read_capabilities(token, path) != ['deny']:
                fail(role + '-credential-excess-capability')
        for item in read(BINDING / 'successor-key-plan.json'):
            path = MOUNT + ('/data/' if role == 'data' else '/metadata/') + item['original']['secretReference']
            capabilities = read_capabilities(token, path)
            if 'read' not in capabilities or set(capabilities) - {'read', 'list'}:
                fail(role + '-credential-exact-read-capability-required')
        return identity, lease

    def lookup(self, role):
        data = json.loads(application_request(self.tokens[role], 'token', 'lookup', '-format=json').stdout)['data']
        return {name: data.get(name) for name in LEASE_FIELDS}

    def metadata(self):
        return {item['original']['secretReference']: json.loads(application_request(self.tokens['metadata'], 'read', '-format=json',
                MOUNT + '/metadata/' + item['original']['secretReference']).stdout)['data']
                for item in read(BINDING / 'successor-key-plan.json')}


def read_capabilities(token, path):
    return json.loads(application_request(token, 'token', 'capabilities', '-format=json', path).stdout)


def validate_scope(state, original, successors, now):
    if state != original:
        fail('current-authority-state-does-not-match-original-reconciliation')
    profile = state.get('profile')
    if (not profile or profile.get('revoked') is not False or profile.get('profileReference') != AUTHORITY
        or instant(profile['notBefore']) > now or instant(profile['validUntil']) <= now):
        fail('original-profile-identity-revoked-expired-or-not-current')
    if (len(state['callers']) != 2 or {item['callerId'] for item in state['callers']} != {QUERY_CLIENT, CUSTODY_CLIENT}
        or any(item.get('enabled') is not True for item in state['callers'])
        or len(state['hosts']) != 2 or {item['inventoryAuthorityReference'] for item in state['hosts']} != {INVENTORY, APP}
        or any(item.get('enrolled') is not True for item in state['hosts'])):
        fail('exact-two-original-hosts-and-callers-required')
    keys = {(item['original']['tenantId'], item['original']['purpose'], item['opaqueVersion']): item for item in successors}
    if len(keys) != 4 or len(state['keyStatuses']) != 4:
        fail('exact-four-existing-successor-statuses-required')
    for status in state['keyStatuses']:
        item = keys.get((status['tenantId'], status['purpose'], status['version']))
        if (item is None or status.get('revoked') is not False or status['profileVersion'] != profile['version']
            or instant(status['notBefore']) != instant(item['mapping']['notBefore'])
            or instant(status['validUntil']) > instant(item['mapping']['verifyUntil'])):
            fail('successor-status-identity-revocation-or-technical-window-mismatch')
    for item in state['callers'] + state['hosts'] + state['keyStatuses']:
        if instant(item['notBefore']) > now or instant(item['validUntil']) <= now:
            fail('original-enrollment-expired-or-not-current')


def runtime_after(public, profile):
    result = deepcopy(public)
    runtime = result['runtime']
    expected = {'profileReference': profile['profileReference'], 'publicTrustReference': profile['publicTrustReference'],
                'profileVersion': profile['version'], 'profileNotBefore': profile['notBefore'],
                'issuer': profile['issuer'], 'audience': profile['audience'],
                'currentRevocationReference': AUTHORITY, 'currentInventoryAuthorityReference': INVENTORY}
    if any(runtime.get(name) != value for name, value in expected.items()):
        fail('public-runtime-profile-or-current-binding-identity-changed')
    if len(null_paths(public)) != 42:
        fail('original-forty-two-null-inputs-required')
    runtime['profileValidUntil'] = profile['validUntil']
    if null_paths(result) != null_paths(public):
        fail('out-of-scope-null-input-change')
    return result


def inspect(instance):
    """Read installed facts and persist one deterministic plan; no installed effects."""
    original = validate_baseline(instance.baseline)
    path = instance.evidence / 'maintenance-plan.json'
    if path.exists():
        plan = read(path)
        validate_plan(instance, plan)
        reconcile_current(instance, plan)
        return plan
    now = datetime.now(timezone.utc)
    status, state = administration_request(instance, 'GET', '/admin/state')
    if status != 200:
        fail('authority-state-unavailable')
    successors = successor_plan(instance.baseline)
    validate_scope(state, original, successors, now)
    deadline = min(now + timedelta(seconds=HORIZON_SECONDS), *(instant(item['mapping']['verifyUntil']) for item in successors))
    if deadline <= instant(state['profile']['validUntil']):
        fail('owner-maintenance-would-not-extend-existing-window')
    credentials = {}
    for role in ('data', 'metadata'):
        identity, lease = instance.credential(role)
        cap = validate_lease(role, identity, lease, now)
        if cap < deadline:
            fail(role + '-seven-day-cap-requires-separate-successor-secret-cutover-and-revocation')
        credentials[role] = {'identity': identity, 'before': lease, 'absoluteDeadline': cap.isoformat()}
    metadata = instance.metadata()
    if metadata != read(instance.baseline / 'key-metadata.before.json'):
        fail('original-and-successor-key-metadata-changed')
    run_id = uuid.uuid4().hex
    operations = operations_for(state, run_id, deadline)
    before_public = read(RUNTIME)
    if before_public != baseline_runtime(instance.baseline):
        fail('current-runtime-drift-from-original-binding-or-verified-baseline')
    if before_public['runtime']['profileValidUntil'] != state['profile']['validUntil']:
        fail('public-runtime-original-profile-deadline-mismatch')
    public = runtime_after(before_public, operations[0]['profile'])
    if public['runtime']['hmacCredentialReference'] != credentials['data']['identity']['credentialReference']:
        fail('public-runtime-original-data-credential-reference-mismatch')
    objects = []
    before_snapshot = snapshot()
    if before_snapshot != read(instance.baseline / 'preserved-kubernetes-before.json'):
        fail('current-kubernetes-intent-drift-from-verified-baseline')
    before_openbao = instance.openbao_snapshot()
    if before_openbao != read(instance.baseline / 'preserved-openbao-before.json'):
        fail('current-openbao-policy-mount-or-auth-drift-from-verified-baseline')
    # Exact existing public copies plus only the two consumers that load subPath files.
    for kind, name in runtime_targets():
        obj = kjson('get', kind, name, '-n', NS)
        reference = kind + '/' + NS + '/' + name
        if obj['metadata']['uid'] != read(instance.baseline / 'preserved-kubernetes-before.json')[reference]['uid']:
            fail('original-isolated-runtime-object-uid-changed')
        intended = intended_object(obj, public, run_id)
        if kind == 'ConfigMap':
            if json.loads(obj['data']['runtime-reference.json']) != before_public:
                fail('dependent-public-runtime-copy-not-coherent')
        else:
            host = next(item for item in state['hosts'] if item['inventoryAuthorityReference'] == (INVENTORY if name == 'inventory' else APP))
            if host['workloadReference'] != NS + '/Deployment/' + name + '@uid:' + obj['metadata']['uid']:
                fail('original-host-workload-uid-changed')
            runtime_volume = [item for item in obj['spec']['template']['spec']['volumes'] if item['name'] == 'runtime']
            if len(runtime_volume) != 1 or runtime_volume[0].get('configMap', {}).get('name') != runtime_targets()[0 if name == 'inventory' else 1][1]:
                fail('original-consumer-runtime-volume-changed')
        objects.append({'reference': reference, 'uid': obj['metadata']['uid'], 'before': obj, 'intended': intended})
    plan = {'schema': 'EXT-SECRETS-1.custody-maintenance.v1', 'runId': run_id, 'preparedAt': now.isoformat(),
            'baseline': str(instance.baseline), 'binding': str(BINDING), 'deadline': wire_time(deadline),
            'beforeState': state, 'operations': operations, 'credentials': credentials, 'keyMetadata': metadata,
            'beforePublicRuntime': before_public, 'publicRuntime': public, 'objects': objects,
            'beforeKubernetes': before_snapshot, 'beforeOpenbao': before_openbao, **FLAGS}
    validate_plan(instance, plan)
    durable_write(path, plan, create_only=True)
    instance.write('inspection.json', {'observedAt': timestamp(), 'revision': state['revision'], 'originalReceiptCount': 10,
                                     'ownerDeadline': plan['deadline'], 'nullInputCount': 42, **FLAGS})
    return plan


def receipt_matches(receipt, operation):
    prefix, generation = operation['expectedRevision'].rsplit(':', 1)
    return (receipt.get('operationId') == operation['operationId'] and receipt.get('kind') == operation['kind']
            and receipt.get('administratorId') == ADMIN_CLIENT and receipt.get('outcome') == 'Applied'
            and receipt.get('previousRevision') == operation['expectedRevision']
            and receipt.get('revision') == prefix + ':' + str(int(generation) + 1)
            and isinstance(receipt.get('payloadSha256'), str) and len(receipt['payloadSha256']) == 64)


def expected_records(plan, count):
    expected = deepcopy(plan['beforeState'])
    for operation in plan['operations'][:count]:
        if operation['profile'] is not None:
            expected['profile'] = deepcopy(operation['profile'])
        else:
            field, collection, identity = next((field, collection, identity) for field, collection, identity in (
                ('caller', 'callers', ('callerId',)), ('host', 'hosts', ('inventoryAuthorityReference',)),
                ('keyStatus', 'keyStatuses', ('tenantId', 'purpose', 'version'))) if operation[field] is not None)
            record = operation[field]
            matches = [index for index, old in enumerate(expected[collection]) if all(old[name] == record[name] for name in identity)]
            if len(matches) != 1:
                fail('maintenance-operation-outside-original-record-scope')
            # AuthorityRepository.Apply removes the old record, then appends.
            expected[collection].pop(matches[0])
            expected[collection].append(deepcopy(record))
    return expected


def validate_progress(instance, plan, state, *, journal_directory=None):
    original = plan['beforeState']
    if state['audit'][:len(original['audit'])] != original['audit']:
        fail('ten-original-receipts-changed-or-removed')
    count = len(state['audit']) - len(original['audit'])
    if count < 0 or count > len(plan['operations']):
        fail('foreign-authority-audit-operation')
    for index, receipt in enumerate(state['audit'][len(original['audit']):]):
        operation = plan['operations'][index]
        journal_path = (journal_directory or instance.evidence) / ('owner-operation-' + str(index) + '.json')
        if not journal_path.exists() or read(journal_path)['operation'] != operation or not receipt_matches(receipt, operation):
            fail('authority-operation-outcome-not-exact-original-journal')
        journal = read(journal_path)
        if journal.get('receipt', receipt) != receipt:
            fail('retained-maintenance-owner-receipt-changed')
    expected = expected_records(plan, count)
    expected.update({'audit': state['audit'], 'generation': original['generation'] + count,
                     'revision': original['instanceId'] + ':' + str(original['generation'] + count)})
    if state != expected:
        fail('authority-record-scope-identity-revocation-or-revision-drift')
    return count


def maintain_owner_records(instance, plan):
    validate_plan(instance, plan)
    _, state = administration_request(instance, 'GET', '/admin/state')
    count = validate_progress(instance, plan, state)
    validate_scope(state, state, read(BINDING / 'successor-key-plan.json'), datetime.now(timezone.utc))
    for index, operation in enumerate(plan['operations']):
        if datetime.now(timezone.utc) >= instant(plan['deadline']):
            fail('owner-maintenance-window-expired-before-next-operation')
        validate_scope(state, state, read(BINDING / 'successor-key-plan.json'), datetime.now(timezone.utc))
        path = instance.evidence / ('owner-operation-' + str(index) + '.json')
        if index < count:
            journal = read(path)
            if journal.get('phase') != 'Committed':
                # Exact original POST is the provider's durable idempotency check.
                status, receipt = administration_request(instance, 'POST', '/admin/operations', operation)
                if status != 200 or receipt != state['audit'][len(plan['beforeState']['audit']) + index] or not receipt_matches(receipt, operation):
                    fail('original-owner-operation-reconciliation-mismatch')
                journal.update(phase='Committed', receipt=receipt, reconciledAt=timestamp())
                durable_write(path, journal)
            continue
        journal = read(path) if path.exists() else {'phase': 'Prepared', 'operation': operation}
        if journal['operation'] != operation:
            fail('persisted-owner-operation-payload-changed')
        durable_write(path, journal, create_only=not path.exists())
        journal.update(phase='Started', startedAt=journal.get('startedAt', timestamp()))
        durable_write(path, journal)
        try:
            status, receipt = administration_request(instance, 'POST', '/admin/operations', operation)
            if status != 200 or not receipt_matches(receipt, operation):
                fail('owner-maintenance-not-applied-exactly')
            journal.update(phase='Committed', receipt=receipt)
            durable_write(path, journal)
        except Exception:
            journal['phase'] = 'OutcomeUnknown'
            durable_write(path, journal)
            raise
        _, state = administration_request(instance, 'GET', '/admin/state')
        if validate_progress(instance, plan, state) != index + 1:
            fail('owner-operation-post-state-mismatch')
    instance.write('owner-state.after.json', state)


def maintain_lease(instance, plan, role):
    validate_plan(instance, plan)
    path = instance.evidence / ('renewal-' + role + '.json')
    prior = read(path) if path.exists() else None
    try:
        identity, before = instance.credential(role)
    except Exception:
        if prior and prior.get('phase') in ('Started', 'OutcomeUnknown'):
            prior['observationValid'] = False
            if role in instance.tokens:
                prior['currentObservation'] = instance.lookup(role)
            durable_write(path, prior)
            fail(role + '-renewal-outcome-unknown-observation-only-owner-reconciliation-required')
        raise
    if identity != plan['credentials'][role]['identity']:
        fail(role + '-original-credential-identity-changed')
    now = datetime.now(timezone.utc)
    cap = validate_lease(role, identity, before, now)
    deadline = instant(plan['deadline'])
    if path.exists():
        journal = read(path)
        if journal.get('identity') != identity or journal.get('requiredDeadline') != plan['deadline']:
            fail(role + '-renewal-original-intent-changed')
        journal['currentObservation'] = before
        durable_write(path, journal)
        if journal['phase'] in ('Started', 'OutcomeUnknown'):
            fail(role + '-renewal-outcome-unknown-observation-only-owner-reconciliation-required')
        if journal['phase'] in ('Renewed', 'AlreadySufficient'):
            if instant(before['expire_time']) < deadline:
                fail(role + '-maintained-credential-no-longer-covers-owner-window')
            return
        fail(role + '-unknown-renewal-journal-phase')
    if deadline <= now or cap < deadline:
        fail(role + '-seven-day-cap-requires-separate-successor-secret-cutover-and-revocation')
    sufficient = instant(before['expire_time']) >= deadline
    # The original explicit maximum is provider-enforced, including fractions.
    # A bounded request can be clamped by that native maximum; post-lookup must
    # still cover the exact deadline and stay within the original absolute cap.
    increment = 0 if sufficient else HORIZON_SECONDS
    journal = {'phase': 'AlreadySufficient' if sufficient else 'Started',
               'identity': identity, 'before': before, 'requiredDeadline': plan['deadline'],
               'incrementSeconds': increment, 'absoluteDeadline': cap.isoformat(), 'startedAt': timestamp(), **FLAGS}
    durable_write(path, journal, create_only=True)
    if journal['phase'] == 'AlreadySufficient':
        return
    try:
        # Discard the renewal response: it can contain the credential value.
        application_request(instance.tokens[role], 'token', 'renew', '-format=json', '-increment=' + str(increment) + 's')
        after = instance.lookup(role)
        validate_lease(role, identity, after, datetime.now(timezone.utc))
        if instant(after['expire_time']) < deadline or instant(after['expire_time']) <= instant(before['expire_time']):
            fail(role + '-renewal-post-lookup-deadline-mismatch')
        journal.update(phase='Renewed', after=after, verifiedAt=timestamp())
        durable_write(path, journal)
    except Exception:
        journal['phase'] = 'OutcomeUnknown'
        durable_write(path, journal)
        raise


def update_public_object(instance, item):
    kind, namespace, name = item['reference'].split('/')
    custody_runtime = APP + '-runtime-' + read(BINDING / 'run.json')['runId'][:12]
    allowed = {'ConfigMap/' + NS + '/' + name for name in ('ext-secrets-1-inventory-runtime', custody_runtime)}
    allowed.update('Deployment/' + NS + '/' + name for name in ('inventory', APP))
    if item['reference'] not in allowed:
        fail('runtime-update-outside-exact-existing-isolated-scope')
    current = kjson('get', kind, name, '-n', namespace)
    if current['metadata']['uid'] != item['uid']:
        fail('original-runtime-object-uid-changed-no-update')
    intended = public_object(item['intended'])
    path = instance.evidence / ('runtime-update-' + kind.lower() + '-' + name + '.json')
    if path.exists():
        journal = read(path)
        if journal.get('reference') != item['reference'] or journal.get('uid') != item['uid'] or journal.get('intended') != intended:
            fail('original-runtime-update-intent-changed')
        if public_object(current) != intended:
            fail('runtime-update-outcome-unknown-no-repeat')
        journal.update(phase='ReconciledCurrent', observedResourceVersion=current['metadata']['resourceVersion'])
        durable_write(path, journal)
        return
    if (public_object(current) != public_object(item['before'])
        or current['metadata']['resourceVersion'] != item['before']['metadata']['resourceVersion']):
        fail('original-runtime-object-resource-version-or-public-intent-changed')
    journal = {'phase': 'Started', 'reference': item['reference'], 'uid': item['uid'],
               'expectedResourceVersion': current['metadata']['resourceVersion'], 'intended': intended}
    durable_write(path, journal, create_only=True)
    kube('replace', '-f', '-', '-o', 'name', data=canonical(item['intended']))
    after = kjson('get', kind, name, '-n', namespace)
    if after['metadata']['uid'] != item['uid'] or public_object(after) != intended:
        fail('runtime-update-post-read-mismatch')
    journal.update(phase='Replaced', observedResourceVersion=after['metadata']['resourceVersion'])
    durable_write(path, journal)


def update_runtime_file(instance, plan):
    path = instance.evidence / 'runtime-file-update.json'
    current = read(RUNTIME)
    if path.exists():
        journal = read(path)
        if journal.get('intended') != plan['publicRuntime'] or current != plan['publicRuntime']:
            fail('public-runtime-file-outcome-unknown-no-repeat')
        journal['phase'] = 'ReconciledCurrent'
        durable_write(path, journal)
        return
    if current != plan['beforePublicRuntime']:
        fail('public-runtime-file-original-content-changed')
    journal = {'phase': 'Started', 'path': str(RUNTIME), 'before': current, 'intended': plan['publicRuntime']}
    durable_write(path, journal, create_only=True)
    durable_write(RUNTIME, plan['publicRuntime'])
    journal['phase'] = 'Replaced'
    durable_write(path, journal)


def reconcile_runtime_prerequisites(instance, plan):
    """Observe every runtime outcome before any new lease or owner effect."""
    for item in plan['objects']:
        kind, namespace, name = item['reference'].split('/')
        current = kjson('get', kind, name, '-n', namespace)
        if current['metadata']['uid'] != item['uid']:
            fail('original-runtime-object-uid-changed-no-update')
        path = instance.evidence / ('runtime-update-' + kind.lower() + '-' + name + '.json')
        if path.exists():
            journal = read(path)
            if (journal.get('reference') != item['reference'] or journal.get('uid') != item['uid']
                or journal.get('intended') != public_object(item['intended'])
                or public_object(current) != public_object(item['intended'])):
                fail('runtime-update-outcome-unknown-no-repeat')
        elif (public_object(current) != public_object(item['before'])
              or current['metadata']['resourceVersion'] != item['before']['metadata']['resourceVersion']):
            fail('original-runtime-object-resource-version-or-public-intent-changed')
    file_path = instance.evidence / 'runtime-file-update.json'
    if file_path.exists():
        journal = read(file_path)
        if journal.get('intended') != plan['publicRuntime'] or read(RUNTIME) != plan['publicRuntime']:
            fail('public-runtime-file-outcome-unknown-no-repeat')
    elif read(RUNTIME) != plan['beforePublicRuntime']:
        fail('public-runtime-file-original-content-changed')
    after = snapshot()
    allowed = {item['reference'] for item in plan['objects']}
    if (set(after) != set(plan['beforeKubernetes'])
        or any(after.get(key) != value for key, value in plan['beforeKubernetes'].items() if key not in allowed)):
        fail('out-of-scope-kubernetes-preservation-change')


def reconcile_current(instance, plan):
    """Retain fresh public observations, including on a cached inspection."""
    validate_plan(instance, plan)
    _, state = administration_request(instance, 'GET', '/admin/state')
    observation = {'observedAt': timestamp(), 'state': state, 'readOnly': True, **FLAGS}
    path = instance.evidence / ('read-only-observation-' + uuid.uuid4().hex + '.json')
    durable_write(path, observation, create_only=True)
    validate_progress(instance, plan, state)
    now = datetime.now(timezone.utc)
    if now >= instant(plan['deadline']):
        fail('original-maintenance-plan-window-expired-owner-reconciliation-required')
    validate_scope(state, state, read(BINDING / 'successor-key-plan.json'), now)
    credentials = {}
    for role in ('data', 'metadata'):
        try:
            identity, lease = instance.credential(role)
        except Exception:
            observation['credentialValidationFailed'] = role
            if role in instance.tokens:
                observation.setdefault('credentials', {})[role] = {'lease': instance.lookup(role)}
            durable_write(path, observation)
            renewal_path = instance.evidence / ('renewal-' + role + '.json')
            if renewal_path.exists() and read(renewal_path).get('phase') in ('Started', 'OutcomeUnknown'):
                fail(role + '-renewal-outcome-unknown-observation-only-owner-reconciliation-required')
            raise
        credentials[role] = {'identity': identity, 'lease': lease}
    observation.update(credentials=credentials, keyMetadata=instance.metadata(), openbao=instance.openbao_snapshot())
    durable_write(path, observation)
    if observation['keyMetadata'] != plan['keyMetadata'] or observation['openbao'] != plan['beforeOpenbao']:
        fail('original-maintenance-prerequisite-key-metadata-or-policies-changed')
    for role, facts in credentials.items():
        renewal_path = instance.evidence / ('renewal-' + role + '.json')
        if renewal_path.exists():
            renewal = read(renewal_path)
            if renewal.get('phase') in ('Started', 'OutcomeUnknown'):
                fail(role + '-renewal-outcome-unknown-observation-only-owner-reconciliation-required')
            expected = renewal.get('after', renewal['before'])
        else:
            expected = plan['credentials'][role]['before']
        if any(facts['lease'].get(key) != expected.get(key) for key in LEASE_FIELDS if key != 'ttl'):
            fail(role + '-current-lease-drift-from-retained-renewal-outcome')
    reconcile_runtime_prerequisites(instance, plan)
    return observation


def require_owner_window(instance, plan):
    _, state = administration_request(instance, 'GET', '/admin/state')
    validate_progress(instance, plan, state)
    if datetime.now(timezone.utc) >= instant(plan['deadline']):
        fail('owner-maintenance-window-expired-before-next-effect')
    validate_scope(state, state, read(BINDING / 'successor-key-plan.json'), datetime.now(timezone.utc))


def exposure_check(instance):
    # Credentials and their hashes are never persisted; test literal/base64 only in memory.
    paths = [path for path in instance.evidence.rglob('*') if path.is_file()] + [RUNTIME, Path(__file__)]
    literals, hashes = [], []
    for value in instance.private_values:
        if len(value) < 16:
            continue
        digest = hashlib.sha256(value).digest()
        literals.extend((value, base64.b64encode(value)))
        hashes.extend((digest, digest.hex().encode(), digest.hex().upper().encode(), base64.b64encode(digest),
                       base64.b64encode(digest).rstrip(b'='), base64.urlsafe_b64encode(digest), base64.urlsafe_b64encode(digest).rstrip(b'=')))
    matches = hash_matches = 0
    for path in paths:
        content = path.read_bytes()
        matches += any(value in content for value in literals)
        hash_matches += any(value in content for value in hashes)
    instance.write('credential-exposure-check.json', {'passed': matches == 0 and hash_matches == 0, 'literalAndBase64Matches': matches,
                   'prohibitedCredentialHashMatches': hash_matches, 'credentialValuesRecorded': bool(matches),
                   'credentialHashesRecorded': bool(hash_matches), 'scannedFileCount': len(paths)})
    if matches or hash_matches:
        fail('credential-exposure-check-failed')


def export_journal_directory(instance, plan):
    """Read either retained layout, never synthesize or copy original journals."""
    candidates = [directory for directory in (instance.evidence, instance.evidence / 'operations')
                  if (directory / 'maintenance-plan.json').exists()]
    if len(candidates) != 1:
        fail('next-maintenance-baseline-requires-one-original-plan-layout')
    directory = candidates[0]
    if read(directory / 'maintenance-plan.json') != plan:
        fail('original-plan-file-changed-before-baseline-emission')
    return directory


def emit_verified_baseline(instance, plan, *, state, credentials, key_metadata, public_runtime,
                           kubernetes, openbao, objects, health, output, public_metadata_acceptance=None):
    """Retain already verified public facts in a fresh directory; no installed calls.

    Coordinators can call this independently after read-only verification of an
    already completed run. Existing source plans, journals and evidence are read-only.
    """
    validate_plan(instance, plan)
    journal_directory = export_journal_directory(instance, plan)
    if validate_progress(instance, plan, state, journal_directory=journal_directory) != len(plan['operations']):
        fail('cannot-emit-baseline-for-incomplete-owner-maintenance')
    validate_scope(state, state, read(BINDING / 'successor-key-plan.json'), datetime.now(timezone.utc))
    if datetime.now(timezone.utc) >= instant(plan['deadline']):
        fail('cannot-emit-baseline-after-owner-deadline')
    if (key_metadata != plan['keyMetadata'] or public_runtime != plan['publicRuntime'] or openbao != plan['beforeOpenbao']
        or set(kubernetes) != set(plan['beforeKubernetes']) or set(credentials) != {'data', 'metadata'}):
        fail('verified-next-baseline-facts-mismatch')
    if len(objects) != 4 or [item['kind'] + '/' + NS + '/' + item['metadata']['name'] for item in objects] != [item['reference'] for item in plan['objects']]:
        fail('verified-next-baseline-object-scope-mismatch')
    allowed = {item['reference'] for item in plan['objects']}
    accepted = None
    if public_metadata_acceptance is not None:
        accepted = accepted_public_metadata(public_metadata_acceptance,
            retained_blocker=instance.baseline / 'next-maintenance-baseline-blocker.json')
        transition = accepted['acceptedDifference']
        reference = transition['reference']
        if (plan['beforeKubernetes'].get(reference) != transition['originalIdentity']
            or kubernetes.get(reference) != transition['observedIdentity']
            or len(plan['beforeState']['audit']) != 10 or len(state['audit']) != 19):
            fail('public-metadata-acceptance-outside-exact-recorded-transition')
        allowed.add(reference)
    drift = [key for key, value in plan['beforeKubernetes'].items() if key not in allowed and kubernetes.get(key) != value]
    if (accepted is None and drift == [PUBLIC_METADATA_TRANSITION['reference']]
        and plan['beforeKubernetes'][drift[0]] == PUBLIC_METADATA_TRANSITION['originalIdentity']
        and kubernetes[drift[0]] == PUBLIC_METADATA_TRANSITION['observedIdentity']):
        fail('public-metadata-acceptance-required-owner-historical-scope-evidence-missing')
    if drift:
        fail('verified-next-baseline-preservation-mismatch')
    for obj, item in zip(objects, plan['objects']):
        if (obj['metadata']['uid'] != item['uid'] or public_object(obj) != public_object(item['intended'])
            or object_identity(obj) != kubernetes[item['reference']]):
            fail('verified-next-baseline-object-intent-mismatch')
    for role, facts in credentials.items():
        identity = credential_identity(role, instance.baseline)
        validate_lease(role, identity, facts['lease'], datetime.now(timezone.utc))
        if facts['identity'] != identity or instant(facts['lease']['expire_time']) < instant(plan['deadline']):
            fail('verified-next-baseline-credential-facts-mismatch')
    if any(health[label][path]['status'] != 200 for label in ('authority', 'inventory', 'custody') for path in ('/alive', '/health', '/ready')):
        fail('verified-next-baseline-requires-nine-passing-health-checks')
    output = Path(output)
    if output.exists():
        if (output / 'baseline-verification.json').exists() and read(output / 'baseline-verification.json').get('runId') == plan['runId']:
            retained = validate_baseline(output)
            if retained != state:
                fail('retained-next-maintenance-baseline-state-changed')
            manifest = read(output / 'baseline-verification.json')
            if (manifest.get('acceptedPublicMetadataDifference') != accepted
                or read(output / 'preserved-kubernetes-before.json') != kubernetes):
                fail('retained-next-maintenance-baseline-acceptance-or-preservation-changed')
            return output
        fail('next-maintenance-baseline-output-must-be-fresh')
    receipts = retained_receipts(instance.baseline, plan['beforeState'])
    for index, receipt in enumerate(state['audit'][len(plan['beforeState']['audit']):]):
        path = journal_directory / ('owner-operation-' + str(index) + '.json')
        journal = read(path)
        if (journal.get('phase') != 'Committed' or journal.get('receipt') != receipt
            or journal.get('operation') != plan['operations'][index]):
            fail('next-maintenance-baseline-requires-exact-committed-receipts')
        receipts.append({'operationId': receipt['operationId'], 'source': str(path), 'sourceSha256': sha(path.read_bytes()),
                         'receipt': receipt, 'receiptRetained': True})
    current_credentials = {role: {'credentialReference': facts['identity']['credentialReference'],
        'originalRecordedSecretIdentity': facts['identity']['secretIdentity'], 'lease': facts['lease'],
        'sourceJournal': str(BINDING / (role + '-credential-issuance.json')),
        'sourceResourceJournal': str(BINDING / 'resource-journal.json')} for role, facts in credentials.items()}
    files = {'authority-state.before.json': state, 'credentials.before.json': current_credentials,
             'key-metadata.before.json': key_metadata, 'runtime-reference.json': public_runtime, 'runtime-objects.json': objects,
             'preserved-kubernetes-before.json': kubernetes, 'preserved-openbao-before.json': openbao,
             'agents-register-before.json': read(instance.baseline / 'agents-register-before.json'),
             'original-public-evidence-recursive-fingerprints.json': preserved_originals(instance.baseline),
             'original-owner-receipts.json': receipts, 'health.before.json': health}
    if accepted is not None:
        files['accepted-public-metadata-difference.json'] = accepted
    sources = {str(path): sha(path.read_bytes()) for path in instance.baseline.glob('*.json')}
    parent_verification = instance.baseline / 'baseline-verification.json'
    if parent_verification.exists():
        sources.update(read(parent_verification)['sourceFingerprints'])
    # Retain renewal/runtime/completion journals alongside the plan and receipts,
    # including the completed run's legacy operations/ evidence layout.
    if accepted is not None or journal_directory != instance.evidence:
        sources.update({str(path): sha(path.read_bytes()) for path in journal_directory.glob('*.json')})
    plan_path = journal_directory / 'maintenance-plan.json'
    sources[str(plan_path)] = sha(plan_path.read_bytes())
    sources.update({source['source']: source['sourceSha256'] for source in receipts})
    if accepted is not None:
        sources.update(accepted['sourceFingerprints'])
    output.mkdir(mode=0o700)
    for name, value in files.items():
        durable_write(output / name, value, create_only=True)
    durable_write(output / 'baseline-verification.json', {'schema': 'EXT-SECRETS-1.verified-maintenance-baseline.v1',
        'phase': 'VerifiedNextMaintenanceBaseline', 'observedAt': timestamp(), 'runId': plan['runId'],
        'parentBaseline': str(instance.baseline), 'receiptCount': len(receipts), 'originalReceiptCount': 10,
        'acceptedPublicMetadataDifference': accepted,
        'sourceFingerprints': sources, 'files': {name: sha((output / name).read_bytes()) for name in files}, **FLAGS}, create_only=True)
    return output


def verify(instance, plan):
    validate_plan(instance, plan)
    preserved_originals(instance.baseline)
    _, state = administration_request(instance, 'GET', '/admin/state')
    if validate_progress(instance, plan, state) != len(plan['operations']):
        fail('owner-maintenance-incomplete')
    validate_scope(state, state, read(BINDING / 'successor-key-plan.json'), datetime.now(timezone.utc))
    if instance.metadata() != plan['keyMetadata']:
        fail('key-technical-metadata-changed')
    credentials = {}
    for role in ('data', 'metadata'):
        identity, lease = instance.credential(role)
        validate_lease(role, identity, lease, datetime.now(timezone.utc))
        if instant(lease['expire_time']) < instant(plan['deadline']):
            fail(role + '-credential-does-not-cover-maintained-owner-window')
        instance.write('credential-' + role + '.after.json', {'identity': identity, 'lease': lease})
        credentials[role] = {'identity': identity, 'lease': lease}
    objects = []
    for item in plan['objects']:
        kind, namespace, name = item['reference'].split('/')
        obj = kjson('get', kind, name, '-n', namespace)
        objects.append(obj)
        if obj['metadata']['uid'] != item['uid'] or public_object(obj) != public_object(item['intended']):
            fail('final-runtime-public-intent-mismatch')
        if kind == 'ConfigMap' and json.loads(obj['data']['runtime-reference.json']) != plan['publicRuntime']:
            fail('final-dependent-runtime-copy-incoherent')
    if read(RUNTIME) != plan['publicRuntime'] or len(null_paths(read(RUNTIME))) != 42:
        fail('final-runtime-file-or-null-inputs-changed')
    after = snapshot()
    changed = [key for key, value in plan['beforeKubernetes'].items() if after.get(key) != value]
    allowed = {item['reference'] for item in plan['objects']}
    if set(changed) - allowed or set(after) != set(plan['beforeKubernetes']):
        fail('out-of-scope-kubernetes-preservation-change')
    if instance.openbao_snapshot() != plan['beforeOpenbao']:
        fail('openbao-mount-auth-or-policy-changed')
    health = {'observedAt': timestamp(), **FLAGS}
    for label, service, port in [('authority', AUTHORITY, 38455), ('inventory', 'inventory', 38454), ('custody', APP, 38453)]:
        health[label] = http_health(service, port)
    instance.write('health.after.json', health)
    if any(health[label][path]['status'] != 200 for label in ('authority', 'inventory', 'custody') for path in ('/alive', '/health', '/ready')):
        fail('all-nine-isolated-health-checks-required')
    instance.write('preservation.json', {'observedAt': timestamp(), 'authorizedChangedReferences': sorted(changed),
        'originalReceiptCount': 10, 'originalReceiptsRetained': True, 'keyTechnicalMetadataUnchanged': True,
        'openbaoMountAuthAndPoliciesUnchanged': True, 'originalJournalsUnchanged': True, 'agentsRegisterUnchanged': True,
        'nullInputCount': 42, 'healthCheckCount': 9, **FLAGS})
    exposure_check(instance)
    # Health/rollout work can consume the remaining window. Re-read and recheck.
    _, final_state = administration_request(instance, 'GET', '/admin/state')
    if final_state != state or datetime.now(timezone.utc) >= instant(plan['deadline']):
        fail('owner-state-or-current-maintenance-deadline-changed-before-completion')
    validate_scope(final_state, final_state, read(BINDING / 'successor-key-plan.json'), datetime.now(timezone.utc))
    emit_verified_baseline(instance, plan, state=final_state, credentials=credentials, key_metadata=plan['keyMetadata'],
        public_runtime=read(RUNTIME), kubernetes=after, openbao=plan['beforeOpenbao'], objects=objects,
        health=health, output=instance.evidence / 'verified-next-baseline')
    exposure_check(instance)
    instance.write('completion.json', {'phase': 'MaintainedMetadataBoundDependencyUnavailable', 'observedAt': timestamp(),
        'ownerDeadline': plan['deadline'], 'revision': state['revision'], 'originalReceiptCount': 10,
        'profileVersion': state['profile']['version'], 'nullInputCount': 42, **FLAGS})


def maintain(instance):
    plan = inspect(instance)
    reconcile_current(instance, plan)
    for role in ('data', 'metadata'):
        require_owner_window(instance, plan)
        maintain_lease(instance, plan, role)
    maintain_owner_records(instance, plan)
    for item in plan['objects'][:2]:
        require_owner_window(instance, plan)
        update_public_object(instance, item)
    require_owner_window(instance, plan)
    update_runtime_file(instance, plan)
    for item in plan['objects'][2:]:
        require_owner_window(instance, plan)
        update_public_object(instance, item)
        name = item['reference'].rsplit('/', 1)[1]
        kube('rollout', 'status', 'deployment/' + name, '-n', NS, '--timeout=150s', timeout=155)
    verify(instance, plan)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command', choices=('inspect', 'maintain'))
    parser.add_argument('--evidence', type=Path)
    parser.add_argument('--baseline', type=Path, default=BASELINE)
    args = parser.parse_args()
    os.umask(0o077)
    evidence = args.evidence or EVIDENCE_ROOT / ('custody-maintenance-run-' + datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ'))
    if evidence.resolve() == args.baseline.resolve() or evidence.resolve() in (BINDING.resolve(), ORIGINAL_AUTHORITY.resolve()):
        parser.error('Use a new maintenance evidence directory; originals are read-only.')
    evidence.mkdir(parents=False, exist_ok=args.evidence is not None)
    print('evidence=' + str(evidence), flush=True)
    instance = None
    try:
        with journal_lock(Path('/tmp/ext-secrets-1-custody-maintenance.lock')), journal_lock(evidence / 'maintenance.lock'):
            instance = Maintenance(evidence, args.baseline)
            with forward('registry-idp', 38444, 8443), forward(AUTHORITY, 38452, 8080):
                if args.command == 'inspect':
                    inspect(instance)
                else:
                    maintain(instance)
            exposure_check(instance)
        print('isolated-maintenance-' + args.command + '-complete; dependency remains unavailable', flush=True)
        return 0
    except Exception as error:
        reason = str(error) if isinstance(error, RuntimeError) else 'sanitized-maintenance-failure'
        durable_write(evidence / ('blocker-' + uuid.uuid4().hex + '.json'), {'observedAt': timestamp(), 'reason': reason,
            'owner': 'Isolated authority, custody and provider credential administrators',
            'action': 'Reconcile the exact original operation and current facts. Expired/nonrenewable/capped credentials require a separately scoped successor credential, new Secret, verified consumer cutover and revocation; never replay unknown renewal or native effects.',
            **FLAGS})
        print('isolated-maintenance-blocked; see retained public blocker journal', flush=True)
        return 1
    finally:
        if instance is not None:
            instance.close()


if __name__ == '__main__':
    sys.exit(main())
