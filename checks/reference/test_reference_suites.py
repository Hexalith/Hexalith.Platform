"""Controlled HTTP/candidate observations verify payload behavior, never hosted E2E evidence."""
import copy
import datetime as dt
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import threading
import time
import unittest
from unittest import mock

import runner

ROOT = Path(__file__).resolve().parent
TOKEN = 'synthetic-secret-never-retained'


class Endpoint(BaseHTTPRequestHandler):
    mode = 'passed'
    calls = []
    party_id = 'fixture-party'
    message_id = ''
    projection_reads = 0
    ingestion_reads = 0
    party_detail = None

    def log_message(self, *_):
        pass

    def do_POST(self):
        self.respond()

    def do_GET(self):
        self.respond()

    def respond(self):
        body = json.loads(self.rfile.read(int(self.headers.get('Content-Length', '0'))) or b'null')
        type(self).calls.append((self.command, self.path, self.headers.get('Authorization'), body))
        if self.mode == 'timeout':
            time.sleep(0.5)
        code = 200
        if self.mode == 'failed':
            code, response = 500, {'error': TOKEN}
        elif self.mode == 'redirect':
            self.send_response(302)
            self.send_header('Location', 'http://127.0.0.1:1/stolen')
            self.end_headers()
            return
        elif self.path == '/api/v1/commands':
            type(self).party_id, type(self).message_id = body['aggregateId'], body['messageId']
            supplied = body['payload']
            type(self).party_detail = {'id': body['aggregateId'], 'type': supplied['Type'], 'isActive': True,
                                       'displayName': supplied['PersonDetails']['FirstName'] + ' ' + supplied['PersonDetails']['LastName'],
                                       'personDetails': {'firstName': supplied['PersonDetails']['FirstName'],
                                                         'lastName': supplied['PersonDetails']['LastName']}}
            code, response = 202, {'messageId': body['messageId']}
        elif self.path.startswith('/api/v1/commands/status/'):
            response = {'status': 'Completed', 'messageId': self.message_id, 'aggregateId': self.party_id,
                        'tenantId': 'synthetic-tenant', 'eventCount': 1}
        elif self.path == '/api/v1/queries':
            type(self).projection_reads += 1
            if self.mode == 'projection-pending' and self.projection_reads == 1:
                code = 404
            detail = copy.deepcopy(self.party_detail) if body['aggregateId'] == self.party_id and self.party_detail else {'id': body['aggregateId']}
            if self.mode == 'projection-id-only':
                detail = {'id': body['aggregateId']}
            elif self.mode == 'projection-name':
                detail['personDetails']['lastName'] = 'Unrelated'
            elif self.mode == 'projection-type':
                detail['type'] = 'Organization'
            elif self.mode == 'projection-inactive':
                detail['isActive'] = False
            elif self.mode == 'projection-display-name':
                detail['displayName'] = 'Unrelated'
            response = {'success': self.mode != 'semantic-failure', 'payload': detail}
        elif self.path == '/api/global-administrators':
            code, response = 403, {'error': 'Forbidden'}
        elif self.path == '/api/v1/ingest':
            code, response = 202, {'instanceId': 'workflow-one'}
        elif self.path == '/api/v1/ingest/workflow-one':
            type(self).ingestion_reads += 1
            response = {'tenantId': 'synthetic-tenant', 'caseId': 'fixture-case',
                        'runtimeStatus': 'Completed', 'memoryUnitId': 'memory-one',
                        'memoryUnitStatus': 'indexed', 'failureSummary': None}
            if self.mode == 'ingest-pending' and self.ingestion_reads == 1:
                code = 404
            elif self.mode == 'ingest-forbidden':
                code = 403
            elif self.mode == 'ingest-server-error':
                code = 500
            elif self.mode == 'ingest-failed':
                response['memoryUnitStatus'] = 'failed'
                response['failureSummary'] = 'Workflow completed with failed ingestion status.'
            elif self.mode == 'ingest-uncertain':
                response['memoryUnitStatus'] = None
            elif self.mode == 'ingest-summary':
                response['failureSummary'] = 'Workflow output could not be projected safely.'
            elif self.mode == 'ingest-unindexed':
                response['memoryUnitStatus'] = 'indexing'
            elif self.mode == 'ingest-empty-id':
                response['memoryUnitId'] = ''
        elif self.path.startswith('/api/v1/search?'):
            results = [{'memoryUnitId': 'memory-one', 'caseId': 'fixture-case'}]
            if self.mode == 'search-empty':
                results = []
            elif self.mode == 'search-wrong-case':
                results[0]['caseId'] = 'unrelated-case'
            elif self.mode == 'search-unrelated-id':
                results[0]['memoryUnitId'] = 'unrelated-memory'
            elif self.mode == 'search-absent-id':
                del results[0]['memoryUnitId']
            elif self.mode == 'search-empty-id':
                results[0]['memoryUnitId'] = ''
            response = {'results': results, 'totalCount': 1, 'hasIndexedMemoryUnits': True,
                        'degraded': self.mode == 'degraded'}
        else:
            code, response = 404, {}
        self.send_response(code)
        self.send_header('Content-Type', 'application/json')
        self.end_headers()
        try:
            self.wfile.write(json.dumps(response).encode())
        except (BrokenPipeError, ConnectionResetError):
            pass


class ReferenceSuiteTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.server = ThreadingHTTPServer(('127.0.0.1', 0), Endpoint)
        cls.thread = threading.Thread(target=cls.server.serve_forever, daemon=True)
        cls.thread.start()
        cls.endpoint = 'http://127.0.0.1:' + str(cls.server.server_port)

    @classmethod
    def tearDownClass(cls):
        cls.server.shutdown()
        cls.server.server_close()
        cls.thread.join()

    def setUp(self):
        Endpoint.mode = 'passed'
        Endpoint.calls = []
        Endpoint.projection_reads = 0
        Endpoint.ingestion_reads = 0
        Endpoint.party_detail = None

    def invocation(self, check, seconds=10):
        now = dt.datetime.now(dt.timezone.utc)
        return dict(schema='hexalith.check-invocation.v1', invocationId='invocation-one', checkId=check['id'],
                    attemptId='attempt-one', environment='staging', executionMode='controlled',
                    servedReleaseDigest='sha256:' + 'a' * 64, suiteDigest='sha256:' + 'b' * 64,
                    descriptorDigest='sha256:' + 'c' * 64, profileDigest='sha256:' + 'd' * 64,
                    configurationDigest='sha256:' + 'e' * 64,
                    workingBaseline=dict(attemptId='baseline-one', releaseDigest='sha256:' + 'f' * 64,
                                         profileDigest='sha256:' + 'd' * 64, configurationDigest='sha256:' + 'e' * 64),
                    verificationEndpoint=self.endpoint, syntheticTenant='synthetic-tenant', syntheticTenantMode=check['tenancy'],
                    fixturePartyId='fixture-party', fixtureCaseId='fixture-case', externalEffectsDisabled=True,
                    startedAt=now.isoformat(), deadlineAt=(now + dt.timedelta(seconds=seconds)).isoformat())

    def descriptor(self, module):
        return runner.load_json(ROOT / 'suites' / (module + '.json'))

    def test_compatible_http_checks_observe_real_response_shapes(self):
        for module in ('parties', 'eventstore', 'memories', 'tenants'):
            descriptor = self.descriptor(module)
            for check in descriptor['checks']:
                with self.subTest(check=check['id']):
                    result = runner.execute(self.invocation(check), descriptor, TOKEN)
                    self.assertEqual(result['outcome'], 'passed')
                    self.assertTrue(result['complete'])
                    self.assertNotIn(TOKEN, json.dumps(result))
        self.assertTrue(all(call[2] == 'Bearer ' + TOKEN for call in Endpoint.calls))
        self.assertFalse(any('/ready' in call[1] or '/health' in call[1] for call in Endpoint.calls))
        self.assertTrue(any(call[0] == 'POST' and call[1] == '/api/v1/ingest' for call in Endpoint.calls))

    def test_staging_waits_for_projection_visibility_without_masking_failures(self):
        descriptor = self.descriptor('parties')
        Endpoint.mode = 'projection-pending'
        result = runner.execute(self.invocation(descriptor['checks'][0]), descriptor, TOKEN)
        self.assertEqual(result['outcome'], 'passed')
        self.assertEqual(Endpoint.projection_reads, 2)
        Endpoint.mode = 'failed'
        result = runner.execute(self.invocation(descriptor['checks'][0]), descriptor, TOKEN)
        self.assertEqual(result['outcome'], 'failed')

    def test_mcpcli_runs_a_real_controlled_candidate_with_bounded_output(self):
        descriptor = self.descriptor('mcpcli')
        original_popen = subprocess.Popen
        with tempfile.TemporaryDirectory() as directory:
            candidate = Path(directory) / 'candidate'
            processes = []
            expected_arguments = ['--url', self.endpoint, '--tenant', 'synthetic-tenant', '--read-only',
                                  '--format', 'json', 'query', 'parties.get-party', '--payload', '{"PartyId": "fixture-party"}']

            def launch(command, **options):
                self.assertEqual(command[0], '/candidate/hexalith')
                self.assertEqual(command[1:], expected_arguments)
                self.assertEqual(options['env'], {'PATH': '/usr/bin:/bin', 'EVENTSTORE_TOKEN': TOKEN,
                                                 'DOTNET_CLI_TELEMETRY_OPTOUT': '1', 'HOME': '/tmp/check-client'})
                process = original_popen([str(candidate), *command[1:]], **options)
                processes.append(process)
                return process

            prelude = ('#!' + sys.executable + '\nimport json, os, sys, time\n'
                       + 'if sys.argv[1:] != ' + repr(expected_arguments) + ': sys.exit(19)\n'
                       + 'if os.environ.get("EVENTSTORE_TOKEN") != ' + repr(TOKEN) + ': sys.exit(20)\n'
                       + 'if "HEXALITH_CHECK_TOKEN" in os.environ: sys.exit(21)\n'
                       + 'print(' + repr(TOKEN) + ', file=sys.stderr)\n')
            behaviors = [('passed', 'print(json.dumps({"document":{"id":"fixture-party"}}))\n', 'passed'),
                         ('nonzero', 'sys.exit(7)\n', 'failed'),
                         ('invalid', 'print(' + repr(TOKEN) + ')\n', 'incomplete'),
                         ('overflow', 'while True: os.write(1, b"x" * 65536)\n', 'failed'),
                         ('timeout', 'time.sleep(60)\n', 'timed-out')]
            with mock.patch('runner.subprocess.Popen', side_effect=launch):
                for mode, behavior, outcome in behaviors:
                    candidate.write_text(prelude + behavior)
                    candidate.chmod(0o700)
                    selected = descriptor['checks'] if mode == 'passed' else descriptor['checks'][1:]
                    for check in selected:
                        with self.subTest(mode=mode, check=check['id']):
                            invocation = self.invocation(check, 0.3 if mode == 'timeout' else 3)
                            result = runner.execute(invocation, descriptor, TOKEN)
                            self.assertEqual(result['outcome'], outcome)
                            self.assertNotIn(TOKEN, json.dumps(result))
                            self.assertIsNotNone(processes[-1].poll())
            self.assertEqual(len(processes), 6)

    def test_corrupted_or_id_only_party_projections_are_refused(self):
        descriptor = self.descriptor('parties')
        for mode in ('projection-id-only', 'projection-name', 'projection-type', 'projection-inactive', 'projection-display-name'):
            with self.subTest(mode=mode):
                Endpoint.mode = mode
                result = runner.execute(self.invocation(descriptor['checks'][0]), descriptor, TOKEN)
                self.assertEqual(result['outcome'], 'failed')

    def test_distinct_long_invocations_create_distinct_synthetic_parties(self):
        descriptor = self.descriptor('parties')
        for suffix in ('first', 'second'):
            invocation = self.invocation(descriptor['checks'][0])
            invocation['invocationId'] = 'x' * 110 + '-' + suffix
            self.assertEqual(runner.execute(invocation, descriptor, TOKEN)['outcome'], 'passed')
        identifiers = [body['aggregateId'] for method, path, _, body in Endpoint.calls
                       if method == 'POST' and path == '/api/v1/commands']
        self.assertEqual(len(identifiers), 2)
        self.assertEqual(len(set(identifiers)), 2)

    def test_memory_completion_requires_indexed_failure_free_output(self):
        descriptor = self.descriptor('memories')
        for mode in ('ingest-failed', 'ingest-uncertain', 'ingest-summary', 'ingest-unindexed', 'ingest-empty-id'):
            with self.subTest(mode=mode):
                Endpoint.mode = mode
                result = runner.execute(self.invocation(descriptor['checks'][0]), descriptor, TOKEN)
                self.assertEqual(result['outcome'], 'failed')

    def test_memory_smoke_requires_a_valid_result_for_the_fixture_case(self):
        descriptor = self.descriptor('memories')
        for mode in ('search-empty', 'search-wrong-case', 'search-absent-id', 'search-empty-id'):
            with self.subTest(mode=mode):
                Endpoint.mode = mode
                result = runner.execute(self.invocation(descriptor['checks'][1]), descriptor, TOKEN)
                self.assertEqual(result['outcome'], 'failed')

    def test_memory_lifecycle_waits_for_status_visibility_but_preserves_hard_errors(self):
        descriptor = self.descriptor('memories')
        Endpoint.mode = 'ingest-pending'
        self.assertEqual(runner.execute(self.invocation(descriptor['checks'][0]), descriptor, TOKEN)['outcome'], 'passed')
        self.assertEqual(Endpoint.ingestion_reads, 2)
        for mode in ('ingest-forbidden', 'ingest-server-error'):
            with self.subTest(mode=mode):
                Endpoint.mode = mode
                self.assertEqual(runner.execute(self.invocation(descriptor['checks'][0]), descriptor, TOKEN)['outcome'], 'failed')

    def test_memory_lifecycle_requires_correlated_search_readback(self):
        descriptor = self.descriptor('memories')
        for mode in ('search-unrelated-id', 'search-absent-id', 'search-wrong-case', 'search-empty'):
            with self.subTest(mode=mode):
                Endpoint.mode = mode
                result = runner.execute(self.invocation(descriptor['checks'][0], 0.25), descriptor, TOKEN)
                self.assertEqual(result['outcome'], 'timed-out')
        Endpoint.mode = 'passed'
        self.assertEqual(runner.execute(self.invocation(descriptor['checks'][0]), descriptor, TOKEN)['outcome'], 'passed')

    def test_synthetic_writes_require_run_scoped_tenancy(self):
        descriptor = self.descriptor('parties')
        descriptor['checks'][0]['tenancy'] = 'existing-synthetic'
        invocation = self.invocation(descriptor['checks'][0])
        result = runner.execute(invocation, descriptor, TOKEN)
        self.assertEqual(result['diagnostics'], ['unsafe-context'])
        self.assertEqual(Endpoint.calls, [])

    def test_naive_timestamps_and_newline_identities_are_refused(self):
        descriptor = self.descriptor('memories')
        original = self.invocation(descriptor['checks'][1])
        for field, value in (('startedAt', dt.datetime.now().isoformat()), ('deadlineAt', dt.datetime.now().isoformat()),
                             ('invocationId', original['invocationId'] + '\n'),
                             ('suiteDigest', original['suiteDigest'] + '\n')):
            invocation = copy.deepcopy(original)
            invocation[field] = value
            with self.subTest(field=field), self.assertRaises(ValueError):
                runner.execute(invocation, descriptor, TOKEN)
        invocation = copy.deepcopy(original)
        invocation['startedAt'] = runner.timestamp(original['startedAt']).astimezone(dt.timezone(dt.timedelta(hours=2))).isoformat()
        invocation['deadlineAt'] = runner.timestamp(original['deadlineAt']).astimezone(dt.timezone(dt.timedelta(hours=2))).isoformat()
        self.assertEqual(runner.execute(invocation, descriptor, TOKEN)['outcome'], 'passed')

    def test_failed_and_degraded_responses_never_pass(self):
        descriptor = self.descriptor('memories')
        for mode in ('failed', 'degraded'):
            with self.subTest(mode=mode):
                Endpoint.mode = mode
                result = runner.execute(self.invocation(descriptor['checks'][1]), descriptor, TOKEN)
                self.assertEqual(result['outcome'], 'failed')
                self.assertNotIn(TOKEN, json.dumps(result))

    def test_http_200_semantic_failure_never_passes(self):
        descriptor = self.descriptor('parties')
        Endpoint.mode = 'semantic-failure'
        self.assertEqual(runner.execute(self.invocation(descriptor['checks'][1]), descriptor, TOKEN)['outcome'], 'failed')

    def test_fresh_install_has_an_explicit_null_baseline(self):
        descriptor = self.descriptor('memories')
        invocation = self.invocation(descriptor['checks'][1])
        invocation['workingBaseline'] = None
        result = runner.execute(invocation, descriptor, TOKEN)
        self.assertEqual(result['outcome'], 'passed')
        self.assertIsNone(result['workingBaseline'])
        del invocation['workingBaseline']
        with self.assertRaises(ValueError):
            runner.execute(invocation, descriptor, TOKEN)

    def test_deadline_is_a_nonpassing_timeout(self):
        descriptor = self.descriptor('memories')
        Endpoint.mode = 'timeout'
        result = runner.execute(self.invocation(descriptor['checks'][1], 0.1), descriptor, TOKEN)
        self.assertEqual(result['outcome'], 'timed-out')
        self.assertFalse(result['complete'])

    def test_expired_deadline_never_contacts_endpoint(self):
        descriptor = self.descriptor('memories')
        invocation = self.invocation(descriptor['checks'][1])
        invocation['startedAt'] = (dt.datetime.now(dt.timezone.utc) - dt.timedelta(seconds=20)).isoformat()
        invocation['deadlineAt'] = (dt.datetime.now(dt.timezone.utc) - dt.timedelta(seconds=10)).isoformat()
        self.assertEqual(runner.execute(invocation, descriptor, TOKEN)['outcome'], 'timed-out')
        self.assertEqual(Endpoint.calls, [])

    def test_skip_and_runner_failure_preserve_nonpassing_outcomes(self):
        descriptor = self.descriptor('memories')
        for error, outcome in ((runner.Skipped(), 'skipped'), (RuntimeError(TOKEN), 'incomplete')):
            with mock.patch('memories.check', side_effect=error):
                result = runner.execute(self.invocation(descriptor['checks'][1]), descriptor, TOKEN)
                self.assertEqual(result['outcome'], outcome)
                self.assertNotIn(TOKEN, json.dumps(result))

    def test_redirect_does_not_forward_authority(self):
        descriptor = self.descriptor('memories')
        Endpoint.mode = 'redirect'
        result = runner.execute(self.invocation(descriptor['checks'][1]), descriptor, TOKEN)
        self.assertNotEqual(result['outcome'], 'passed')
        self.assertEqual(len(Endpoint.calls), 1)

    def test_missing_token_and_unknown_check_never_contact_endpoint(self):
        descriptor = self.descriptor('memories')
        invocation = self.invocation(descriptor['checks'][1])
        self.assertEqual(runner.execute(invocation, descriptor, '')['diagnostics'], ['invalid-invocation'])
        invocation['checkId'] = 'unknown.check'
        self.assertEqual(runner.execute(invocation, descriptor, TOKEN)['diagnostics'], ['unsupported-check'])
        self.assertEqual(Endpoint.calls, [])

    def test_invalid_or_authority_bearing_invocations_are_refused(self):
        descriptor = self.descriptor('memories')
        original = self.invocation(descriptor['checks'][1])
        for field, value in (('token', TOKEN), ('workingBaseline', {}), ('externalEffectsDisabled', False),
                             ('verificationEndpoint', 'http://remote.example'), ('verificationEndpoint', 'https://user:secret@example.com')):
            invocation = copy.deepcopy(original)
            invocation[field] = value
            with self.subTest(field=field), self.assertRaises(ValueError):
                runner.execute(invocation, descriptor, TOKEN)
        self.assertEqual(Endpoint.calls, [])

    def test_production_smokes_are_read_only_with_existing_synthetic_tenants(self):
        for module in ('parties', 'eventstore', 'memories', 'mcpcli', 'tenants'):
            for check in self.descriptor(module)['checks']:
                if 'production' in check['environments']:
                    self.assertEqual((check['kind'], check['effect'], check['tenancy']), ('smoke', 'read', 'existing-synthetic'))
        descriptor = self.descriptor('parties')
        invocation = self.invocation(descriptor['checks'][0])
        invocation.update(environment='production', syntheticTenantMode='existing-synthetic')
        self.assertEqual(runner.execute(invocation, descriptor, TOKEN)['diagnostics'], ['unsafe-context'])

    def test_tenants_positive_flow_mappings_remain_blocked(self):
        registry = runner.load_json(ROOT / 'registry.json')
        tenants = next(module for module in registry['modules'] if module['module'] == 'tenants')
        self.assertEqual({flow['name'] for flow in tenants['criticalFlows']}, {'tenant-management', 'global-administrator-management'})
        for mapping in tenants['criticalFlows'] + tenants['smokeSurfaces']:
            self.assertEqual(mapping['status'], 'blocked')
            self.assertEqual(mapping['checks'], [])

    def test_packaged_cli_outputs_failure_exit_and_sanitized_records(self):
        descriptor = self.descriptor('memories')
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / 'payload').mkdir()
            for file in descriptor['payload']:
                shutil.copyfile(ROOT / file, root / 'payload' / file)
            (root / 'descriptor.json').write_text(json.dumps(descriptor))
            (root / 'invocation.json').write_text(json.dumps(self.invocation(descriptor['checks'][1])))
            import os
            environment = {'PATH': os.environ['PATH'], 'HEXALITH_CHECK_TOKEN': TOKEN}
            payload_bytes = {path.relative_to(root / 'payload'): path.read_bytes()
                             for path in (root / 'payload').rglob('*') if path.is_file()}
            descriptor_bytes = (root / 'descriptor.json').read_bytes()
            for mode, expected in (('passed', 0), ('failed', 1)):
                Endpoint.mode = mode
                process = subprocess.run([sys.executable, str(root / 'payload' / 'runner.py'), '--invocation', str(root / 'invocation.json')],
                                         capture_output=True, text=True, env=environment, timeout=5)
                self.assertEqual(process.returncode, expected)
                self.assertNotIn(TOKEN, process.stdout + process.stderr)
                self.assertEqual(json.loads(process.stdout)['outcome'], mode)
                self.assertEqual({path.relative_to(root / 'payload'): path.read_bytes()
                                  for path in (root / 'payload').rglob('*') if path.is_file()}, payload_bytes)
                self.assertEqual((root / 'descriptor.json').read_bytes(), descriptor_bytes)


if __name__ == '__main__':
    unittest.main()
