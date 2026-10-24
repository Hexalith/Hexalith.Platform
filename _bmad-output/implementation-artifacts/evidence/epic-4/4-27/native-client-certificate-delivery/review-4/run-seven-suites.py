#!/usr/bin/env python3
"""Run the seven prescribed offline suites and capture exact commands/results."""
import hashlib
import json
from pathlib import Path
import re
import subprocess
import time

PACKET = Path(__file__).absolute().parent
SUITES = {
    'native-client-certificate': 'test_native_client_certificate.py',
    'execution-boundary': 'test_recovery_execution_boundary.py',
    'immutable-readback': 'test_immutable_readback_driver.py',
    'policy-authentication': 'test_policy_authentication_driver.py',
    'immutable-s3-provider': 'test_immutable_s3_provider.py',
    'common-adapter': 'test_recovery_adapters.py',
    'telemetry-postgres': 'test_telemetry_postgres_adapter.py',
}
results = []
for name, filename in SUITES.items():
    argv = ['/usr/bin/python3.14', '-I', '-S', '-B', 'eng/recovery-preparation/' + filename]
    start = time.monotonic()
    result = subprocess.run(argv, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, check=False)
    (PACKET / (name + '.log')).write_bytes(result.stdout)
    text = result.stdout.decode('utf-8')
    counts = re.findall(r'^Ran ([0-9]+) tests? in [0-9.]+s$', text, re.MULTILINE)
    outcomes = re.findall(r'^(OK|FAILED)(?: \(([^\n]*)\))?$', text, re.MULTILINE)
    assert len(counts) == len(outcomes) == 1 and outcomes[0] == ('OK', '') and result.returncode == 0, name + ' failed'
    results.append({'name': name, 'argv': argv, 'exitCode': result.returncode, 'tests': int(counts[0]), 'skips': 0,
        'outcome': outcomes[0][0], 'elapsedSeconds': round(time.monotonic() - start, 3), 'log': name + '.log',
        'logSha256': hashlib.sha256(result.stdout).hexdigest()})
    print(name + ': ' + counts[0] + ' tests, zero skips', flush=True)
(PACKET / 'command-results.json').write_text(json.dumps(results, indent=2, sort_keys=True) + '\n')
