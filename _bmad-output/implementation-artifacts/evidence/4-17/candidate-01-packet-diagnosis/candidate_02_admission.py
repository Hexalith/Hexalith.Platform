"""Offline admission of the candidate-02 configuration against an EventStore source tree.

Usage (cwd = a clean `git archive` of the proposed EventStore revision):
    PYTHONOPTIMIZE=0 python3 -B candidate_02_admission.py CONFIG EXPECTED_SHA256 RESULT_JSON

Runs the workflow's own owner-admission checks (validate_profile, profile identity, broker/scenario
matrix, strict configuration parser), then the actual producer constructor with subprocess and
socket calls blocked, exactly as candidate-01's documented offline admission did. No preparation,
capture, Docker, network or verification of a packet is invoked.
"""
import hashlib
import importlib.util
import json
import os
import sys
import tempfile
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import patch

if not __debug__:
    raise RuntimeError('admission requires Python assertions enabled')
config, expected, result = Path(sys.argv[1]).resolve(), sys.argv[2], Path(sys.argv[3]).resolve()
sys.path.insert(0, 'tools')
from dapr_broker_qualification import BROKERS, SCENARIOS, validate_profile
from dapr_platform_broker_configuration import (CONFIGURATION_PATH, PROVENANCE_PATH, apply_resiliency, component_source,
                                                load_configuration, provenance, settings)
from dapr_runtime_qualification import PLATFORM_PROFILE, profile_path

data, configuration = load_configuration(config)
assert hashlib.sha256(data).hexdigest() == expected, 'candidate SHA-256 mismatch'
profile = json.loads(Path(PLATFORM_PROFILE).read_text())
validate_profile(profile, Path.cwd())
assert profile_path(profile) == PLATFORM_PROFILE
assert BROKERS == ('rabbitmq', 'kafka') and len(SCENARIOS) == 12
effective = settings(configuration)
source = Path('deploy/dapr/qualification/rabbitmq.yaml').read_text()
resiliency = {'spec': {'policies': {'timeouts': {'bounded': '3s'}}, 'targets': {'components': {'pubsub': {'outbound': {'timeout': 'bounded'}}}}}}
apply_resiliency(resiliency, 'rabbitmq', configuration)
rabbit_default = component_source(source, 'rabbitmq', configuration) == source and resiliency['spec']['targets']['components']['pubsub']['outbound']['timeout'] == 'bounded'
spec = importlib.util.spec_from_file_location('broker_producer', 'tools/qualify-dapr-brokers.py')
producer = importlib.util.module_from_spec(spec); spec.loader.exec_module(producer)
with tempfile.TemporaryDirectory(prefix='candidate-02-admission-') as scratch:
    scratch = Path(scratch)
    with (patch.dict(os.environ, {'QUALIFICATION_SUPERVISOR_ROOT': ''}), patch.object(tempfile, 'tempdir', str(scratch)),
          patch('subprocess.Popen', side_effect=AssertionError('subprocess forbidden')) as process,
          patch('socket.socket', side_effect=AssertionError('socket forbidden')) as network):
        run = producer.Run(SimpleNamespace(profile=Path(PLATFORM_PROFILE), platform_config=config, output=scratch / 'admission'))
        assert (run.output / CONFIGURATION_PATH).read_bytes() == data
        assert json.loads((run.output / PROVENANCE_PATH).read_text()) == provenance(data, configuration)
        assert run.packet['schemaVersion'] == 3 and run.packet['profile'] == PLATFORM_PROFILE
        assert run.packet['selectedBroker'] is None and run.packet['productionEligible'] is False
        assert run.receipts == [] and run.containers == [] and run.processes == []
        process.assert_not_called(); network.assert_not_called()
record = {'configurationSha256': expected, 'platformRevision': configuration['platformRevision'],
          'platformPath': configuration['platformPath'], 'effectiveSettings': effective,
          'rabbitmqRendersHistoricalDefaults': rabbit_default, 'profile': PLATFORM_PROFILE,
          'profileSha256': hashlib.sha256(Path(PLATFORM_PROFILE).read_bytes()).hexdigest(),
          'profileInputs': len(profile['inputs']), 'validateProfile': 'PASS', 'producerAdmission': 'PASS',
          'noSubprocessOrSocket': True}
result.write_text(json.dumps(record, indent=2) + '\n')
print(json.dumps(record, indent=1))
