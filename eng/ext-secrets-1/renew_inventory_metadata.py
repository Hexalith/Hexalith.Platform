#!/usr/bin/env python3
"""Renew only the installed restricted metadata token, recording lease metadata only."""
import base64
import json
from pathlib import Path
import sys
from datetime import datetime, timezone

from install_isolated_registry import NS, kube
from install_current_inventory import application_request
from native_journal import durable_write


def renew(evidence):
    token = bytearray(base64.b64decode(kube('get', 'secret', 'ext-secrets-1-inventory-metadata-token', '-n', NS,
                                           '-o', 'jsonpath={.data.token}').stdout, validate=True))
    try:
        result = json.loads(application_request(token, 'token', 'renew', '-format=json').stdout)['auth']
        public = {'observedAt': datetime.now(timezone.utc).isoformat(), 'credentialReference': NS + '/Secret/ext-secrets-1-inventory-metadata-token#token',
                  'accessor': result['accessor'], 'leaseDurationSeconds': result['lease_duration'], 'renewable': result['renewable'],
                  'credentialValueRecorded': False, 'explicitMaximumLifetimeSeconds': 604800}
        durable_write(Path(evidence) / 'metadata-credential-renewal.json', public)
        return public
    finally:
        token[:] = b'\0' * len(token)


if __name__ == '__main__':
    renew(sys.argv[1])
    print('restricted metadata credential renewed; only public lease metadata recorded')
