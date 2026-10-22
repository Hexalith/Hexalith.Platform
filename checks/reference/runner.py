#!/usr/bin/env python3
"""V1 reference payload. Hosted authority, release observation and isolation belong to the executor."""
import argparse
import datetime as dt
import importlib
import json
import os
from pathlib import Path
import re
import selectors
import signal
import socket
import subprocess
import sys
import time
import urllib.error
import urllib.parse
import urllib.request

IDENTITY = ('invocationId', 'checkId', 'attemptId', 'environment', 'executionMode',
            'servedReleaseDigest', 'suiteDigest', 'descriptorDigest', 'profileDigest',
            'configurationDigest', 'workingBaseline')
INVOCATION_FIELDS = {'schema', *IDENTITY, 'verificationEndpoint', 'syntheticTenant',
                     'syntheticTenantMode', 'fixturePartyId', 'fixtureCaseId',
                     'externalEffectsDisabled', 'startedAt', 'deadlineAt'}
ID = re.compile(r'[a-z][a-z0-9.-]{0,127}\Z')
DIGEST = re.compile(r'sha256:[0-9a-f]{64}\Z')
TIMESTAMP = re.compile(r'[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}(?:\.[0-9]{1,7})?(?:Z|[+-][0-9]{2}:[0-9]{2})\Z')


class Skipped(Exception):
    """An explicit nonpassing skip."""


def timestamp(value):
    if not isinstance(value, str) or not TIMESTAMP.fullmatch(value):
        raise ValueError('explicit timestamp offset required')
    parsed = dt.datetime.fromisoformat(value.replace('Z', '+00:00'))
    if parsed.tzinfo is None:
        raise ValueError('timezone required')
    return parsed


def instant():
    return dt.datetime.now(dt.timezone.utc).isoformat().replace('+00:00', 'Z')


def load_json(path):
    def unique(pairs):
        result = {}
        for key, value in pairs:
            if key in result:
                raise ValueError('duplicate property')
            result[key] = value
        return result
    if Path(path).stat().st_size > 4 * 1024 * 1024:
        raise ValueError('oversize input')
    return json.loads(Path(path).read_bytes(), object_pairs_hook=unique)


def validate_invocation(invocation):
    if set(invocation) != INVOCATION_FIELDS or invocation['schema'] != 'hexalith.check-invocation.v1':
        raise ValueError('closed invocation required')
    for field in ('invocationId', 'checkId', 'attemptId', 'syntheticTenant', 'fixturePartyId', 'fixtureCaseId'):
        if not isinstance(invocation[field], str) or not ID.fullmatch(invocation[field]):
            raise ValueError('invalid identity')
    for field in ('servedReleaseDigest', 'suiteDigest', 'descriptorDigest', 'profileDigest', 'configurationDigest'):
        if not isinstance(invocation[field], str) or not DIGEST.fullmatch(invocation[field]):
            raise ValueError('invalid digest')
    if invocation['environment'] not in ('staging', 'production') or invocation['executionMode'] not in ('hosted', 'controlled'):
        raise ValueError('invalid context')
    baseline = invocation['workingBaseline']
    if baseline is not None:
        if not isinstance(baseline, dict) or set(baseline) != {'attemptId', 'releaseDigest', 'profileDigest', 'configurationDigest'}:
            raise ValueError('baseline required')
        if not ID.fullmatch(baseline['attemptId']) or any(not DIGEST.fullmatch(baseline[key]) for key in baseline if key != 'attemptId'):
            raise ValueError('invalid baseline')
    if invocation['syntheticTenantMode'] not in ('existing-synthetic', 'run-scoped-synthetic') or invocation['externalEffectsDisabled'] is not True:
        raise ValueError('unsafe context')
    if invocation['environment'] == 'production' and invocation['syntheticTenantMode'] != 'existing-synthetic':
        raise ValueError('unsafe production context')
    endpoint = urllib.parse.urlsplit(invocation['verificationEndpoint'])
    loopback = endpoint.hostname in ('localhost', '127.0.0.1', '::1')
    if not endpoint.hostname or endpoint.username is not None or endpoint.password is not None or endpoint.query or endpoint.fragment:
        raise ValueError('invalid endpoint')
    if endpoint.scheme != 'https' and not (invocation['executionMode'] == 'controlled' and endpoint.scheme == 'http' and loopback):
        raise ValueError('https required')
    duration = (timestamp(invocation['deadlineAt']) - timestamp(invocation['startedAt'])).total_seconds()
    if not 0 < duration <= 3600:
        raise ValueError('finite deadline required')


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, request, fp, code, message, headers, new_url):
        return None


