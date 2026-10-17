#!/usr/bin/env python3
"""Renew only the exact isolated HMAC data credential; record public lease facts."""
import base64
from datetime import datetime, timezone
import json
from pathlib import Path
import sys

from install_isolated_registry import NS, kube
from install_current_inventory import application_request
from native_journal import durable_write
from provision_operational_hmac import DATA_SECRET, DATA_POLICY


def renew(evidence):
    token = bytearray(base64.b64decode(kube('get', 'secret', DATA_SECRET, '-n', NS, '-o', 'jsonpath={.data.token}').stdout, validate=True))
    try:
        before = json.loads(application_request(token, 'token', 'lookup', '-format=json').stdout)['data']
        if before.get('policies') != [DATA_POLICY]:
            raise RuntimeError('unexpected-isolated-data-credential-policy')
        auth = json.loads(application_request(token, 'token', 'renew', '-format=json').stdout)['auth']
        after = json.loads(application_request(token, 'token', 'lookup', '-format=json').stdout)['data']
        public = {'observedAt': datetime.now(timezone.utc).isoformat(),
                  'credentialReference': NS + '/Secret/' + DATA_SECRET + '#token',
                  'lease': {key: after.get(key) for key in ['accessor', 'creation_time', 'expire_time', 'ttl', 'explicit_max_ttl', 'renewable', 'policies']},
                  'renewedLeaseDurationSeconds': auth['lease_duration'], 'credentialValueRecorded': False,
                  'replacementRequiredBeforeMaximumLifetime': True, 'contractOperations': 0, 'providerDataReads': 0}
        durable_write(Path(evidence) / 'hmac-data-credential-renewal.json', public)
        return public
    finally:
        token[:] = b'\0' * len(token)


if __name__ == '__main__':
    try:
        renew(sys.argv[1])
        print('restricted HMAC data credential renewed; only public lease metadata retained')
    except Exception:
        raise SystemExit('isolated HMAC data credential renewal failed; values suppressed') from None
