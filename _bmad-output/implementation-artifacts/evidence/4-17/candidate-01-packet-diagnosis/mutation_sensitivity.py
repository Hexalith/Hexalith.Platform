"""Show the new controls fail against weaker or reverted rules (scratch copies only).

Usage: python3 -I mutation_sensitivity.py WORKTREE SCRATCH_PARENT REFRESH_SCRIPT RESULT_JSON
Each mutation copies the worktree (without .git) to a fresh directory, edits one rule, re-seals
the Platform lock so profile admission is not the failing reason, and runs the four new controls.
"""
import json
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

worktree, parent, refresh, result_path = (Path(a).resolve() for a in sys.argv[1:5])
CONFIG = 'tools/dapr_platform_broker_configuration.py'
HARNESS = 'tools/qualify-dapr-brokers.py'
TESTS = ['test_dapr_platform_broker_configuration.PlatformPacketTests.testProducerKafkaTopicCreationConfigIsNotIdentifiedAsSidecarLaunch',
         'test_dapr_platform_broker_configuration.PlatformPacketTests.testUnmeasuredExecutablesAndMisplacedSidecarFlagsStillFail',
         'test_dapr_platform_broker_configuration.PlatformPacketTests.testDuplicatedOrAssignmentFormSidecarFlagsStillFail',
         'test_dapr_platform_broker_configuration.ConfigurationTests.testActualProducerKeepsKafkaPressureRecordsBeyondTopicRetention']
MUTATIONS = {
    'M1-pinned-02a4d3b2-rule': (CONFIG, '        fields = SIDECAR_FIELDS[:2] if administration else SIDECAR_FIELDS\n',
                                '        fields = SIDECAR_FIELDS\n'),
    'M2-skip-every-docker-exec': (CONFIG, '        fields = SIDECAR_FIELDS[:2] if administration else SIDECAR_FIELDS\n',
                                  '        if argv[:2] == ["docker", "exec"]:\n            continue\n        fields = SIDECAR_FIELDS\n'),
    'M3-exemption-without-container-binding': (CONFIG, 'and len(argv) > 8 and argv[:3] == ["docker", "exec", container]',
                                               'and len(argv) > 8 and argv[:2] == ["docker", "exec"]'),
    'M4-sixty-second-pressure-retention': (HARNESS, '"retention.ms=-1" if topic == "qualification.backpressure" else ', ''),
}
results = {}
for name, (target, old, new) in MUTATIONS.items():
    copy = Path(tempfile.mkdtemp(prefix=name + '-', dir=parent))
    shutil.copytree(worktree, copy / 'src', ignore=shutil.ignore_patterns('.git', '__pycache__'))
    source = copy / 'src' / target
    text = source.read_text()
    assert text.count(old) == 1, (name, old)
    source.write_text(text.replace(old, new))
    subprocess.run([sys.executable, '-I', str(refresh), str(copy / 'src'), 'refresh', target], check=True, capture_output=True)
    run = subprocess.run([sys.executable, '-B', '-m', 'unittest', '-v', *TESTS], cwd=copy / 'src/tools/tests',
                         capture_output=True, text=True, env={'PATH': '/usr/bin:/bin', 'PYTHONDONTWRITEBYTECODE': '1'})
    outcome = {}
    for line in run.stderr.splitlines():
        for test in TESTS:
            method = test.rsplit('.', 1)[1]
            if line.startswith(method + ' (') and ' ... ' in line:
                outcome[method] = line.rsplit(' ... ', 1)[1]
    failures = [line for line in run.stderr.splitlines() if line.startswith(('FAIL:', 'ERROR:'))]
    results[name] = {'file': target, 'exitCode': run.returncode, 'perTest': outcome,
                     'failureHeadlines': failures, 'lastLine': run.stderr.strip().splitlines()[-1]}
    shutil.rmtree(copy)
result_path.write_text(json.dumps(results, indent=2) + '\n')
print(json.dumps({name: (value['exitCode'], value['perTest']) for name, value in results.items()}, indent=1))
