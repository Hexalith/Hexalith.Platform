"""Slim the re-download record and compile the validation record from retained run logs.

Usage: python3 -I finalize_evidence.py EVIDENCE_DIR LOG_DIR COMMITTED_DIGESTS_JSON
The logs stay outside Git; only their SHA-256, byte counts and parsed totals are recorded.
"""
import hashlib
import json
import re
import sys
from pathlib import Path

evidence, logs, committed = Path(sys.argv[1]), Path(sys.argv[2]), Path(sys.argv[3])
record_path = evidence / 'artifact-redownload.json'
record = json.loads(record_path.read_text())
if 'sha256' in record.get('extracted', {}):
    extracted = record['extracted']['sha256']
    previous = {name: row['sha256'] for name, row in json.loads(committed.read_text())['files'].items()}
    canonical = json.dumps(extracted, sort_keys=True, separators=(',', ':')).encode()
    record['extracted'] = {'files': record['extracted']['files'], 'anySymlink': record['extracted']['anySymlink'],
                           'digestMapSha256': hashlib.sha256(canonical).hexdigest(),
                           'identicalToCommittedRecord': extracted == previous,
                           'committedRecord': '../candidate-01-hosted-attempt-2/artifact-extracted-digests.json'}
    record['scratchDirectory'] = 'fresh session scratch directory (outside Git); the archive bytes are not committed'
    record_path.write_text(json.dumps(record, indent=2) + '\n')


def log(name):
    data = (logs / name).read_bytes()
    text = data.decode()
    row = {'log': name, 'logSha256': hashlib.sha256(data).hexdigest(), 'logBytes': len(data)}
    ran = re.search(r'^Ran (\d+) tests? in ([0-9.]+)s$', text, re.M)
    if ran:
        tail = re.findall(r'^(OK|FAILED \(.*\))$', text, re.M)[-1]
        row.update(tests=int(ran[1]), seconds=float(ran[2]), result=tail)
        failed = re.search(r'FAILED \((.*)\)', tail)
        if failed:
            row['counts'] = dict((key, int(value)) for key, value in (part.split('=') for part in failed[1].split(', ')))
    for key in ('tests', 'pass', 'fail'):
        node = re.search(rf'^ℹ {key} (\d+)$', text, re.M)
        if node:
            row['node_' + key] = int(node[1])
    return row


