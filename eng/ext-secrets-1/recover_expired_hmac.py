#!/usr/bin/env python3
"""Observe or apply the exact chat-approved expired-HMAC scope, with durable originals."""
import argparse
import base64
from contextlib import contextmanager, ExitStack
from copy import deepcopy
from datetime import datetime, timezone
import fcntl
import hashlib
import http.client
import importlib
import json
import os
from pathlib import Path
import re
import socket
import ssl
import subprocess
import traceback
import sys
import uuid

from prepare_expired_hmac_recovery import canonical, digest, utc

APPROVED = 'ef4a6896ed192137c4394cf5ef177ace1ce43ddf27a632731cf891f1c80cfbe3'
HELPER_COMMIT = '251bc9465459b9c28ce1d1738bfbb0525fb07293'
NS = 'ext-secrets-1-isolated'
AUTHORITY = 'ext-secrets-1-hmac-authority'
FLAGS = {'qualificationPassed': False, 'dependencyAvailable': False, 'contractOperations': 0,
         'providerDataReads': 0, 'keyWrites': 0, 'keyRemovals': 0}
LEASE_FIELDS = ('accessor', 'creation_time', 'issue_time', 'expire_time', 'explicit_max_ttl', 'policies', 'renewable', 'ttl')
APPLICATION_REGISTRY = Path('/home/administrator/hexalith-secrets-evidence/ext-secrets-1/approved-recovery-originals')
NATIVE_ENROLLMENT_SHA256 = 'c4bf09a87927fb6b54ea94597def63b3142b578e05810d9c74692f2b2a6fbacc'
ORIGINAL_TENANT_HASHES = {
    '01M54GTX47NH1PRB00T2YS5048': '1a88385a9993c767ccc3ff9e2a069e022756d02153619ff701243f6bc1e54514',
    '01M54GTX479F4Y9A3MRW578C4R': '6033884321fa74be9c4507901c9aeec33d2441f185fd19bfb08d7c2caf7558be'}


class Blocked(RuntimeError):
    pass


def need(value, reason):
    if not value:
        raise Blocked(reason)


def now():
    return datetime.now(timezone.utc)


def load(path):
    def pairs(items):
        result = {}
        for key, value in items:
            need(key not in result, 'duplicate-public-field')
            result[key] = value
        return result
    path = Path(path)
    need(path.is_file() and not path.is_symlink() and path.stat().st_size <= 16 * 1024 * 1024, 'bounded-public-file-required')
    return json.loads(path.read_bytes(), object_pairs_hook=pairs)


def durable(path, value):
    raw = canonical(value)
    temporary = path.parent / ('.' + path.name + '-' + uuid.uuid4().hex)
    with temporary.open('xb') as output:
        output.write(raw); output.flush(); os.fsync(output.fileno())
    os.replace(temporary, path)
    descriptor = os.open(path.parent, os.O_DIRECTORY)
    try:
        os.fsync(descriptor)
    finally:
        os.close(descriptor)


def best_effort_evidence(path, value):
    try:
        durable(path, value)
    except Exception:
        pass  # Evidence failure must not suppress exact-known credential revocation.


def original_tenants(items):
    need(isinstance(items, list) and len(items) == 2 and all(isinstance(item, dict) for item in items)
         and {item.get('tenantId') for item in items} == set(ORIGINAL_TENANT_HASHES)
         and all(item.get('sourceStreamSha256') == ORIGINAL_TENANT_HASHES[item['tenantId']] for item in items),
         'exact-original-native-tenant-hashes-required')


def validate_scope(scope):
    original = deepcopy(scope); recorded = original.pop('scopeSha256', None)
    need(recorded == APPROVED == digest(canonical(original)), 'exact-approved-scope-required')
    need(scope['mutationsExecuted'] == 0 and len(scope['originalReceipts']) == 19
         and len(scope['nativeOwnerOperations']) == 9 and scope['nativeOwnerOperations'][-1]['kind'] == 'PublishProfile', 'approved-original-scope-malformed')
    for name, expected in scope['inputSha256'].items():
        path = Path(name)
        need(path.is_absolute() and path.is_file() and not path.is_symlink()
            and digest(path.read_bytes()) == expected, 'approved-original-input-journal-changed')


def window(scope, clock=now):
    need(utc(scope['proposedWindow']['executionNotBefore']) <= clock() < utc(scope['proposedWindow']['exclusiveDeadline']), 'approved-window-closed')


def pinned_input(scope, suffix):
    paths = [path for path in scope['inputSha256'] if path.endswith(suffix)]
    need(len(paths) == 1, 'exact-pinned-input-required')
    path = Path(paths[0]); need(digest(path.read_bytes()) == scope['inputSha256'][paths[0]], 'pinned-public-input-changed')
    return load(path)


def progressed_state(base, state, operations):
    """Compare the complete coherent native state, including collection order."""
    suffix = state['audit'][len(base['audit']):]
    need(state['audit'][:len(base['audit'])] == base['audit'] and len(suffix) <= len(operations), 'native-original-receipt-continuity-failed')
    expected = deepcopy(base)
    for index, receipt in enumerate(suffix):
        operation = operations[index]
        need(receipt['operationId'] == operation['operationId'] and receipt['kind'] == operation['kind']
            and receipt['administratorId'] == 'ext-secrets-1-hmac-owner' and receipt['outcome'] == 'Applied'
            and receipt['previousRevision'] == operation['expectedRevision']
            and receipt['revision'] == base['instanceId'] + ':' + str(base['generation'] + index + 1), 'foreign-or-conflicting-native-receipt')
        expected['generation'] += 1
        expected['revision'] = receipt['revision']
        if operation['kind'] == 'PublishProfile':
            expected['profile'] = deepcopy(operation['profile'])
        else:
            collection, field, keys = {'SetInventoryHost': ('hosts', 'host', ('inventoryAuthorityReference',)),
                'SetCaller': ('callers', 'caller', ('callerId',)),
                'SetKeyStatus': ('keyStatuses', 'keyStatus', ('tenantId', 'purpose', 'version'))}[operation['kind']]
            item = operation[field]
            expected[collection] = [old for old in expected[collection] if any(old[key] != item[key] for key in keys)] + [deepcopy(item)]
        expected['audit'].append(receipt)
    need(expected == state, 'coherent-progressed-authority-state-mismatch')
    return suffix


def public_identity(value, uid):
    material = {key: item for key, item in value.items() if key not in ('metadata', 'status', 'kind', 'apiVersion')}
    material['metadata'] = {key: value['metadata'][key] for key in ('labels', 'annotations') if key in value['metadata']}
    return {'uid': uid, 'specSha256': digest(canonical(material))}


