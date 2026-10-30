"""Real owner wire shapes; synthetic transport, delayed persisted reads, no network."""
import copy
import io
import hashlib
from datetime import datetime, timedelta, timezone
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import seed

TENANT = 'mcpcli-417-test'
IDS = ['01ARZ3NDEKTSV4RRFFQ69G5FA' + c for c in 'VWX']


def plan():
    steps = []
    for name, message, command_type in [('bootstrap', IDS[0], 'bootstrap-global-admin'), ('create-tenant', IDS[1], 'create-tenant'), ('add-member', IDS[2], 'add-user-to-tenant')]:
        body = {'messageId': message, 'tenant': 'system', 'domain': 'tenants', 'aggregateId': TENANT,
                'commandType': command_type, 'payload': {'TenantId': TENANT}}
        if name == 'bootstrap':
            body.update(domain='global-administrators', aggregateId='global-administrators', payload={'UserId': 'bootstrap-user'})
            body = {'command': body, 'principalId': 'bootstrap-user'}
        if name == 'add-member':
            body['payload'].update(UserId='operator-user', Role='TenantContributor')
        steps.append({'name': name, 'method': 'POST', 'url': 'http://127.0.0.1:18170/api/v1/commands' + ('/bound/workload' if name == 'bootstrap' else ''),
                      'credential': 'bootstrap' if name == 'bootstrap' else 'admin', 'body': body, 'expectedStatus': 202, 'assertions': {}})
        steps.append({'name': {'bootstrap': 'bootstrap-status', 'create-tenant': 'create-status', 'add-member': 'member-status'}[name], 'method': 'GET',
                      'url': 'http://127.0.0.1:18170/api/v1/commands/status/' + message, 'credential': 'admin', 'expectedStatus': 200,
                      'assertions': {'messageId': message, 'status': 'Completed', 'minimum:committedEventSequence': 1, 'minimum:eventCount': 1}})
    steps.extend([
        {'name': 'get-tenant', 'method': 'POST', 'url': 'http://127.0.0.1:18170/api/v1/queries', 'credential': 'operator', 'expectedStatus': 200,
         'body': {'tenant': 'system', 'domain': 'tenants', 'aggregateId': TENANT, 'queryType': 'get-tenant', 'projectionType': 'tenants', 'payload': {'TenantId': TENANT}},
         'assertions': {'payload.tenantId': TENANT, 'payload.status': 'Active'}},
        {'name': 'provision-memories', 'method': 'POST', 'url': 'http://127.0.0.1:18171/api/v1/tenants', 'credential': 'memories', 'expectedStatus': 202,
         'body': {'tenantId': TENANT, 'displayName': 'Operator setup', 'vectorDimensions': 3}, 'assertions': {}},
        {'name': 'get-memories', 'method': 'GET', 'url': 'http://127.0.0.1:18171/api/v1/tenants/' + TENANT, 'credential': 'memories', 'expectedStatus': 200,
         'assertions': {'id': TENANT, 'status': 'Active'}}])
    return {'namespace': TENANT, 'tenant': TENANT, 'operatorRole': 'TenantContributor', 'operatorSubject': 'operator-user',
            'forwardedPorts': [18170, 18171], 'steps': steps}


class Response(io.BytesIO):
    status = 200


class Transport:
    def __init__(self, terminal=False, pending_forever=False):
        self.calls = []; self.delayed = False; self.terminal = terminal; self.pending_forever = pending_forever

    def open(self, request, timeout):
        self.calls.append((request.method, request.full_url))
        if '/status/' in request.full_url:
            if self.terminal:
                document = {'status': 'Rejected'}
            elif not self.delayed or self.pending_forever:
                document = {'status': 'Processing'}; self.delayed = True
            else:
                document = {'messageId': request.full_url.rsplit('/', 1)[1], 'status': 'Completed', 'committedEventSequence': 1, 'eventCount': 1}
        elif request.full_url.endswith('/queries'):
            document = {'success': True, 'payload': {'tenantId': TENANT, 'status': 'Active'}}
        elif request.full_url.endswith('/api/v1/tenants/' + TENANT):
            document = {'id': TENANT, 'status': 'Active'}
        else:
            response = Response(b''); response.status = 202; return response
        return Response(json.dumps(document).encode())