ES_CWD = 'clean `git archive` of the stated EventStore revision, or the branch worktree'
validation = {
    'pythonVersion': '3.14.4', 'nodeVersion': 'v26.4.0', 'gitVersion': '2.53.0',
    'eventStore': [
        {'revision': 'bb9e2b51fc8a609181077eba2ae900bcbe40745a', 'purpose': 'baseline before any change',
         'argv': ['python3', '-B', '-m', 'unittest', 'discover', '-v', '-s', 'tools/tests', '-p', 'test_dapr*qualification.py'],
         'exitCode': 1, **log('baseline-bb9e2b51-dapr-qualification.log'),
         'firstRejection': 'inputs..github/workflows/dapr-runtime-qualification.yml: source/component bytes drifted'},
        {'revision': 'bb9e2b51fc8a609181077eba2ae900bcbe40745a', 'purpose': 'workflow owner-admission validate_profile on the unfixed base',
         'exitCode': 0, 'result': (logs / 'base-bb9e2b51-owner-admission.log').read_text().strip()},
        {'revision': 'a19bad9de1851b5ebfb22689dbe429c890150a13 (working tree before commit)', 'purpose': 'inventory restore only',
         'argv': ['python3', '-B', '-m', 'unittest', 'discover', '-v', '-s', 'tools/tests', '-p', 'test_dapr*qualification.py'],
         'exitCode': 0, **log('inventory-only-dapr-qualification.log')},
        {'revision': '78bc35aa57e1e33c376847fb2902e69565cce16c (working tree before commit)', 'purpose': 'verifier fix and topic setup extraction',
         'argv': ['python3', '-B', '-m', 'unittest', 'discover', '-s', 'tools/tests', '-p', 'test_dapr*qualification.py'],
         'exitCode': 0, **log('commit2-test_daprqualification.py.log')},
        {'revision': '78bc35aa57e1e33c376847fb2902e69565cce16c (working tree before commit)', 'purpose': 'verifier fix and topic setup extraction',
         'argv': ['python3', '-B', '-m', 'unittest', 'discover', '-s', 'tools/tests', '-p', 'test_dapr_platform_broker_configuration.py'],
         'exitCode': 0, **log('commit2-test_dapr_platform_broker_configuration.py.log')},
        *[{'revision': '495dd24943031ec75e2822c6168ada3c8b9fdb82', 'cwd': ES_CWD, 'purpose': 'final committed tip',
           'argv': ['python3', '-B', '-m', 'unittest', 'discover', '-v', '-s', 'tools/tests', '-p', pattern],
           'exitCode': 0, **log(name)} for pattern, name in (
              ('test_dapr*qualification.py', 'tip-test_daprqualification.py.log'),
              ('test_dapr_platform_broker_configuration.py', 'tip-test_dapr_platform_broker_configuration.py.log'),
              ('test_qualify_internal_actor_admission.py', 'tip-test_qualify_internal_actor_admission.py.log'))],
        {'revision': '495dd24943031ec75e2822c6168ada3c8b9fdb82', 'purpose': 'mutation sensitivity of the four new controls',
         'argv': ['python3', '-I', 'mutation_sensitivity.py', 'WORKTREE', 'SCRATCH', 'refresh_qualification_inventory.py', 'mutation-sensitivity.json'],
         'exitCode': 0, 'result': 'see mutation-sensitivity.json: every mutation fails at least one new control'},
        {'revision': '495dd24943031ec75e2822c6168ada3c8b9fdb82', 'purpose': 'candidate-02 owner admission and producer admission',
         'argv': ['python3', '-B', 'candidate_02_admission.py', 'docs/implementation/story-4-17-broker-candidate-02.json',
                  'b8601e3a6d4e8460d039573ca2f7e1728e52aa67e61fc0710d74cab5d072580b', 'candidate-02-offline-admission.json'],
         'exitCode': 0, 'result': 'see candidate-02-offline-admission.json'},
        {'revision': '02a4d3b2c9fedd18ea2da4d754ab4b68f7e29213 with the fixed file (non-authoritative)',
         'purpose': 'fixed sidecar identification against the real attempt-2 packet',
         'argv': ['python3', '-I', 'diagnostic_fixed_identification.py', 'PINNED_ARCHIVE', 'FIXED_FILE', 'PACKET', 'ORIGINAL_CONFIG', 'FRESH_COPY', 'diagnostic-fixed-identification.json'],
         'exitCode': 0, 'result': 'integrity 0 (PASS), technical 1 (six named FAILs), authority 1 (exact expected rejection)'},
        {'purpose': 'merge into current EventStore main without writing refs',
         'argv': ['git', 'merge-tree', '--write-tree', 'f2062f7f7bd8b3dcae946ba209b933ad8e610ff2', '495dd24943031ec75e2822c6168ada3c8b9fdb82'],
         'exitCode': 0, 'result': 'clean, tree ba11ff85d34b779136ecd8837292fad10368a81b; none of the 37 files changed by bb9e2b51f..f2062f7f7 is a platform-config-v1 input'},
    ],
    'platform': [
        {'revision': 'aa921804b5e3fcf698bf333f40ec50c390646692 (current workflow)', 'cwd': 'Platform checkout',
         'argv': ['node', '--test', 'tests/source-release/broker-candidate-workflow.test.mjs'], 'exitCode': 0,
         **log('platform-current-broker-workflow.log')},
        {'revision': 'aa921804b5e3fcf698bf333f40ec50c390646692 + candidate-02-platform-change.patch', 'cwd': 'detached scratch worktree',
         'argv': ['node', '--test', 'tests/source-release/broker-candidate-workflow.test.mjs'], 'exitCode': 0,
         **log('platform-candidate-02-broker-workflow.log')},
        {'revision': 'aa921804b5e3fcf698bf333f40ec50c390646692 + candidate-02-platform-change.patch', 'cwd': 'detached scratch worktree',
         'argv': ['npm', 'run', 'test:source-release'], 'exitCode': 0, **log('platform-candidate-02-source-release.log')},
    ],
    'notRun': ['dotnet build or C# tests: no C#, project or workflow file changed in EventStore',
               'any Docker, broker, hosted CI, dispatch, tag or push'],
}
(evidence / 'validation-results.json').write_text(json.dumps(validation, indent=2) + '\n')
print(json.dumps({row.get('log', row.get('purpose')): row.get('tests', row.get('node_tests', row.get('result'))) for row in validation['eventStore'] + validation['platform']}, indent=1))