def metadata_record(path, scope):
    record = load(path)
    expected = {'schema', 'scopeSha256', 'secretName', 'credentialReference', 'secretIdentity', 'accessor', 'policy',
                'ownerIdentity', 'authenticatedOwnerChannelReference', 'provenance'}
    need(set(record) == expected and record['schema'] == 'EXT-SECRETS-1.owner-metadata-credential.v1'
        and record['scopeSha256'] == APPROVED, 'exact-public-metadata-record-required')
    need(isinstance(record['secretName'], str) and re.fullmatch(r'[a-z0-9](?:[-a-z0-9.]{0,251}[a-z0-9])?', record['secretName'])
        and record['credentialReference'] == NS + '/Secret/' + record['secretName'] + '#token', 'isolated-metadata-secret-reference-required')
    identity = record['secretIdentity']
    need(isinstance(identity, dict) and set(identity) == {'uid', 'resourceVersion'}
        and str(uuid.UUID(identity['uid'])) == identity['uid'] and re.fullmatch(r'[1-9][0-9]*', identity['resourceVersion']), 'exact-metadata-secret-uid-version-required')
    need(isinstance(record['accessor'], str) and re.fullmatch(r'[A-Za-z0-9_-]{8,128}', record['accessor'])
        and record['policy'] == scope['credentialBranches']['metadata']['original']['policy'], 'restricted-metadata-accessor-policy-required')
    provenance = record['provenance']
    need(isinstance(provenance, dict) and set(provenance) == {'sourceSystem', 'observationReference', 'observedAt'}, 'owner-metadata-provenance-required')
    for value in (record['ownerIdentity'], record['authenticatedOwnerChannelReference'], provenance['sourceSystem'], provenance['observationReference']):
        need(isinstance(value, str) and 0 < len(value) <= 2048 and not any(ord(char) < 32 for char in value), 'public-owner-provenance-reference-required')
    need(utc(provenance['observedAt']) <= now(), 'owner-provenance-observation-in-future')
    return record