class Context:
    """Only the endpoint and in-memory synthetic token supply network authority."""
    def __init__(self, invocation, token):
        self.invocation = invocation
        self.token = token
        self.deadline = timestamp(invocation['deadlineAt'])
        self.opener = urllib.request.build_opener(urllib.request.ProxyHandler({}), NoRedirect())

    def remaining(self):
        remaining = (self.deadline - dt.datetime.now(dt.timezone.utc)).total_seconds()
        if remaining <= 0:
            raise TimeoutError()
        return remaining

    def request(self, method, path, payload=None):
        if not path.startswith('/') or path.startswith('//') or any(segment in ('.', '..') for segment in urllib.parse.urlsplit(path).path.split('/')):
            raise ValueError('unsafe relative route')
        endpoint = self.invocation['verificationEndpoint'].rstrip('/')
        data = None if payload is None else json.dumps(payload, separators=(',', ':')).encode()
        request = urllib.request.Request(endpoint + path, data=data, method=method,
                                         headers={'Authorization': 'Bearer ' + self.token, 'Content-Type': 'application/json'})
        try:
            response = self.opener.open(request, timeout=self.remaining())
        except urllib.error.HTTPError as error:
            response = error
        with response:
            body = response.read(1024 * 1024 + 1)
            if len(body) > 1024 * 1024:
                raise ValueError('oversize response')
            self.remaining()
            return response.status, json.loads(body) if body else None

    def poll(self, callback):
        while True:
            value = callback()
            if value:
                return value
            time.sleep(min(0.1, self.remaining()))

    def run_candidate(self, operation, payload):
        # Story 4.20 mounts the exact release-bound McpCli candidate at this fixed path.
        command = ['/candidate/hexalith', '--url', self.invocation['verificationEndpoint'],
                   '--tenant', self.invocation['syntheticTenant'], '--read-only', '--format', 'json',
                   'query', operation, '--payload', json.dumps(payload)]
        environment = {'PATH': '/usr/bin:/bin', 'EVENTSTORE_TOKEN': self.token,
                       'DOTNET_CLI_TELEMETRY_OPTOUT': '1', 'HOME': '/tmp/check-client'}
        with subprocess.Popen(command, stdin=subprocess.DEVNULL, stdout=subprocess.PIPE,
                              stderr=subprocess.DEVNULL, env=environment, bufsize=0) as process:
            try:
                output = bytearray()
                with selectors.DefaultSelector() as selector:
                    selector.register(process.stdout, selectors.EVENT_READ)
                    while True:
                        if not selector.select(timeout=self.remaining()):
                            raise TimeoutError()
                        chunk = os.read(process.stdout.fileno(), min(65536, 1024 * 1024 + 1 - len(output)))
                        if not chunk:
                            break
                        output.extend(chunk)
                        if len(output) > 1024 * 1024:
                            raise AssertionError()
                if process.wait(timeout=self.remaining()) != 0:
                    raise AssertionError()
                self.remaining()
                return json.loads(output)
            except BaseException:
                process.kill()
                process.wait()
                raise


def execute(invocation, descriptor, token):
    """Returns a self-report, which only executor-side trusted validation may admit as evidence."""
    validate_invocation(invocation)
    matches = [check for check in descriptor['checks'] if check['id'] == invocation['checkId']]
    started = instant()
    result = {'schema': 'hexalith.check-result.v1', **{key: invocation[key] for key in IDENTITY},
              'outcome': 'incomplete', 'complete': False, 'startedAt': started,
              'completedAt': started, 'diagnostics': ['runner-failure']}
    try:
        if len(matches) != 1:
            result['diagnostics'] = ['unsupported-check']
            return result
        check = matches[0]
        duration = (timestamp(invocation['deadlineAt']) - timestamp(invocation['startedAt'])).total_seconds()
        if invocation['environment'] not in check['environments'] or invocation['syntheticTenantMode'] != check['tenancy'] or duration > check['timeoutSeconds']:
            result['diagnostics'] = ['unsafe-context']
            return result
        if check['effect'] == 'synthetic-write' and invocation['syntheticTenantMode'] != 'run-scoped-synthetic':
            result['diagnostics'] = ['unsafe-context']
            return result
        if invocation['environment'] == 'production' and (check['effect'] != 'read' or check['kind'] != 'smoke'):
            result['diagnostics'] = ['unsafe-context']
            return result
        if not token or '\r' in token or '\n' in token:
            result['diagnostics'] = ['invalid-invocation']
            return result
        context = Context(invocation, token)
        context.remaining()
        module = descriptor['module']
        if module not in ('parties', 'eventstore', 'tenants', 'memories', 'mcpcli'):
            raise ValueError('unsupported module')
        implementation = importlib.import_module(module)
        # The executor kills the sandbox at its own deadline; this is a local fail-fast aid.
        previous = signal.signal(signal.SIGALRM, lambda *_: (_ for _ in ()).throw(TimeoutError()))
        signal.setitimer(signal.ITIMER_REAL, context.remaining())
        try:
            implementation.check(context, check['id'])
            context.remaining()
        finally:
            signal.setitimer(signal.ITIMER_REAL, 0)
            signal.signal(signal.SIGALRM, previous)
        result.update(outcome='passed', complete=True, diagnostics=[])
    except (TimeoutError, socket.timeout, subprocess.TimeoutExpired):
        result.update(outcome='timed-out', complete=False, diagnostics=['deadline-expired'])
    except Skipped:
        result.update(outcome='skipped', complete=False, diagnostics=['skipped'])
    except AssertionError:
        result.update(outcome='failed', complete=True, diagnostics=['assertion-failed'])
    except Exception:
        result.update(outcome='incomplete', complete=False, diagnostics=['runner-failure'])
    finally:
        result['completedAt'] = instant()
    return result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--invocation', required=True)
    arguments = parser.parse_args()
    try:
        invocation = load_json(arguments.invocation)
        descriptor = load_json(Path(__file__).resolve().parent.parent / 'descriptor.json')
        result = execute(invocation, descriptor, os.environ.get('HEXALITH_CHECK_TOKEN', ''))
    except Exception:
        print('{"error":"invalid-invocation"}')
        return 2
    print(json.dumps(result, sort_keys=True, separators=(',', ':')))
    return 0 if result['outcome'] == 'passed' and result['complete'] else 1


if __name__ == '__main__':
    sys.exit(main())