class SeedTests(unittest.TestCase):
    def test_real_wire_shapes_validate_and_delayed_observation_never_repeats_writes(self):
        value = plan(); seed.validate(value, TENANT, TENANT)
        transport = Transport()
        with tempfile.TemporaryDirectory() as directory, patch.object(seed, 'build_opener', return_value=transport):
            seed.execute(value, {k: {} for k in ('bootstrap', 'admin', 'operator', 'memories')}, Path(directory) / 'journal', pause=lambda _: None)
            receipts = [json.loads(p.read_text()) for p in sorted((Path(directory) / 'journal').glob('*.json'))]
        self.assertEqual(9, len(receipts)); self.assertTrue(all(r['outcome'] == 'confirmed' for r in receipts))
        self.assertEqual(2, receipts[1]['attempts'])
        self.assertEqual(1, sum(url.endswith('/commands/bound/workload') for _, url in transport.calls))
        self.assertEqual(2, sum(url.endswith('/commands') for _, url in transport.calls))
        self.assertEqual(1, sum(method == 'POST' and url.endswith('/api/v1/tenants') for method, url in transport.calls))

    def test_changed_private_plan_is_refused_before_any_network(self):
        with tempfile.TemporaryDirectory() as directory:
            private_plan = Path(directory) / 'seed.json'; private_plan.write_text(json.dumps(plan()))
            review = {'schema': 'hexalith.mcpcli.operator-review/v1', 'target': {'namespace': TENANT}, 'tenant': TENANT,
                      'expiresAt': (datetime.now(timezone.utc) + timedelta(hours=1)).isoformat(),
                      'seed': {'privatePlanSha256': hashlib.sha256(private_plan.read_bytes()).hexdigest()}}
            packet = Path(directory) / 'review.json'; packet.write_text(json.dumps(review))
            packet_sha = hashlib.sha256(packet.read_bytes()).hexdigest()
            seed.verify_review(private_plan, packet, packet_sha, TENANT, TENANT)
            private_plan.write_text(json.dumps({**plan(), 'operatorSubject': 'changed-after-review'}))
            with patch.object(seed, 'build_opener') as network:
                with self.assertRaisesRegex(ValueError, 'reviewed-seed-plan-changed'):
                    seed.verify_review(private_plan, packet, packet_sha, TENANT, TENANT)
                network.assert_not_called()

    def test_terminal_failure_stops_without_next_write(self):
        transport = Transport(terminal=True)
        with tempfile.TemporaryDirectory() as directory, patch.object(seed, 'build_opener', return_value=transport):
            with self.assertRaisesRegex(ValueError, 'owner-command-terminal-failure'):
                seed.execute(plan(), {k: {} for k in ('bootstrap', 'admin', 'operator', 'memories')}, Path(directory) / 'journal')
            receipt = json.loads((Path(directory) / 'journal/01-bootstrap-status.json').read_text())
        self.assertEqual('terminal-failure', receipt['outcome']); self.assertEqual(2, len(transport.calls))

    def test_pending_observations_stop_at_deadline(self):
        transport = Transport(pending_forever=True); ticks = iter(range(100))
        with tempfile.TemporaryDirectory() as directory, patch.object(seed, 'build_opener', return_value=transport):
            with self.assertRaisesRegex(ValueError, 'owner-observation-deadline-exceeded'):
                seed.execute(plan(), {k: {} for k in ('bootstrap', 'admin', 'operator', 'memories')}, Path(directory) / 'journal',
                             timeout_seconds=3, clock=lambda: next(ticks), pause=lambda _: None)
        self.assertEqual(1, sum(url.endswith('/commands/bound/workload') for _, url in transport.calls))

    def test_wrapper_document_shape_and_unversioned_memories_routes_are_refused(self):
        for change in ('wrapper', 'route'):
            value = plan()
            if change == 'wrapper': value['steps'][6]['assertions'] = {'document.tenantId': TENANT}
            else: value['steps'][7]['url'] = 'http://127.0.0.1:18171/tenants'
            with self.subTest(change=change), self.assertRaises(ValueError): seed.validate(value, TENANT, TENANT)


if __name__ == '__main__':
    unittest.main()
