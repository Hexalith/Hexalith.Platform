#!/usr/bin/env python3
"""Inspect or maintain the exact completed isolated custody binding.

Public plans and per-effect journals precede owner operations, lease renewals and
conditional runtime updates. No installer, issuance, key write or contract case
is called. Unknown native outcomes are observed, never replayed.
"""
import argparse
import base64
from copy import deepcopy
from datetime import datetime, timedelta, timezone
import json
import math
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


def preserved_originals(baseline):
    expanded = baseline / 'original-public-evidence-recursive-fingerprints.json'
    fingerprints = read(expanded if expanded.exists() else baseline / 'original-public-evidence-fingerprints.json')
    if not fingerprints or any(sha(Path(name).read_bytes()) != expected for name, expected in fingerprints.items()):
        fail('original-public-evidence-changed')
    if sha(REGISTER.read_bytes()) != read(baseline / 'agents-register-before.json')['sha256']:
        fail('agents-register-changed')
    return fingerprints


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
    preserved_originals(instance.baseline)
    path = instance.evidence / 'maintenance-plan.json'
    if path.exists():
        plan = read(path)
        if plan.get('baseline') != str(instance.baseline) or plan.get('binding') != str(BINDING):
            fail('original-maintenance-plan-source-changed')
        return plan
    now = datetime.now(timezone.utc)
    original = read(instance.baseline / 'authority-state.before.json')
    status, state = administration_request(instance, 'GET', '/admin/state')
    if status != 200:
        fail('authority-state-unavailable')
    successors = read(BINDING / 'successor-key-plan.json')
    validate_scope(state, original, successors, now)
    receipts = read(instance.baseline / 'original-owner-receipts.json')
    if len(receipts) != 10 or len(state['audit']) != 10:
        fail('ten-original-owner-receipts-required')
    for source in receipts:
        journal = read(source['source'])
        if (journal.get('phase') != 'Committed' or journal['operation']['operationId'] != source['operationId']
            or journal['receipt'] not in state['audit']):
            fail('original-owner-receipt-not-retained')
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
    operations = []
    desired = deepcopy(state)
    desired['profile']['validUntil'] = wire_time(deadline)
    records = [('profile', 'PublishProfile', desired['profile'])]
    for collection, field, kind in (('hosts', 'host', 'SetInventoryHost'), ('callers', 'caller', 'SetCaller'), ('keyStatuses', 'keyStatus', 'SetKeyStatus')):
        for item in desired[collection]:
            item['validUntil'] = wire_time(deadline)
            records.append((field, kind, item))
    for index, (field, kind, value) in enumerate(records):
        operation = {'operationId': uuid.uuid5(uuid.UUID(run_id), str(index)).hex,
                     'expectedRevision': state['instanceId'] + ':' + str(state['generation'] + index), 'kind': kind,
                     'profile': None, 'caller': None, 'host': None, 'keyStatus': None}
        operation[field] = deepcopy(value)
        operations.append(operation)
    before_public = read(RUNTIME)
    if before_public['runtime']['profileValidUntil'] != state['profile']['validUntil']:
        fail('public-runtime-original-profile-deadline-mismatch')
    public = runtime_after(before_public, desired['profile'])
    if public['runtime']['hmacCredentialReference'] != credentials['data']['identity']['credentialReference']:
        fail('public-runtime-original-data-credential-reference-mismatch')
    custody_runtime = APP + '-runtime-' + read(BINDING / 'run.json')['runId'][:12]
    objects = []
    before_snapshot = snapshot()
    # Exact existing public copies plus only the two consumers that load subPath files.
    for kind, name in [('ConfigMap', 'ext-secrets-1-inventory-runtime'), ('ConfigMap', custody_runtime),
                       ('Deployment', 'inventory'), ('Deployment', APP)]:
        obj = kjson('get', kind, name, '-n', NS)
        reference = kind + '/' + NS + '/' + name
        if obj['metadata']['uid'] != read(instance.baseline / 'preserved-kubernetes-before.json')[reference]['uid']:
            fail('original-isolated-runtime-object-uid-changed')
        intended = deepcopy(obj)
        intended.pop('status', None)
        intended['metadata'].pop('managedFields', None)
        if kind == 'ConfigMap':
            if json.loads(obj['data']['runtime-reference.json']) != before_public:
                fail('dependent-public-runtime-copy-not-coherent')
            intended['data']['runtime-reference.json'] = json.dumps(public, sort_keys=True)
        else:
            host = next(item for item in state['hosts'] if item['inventoryAuthorityReference'] == (INVENTORY if name == 'inventory' else APP))
            if host['workloadReference'] != NS + '/Deployment/' + name + '@uid:' + obj['metadata']['uid']:
                fail('original-host-workload-uid-changed')
            runtime_volume = [item for item in obj['spec']['template']['spec']['volumes'] if item['name'] == 'runtime']
            if len(runtime_volume) != 1 or runtime_volume[0].get('configMap', {}).get('name') != ('ext-secrets-1-inventory-runtime' if name == 'inventory' else custody_runtime):
                fail('original-consumer-runtime-volume-changed')
            intended['spec']['template']['metadata'].setdefault('annotations', {})['hexalith.io/custody-maintenance'] = run_id
        objects.append({'reference': reference, 'uid': obj['metadata']['uid'], 'before': obj, 'intended': intended})
    plan = {'schema': 'EXT-SECRETS-1.custody-maintenance.v1', 'runId': run_id, 'preparedAt': now.isoformat(),
            'baseline': str(instance.baseline), 'binding': str(BINDING), 'deadline': wire_time(deadline),
            'beforeState': state, 'operations': operations, 'credentials': credentials, 'keyMetadata': metadata,
            'beforePublicRuntime': before_public, 'publicRuntime': public, 'objects': objects,
            'beforeKubernetes': before_snapshot, 'beforeOpenbao': instance.openbao_snapshot(), **FLAGS}
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
            expected[collection][matches[0]] = deepcopy(record)
    return expected


