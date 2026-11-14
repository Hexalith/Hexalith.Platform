"""Offline artifact verification; invoke with run-with-temp while source is held.

--source identifies the pinned checkout with the final patch applied.
No broker, Dapr runtime, or qualification producer/verifier is executed.
"""
import argparse
import ast
import hashlib
import json
import os
from pathlib import Path
import subprocess
import tarfile
import tempfile
from datetime import datetime, timezone

parser = argparse.ArgumentParser()
parser.add_argument('--source', type=Path, required=True)
args = parser.parse_args()
source = args.source.resolve()
out = Path(__file__).resolve().parent
platform = out.parents[3]

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def git(*argv):
    return subprocess.check_output(['git', *argv], cwd=source, text=True).strip()

identity = json.loads((out / 'source-identity.json').read_text())
assert git('rev-parse', 'HEAD') == identity['commit']
assert git('rev-parse', 'HEAD^{tree}') == identity['tree']
changed = set(git('diff', '--name-only', 'HEAD').splitlines())
changed.update(git('ls-files', '--others', '--exclude-standard').splitlines())
expected = {'pubsub/rabbitmq/rabbitmq.go', 'pubsub/rabbitmq/rabbitmq_test.go', 'pubsub/rabbitmq/rabbitmq_integration_test.go', 'pubsub/rabbitmq/rabbitmq_publish_test.go', 'pubsub/rabbitmq/rabbitmq_publish_transport_test.go'}
assert changed == expected, changed
patch = out / 'contribution.patch'
diff = subprocess.check_output(['git', 'diff', '--binary', '--full-index', 'HEAD'], cwd=source)
for name in sorted(git('ls-files', '--others', '--exclude-standard').splitlines()):
    proc = subprocess.run(['git', 'diff', '--no-index', '--binary', '--full-index', '--', '/dev/null', name], cwd=source, stdout=subprocess.PIPE)
    assert proc.returncode in (0, 1)
    diff += proc.stdout
assert diff == patch.read_bytes(), 'Saved patch differs from held source'
subprocess.run(['git', 'apply', '--reverse', '--check', str(patch)], cwd=source, check=True)
scratch = Path(os.environ['TMPDIR'])
archive = scratch / 'official-source.tar'
with archive.open('wb') as stream:
    subprocess.run(['git', 'archive', 'HEAD'], cwd=source, stdout=stream, check=True)
with tempfile.TemporaryDirectory(dir=scratch, prefix='apply-check-') as verification_tmp:
    clean = Path(verification_tmp)
    with tarfile.open(archive) as package:
        package.extractall(clean, filter='data')
    subprocess.run(['git', 'init', '-q', str(clean)], check=True)
    subprocess.run(['git', 'apply', '--check', '--whitespace=error-all', str(patch)], cwd=clean, check=True)
    subprocess.run(['git', 'apply', str(patch)], cwd=clean, check=True)
    files = {name: sha(source / name) for name in sorted(expected)}
    assert files == {name: sha(clean / name) for name in sorted(expected)}
    for name in ('go.mod', 'go.sum'):
        assert (source / name).read_bytes() == (clean / name).read_bytes()
before = json.loads((out / 'preservation-before.json').read_text())
assert all(sha(platform / name) == digest for name, digest in before['sha256'].items()), 'Preserved input changed'
parents = {
    '_bmad-output/implementation-artifacts/sprint-status.yaml': '  4-17-select-and-qualify-the-durable-production-broker: in-progress',
    'references/Hexalith.EventStore/_bmad-output/implementation-artifacts/sprint-status.yaml': '  3-21-production-dapr-runtime-pin-and-durable-broker-qualification: in-progress',
}
assert all(value in (platform / name).read_text().splitlines() for name, value in parents.items())
vector_source = platform / 'references/Hexalith.EventStore/tools/dapr_broker_qualification.py'
constants = {}
for node in ast.parse(vector_source.read_text()).body:
    if isinstance(node, ast.Assign):
        for target in node.targets:
            if isinstance(target, ast.Name) and target.id in ('BROKERS', 'SCENARIOS'):
                constants[target.id] = ast.literal_eval(node.value)
assert len(constants['BROKERS']) * len(constants['SCENARIOS']) == 24
record = {
    'checkedUtc': datetime.now(timezone.utc).isoformat(), 'officialCommit': identity['commit'], 'officialTree': identity['tree'],
    'patchSha256': sha(patch), 'upstreamFileSha256': files, 'reverseApplyCheck': 'PASS', 'cleanOfficialArchiveForwardApply': 'PASS',
    'appliedFilesMatchTestedCheckout': True, 'unchangedGoModuleFiles': True, 'preservedInputCount': len(before['sha256']),
    'preservedInputsMatch': True, 'parentStatuses': 'both in-progress', 'qualificationVectorCount': 24,
    'method': 'static source/hash/status and patch checks only; no Platform qualification',
}
(out / 'artifact-verification.json').write_text(json.dumps(record, indent=2)+'\n')
print(json.dumps(record, indent=2))
