#!/usr/bin/env python3
"""Enroll two tenants through persisted native commands, retaining exact requests for recovery.

These are native registry operations, never EXT-SECRETS-1 contract operations.
"""
import http.client
import json
import secrets
import sys
import time
from datetime import datetime, timezone

from install_isolated_registry import Installer, canonical, sha
from provision_registry_identity import Idp, REALM
from native_journal import durable_write, journal_lock


def ulid():
    number = (int(time.time() * 1000) << 80) | secrets.randbits(80)
    alphabet = '0123456789ABCDEFGHJKMNPQRSTVWXYZ'
    return ''.join(alphabet[(number >> shift) & 31] for shift in range(125, -1, -5))


class NativeRegistry:
    def __init__(self, installer):
        self.installer = installer
        self.idp = Idp(installer)
        self.bearer = None

    def authenticate(self):
        value, _ = self.idp.call('POST', '/realms/' + REALM + '/protocol/openid-connect/token', {
            'grant_type': 'password', 'client_id': self.installer.secret_field('ext-secrets-1-registry-oidc-registry-provisioner', 'client-id'),
            'client_secret': self.installer.secret_field('ext-secrets-1-registry-oidc-registry-provisioner', 'client-secret'),
            'username': self.installer.secret_field('ext-secrets-1-registry-provisioner', 'username'),
            'password': self.installer.secret_field('ext-secrets-1-registry-provisioner', 'password')}, form=True)
        self.bearer = value['access_token']

    def call(self, method, path, body=None, accepted=(200, 202)):
        connection = http.client.HTTPConnection('127.0.0.1', 38446, timeout=30)
        headers = {'Authorization': 'Bearer ' + self.bearer} if self.bearer else {}
        if body is not None:
            headers['Content-Type'] = 'application/json'
        connection.request(method, path, canonical(body) if body is not None else None, headers)
        response = connection.getresponse()
        raw = response.read(1024 * 1024)
        status = response.status
        connection.close()
        if status not in accepted:
            parsed = json.loads(raw) if raw and raw[:1] == b'{' else {}
            # Retain content-free native error classes only, never raw response payloads.
            raise RuntimeError('native-registry-request-failed:' + str(status) + ':' + str(parsed.get('reasonCode', parsed.get('title', 'unavailable'))))
        return json.loads(raw) if raw and raw[:1] in (b'{', b'[') else None

    def process(self, request):
        with journal_lock(self.installer.evidence / ('native-command-' + request['messageId'] + '.lock')):
            return self.process_locked(request)

    def process_locked(self, request):
        path = self.installer.evidence / ('native-command-' + request['messageId'] + '.json')
        if path.exists():
            record = json.loads(path.read_text())
            assert record['request'] == request
        else:
            record = {'request': request, 'phase': 'Prepared', 'originalStatus': None}
            durable_write(path, record, create_only=True)
        if 'phase' not in record:
            record['phase'] = 'AdmissionDenied' if record.get('admissionDenied') else 'Uncertain'
        if isinstance(record.get('originalStatus'), dict) and record['originalStatus'].get('status') == 'Completed':
            return record['originalStatus']
        if record['phase'] in ['Prepared', 'AdmissionDenied']:
            # Prepared proves no admission attempt was started. Once this phase
            # is durable, interruption is uncertain even if the socket never sent.
            record['phase'] = 'AdmissionStarted'
            durable_write(path, record)
            try:
                self.call('POST', '/api/v1/commands', request)
            except RuntimeError as error:
                if str(error).startswith(('native-registry-request-failed:401:', 'native-registry-request-failed:403:', 'native-registry-request-failed:400:')):
                    record['phase'] = 'AdmissionDenied'
                    record['submissionDenial'] = str(error)
                else:
                    record['phase'] = 'Uncertain'
                durable_write(path, record)
                raise
            record['phase'] = 'Admitted'
            durable_write(path, record)
        for _ in range(35):
            # Exact native status lookup is read-only. A missing status never
            # proves non-admission and never authorizes another submission.
            status = self.call('GET', '/api/v1/commands/status/' + request['messageId'])
            if status.get('messageId') != request['messageId']:
                raise RuntimeError('native-command-status-identity-mismatch')
            record['originalStatus'] = {key: status.get(key) for key in ['messageId', 'correlationId', 'status', 'rejectionEventType', 'failureReason']}
            record['phase'] = 'Completed' if status.get('status') == 'Completed' else record['phase']
            durable_write(path, record)
            if status.get('status') == 'Completed':
                return record['originalStatus']
            if status.get('status') in ['Rejected', 'Failed', 'DeadLettered', 'PublishFailed', 'TimedOut']:
                raise RuntimeError('native-command-terminal:' + str(status.get('status')) + ':' + str(status.get('rejectionEventType')))
            time.sleep(1)
        raise RuntimeError('native-command-outcome-unresolved:' + request['messageId'])

    def stream(self, domain, aggregate_id):
        body = {'tenant': 'system', 'domain': domain, 'aggregateId': aggregate_id, 'fromSequence': 0, 'pageSize': 100}
        return self.call('POST', '/api/v1/streams/read', body)