class Recovery:
    def __init__(self, scope, directory, backend, clock=now, registry_root=APPLICATION_REGISTRY):
        validate_scope(scope)
        self.scope, self.directory, self.backend, self.clock = scope, Path(directory), backend, clock
        self.registry_root = Path(registry_root)
        self.directory.mkdir(exist_ok=True, parents=False)
        need(not self.directory.is_symlink(), 'run-directory-must-not-be-symlink')
        self.lock = (self.directory / 'run.lock').open('a+b')
        fcntl.flock(self.lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        path = self.directory / 'run.json'
        if path.exists():
            need(load(path)['approvedScopeSha256'] == APPROVED, 'original-run-scope-changed')
        else:
            durable(path, {'approvedScopeSha256': APPROVED, 'approvalSource': 'Explicit user scope approval recorded in the invoked spec',
                'startedAt': self.clock().isoformat(), **FLAGS})

    def close(self):
        self.backend.close()
        self.lock.close()

    @contextmanager
    def application_original(self):
        """One permanent scope lock and authoritative journal directory across all CLI paths."""
        self.registry_root.mkdir(parents=True, exist_ok=True)
        need(not self.registry_root.is_symlink(), 'application-registry-must-not-be-symlink')
        original = APPROVED + '-' + self.scope['runId']
        with (self.registry_root / (original + '.lock')).open('a+b') as lock:
            try:
                fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
            except BlockingIOError:
                raise Blocked('approved-application-original-already-running') from None
            path = self.registry_root / (original + '.json')
            intended = {'scopeSha256': APPROVED, 'runId': self.scope['runId'], 'journalDirectory': str(self.directory.resolve())}
            if path.exists():
                need(load(path) == intended, 'approved-original-bound-to-different-application-directory')
            else:
                durable(path, intended)
            yield

    def observe(self, groups=None):
        """All independent observations run even when another observation fails."""
        result = {'observedAt': self.clock().isoformat(), 'groupObservedAt': {}, 'facts': {}, 'unavailable': {}, **FLAGS}
        for group in groups or ('kubernetes', 'authority', 'administration', 'provider', 'enrollment', 'health'):
            try:
                result['groupObservedAt'][group] = self.clock().isoformat()
                result['facts'][group] = getattr(self.backend, 'observe_' + group)()
            except Exception as error:
                result['facts'][group] = None
                result['unavailable'][group] = 'authenticated-observation-unavailable; see exact requested public paths in source/scope'
                result.setdefault('failureLocations', {})[group] = {'type': type(error).__name__,
                    'frames': [{'file': Path(frame.filename).name, 'function': frame.name, 'line': frame.lineno}
                               for frame in traceback.extract_tb(error.__traceback__)]}
        result['finishedAt'] = self.clock().isoformat()
        durable(self.directory / ('observation-' + uuid.uuid4().hex + '.json'), result)
        return result

    def preservation(self, facts, decision_path):
        need(decision_path is not None, 'authentic-historical-preservation-decision-required')
        record = load(decision_path)
        current = facts['kubernetes']
        baseline = pinned_input(self.scope, '/provider-inventory-reconciliation-20261029TJ6KL9LyH/preservation-after.json')
        normalized = deepcopy(current['snapshot'])
        for reference in self.scope['installedResourcePreconditions']:
            path = self.directory / ('object-' + reference.replace('/', '_') + '.json')
            if path.exists():
                journal = load(path)
                need(self.backend.verify_planned_object(reference, journal, current['snapshot'].get(reference)), 'planned-resource-current-state-mismatch')
                normalized[reference] = baseline[reference]
        for role in ('data', 'metadata'):
            path = self.directory / ('credential-' + role + '.json')
            if path.exists():
                journal = load(path)
                if journal.get('phase') == 'Stored':
                    reference = 'Secret/' + NS + '/' + journal['selectedSecret']
                    need(reference not in baseline and current['snapshot'].get(reference) == journal['secretIdentity'], 'planned-successor-secret-identity-mismatch')
                    normalized.pop(reference)
        need(normalized == baseline, 'new-unaccepted-preservation-drift')
        need(record.get('schema') == 'EXT-SECRETS-1.expired-recovery-preservation-decision.v1'
            and record.get('scopeSha256') == APPROVED and record.get('ownerAccepted') is True
            and record.get('ownerIdentity') and record.get('authenticatedOwnerChannelReference')
            and record.get('currentSnapshotSha256') == digest(canonical(normalized))
            and record.get('currentRegisterSha256') == current['registerSha256']
            and record.get('originalRegisterSha256') == '5bf35ac2857664c0589966a10944cd66278af85242c7c1f08e31ae29aa40e8a2',
            'preservation-decision-does-not-bind-current-originals')
        need(record.get('acceptedDifferences') == self.scope['unresolvedPreservationDifferences'], 'exact-historical-differences-required')
        historical = record.get('historicalSecretWriteEvidence', {})
        reference = 'Secret/kubesphere-system/kubesphere-sa-token'
        need(historical.get('reference') == reference and historical.get('originalResourceVersion') == '36774577'
            and historical.get('currentIdentity') == current['snapshot'][reference]
            and historical.get('completeIntervalCovered') is True and historical.get('noUnacceptedSecretFieldChanges') is True
            and historical.get('sourceSystem') and historical.get('retentionInterval'), 'authentic-complete-secret-write-coverage-required')
        sources = historical.get('sourceFingerprints', {})
        need(bool(sources), 'historical-source-files-required')
        for path, expected in sources.items():
            need(Path(path).is_absolute() and digest(Path(path).read_bytes()) == expected, 'historical-source-fingerprint-mismatch')
        # Human-authenticated owner provenance is a required trust input, never created here.
        return digest(Path(decision_path).read_bytes())

    def admit(self, observation, preservation):
        validate_scope(self.scope)
        window(self.scope, self.clock)
        need(not observation['unavailable'], 'fresh-authenticated-observations-incomplete')
        facts = observation['facts']
        need(set(facts) == {'kubernetes', 'authority', 'administration', 'provider', 'enrollment', 'health'}, 'complete-current-observation-required')
        self.require_fresh(observation)
        state = facts['administration']
        self.require_health(facts['health'], state['profile'])
        need(state == facts['authority'] and state['audit'][:19] == self.scope['originalReceipts'], 'native-original-receipt-continuity-failed')
        base = pinned_input(self.scope, '/provider-inventory-reconciliation-20261029TJ6KL9LyH/authority-state.current.json')
        audit = progressed_state(base, state, self.scope['nativeOwnerOperations'])
        for index, receipt in enumerate(audit):
            operation = self.scope['nativeOwnerOperations'][index]
            need(receipt['operationId'] == operation['operationId'] and receipt['kind'] == operation['kind']
                and receipt['outcome'] == 'Applied' and receipt['previousRevision'] == operation['expectedRevision'], 'foreign-or-conflicting-native-receipt')
            # Audit echoes alone cannot certify the payload or administrator on resume.
            need(self.backend.administer(operation) == receipt, 'resumed-native-exact-original-payload-mismatch')
        need(facts['provider']['configurationSha256'] == self.scope['retainedProviderConfigurationSha256']
            and facts['provider']['metadataSha256'] == self.scope['retainedNativeMetadataSha256'], 'current-provider-metadata-or-configuration-changed')
        original_tenants(facts['enrollment'].get('tenantStreams'))
        need(facts['enrollment']['restrictedReadVerified'] and facts['enrollment']['originalHashesMatch']
             and all(item.get('completeNativeEnrollmentRead') is True for item in facts['enrollment']['tenantStreams']), 'current-native-enrollment-not-verified')
        for reference, expected in self.scope['installedResourcePreconditions'].items():
            # Planned objects can differ only after a corresponding run journal exists.
            journal = self.directory / ('object-' + reference.replace('/', '_') + '.json')
            if not journal.exists():
                need(facts['kubernetes']['snapshot'].get(reference) == expected, 'installed-target-identity-or-spec-changed')
            else:
                value = load(journal)
                need(self.backend.verify_planned_object(reference, value, facts['kubernetes']['snapshot'].get(reference)), 'resumed-resource-state-mismatch')
        for role, branch in self.scope['credentialBranches'].items():
            lease_observation = facts['provider']['leases'][role]
            need(lease_observation.get('classification') in ('Present', 'Absent'), 'original-accessor-state-unknown:' + role)
            if lease_observation['classification'] == 'Present':
                self.backend.verify_capabilities(role, branch['original']['accessor'])
        preservation_sha = self.preservation(facts, preservation)
        durable(self.directory / 'admission.json', {'scopeSha256': APPROVED, 'preservationDecisionSha256': preservation_sha,
            'observedFactsSha256': digest(canonical(facts)), 'admittedAt': self.clock().isoformat(), **FLAGS})
        self.require_fresh(observation)
        window(self.scope, self.clock)
        return facts

    def require_fresh(self, observation):
        need(set(observation['groupObservedAt']) == set(observation['facts'])
             and all(0 <= (self.clock() - utc(value)).total_seconds() <= 30
                     for value in observation['groupObservedAt'].values()), 'fresh-observations-expired')

    def require_health(self, health, profile):
        need(isinstance(health, dict) and set(health) == {'authority', 'inventory', 'custody'}, 'complete-recovery-health-required')
        profile_current = (utc(profile['notBefore']) <= self.clock() < utc(profile['validUntil'])
                           and profile.get('revoked') is False)
        for name in ('authority', 'inventory', 'custody'):
            endpoints = health[name]
            need(isinstance(endpoints, dict) and set(endpoints) == {'/alive', '/health', '/ready'}, 'complete-recovery-health-required')
            need(all(isinstance(endpoints[path], dict) for path in endpoints), 'complete-recovery-health-required')
            need(endpoints['/alive'].get('status') == 200 and endpoints['/health'].get('status') == 200,
                 'recovery-liveness-or-health-failed:' + name)
            body = endpoints['/health'].get('body')
            need(isinstance(body, dict) and body.get('qualificationPassed') is False
                 and body.get('dependencyAvailable') is False, 'recovery-health-availability-mismatch:' + name)
            expected = 200 if name == 'authority' or profile_current else 503
            need(endpoints['/ready'].get('status') == expected, 'recovery-readiness-mismatch:' + name)

    def validate_selected(self, role, journal):
        original = journal['original']
        accessor = journal['accessor'] if journal['phase'] == 'Stored' else original['accessor']
        observed = self.backend.lease(accessor)
        need(observed.get('classification') == 'Present', 'selected-credential-not-current:' + role)
        lease = observed['lease']
        need(lease['accessor'] == accessor and lease['policies'] == [original['policy']] and lease['ttl'] > 0
            and lease['explicit_max_ttl'] == 604800
            and utc(lease['expire_time']) >= utc(self.scope['proposedWindow']['exclusiveDeadline'])
            and utc(lease['expire_time']).timestamp() <= utc(lease['issue_time']).timestamp() + 604800,
            'selected-credential-does-not-cover-exact-scope:' + role)
        if journal['phase'] != 'Stored':
            need(lease['creation_time'] == original['creationTime'] and lease['issue_time'] == original['issueTime'], 'selected-original-lease-identity-changed')
        self.backend.verify_selected_secret(journal, accessor)

    def authority_operation(self, index, operation, observation=None):
        path = self.directory / ('authority-' + str(index) + '.json')
        journal = load(path) if path.exists() else {'phase': 'Prepared', 'operation': operation}
        need(journal['operation'] == operation, 'original-owner-payload-changed')
        state = self.backend.observe_administration()
        base = pinned_input(self.scope, '/provider-inventory-reconciliation-20261029TJ6KL9LyH/authority-state.current.json')
        progressed_state(base, state, self.scope['nativeOwnerOperations'] + self.scope['failureContainment']['proposedDenyOperations'])
        matches = [receipt for receipt in state['audit'] if receipt['operationId'] == operation['operationId']]
        if matches:
            receipt = matches[0]
            need(receipt['outcome'] == 'Applied' and receipt['kind'] == operation['kind']
                and receipt['previousRevision'] == operation['expectedRevision'], 'original-owner-outcome-conflicts')
            # Reuse native exact-original idempotency to verify administrator and full payload hash.
            window(self.scope, self.clock)
            if operation['kind'] == 'PublishProfile':
                need(observation is not None, 'profile-publication-current-observation-required')
                self.require_fresh(observation)
            verified = self.backend.administer(operation)
            need(verified == receipt, 'native-exact-original-receipt-mismatch')
        else:
            need(state['revision'] == operation['expectedRevision'], 'expected-authority-revision-changed')
            window(self.scope, self.clock)
            journal['phase'] = 'Started'; durable(path, journal)
            window(self.scope, self.clock)
            if operation['kind'] == 'PublishProfile':
                need(observation is not None, 'profile-publication-current-observation-required')
                self.require_fresh(observation)
            try:
                receipt = self.backend.administer(operation)
            except Exception:
                journal['phase'] = 'Unknown'; durable(path, journal)
                raise Blocked('owner-original-uncertain-exact-native-reconciliation-required') from None
            need(receipt['operationId'] == operation['operationId'] and receipt['previousRevision'] == operation['expectedRevision']
                and receipt['outcome'] == 'Applied', 'original-owner-receipt-invalid')
        journal.update(phase='Committed', receipt=receipt); durable(path, journal)
        return receipt

    def credential(self, role, facts):
        branch = self.scope['credentialBranches'][role]
        observation = facts['provider']['leases'][role]
        need(observation.get('classification') in ('Present', 'Absent'), 'fresh-original-lease-state-required:' + role)
        lease = observation.get('lease'); original = branch['original']
        if lease is not None:
            need(lease['accessor'] == original['accessor'] and lease['policies'] == [original['policy']]
                and lease['creation_time'] == original['creationTime'] and lease['issue_time'] == original['issueTime']
                and lease['explicit_max_ttl'] == original['explicitMaximumTtlSeconds'], 'original-credential-identity-or-policy-changed')
        deadline = utc(self.scope['proposedWindow']['exclusiveDeadline'])
        path = self.directory / ('credential-' + role + '.json')
        if path.exists():
            journal = load(path)
            need(journal['original'] == original, 'original-credential-journal-changed')
            need(journal['phase'] in ('Reused', 'Renewed', 'Stored'), 'unknown-issuance-or-renewal-never-repeated')
            self.validate_selected(role, journal)
            return journal
        if lease is not None and utc(lease['expire_time']) >= deadline and lease['ttl'] > 0:
            result = {'phase': 'Reused', 'original': original, 'selectedSecret': original['secretName'], 'credentialReference': original['credentialReference']}
        else:
            renewable = (lease is not None and lease['ttl'] > 0 and utc(lease['expire_time']) > self.clock()
                and lease['renewable'] is True and utc(original['issueTime']).timestamp()
                + original['explicitMaximumTtlSeconds'] >= deadline.timestamp())
            if not renewable:
                need(observation['classification'] == 'Absent' or lease is not None, 'authoritative-replacement-condition-required')
                window(self.scope, self.clock)
                result = self.backend.issue_successor(role, branch, path, lambda: window(self.scope, self.clock))
                self.validate_selected(role, result)
                return result
            result = {'phase': 'RenewalStarted', 'original': original}; durable(path, result)
            window(self.scope, self.clock)
            try:
                self.backend.renew(role, original)
                after = self.backend.lease(original['accessor'])
                need(after['status'] == 200 and after['lease']['accessor'] == original['accessor']
                    and after['lease']['policies'] == [original['policy']]
                    and utc(after['lease']['expire_time']) >= deadline
                    and utc(after['lease']['expire_time']).timestamp() <= utc(original['issueTime']).timestamp() + original['explicitMaximumTtlSeconds'],
                    'renewed-original-does-not-cover-window')
            except Exception:
                result['phase'] = 'RenewalUnknown'; durable(path, result)
                raise Blocked('unknown-renewal-observation-only-required') from None
            result.update(phase='Renewed', selectedSecret=original['secretName'], credentialReference=original['credentialReference'])
        durable(path, result)
        self.validate_selected(role, result)
        return result

    def apply(self, preservation):
        with self.application_original():
            return self._apply(preservation)

    def _apply(self, preservation):
        credentials = {}
        try:
            need(not (self.directory / 'containment.json').exists(), 'failed-recovery-containment-must-remain-denied')
            observation = self.observe()
            facts = self.admit(observation, preservation)
            for role in ('metadata', 'data'):
                credentials[role] = self.credential(role, facts)
            public = deepcopy(self.scope['proposedRuntimeForReuseOrRenew'])
            public['runtime']['hmacCredentialReference'] = credentials['data']['credentialReference']
            for reference in self.scope['installedResourcePreconditions']:
                if reference.startswith(('ConfigMap/', 'Deployment/')):
                    window(self.scope, self.clock)
                    self.backend.stage(reference, public, credentials, self.directory)
            self.backend.rollout()
            self.require_health(self.backend.observe_health(), self.backend.observe_administration()['profile'])
            for index, operation in enumerate(self.scope['nativeOwnerOperations']):
                current_observation = None
                if operation['kind'] == 'PublishProfile':
                    # Activation uses newly observed facts, never the pre-rollout admission.
                    current_observation = self.observe()
                    current = self.admit(current_observation, preservation)
                    need(len(current['administration']['audit']) in (27, 28), 'profile-activation-requires-exact-eight-prior-operations')
                    for role, credential in credentials.items():
                        self.validate_selected(role, credential)
                    self.require_fresh(current_observation)
                self.authority_operation(index, operation, current_observation)
            window(self.scope, self.clock)
            self.backend.verify_bound(public)
            for role, credential in credentials.items():
                if credential['phase'] == 'Stored':
                    window(self.scope, self.clock)
                    self.backend.revoke_original(role, credential, self.directory)
            provider_observed = self.clock()
            verified = self.backend.observe_provider()
            need(verified['configurationSha256'] == self.scope['retainedProviderConfigurationSha256']
                and verified['metadataSha256'] == self.scope['retainedNativeMetadataSha256'], 'post-recovery-provider-preservation-failed')
            health_observed = self.clock()
            health = self.backend.observe_health()
            self.require_health(health, self.backend.observe_administration()['profile'])
            need(all(0 <= (self.clock() - instant).total_seconds() <= 30 for instant in (provider_observed, health_observed)),
                 'final-provider-or-health-observation-expired')
            window(self.scope, self.clock)
            durable(self.directory / 'completion.json', {'status': 'BoundedRecoveryCompleted', 'completedAt': self.clock().isoformat(), **FLAGS})
        except Exception:
            reconciliation = {'authorityContainment': 'Unverified', 'successorCleanup': {}, **FLAGS}
            try:
                self.contain_failure()
                reconciliation['authorityContainment'] = 'Reconciled'
            except Exception:
                reconciliation['authorityContainment'] = 'AuthenticatedExactOriginalReconciliationRequired'
            for role in ('metadata', 'data'):
                try:
                    path = self.directory / ('credential-' + role + '.json')
                    if role not in credentials and path.exists() and load(path).get('phase') == 'Stored':
                        credentials[role] = load(path)
                    credential = credentials.get(role)
                    if credential and credential['phase'] == 'Stored':
                        window(self.scope, self.clock)
                        self.backend.cleanup_unused_successor(role, credential, self.directory)
                        reconciliation['successorCleanup'][role] = 'AttemptedExactKnownSafeCleanup'
                except Exception:
                    reconciliation['successorCleanup'][role] = 'ExactKnownSuccessorReconciliationRequired'
            best_effort_evidence(self.directory / 'failure-reconciliation.json', reconciliation)
            raise

    def contain_failure(self):
        current = self.backend.observe_administration()
        base = pinned_input(self.scope, '/provider-inventory-reconciliation-20261029TJ6KL9LyH/authority-state.current.json')
        operations = self.scope['nativeOwnerOperations'] + self.scope['failureContainment']['proposedDenyOperations']
        audit = progressed_state(base, current, operations)
        if len(audit) < len(self.scope['nativeOwnerOperations']):
            return  # The retained profile is still expired; no caller activation occurred.
        path = self.directory / 'containment.json'
        durable(path, {'phase': 'Started', 'scopeSha256': APPROVED, 'observedRevision': current['revision'], **FLAGS})
        for operation, receipt in zip(operations, audit):
            window(self.scope, self.clock)
            need(self.backend.administer(operation) == receipt, 'containment-exact-original-payload-mismatch')
        for index, operation in enumerate(self.scope['failureContainment']['proposedDenyOperations']):
            self.authority_operation('deny-' + str(index), operation)
        durable(path, {'phase': 'CommittedDenied', 'scopeSha256': APPROVED, **FLAGS})


class LiveBackend:
    """Only the pinned native read/admin/provider protocols and named resource mutations."""
    def __init__(self, scope, directory, helper_root, metadata_credential_record=None):
        self.scope, self.evidence = scope, Path(directory)
        self.metadata_credential_record = Path(metadata_credential_record) if metadata_credential_record is not None else None
        self.private = []
        repository = Path(helper_root).resolve()
        def git(*args):
            return subprocess.check_output(['git', '-C', str(repository), *args], stderr=subprocess.DEVNULL)
        need(git('rev-parse', HELPER_COMMIT + '^{commit}').decode().strip() == HELPER_COMMIT, 'reviewed-helper-commit-unavailable')
        helper = self.evidence / 'immutable-helper-source/eng/ext-secrets-1'
        helper.mkdir(parents=True, exist_ok=True)
        names = [name for name in git('ls-tree', '-r', '--name-only', HELPER_COMMIT, '--', 'eng/ext-secrets-1').decode().splitlines() if name.endswith('.py') and name.count('/') == 2]
        manifest = {}
        for name in names:
            data = git('show', HELPER_COMMIT + ':' + name)
            destination = helper / Path(name).name
            if destination.exists(): need(destination.read_bytes() == data, 'immutable-helper-copy-changed')
            else:
                with destination.open('xb') as output: output.write(data)
            manifest[name] = digest(data)
        for path, expected in scope['inputSha256'].items():
            if str(path).startswith(str(repository) + '/'):
                relative = str(Path(path).relative_to(repository))
                need(digest(git('show', HELPER_COMMIT + ':' + relative)) == expected, 'reviewed-native-protocol-does-not-match-approved-source')
        manifest = {'commit': HELPER_COMMIT, 'files': manifest}
        pin = self.evidence / 'native-helper-pins.json'
        if pin.exists():
            need(load(pin) == manifest, 'pinned-helper-closure-changed')
        else:
            durable(pin, manifest)
        sys.path.insert(0, str(helper)); sys.dont_write_bytecode = True
        self.k = importlib.import_module('install_isolated_registry')
        self.h = importlib.import_module('install_hmac_authority')
        self.p = importlib.import_module('provision_operational_hmac')
        self.v = importlib.import_module('verify_inventory_registry_reads')
        self.i = importlib.import_module('install_current_inventory')
        self.b = importlib.import_module('bind_isolated_custody')
        need(all(Path(module.__file__).resolve().parent == helper for module in (self.k, self.h, self.p, self.v, self.i, self.b)), 'pinned-helper-import-location-mismatch')

    def close(self):
        for value in self.private:
            value[:] = b'\0' * len(value)
        self.private.clear()

    def write(self, name, value):
        durable(self.evidence / name, value)

    def secret_field(self, name, field):
        allowed = {('ext-secrets-1-registry-idp-tls', 'ca\\.crt'), (AUTHORITY + '-owner-identity', 'client-secret'),
            (AUTHORITY + '-administrator-channel-token', 'token'), ('ext-secrets-1-registry-oidc-inventory', 'client-id'),
            ('ext-secrets-1-registry-oidc-inventory', 'client-secret'), ('ext-secrets-1-inventory-dapr-token', 'token')}
        need((name, field) in allowed, 'unapproved-credential-field')
        value = bytearray(base64.b64decode(self.k.kube('get', 'secret', name, '-n', NS, '-o', 'jsonpath={.data.' + field + '}').stdout, validate=True))
        need(bool(value), 'intended-credential-unavailable'); self.private.append(value)
        return value.decode()

    def bao(self, *args, data=None, allowed=(0,)):
        if not getattr(self, 'token', None):
            self.token = bytearray(base64.b64decode(self.k.kube('get', 'secret', 'openbao-operator-credentials', '-n', 'openbao', '-o', 'jsonpath={.data.operator-token}').stdout, validate=True))
            need(bool(self.token), 'authorized-provisioning-token-unavailable'); self.private.append(self.token)
        return self.k.Installer.bao(self, *args, data=data, allowed=allowed)

    def observe_kubernetes(self):
        register = Path('/home/administrator/projects/hexalith/agents/_bmad-output/planning-artifacts/external-dependency-register.md')
        return {'snapshot': self.k.snapshot(), 'registerSha256': digest(register.read_bytes())}

    def observe_authority(self):
        raw = self.k.kube('exec', '-n', NS, AUTHORITY + '-0', '-c', 'authority', '--', 'cat', '/var/lib/hmac-authority/authority.json').stdout
        value = json.loads(raw); state_raw = raw.split(b'"state":', 1)[1][:-1]
        need(digest(state_raw).upper() == value['sha256'], 'native-authority-checksum-invalid')
        return value['state']

    def admin(self, method, body=None):
        with self.p.forward('service/registry-idp', NS, 8443) as idp_port, self.p.forward('service/' + AUTHORITY, NS, 8080) as port:
            idp = self.h.Idp(self, idp_port)
            token, _ = idp.call('POST', '/realms/' + AUTHORITY + '/protocol/openid-connect/token',
                {'grant_type': 'client_credentials', 'client_id': 'ext-secrets-1-hmac-owner',
                 'client_secret': self.secret_field(AUTHORITY + '-owner-identity', 'client-secret')}, form=True)
            channel = self.secret_field(AUTHORITY + '-administrator-channel-token', 'token')
            connection = http.client.HTTPConnection('127.0.0.1', port, timeout=20)
            try:
                if method == 'POST': window(self.scope)
                connection.request(method, '/admin/state' if method == 'GET' else '/admin/operations',
                    canonical(body) if body is not None else None,
                    {'Authorization': 'Bearer ' + token['access_token'], 'authority-admin-token': channel, 'Content-Type': 'application/json'})
                response = connection.getresponse(); raw = response.read(16 * 1024 * 1024 + 1)
                need(response.status == 200 and len(raw) <= 16 * 1024 * 1024, 'native-owner-request-denied-or-failed')
                return json.loads(raw)
            finally:
                connection.close()

    def observe_administration(self):
        return self.admin('GET')

    def administer(self, operation):
        return self.admin('POST', operation)

    @contextmanager
    def provider(self, default_credential=None):
        token = default_credential
        if token is None:
            raw = self.k.kube('get', 'secret', 'openbao-operator-credentials', '-n', 'openbao', '-o', 'jsonpath={.data.operator-token}').stdout
            token = bytearray(base64.b64decode(raw, validate=True)); need(bool(token), 'authorized-provisioning-token-unavailable')
            self.private.append(token)
        ca = self.k.kjson('get', 'configmap', 'ext-secrets-1-openbao-ca', '-n', NS)['data']['ca.pem']
        context = ssl.create_default_context(cadata=ca)
        with self.p.forward('service/hexalith-keys-active', 'openbao', 8200) as port:
            def request(method, path, body=None, credential=None):
                need(not path.startswith('/v1/ext-secrets-1-isolated/data/'), 'provider-data-route-forbidden')
                connection = http.client.HTTPSConnection(self.k.BAO_NAME, 8200, timeout=20, context=context)
                try:
                    connection.sock = context.wrap_socket(socket.create_connection(('127.0.0.1', port), timeout=20), server_hostname=self.k.BAO_NAME)
                    connection.request(method, path, canonical(body) if body is not None else None,
                        {'X-Vault-Token': bytes(credential or token).decode(), 'Content-Type': 'application/json', 'Connection': 'close'})
                    response = connection.getresponse(); data = response.read(2 * 1024 * 1024 + 1)
                    need(len(data) <= 2 * 1024 * 1024, 'provider-response-exceeds-bound')
                    try:
                        value = json.loads(data) if data else {}
                    except ValueError:
                        value = {'publicResponseShapeInvalid': True}
                    return response.status, value if isinstance(value, dict) else {'publicResponseShapeInvalid': True}
                finally:
                    connection.close()
            yield request

    @staticmethod
    def public_lease(status, value):
        errors = value.get('errors')
        canonical_absence = errors in (['invalid accessor'], ['1 error occurred:\n\t* invalid accessor\n\n'])
        classification = 'Present' if status == 200 and isinstance(value.get('data'), dict) else (
            'Absent' if status == 400 and canonical_absence else 'Unknown')
        public_error = ('CanonicalInvalidAccessor' if canonical_absence else
                        'PermissionDenied' if status == 403 else 'UnclassifiedNativeError' if status != 200 else None)
        return {'status': status, 'classification': classification, 'nativeErrorClassification': public_error,
                'lease': {key: value['data'].get(key) for key in LEASE_FIELDS}
                if status == 200 and isinstance(value.get('data'), dict) else None}

    def lease(self, accessor):
        with self.provider() as request:
            return self.public_lease(*request('POST', '/v1/auth/token/lookup-accessor', {'accessor': accessor}))

    def observe_provider(self):
        if not getattr(self, 'metadata_credential_record', None):
            return self._observe_operator_provider()
        try:
            result = self._observe_operator_provider()
        except Exception:
            result = {'leases': {role: {'status': None, 'classification': 'Unknown', 'lease': None} for role in ('data', 'metadata')},
                'capabilities': {}, 'configuration': None, 'configurationSha256': None, 'operatorObservationUnavailable': True}
        try:
            result.update(self.owner_metadata())
        except Exception:
            references = [item['original']['secretReference'] for item in self.scope['keyPlan']]
            result.update(metadata={key: None for key in references}, metadataHttpStatuses={key: None for key in references},
                          metadataSha256=None, ownerMetadataCredentialVerified=False)
        return result

    def _observe_operator_provider(self):
        with self.provider() as native_request:
            def request(*args, **kwargs):
                try:
                    return native_request(*args, **kwargs)
                except Exception:
                    return None, {'publicObservationUnavailable': True}
            leases, capabilities, metadata, metadata_statuses = {}, {}, {}, {}
            for role, branch in self.scope['credentialBranches'].items():
                identity = branch['original']
                leases[role] = self.public_lease(*request('POST', '/v1/auth/token/lookup-accessor', {'accessor': identity['accessor']}))
                roots = ['ext-secrets-1-isolated/' + ('data/' if role == 'data' else 'metadata/') + item['original']['secretReference'] for item in self.scope['keyPlan']]
                paths = roots + ['secret/data/*', 'sys/policies/acl/*', 'ext-secrets-1-isolated/' + ('metadata/*' if role == 'data' else 'data/*')]
                status, value = request('POST', '/v1/sys/capabilities-accessor', {'accessor': identity['accessor'], 'paths': paths})
                capabilities[role] = {'status': status, 'paths': value.get('data') if status == 200 else None}
            for item in self.scope['keyPlan']:
                path = item['original']['secretReference']
                status, value = (None, {}) if getattr(self, 'metadata_credential_record', None) else request('GET', '/v1/ext-secrets-1-isolated/metadata/' + path)
                metadata_statuses[path] = status
                metadata[path] = value.get('data') if status == 200 else None
            status_mounts, mounts = request('GET', '/v1/sys/mounts')
            status_auth, auth = request('GET', '/v1/sys/auth')
            status_names, names = request('LIST', '/v1/sys/policies/acl')
            policies = {}
            complete = status_mounts == status_auth == status_names == 200
            for name in names.get('data', {}).get('keys', []) if status_names == 200 else []:
                if name == 'root': policies[name] = 'builtin-root'; continue
                status, value = request('GET', '/v1/sys/policies/acl/' + name)
                if status != 200:
                    complete = False; policies[name] = None; continue
                # Match native `bao policy read` bytes including its final newline.
                policies[name] = digest((value['data']['policy'].rstrip('\n') + '\n').encode())
            configuration = {'mounts': {key: digest(canonical(value)) for key, value in mounts.get('data', {}).items()} if status_mounts == 200 else None,
                'authMounts': {key: digest(canonical(value)) for key, value in auth.get('data', {}).items()} if status_auth == 200 else None, 'policies': policies}
            native_configuration = None
            try:
                native_configuration = self.k.Installer.openbao_snapshot(self)
            except Exception:
                pass
            return {'leases': leases, 'capabilities': capabilities, 'metadata': metadata, 'metadataHttpStatuses': metadata_statuses,
                'metadataSha256': digest(canonical(metadata)) if all(value is not None for value in metadata.values()) else None,
                'httpConfiguration': configuration, 'configurationHttpStatuses': {'mounts': status_mounts, 'auth': status_auth, 'policyList': status_names},
                'configuration': native_configuration,
                'configurationSha256': digest(canonical(native_configuration)) if native_configuration is not None else None,
                'configurationCanonicalization': 'Pinned original Installer.openbao_snapshot; native CLI public formats match original preservation hashes.'}

    def verify_capabilities(self, role, accessor, credential=None):
        roots = ['ext-secrets-1-isolated/' + ('data/' if role == 'data' else 'metadata/') + item['original']['secretReference'] for item in self.scope['keyPlan']]
        denied = ['secret/data/*', 'sys/policies/acl/*', 'ext-secrets-1-isolated/' + ('metadata/*' if role == 'data' else 'data/*')]
        if credential is None:
            with self.provider() as request:
                status, value = request('POST', '/v1/sys/capabilities-accessor', {'accessor': accessor, 'paths': roots + denied})
            if status == 403:
                original = self.scope['credentialBranches'][role]['original']
                need(accessor == original['accessor'], 'selected-credential-required-for-self-scope-check')
                credential = self.i.read_pinned_credential_secret(original['secretName'], original['secretIdentity']); self.private.append(credential)
        if credential is not None:
            with self.provider(default_credential=credential) as request:
                status, identity = request('GET', '/v1/auth/token/lookup-self')
                need(status == 200 and identity.get('data', {}).get('accessor') == accessor, 'self-scope-token-accessor-mismatch')
                status, value = request('POST', '/v1/sys/capabilities-self', {'paths': roots + denied})
        actual = value.get('data', {})
        need(status == 200 and all(actual.get(path) == ['deny'] for path in denied)
            and all('read' in actual.get(path, []) and not set(actual[path]) - {'read', 'list'} for path in roots), 'effective-credential-scopes-not-exact')

    def owner_metadata(self):
        record = metadata_record(self.metadata_credential_record, self.scope)
        pin_path = self.evidence / 'owner-metadata-record-pin.json'
        pin = {'recordPath': str(self.metadata_credential_record.resolve()), 'sha256': digest(canonical(record)),
               'fingerprintFormat': 'canonical parsed public JSON'}
        if pin_path.exists(): need(load(pin_path) == pin, 'owner-metadata-record-changed-within-run')
        token = self.i.read_pinned_credential_secret(record['secretName'], record['secretIdentity']); self.private.append(token)
        try:
            with self.provider(default_credential=token) as request:
                status, value = request('GET', '/v1/auth/token/lookup-self')
                lease = value.get('data', {})
                need(status == 200 and lease.get('accessor') == record['accessor'] and lease.get('policies') == [record['policy']]
                    and type(lease.get('ttl')) is int and lease['ttl'] > 0 and utc(lease['expire_time']) > now()
                    and type(lease.get('explicit_max_ttl')) is int and 0 < lease['explicit_max_ttl'] <= 604800
                    and utc(lease['expire_time']).timestamp() <= utc(lease['issue_time']).timestamp() + lease['explicit_max_ttl'],
                    'owner-metadata-credential-not-current-and-restricted')
            self.verify_capabilities('metadata', record['accessor'], credential=token)
            durable(pin_path, pin)
            metadata, statuses = {}, {}
            with self.provider(default_credential=token) as request:
                for item in self.scope['keyPlan']:
                    relative = item['original']['secretReference']
                    try:
                        status, value = request('GET', '/v1/ext-secrets-1-isolated/metadata/' + relative)
                    except Exception:
                        status, value = None, {}
                    statuses[relative] = status
                    metadata[relative] = value.get('data') if status == 200 else None
            return {'metadata': metadata, 'metadataHttpStatuses': statuses,
                'metadataSha256': digest(canonical(metadata)) if all(value is not None for value in metadata.values()) else None,
                'ownerMetadataCredentialVerified': True, 'ownerMetadataRecordSha256': pin['sha256'],
                'ownerMetadataCredentialReference': record['credentialReference'],
                'provenanceTrust': 'Owner-returned authenticated provenance is an external trust input; native current identity/scopes are verified here.'}
        finally:
            token[:] = b'\0' * len(token)

    def issue_successor(self, role, branch, path, before_effect):
        proposal = branch['replace']; name = proposal['newSecretName']
        need(self.i.secret_identity(name) is None, 'successor-secret-name-already-exists')
        journal = {'phase': 'IssuanceStarted', 'original': branch['original'], 'request': proposal['nativeRequest'],
            'selectedSecret': name, 'credentialReference': proposal['newCredentialReference']}
        durable(path, journal)
        token = None
        try:
            with self.provider() as request:
                before_effect()
                status, value = request('POST', proposal['nativeRequest']['path'], proposal['nativeRequest']['body'])
                need(status == 200 and value.get('auth', {}).get('accessor') and value['auth'].get('client_token'), 'native-issuance-outcome-not-established')
                token = bytearray(value['auth']['client_token'].encode()); self.private.append(token)
                journal.update(phase='Issued', accessor=value['auth']['accessor']); durable(path, journal)
            lease = self.lease(journal['accessor'])
            need(lease['status'] == 200 and lease['lease']['policies'] == [branch['original']['policy']]
                and lease['lease']['explicit_max_ttl'] == 604800 and lease['lease']['renewable'] is True
                and utc(lease['lease']['expire_time']) >= utc(self.scope['proposedWindow']['exclusiveDeadline']), 'successor-lease-or-policy-invalid')
            self.verify_capabilities(role, journal['accessor'], credential=token)
            before_effect(); journal['phase'] = 'PublicationStarted'; durable(path, journal)
            secret = {'apiVersion': 'v1', 'kind': 'Secret', 'metadata': {'namespace': NS, 'name': name},
                'type': 'Opaque', 'data': {'token': base64.b64encode(token).decode()}}
            output = self.k.kube('create', '-f', '-', '-o', 'jsonpath={.metadata.uid}{" "}{.metadata.resourceVersion}', data=canonical(secret)).stdout
            identity = self.k.creation_identity(output)
            journal.update(phase='Stored', secretIdentity=identity); durable(path, journal)
            return journal
        except Exception:
            journal['phase'] = 'IssuanceUnknown' if not journal.get('accessor') else 'PublicationUnknown'
            best_effort_evidence(path, journal)
            # Exact known issued accessor is recoverable. Do not guess a Secret creation identity.
            if journal.get('accessor'):
                try:
                    before_effect()
                    with self.provider() as request:
                        journal['cleanupRevocation'] = 'Started'
                        best_effort_evidence(path, journal)
                        status, _ = request('POST', '/v1/auth/token/revoke-accessor', {'accessor': journal['accessor']})
                        journal['cleanupRevocation'] = 'Acknowledged' if status in (200, 204) else 'Unknown'
                except Exception:
                    journal['cleanupRevocation'] = 'Unknown'
                best_effort_evidence(path, journal)
            raise Blocked('successor-issuance-or-publication-original-needs-reconciliation') from None
        finally:
            if token is not None: token[:] = b'\0' * len(token)

    def revoke_original(self, role, credential, directory):
        path = directory / ('revoke-original-' + role + '.json')
        original = credential['original']; observed = self.lease(original['accessor'])
        if path.exists():
            journal = load(path)
            need(journal['accessor'] == original['accessor'], 'original-revocation-identity-changed')
            if observed['classification'] == 'Absent':
                journal['phase'] = 'Acknowledged'; durable(path, journal); return
            need(journal['phase'] == 'Prepared', 'unknown-original-revocation-observation-only')
        if observed['classification'] == 'Absent':
            durable(path, {'phase': 'Acknowledged', 'accessor': original['accessor'], 'nativeAlreadyAbsent': True}); return
        need(observed['classification'] == 'Present', 'original-accessor-state-unknown-no-revoke')
        journal = {'phase': 'Started', 'accessor': original['accessor']}; durable(path, journal)
        with self.provider() as request:
            window(self.scope)
            status, _ = request('POST', '/v1/auth/token/revoke-accessor', {'accessor': original['accessor']})
        need(status in (200, 204) and self.lease(original['accessor'])['classification'] == 'Absent', 'original-revocation-outcome-unknown')
        journal['phase'] = 'Acknowledged'; durable(path, journal)

    def cleanup_unused_successor(self, role, credential, directory):
        name = credential['selectedSecret']
        if any(directory.glob('object-Deployment_*.json')):
            durable(directory / ('cleanup-' + role + '.json'), {'phase': 'ConsumerCutoverStartedPreserved',
                'secretName': name, 'accessor': credential['accessor'], 'revocationOrDeletionAttempted': False})
            return
        for deployment in ('inventory', 'ext-secrets-1-custody'):
            spec = self.k.kjson('get', 'deployment', deployment, '-n', NS)['spec']['template']['spec']
            if any(volume.get('secret', {}).get('secretName') == name for volume in spec.get('volumes', [])):
                durable(directory / ('cleanup-' + role + '.json'), {'phase': 'StillReferenced', 'secretName': name, 'accessor': credential['accessor']}); return
        path = directory / ('cleanup-' + role + '.json')
        need(not path.exists(), 'successor-cleanup-original-already-started')
        durable(path, {'phase': 'Started', 'secretName': name, 'accessor': credential['accessor']})
        with self.provider() as request:
            window(self.scope)
            status, _ = request('POST', '/v1/auth/token/revoke-accessor', {'accessor': credential['accessor']})
        need(status in (200, 204), 'successor-cleanup-revocation-unknown')
        window(self.scope)
        self.i.delete_matching_credential_secret(name, identity=credential['secretIdentity'])
        durable(path, {'phase': 'Acknowledged', 'secretName': name, 'accessor': credential['accessor']})

    def observe_enrollment(self):
        receipt_file = next(Path(path) for path in self.scope['inputSha256'] if path.endswith('/original-owner-receipts.json'))
        native = receipt_file.parent.parent / 'native-registry-inventory-20261017T082852516535Z/native-enrollment-receipts.json'
        need(native.is_file() and not native.is_symlink() and native.stat().st_size <= 16 * 1024 * 1024,
             'original-native-enrollment-receipt-pin-mismatch')
        receipt_bytes = native.read_bytes()
        need(digest(receipt_bytes) == NATIVE_ENROLLMENT_SHA256, 'original-native-enrollment-receipt-pin-mismatch')
        receipts = json.loads(receipt_bytes)
        original_tenants(receipts.get('tenants'))
        durable(self.evidence / 'native-enrollment-receipts.json', receipts)
        with self.p.forward('service/registry-idp', NS, 8443) as idp, self.p.forward('deployment/inventory', NS, 3500) as dapr:
            result = self.v.verify(self, forward_port=dapr, idp_port=idp)
        original_tenants(result.get('tenantStreams'))
        need(result.get('restrictedReadVerified') is True
             and all(item.get('completeNativeEnrollmentRead') is True for item in result['tenantStreams']), 'complete-restricted-native-enrollment-required')
        result['originalHashesMatch'] = True
        return result

    def observe_health(self):
        return {name: self.b.http_health(service, port) for name, service, port in
            [('authority', AUTHORITY, 38455), ('inventory', 'inventory', 38454), ('custody', 'ext-secrets-1-custody', 38453)]}

    def renew(self, role, identity):
        token = self.i.read_pinned_credential_secret(identity['secretName'], identity['secretIdentity']); self.private.append(token)
        with self.provider() as request:
            window(self.scope)
            status, value = request('POST', '/v1/auth/token/renew-self', {'increment': 86400}, credential=token)
            need(status == 200, 'original-self-renew-denied-or-failed')
            # Renewal can include token material; it is never serialized or returned.

    def verify_selected_secret(self, journal, accessor):
        identity = journal['secretIdentity'] if journal['phase'] == 'Stored' else journal['original']['secretIdentity']
        token = self.i.read_pinned_credential_secret(journal['selectedSecret'], identity); self.private.append(token)
        with self.provider() as request:
            status, value = request('GET', '/v1/auth/token/lookup-self', credential=token)
        need(status == 200 and value.get('data', {}).get('accessor') == accessor, 'selected-secret-token-accessor-mismatch')
        role = 'data' if journal['original']['policy'] == self.scope['credentialBranches']['data']['original']['policy'] else 'metadata'
        self.verify_capabilities(role, accessor, credential=token)

    def verify_planned_object(self, reference, journal, observed_identity):
        kind, namespace, name = reference.split('/')
        actual = self.k.kjson('get', kind, name, '-n', namespace)
        identity = public_identity(actual, actual['metadata']['uid'])
        return (actual['metadata']['uid'] == journal['uid'] and identity == observed_identity
                and (self.b.public_object(actual) == journal['intended']
                     or journal.get('phase') == 'Started' and actual['metadata']['resourceVersion'] == journal['resourceVersion']
                     and identity == self.scope['installedResourcePreconditions'].get(reference)))

    def stage(self, reference, public, credentials, directory):
        kind, namespace, name = reference.split('/')
        path = directory / ('object-' + reference.replace('/', '_') + '.json')
        current = self.k.kjson('get', kind, name, '-n', namespace)
        if path.exists():
            journal = load(path)
            need(journal['phase'] in ('Started', 'Committed'), 'object-journal-phase-invalid')
            need(current['metadata']['uid'] == journal['uid'], 'original-object-uid-changed')
            if self.b.public_object(current) == journal['intended']:
                journal['phase'] = 'Committed'; durable(path, journal); return
            need(journal['phase'] != 'Committed', 'committed-object-changed')
            need(current['metadata']['uid'] == journal['uid'] and current['metadata']['resourceVersion'] == journal['resourceVersion'], 'uncertain-object-outcome-needs-reconciliation')
            intended = deepcopy(current)
        else:
            need(public_identity(current, current['metadata']['uid']) == self.scope['installedResourcePreconditions'][reference], 'object-precondition-changed')
            intended = deepcopy(current)
        if kind == 'ConfigMap':
            intended['data']['runtime-reference.json'] = json.dumps(public, sort_keys=True)
        else:
            for volume in intended['spec']['template']['spec'].get('volumes', []):
                for role, credential in credentials.items():
                    if volume.get('secret', {}).get('secretName') == self.scope['credentialBranches'][role]['original']['secretName']:
                        volume['secret']['secretName'] = credential['selectedSecret']
            intended['spec']['template']['metadata'].setdefault('annotations', {})['hexalith.io/expired-hmac-recovery'] = self.scope['runId']
        if not path.exists():
            journal = {'phase': 'Started', 'uid': current['metadata']['uid'], 'resourceVersion': current['metadata']['resourceVersion'],
                       'intended': self.b.public_object(intended)}
            durable(path, journal)
        intended.pop('status', None); intended['metadata'].pop('managedFields', None)
        window(self.scope)
        self.k.kube('replace', '-f', '-', data=canonical(intended))
        actual = self.k.kjson('get', kind, name, '-n', namespace)
        need(actual['metadata']['uid'] == journal['uid'] and self.b.public_object(actual) == journal['intended'], 'conditional-object-readback-mismatch')
        journal['phase'] = 'Committed'; durable(path, journal)

    def rollout(self):
        for name in ('inventory', 'ext-secrets-1-custody'):
            self.k.kube('rollout', 'status', 'deployment/' + name, '-n', NS, '--timeout=90s', timeout=100)

    def verify_bound(self, public):
        health = self.observe_health()
        need(all(value[path]['status'] == 200 for value in health.values() for path in ('/alive', '/health', '/ready')), 'bound-health-verification-failed')
        for name in ('ext-secrets-1-inventory-runtime', 'ext-secrets-1-custody-runtime-29a4ace57e04'):
            actual = self.k.kjson('get', 'configmap', name, '-n', NS)
            need(json.loads(actual['data']['runtime-reference.json']) == public, 'bound-runtime-readback-mismatch')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('operation', choices=('observe', 'apply'))
    parser.add_argument('--scope', type=Path, required=True)
    parser.add_argument('--run-directory', type=Path, required=True)
    parser.add_argument('--helper-source', type=Path, required=True)
    parser.add_argument('--preservation-decision', type=Path)
    parser.add_argument('--metadata-credential-record', type=Path)
    parser.add_argument('--observe-group', choices=('kubernetes', 'authority', 'administration', 'provider', 'enrollment', 'health'), action='append')
    args = parser.parse_args()
    instance = None
    try:
        os.umask(0o077); sys.dont_write_bytecode = True
        scope = load(args.scope); validate_scope(scope)
        args.run_directory.mkdir(exist_ok=True, parents=False)
        durable(args.run_directory / ('execution-source-' + uuid.uuid4().hex + '.json'), {
            'capturedAtProcessStart': now().isoformat(), 'argv': sys.argv,
            'executorSha256': digest(Path(__file__).read_bytes()),
            'preparerSha256': digest(Path(__file__).with_name('prepare_expired_hmac_recovery.py').read_bytes())})
        backend = LiveBackend(scope, args.run_directory, args.helper_source, args.metadata_credential_record)
        instance = Recovery(scope, args.run_directory, backend)
        if args.operation == 'observe':
            result = instance.observe(args.observe_group)
            print(json.dumps({'observed': True, 'unavailableGroups': sorted(result['unavailable']), 'runDirectory': str(args.run_directory), **FLAGS}))
        else:
            instance.apply(args.preservation_decision)
            print(json.dumps({'boundedRecoveryCompleted': True, 'runDirectory': str(args.run_directory), **FLAGS}))
        return 0
    except Exception as error:
        reason = str(error) if isinstance(error, Blocked) else 'sanitized-native-recovery-failure'
        if args.run_directory.is_dir():
            durable(args.run_directory / ('blocker-' + uuid.uuid4().hex + '.json'), {'reason': reason,
                'owner': 'Platform recovery implementation / provider or historical evidence owner as identified by the failed scoped prerequisite',
                'nextAction': 'Reconcile this exact original and supply the actual missing authenticated observation or historical decision. Do not repeat uncertain effects or change the approved window.', **FLAGS})
        print(json.dumps({'blocked': True, 'reason': reason, 'runDirectory': str(args.run_directory), **FLAGS}))
        return 2
    finally:
        if instance is not None:
            instance.close()


if __name__ == '__main__':
    raise SystemExit(main())
