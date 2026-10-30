"""Verify retained auditing without the checkout or original runtime paths."""
from pathlib import Path
import json
import shutil
import subprocess
import sys
import tempfile

out = Path(__file__).resolve().parent
with tempfile.TemporaryDirectory(prefix='story25-requalification-relocated-') as temporary:
    retained = Path(temporary) / 'retained'
    shutil.copytree(out, retained)
    for path in (retained / 'live-final').glob('command-*.json'):
        command = json.loads(path.read_text())
        command['testResultPath'] = str(Path(temporary) / 'unavailable-original-runtime' / path.stem / 'test-result.json')
        path.write_text(json.dumps(command, indent=2) + '\n')
    result = subprocess.run([sys.executable, str(retained / 'audit.py')], cwd=temporary, capture_output=True, text=True)
    assert result.returncode == 0, result.stdout + result.stderr
    audit = json.loads((retained / 'retained-audit.json').read_text())
    assert audit['runtimeArchiveBytesReobserved'] is False
    assert len(audit['scenarios']) == 4
    negative_controls = []
    def refuses(label, path, replacement):
        original = path.read_bytes()
        if replacement is None:
            path.unlink()
        else:
            path.write_bytes(replacement)
        try:
            check = subprocess.run([sys.executable, str(retained / 'audit.py')], cwd=temporary, capture_output=True, text=True)
            assert check.returncode != 0, label
            negative_controls.append(label)
        finally:
            path.write_bytes(original)
    refuses('deleted-unreferenced-final-archive-file', retained / 'live-final/final-diagnostics/failure/cleanup-request.json', None)
    path = retained / 'live-final/final-diagnostics/failure/test-result.json'
    changed = json.loads(path.read_text())
    changed['retention']['failureRunIds'] = []
    refuses('lost-historical-failure-identities', path, json.dumps(changed).encode())
    path = retained / 'live-final/peer-before.json'
    changed = json.loads(path.read_text())
    changed['ContainerIds'][0] = 'changed-initial-peer-container'
    refuses('changed-initial-peer-identity', path, json.dumps(changed).encode())
    attempts = sorted((retained / 'live-final').glob('cleanup-failure-[12].json'))
    refuses('missing-first-cleanup-attempt', attempts[0], None)
    path = retained / 'prior-source-fingerprints.json'
    changed = json.loads(path.read_text())
    changed[next(iter(changed))] = '0' * 64
    refuses('mismatched-prior-source-fingerprint', path, json.dumps(changed).encode())
    receipt = {'passed': True, 'workingDirectoryOutsideCheckout': True, 'originalRuntimePathsUnavailable': True, 'scenariosAudited': 4, 'auditOutput': result.stdout.strip(), 'negativeControlsPassed': negative_controls}
    (out / 'portability.json').write_text(json.dumps(receipt, indent=2) + '\n')
    print(json.dumps(receipt))
