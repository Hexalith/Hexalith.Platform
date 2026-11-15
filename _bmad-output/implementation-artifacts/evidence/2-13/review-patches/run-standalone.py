"""Run the continuing standalone controls and retain their actual no-skip counts."""
from pathlib import Path
import hashlib, json, re, subprocess, sys

root = Path(__file__).resolve().parents[5]
out = Path(__file__).resolve().parent
commands = [(name, [sys.executable, '-B', str(root / 'eng' / (name + '.py'))], 'python') for name in
    ['test_builds_tuple', 'test_builds_publication', 'test_current_tuple_source', 'test_tuple_collectors',
     'test_tuple_runtime_context', 'test_tuple_sdk_suites', 'test_tuple_output_identity', 'test_current_catalog_view',
     'test_host_builds_resolution', 'test_historical_security_packages', 'test_verify_platform_pins']]
commands += [('source-release', ['npm', 'run', 'test:source-release'], 'node'),
             ('extension-node', ['node', '--test', 'eng/test-extension-release/gate.test.mjs'], 'node'),
             ('extension-python', [sys.executable, '-B', 'eng/test-extension-release/run-controls.py'], 'python')]
rows = []
for name, command, kind in commands:
    log = out / (name + '.log')
    with log.open('x') as stream:
        result = subprocess.run(command, cwd=root, stdout=stream, stderr=subprocess.STDOUT, timeout=1200)
    text = log.read_text()
    if kind == 'python':
        counts = [int(value) for value in re.findall(r'Ran (\d+) tests? in ', text)]
        count = sum(counts)
        assert count > 0 and not re.search(r'\bskipped=|\bFAILED\b', text), name
    else:
        counts = re.findall(r'(?:# |ℹ )tests (\d+)', text)
        count = int(counts[-1])
        assert re.search(r'(?:# |ℹ )skipped 0', text), name
        assert re.search(r'(?:# |ℹ )fail 0', text), name
    row = {'label': name, 'command': command, 'cwd': str(root), 'exitCode': result.returncode,
           'executed': count, 'skipped': 0, 'logSha256': hashlib.sha256(log.read_bytes()).hexdigest()}
    rows.append(row)
    (out / 'standalone-results.json').write_text(json.dumps(rows, indent=2) + '\n')
    print(name, result.returncode, count, flush=True)
    assert result.returncode == 0, name
(root / '_bmad-output/implementation-artifacts/evidence/2-13/review-patches/standalone-summary.json').write_text(json.dumps({
    'passed': True, 'standalonePython': sum(r['executed'] for r in rows[:-3]),
    'sourceRelease': rows[-3]['executed'], 'extensionNode': rows[-2]['executed'],
    'extensionPython': rows[-1]['executed'], 'skipped': 0, 'hostedMainAcceptance': False}, indent=2) + '\n')
