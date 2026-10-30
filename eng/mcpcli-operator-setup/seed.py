#!/usr/bin/env python3
"""Task-only owner-API seed executor; reviewed input, private port forwards, no retries."""
from __future__ import annotations
import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import re
import stat
import sys
import time
from urllib.error import HTTPError
from urllib.parse import urlsplit
from urllib.request import Request, build_opener, ProxyHandler, HTTPRedirectHandler
from prepare import require

STEPS = ['bootstrap', 'bootstrap-status', 'create-tenant', 'create-status', 'add-member',
         'member-status', 'get-tenant', 'provision-memories', 'get-memories']


def private_json(path):
    path = Path(path)
    require(path.is_absolute() and path.is_file() and not path.is_symlink()
            and stat.S_IMODE(path.stat().st_mode) == 0o600, 'private-input-mode-must-be-0600')
    return json.loads(path.read_text())


def validate(plan, namespace, tenant):
    require(plan.get('namespace') == namespace and plan.get('tenant') == tenant
            and re.fullmatch(r'mcpcli-417-[a-z0-9-]+', namespace) is not None, 'seed-scope-mismatch')
    require([s.get('name') for s in plan.get('steps', [])] == STEPS, 'complete-ordered-owner-seed-required')
    require(plan.get('operatorRole') == 'TenantContributor' and plan.get('operatorSubject'), 'writer-subject-and-role-required')
    allowed_ports = plan.get('forwardedPorts')
    require(isinstance(allowed_ports, list) and len(allowed_ports) == 2
            and len(set(allowed_ports)) == 2 and all(type(p) is int and 1024 <= p <= 65535 for p in allowed_ports), 'private-forwarded-ports-required')
    for step in plan['steps']:
        endpoint = urlsplit(step.get('url', ''))
        require(endpoint.scheme == 'http' and endpoint.hostname == '127.0.0.1' and endpoint.port in allowed_ports
                and not endpoint.username and not endpoint.query and not endpoint.fragment, 'external-seed-write-refused')
        require(step.get('method') in ('GET', 'POST') and step.get('credential') in ('bootstrap', 'admin', 'operator', 'memories'), 'seed-request-invalid')
        require(step.get('expectedStatus') in (200, 202) and isinstance(step.get('assertions'), dict)
                and (step['assertions'] or step.get('expectedStatus') == 202), 'persisted-assertions-required')
    for name, wire in [('create-tenant', 'create-tenant'), ('add-member', 'add-user-to-tenant')]:
        step = next(s for s in plan['steps'] if s['name'] == name)
        body = step.get('body', {}); payload = body.get('payload', {})
        require(step['method'] == 'POST' and urlsplit(step['url']).path == '/api/v1/commands'
                and body.get('tenant') == 'system' and body.get('domain') == 'tenants'
                and body.get('aggregateId') == tenant and body.get('commandType') == wire
                and payload.get('TenantId') == tenant, 'seed-command-outside-task-tenant')
        require(re.fullmatch(r'[0-7][0-9A-HJKMNP-TV-Z]{25}', body.get('messageId', '')) is not None, 'canonical-message-id-required')
        if name == 'add-member':
            require(payload.get('UserId') == plan['operatorSubject'] and payload.get('Role') == 'TenantContributor', 'seed-member-scope-invalid')
    bootstrap = plan['steps'][0]
    require(bootstrap['method'] == 'POST' and urlsplit(bootstrap['url']).path == '/api/v1/commands/bound/workload'
            and bootstrap['credential'] == 'bootstrap', 'owner-bound-bootstrap-required')
    principal = bootstrap.get('body', {}).get('principalId')
    require(principal and principal != plan['operatorSubject']
            and bootstrap.get('body', {}).get('command', {}).get('payload', {}).get('UserId') == principal, 'bootstrap-and-client-identities-must-be-separated')
    command = bootstrap.get('body', {}).get('command', {})
    require(command.get('tenant') == 'system' and command.get('domain') == 'global-administrators'
            and command.get('commandType') == 'bootstrap-global-admin', 'owner-bootstrap-command-invalid')
    for submitted, observed in [('bootstrap', 'bootstrap-status'), ('create-tenant', 'create-status'), ('add-member', 'member-status')]:
        request = next(s for s in plan['steps'] if s['name'] == submitted)
        body = request['body']['command'] if submitted == 'bootstrap' else request['body']
        status = next(s for s in plan['steps'] if s['name'] == observed)
        require(urlsplit(status['url']).path == '/api/v1/commands/status/' + body['messageId']
                and status['assertions'].get('messageId') == body['messageId'], 'status-message-binding-required')
    for name in ('bootstrap-status', 'create-status', 'member-status'):
        step = next(s for s in plan['steps'] if s['name'] == name)
        require(step['method'] == 'GET' and urlsplit(step['url']).path.startswith('/api/v1/commands/status/')
                and step['assertions'].get('status') == 'Completed'
                and step['assertions'].get('minimum:committedEventSequence', 0) > 0
                and step['assertions'].get('minimum:eventCount', 0) > 0, 'committed-owner-state-required')
    query = next(s for s in plan['steps'] if s['name'] == 'get-tenant')
    require(query['method'] == 'POST' and urlsplit(query['url']).path == '/api/v1/queries'
            and query.get('body', {}).get('tenant') == 'system'
            and query.get('body', {}).get('payload', {}).get('TenantId') == tenant
            and query['assertions'].get('payload.tenantId') == tenant, 'persisted-tenant-confirmation-required')
    provisioning = next(s for s in plan['steps'] if s['name'] == 'provision-memories')
    require(provisioning['method'] == 'POST' and urlsplit(provisioning['url']).path == '/api/v1/tenants'
            and provisioning.get('body', {}).get('tenantId') == tenant, 'task-memories-provisioning-required')
    memories = plan['steps'][-1]
    require(memories['method'] == 'GET' and urlsplit(memories['url']).path == '/api/v1/tenants/' + tenant
            and memories['assertions'].get('id') == tenant and memories['assertions'].get('status') == 'Active', 'persisted-memories-confirmation-required')