def enroll(installer):
    registry = NativeRegistry(installer)
    registry.call('GET', '/ready')
    registry.authenticate()
    subject = installer.secret_field('ext-secrets-1-registry-provisioner', 'subject-id')
    plan_path = installer.evidence / 'native-tenant-provisioning-plan.json'
    if plan_path.exists():
        plan = json.loads(plan_path.read_text())
    else:
        plan = {'bootstrap': {'messageId': ulid(), 'tenant': 'system', 'domain': 'global-administrators',
                              'aggregateId': 'global-administrators', 'commandType': 'BootstrapGlobalAdmin', 'payload': {'userId': subject}},
                'tenants': []}
        for index in ['A', 'B']:
            tenant_id = ulid()
            plan['tenants'].append({'messageId': ulid(), 'tenant': 'system', 'domain': 'tenants', 'aggregateId': tenant_id,
                'commandType': 'CreateTenant', 'payload': {'tenantId': tenant_id, 'name': 'EXT-SECRETS-1 isolated qualification ' + index,
                  'description': 'Isolated native registry enrollment; no independent secrets approval or provider key is implied.'}})
        durable_write(plan_path, plan, create_only=True)
    bootstrap = registry.process(plan['bootstrap'])
    source = registry.stream('global-administrators', 'global-administrators')
    assert any('GlobalAdministratorSet' in item['eventTypeName'] for item in source['events'])
    # Grant the IdP hint only after native persisted administrator enrollment;
    # /process still verifies current native membership on every command.
    idp = Idp(installer)
    idp.login()
    value, _ = idp.call('GET', '/admin/realms/' + REALM + '/users/' + subject)
    value.setdefault('attributes', {})['global_admin'] = ['true']
    idp.call('PUT', '/admin/realms/' + REALM + '/users/' + subject, value)
    registry.authenticate()
    receipts = []
    for request in plan['tenants']:
        status = registry.process(request)
        source = registry.stream('tenants', request['aggregateId'])
        events = source['events']
        assert source['tenant'] == 'system' and source['domain'] == 'tenants' and source['aggregateId'] == request['aggregateId']
        assert len(events) == 1 and 'TenantCreated' in events[0]['eventTypeName']
        receipt = {'tenantId': request['aggregateId'], 'commandMessageId': request['messageId'], 'status': status,
                   'eventType': events[0]['eventTypeName'], 'sequenceNumber': events[0]['sequenceNumber'],
                   'sourceStreamSha256': sha(canonical(source)), 'nativeStreamMetadata': source['metadata'],
                   'observedAt': datetime.now(timezone.utc).isoformat(), 'currentEnrollment': 'Active',
                   'providerMapping': None, 'independentSecretsApproval': None}
        receipts.append(receipt)
        installer.write('native-enrollment-receipts.json', {'bootstrap': bootstrap, 'tenants': receipts, 'contractOperations': 0})
    return receipts


if __name__ == '__main__':
    instance = Installer(sys.argv[1])
    try:
        enroll(instance)
        print('two native persisted qualification tenants enrolled')
    except Exception as error:
        instance.write('native-enrollment-failure.json', {'exceptionType': type(error).__name__, 'reason': str(error) if isinstance(error, RuntimeError) else 'sanitized-enrollment-failure'})
        raise SystemExit(1) from None
    finally:
        instance.close()