def validate_progress(instance, plan, state):
    original = plan['beforeState']
    if state['audit'][:len(original['audit'])] != original['audit']:
        fail('ten-original-receipts-changed-or-removed')
    count = len(state['audit']) - len(original['audit'])
    if count < 0 or count > len(plan['operations']):
        fail('foreign-authority-audit-operation')
    for index, receipt in enumerate(state['audit'][len(original['audit']):]):
        operation = plan['operations'][index]
        journal_path = instance.evidence / ('owner-operation-' + str(index) + '.json')
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
    _, state = administration_request(instance, 'GET', '/admin/state')
    count = validate_progress(instance, plan, state)
    for index, operation in enumerate(plan['operations']):
        path = instance.evidence / ('owner-operation-' + str(index) + '.json')
        if index < count:
            journal = read(path)
            if journal.get('phase') != 'Committed':
                # Exact original POST is the provider's durable idempotency check.
                status, receipt = administration_request(instance, 'POST', '/admin/operations', operation)
                if status != 200 or receipt != state['audit'][10 + index] or not receipt_matches(receipt, operation):
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
    identity, before = instance.credential(role)
    if identity != plan['credentials'][role]['identity']:
        fail(role + '-original-credential-identity-changed')
    now = datetime.now(timezone.utc)
    cap = validate_lease(role, identity, before, now)
    deadline = instant(plan['deadline'])
    path = instance.evidence / ('renewal-' + role + '.json')
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
    increment = min(HORIZON_SECONDS, math.floor((cap - now).total_seconds()))
    if increment <= 0 or now + timedelta(seconds=increment) < deadline:
        fail(role + '-bounded-renewal-cannot-cover-owner-window')
    journal = {'phase': 'AlreadySufficient' if instant(before['expire_time']) >= deadline else 'Started',
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


def exposure_check(instance):
    # Credentials and their hashes are never persisted; test literal/base64 only in memory.
    paths = [path for path in instance.evidence.rglob('*') if path.is_file()] + [RUNTIME, Path(__file__)]
    matches = sum(any(value in path.read_bytes() or base64.b64encode(value) in path.read_bytes()
                      for value in instance.private_values if len(value) >= 16) for path in paths)
    instance.write('credential-exposure-check.json', {'passed': matches == 0, 'literalAndBase64Matches': matches,
                   'credentialValuesRecorded': False, 'credentialHashesRecorded': False, 'scannedFileCount': len(paths)})
    if matches:
        fail('credential-exposure-check-failed')


def verify(instance, plan):
    preserved_originals(instance.baseline)
    _, state = administration_request(instance, 'GET', '/admin/state')
    if validate_progress(instance, plan, state) != len(plan['operations']):
        fail('owner-maintenance-incomplete')
    if instance.metadata() != plan['keyMetadata']:
        fail('key-technical-metadata-changed')
    for role in ('data', 'metadata'):
        identity, lease = instance.credential(role)
        validate_lease(role, identity, lease, datetime.now(timezone.utc))
        if instant(lease['expire_time']) < instant(plan['deadline']):
            fail(role + '-credential-does-not-cover-maintained-owner-window')
        instance.write('credential-' + role + '.after.json', {'identity': identity, 'lease': lease})
    for item in plan['objects']:
        kind, namespace, name = item['reference'].split('/')
        obj = kjson('get', kind, name, '-n', namespace)
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
    instance.write('completion.json', {'phase': 'MaintainedMetadataBoundDependencyUnavailable', 'observedAt': timestamp(),
        'ownerDeadline': plan['deadline'], 'revision': state['revision'], 'originalReceiptCount': 10,
        'profileVersion': state['profile']['version'], 'nullInputCount': 42, **FLAGS})


def maintain(instance):
    plan = inspect(instance)
    if datetime.now(timezone.utc) >= instant(plan['deadline']):
        fail('original-maintenance-plan-window-expired-owner-reconciliation-required')
    _, state = administration_request(instance, 'GET', '/admin/state')
    validate_progress(instance, plan, state)
    # Reconcile every original prerequisite before any effect, also on recovery.
    for role in ('data', 'metadata'):
        instance.credential(role)
    if instance.metadata() != plan['keyMetadata'] or instance.openbao_snapshot() != plan['beforeOpenbao']:
        fail('original-maintenance-prerequisite-key-metadata-or-policies-changed')
    for role in ('data', 'metadata'):
        maintain_lease(instance, plan, role)
    maintain_owner_records(instance, plan)
    for item in plan['objects'][:2]:
        update_public_object(instance, item)
    update_runtime_file(instance, plan)
    for item in plan['objects'][2:]:
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