def verify_review(plan_path, review_path, review_sha256, namespace, tenant):
    raw = Path(review_path).read_bytes()
    require(hashlib.sha256(raw).hexdigest() == review_sha256, 'review-packet-hash-mismatch')
    review = json.loads(raw)
    require(review.get('schema') == 'hexalith.mcpcli.operator-review/v1'
            and review.get('target', {}).get('namespace') == namespace and review.get('tenant') == tenant,
            'review-scope-mismatch')
    expiry = datetime.fromisoformat(review['expiresAt'].replace('Z', '+00:00'))
    require(expiry.tzinfo is not None and datetime.now(timezone.utc) < expiry, 'review-expired')
    plan_raw = Path(plan_path).read_bytes()
    require(hashlib.sha256(plan_raw).hexdigest() == review.get('seed', {}).get('privatePlanSha256'),
            'reviewed-seed-plan-changed')
    return review, json.loads(plan_raw)


class NoRedirect(HTTPRedirectHandler):
    def redirect_request(self, request, file, code, message, headers, newurl):
        raise ValueError('seed-redirect-refused')


def assertion(document, path, expected):
    minimum = path.startswith('minimum:')
    if minimum:
        path = path[len('minimum:'):]
    for key in path.split('.'):
        document = document[key]
    require(document >= expected if minimum else document == expected, 'persisted-owner-assertion-failed')


def execute(plan, authorities, journal, timeout_seconds=45, clock=time.monotonic, pause=time.sleep):
    journal = Path(journal)
    require(not journal.exists(), 'seed-journal-exists-do-not-retry')
    journal.mkdir(mode=0o700, parents=True)
    opener = build_opener(ProxyHandler({}), NoRedirect())
    for index, step in enumerate(plan['steps']):
        observation = step['name'] in ('bootstrap-status', 'create-status', 'member-status', 'get-tenant', 'get-memories')
        receipt = {'step': step['name'], 'outcome': 'uncertain', 'attempts': 0, 'requestSha256': hashlib.sha256(
            json.dumps(step, sort_keys=True).encode()).hexdigest()}
        path = journal / (str(index).zfill(2) + '-' + step['name'] + '.json')
        path.write_text(json.dumps(receipt) + '\n')
        deadline = clock() + timeout_seconds
        while True:
            remaining = deadline - clock()
            require(remaining > 0, 'owner-observation-deadline-exceeded')
            headers = authorities[step['credential']]
            request = Request(step['url'], method=step['method'], headers={**headers, 'Content-Type': 'application/json'},
                              data=json.dumps(step['body']).encode() if 'body' in step else None)
            receipt['attempts'] += 1
            path.write_text(json.dumps(receipt) + '\n')
            try:
                with opener.open(request, timeout=min(30, remaining)) as response:
                    require(response.status == step['expectedStatus'], 'owner-api-status-failed')
                    raw = response.read()
                    document = json.loads(raw) if raw.strip() else {}
            except HTTPError as error:
                if not (observation and error.code == 404):
                    raise
                document = {}
            if observation and document.get('status') in ('Rejected', 'Failed', 'DeadLettered', 'TimedOut'):
                receipt['outcome'] = 'terminal-failure'; path.write_text(json.dumps(receipt) + '\n')
                raise ValueError('owner-command-terminal-failure')
            try:
                for key, expected in step['assertions'].items():
                    assertion(document, key, expected)
            except (ValueError, KeyError, TypeError):
                require(observation, 'submitted-write-outcome-uncertain')
                pause(min(0.5, max(0, deadline - clock())))
                continue
            receipt.update(outcome='confirmed', assertions=step['assertions'])
            path.write_text(json.dumps(receipt) + '\n')
            break


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--plan', required=True); p.add_argument('--namespace', required=True); p.add_argument('--tenant', required=True)
    p.add_argument('--authorities', help='Private owner-minted headers, separate from operator credentials')
    p.add_argument('--review'); p.add_argument('--review-sha256')
    p.add_argument('--journal'); p.add_argument('--execute-reviewed', action='store_true')
    args = p.parse_args()
    try:
        plan = private_json(args.plan); validate(plan, args.namespace, args.tenant)
        if args.execute_reviewed:
            require(args.authorities and args.journal and args.review and args.review_sha256, 'review-hash-private-authorities-and-fresh-journal-required')
            _, plan = verify_review(args.plan, args.review, args.review_sha256, args.namespace, args.tenant)
            validate(plan, args.namespace, args.tenant)
            execute(plan, private_json(args.authorities), args.journal)
        print('Seed plan validated.' if not args.execute_reviewed else 'Seed assertions confirmed.')
        return 0
    except Exception as error:
        print('Seed stopped: ' + (str(error) if isinstance(error, ValueError) else type(error).__name__)
              + '. An uncertain write must not be retried automatically.', file=sys.stderr)
        return 1


if __name__ == '__main__':
    raise SystemExit(main())
