from pathlib import Path
import hashlib
import json
import re
import subprocess
import tempfile
import time

output = Path(tempfile.mkdtemp(prefix='bmad427-final-parent-'))
print(output, flush=True)
packet = Path('_bmad-output/implementation-artifacts/evidence/epic-4/4-27/native-client-certificate-delivery/review-5')
sources = json.loads((packet / 'verification.json').read_text())['sourceSha256']
for name, expected in sources.items():
    assert hashlib.sha256(Path(name).read_bytes()).hexdigest() == expected, name
results = []
for name, filename in {
    'native-client-certificate': 'test_native_client_certificate.py',
    'execution-boundary': 'test_recovery_execution_boundary.py',
    'immutable-readback': 'test_immutable_readback_driver.py',
    'policy-authentication': 'test_policy_authentication_driver.py',
    'immutable-s3-provider': 'test_immutable_s3_provider.py',
    'common-adapter': 'test_recovery_adapters.py',
    'telemetry-postgres': 'test_telemetry_postgres_adapter.py',
    'receipt-self-test': None,
    'parser-mutant': None,
    'cancellation-sweeps': None,
    'diff-check': None,
}.items():
    if filename:
        argv = ['/usr/bin/python3.14', '-I', '-S', '-B', 'eng/recovery-preparation/' + filename]
    elif name == 'diff-check':
        argv = ['git', 'diff', '--check']
    else:
        script = {'receipt-self-test': 'verify-receipt.py', 'parser-mutant': 'check-parser-mutant.py',
                  'cancellation-sweeps': 'run-cancellation-sweeps.py'}[name]
        argv = ['/usr/bin/python3.14', '-I', '-S', '-B', str(packet / script)]
        if name == 'receipt-self-test':
            argv.append('--self-test')
    started = time.monotonic()
    result = subprocess.run(argv, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    raw = result.stdout
    (output / (name + '.log')).write_bytes(raw)
    item = {'name': name, 'argv': argv, 'exitCode': result.returncode,
            'elapsedSeconds': round(time.monotonic() - started, 3),
            'log': name + '.log', 'logSha256': hashlib.sha256(raw).hexdigest()}
    if filename:
        counts = re.findall(r'^Ran (\d+) tests? in [0-9.]+s$', raw.decode(), re.M)
        outcomes = re.findall(r'^(OK|FAILED)(?: \(([^\n]*)\))?$', raw.decode(), re.M)
        assert len(counts) == 1 and outcomes == [('OK', '')], name
        item.update(tests=int(counts[0]), skips=0, outcome='OK')
    assert result.returncode == 0, (name, raw.decode())
    results.append(item)
    (output / 'commands.json').write_text(json.dumps(results, indent=2) + '\n')
    print(json.dumps(item), flush=True)
for name, expected in sources.items():
    assert hashlib.sha256(Path(name).read_bytes()).hexdigest() == expected, name
(output / 'source-sha256.json').write_text(json.dumps(sources, indent=2, sort_keys=True) + '\n')
print('PASSED', sum(item.get('tests', 0) for item in results), 'tests; source content unchanged', flush=True)
