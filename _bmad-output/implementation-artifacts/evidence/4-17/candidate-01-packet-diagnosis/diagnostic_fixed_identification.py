"""NON-AUTHORITATIVE: does the candidate-02 sidecar identification admit the real attempt-2 packet?

Usage:
    python3 -I diagnostic_fixed_identification.py PINNED_ARCHIVE FIXED_FILE PACKET ORIGINAL_CONFIG FRESH_COPY RESULT_JSON

PINNED_ARCHIVE is an unpatched `git archive` of EventStore 02a4d3b2. FRESH_COPY receives a copy with
exactly two kinds of edit, never in a checkout:
  1. tools/dapr_platform_broker_configuration.py is replaced by FIXED_FILE (the branch's fixed bytes;
     the file is byte-identical between 02a4d3b2 and bb9e2b51 before the fix);
  2. the trusted owning-checkout root in tools/dapr_broker_qualification.py points at PINNED_ARCHIVE,
     so source-binding checks compare the packet with the pinned bytes it was captured with.
The packet is data only. Nothing from it is imported or executed; the verifier receives paths.
"""
import datetime
import hashlib
import json
import shutil
import subprocess
import sys
from pathlib import Path

pinned, fixed, packet, original, copy, result = (Path(a).resolve() for a in sys.argv[1:7])
shutil.copytree(pinned, copy, symlinks=True)
target = copy / 'tools/dapr_platform_broker_configuration.py'
before = hashlib.sha256(target.read_bytes()).hexdigest()
shutil.copyfile(fixed, target)
EDITS = [('(Path(__file__).resolve().parents[1] / selected_profile)', '(Path(PINNED_ROOT) / selected_profile)'),
         ('owning_root = Path(__file__).resolve().parents[1]', 'owning_root = Path(PINNED_ROOT)')]
contract = copy / 'tools/dapr_broker_qualification.py'
text = contract.read_text()
for old, new in EDITS:
    assert text.count(old) == 1, old
    text = text.replace(old, new.replace('PINNED_ROOT', repr(str(pinned))))
contract.write_text(text)
modes = {'integrity': ['--allow-failures'], 'technical': [], 'authority': ['--require-production-authority']}
runs = {}
for mode, flags in modes.items():
    proc = subprocess.run(['python3', '-B', 'tools/validate-dapr-broker-qualification.py', str(packet), '--platform-config', str(original), *flags],
                          cwd=copy, capture_output=True, text=True,
                          env={'PATH': '/usr/bin:/bin', 'PYTHONDONTWRITEBYTECODE': '1', 'PYTHONOPTIMIZE': '0'})
    runs[mode] = {'flags': flags, 'exitCode': proc.returncode, 'stdout': proc.stdout, 'stderr': proc.stderr,
                  'stdoutSha256': hashlib.sha256(proc.stdout.encode()).hexdigest()}
record = {
    'observedUtc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
    'authoritative': False,
    'purpose': 'check the candidate-02 sidecar identification against the real attempt-2 packet; not qualification evidence',
    'pinnedSource': 'git archive of EventStore 02a4d3b2c9fedd18ea2da4d754ab4b68f7e29213',
    'replacedFile': {'path': 'tools/dapr_platform_broker_configuration.py', 'pinnedSha256': before,
                     'fixedSha256': hashlib.sha256(fixed.read_bytes()).hexdigest()},
    'trustedRootRedirect': [old for old, _ in EDITS],
    'packetJsonSha256': hashlib.sha256((packet / 'packet.json').read_bytes()).hexdigest(),
    'originalConfigurationSha256': hashlib.sha256(original.read_bytes()).hexdigest(),
    'runs': runs,
}
result.write_text(json.dumps(record, indent=2) + '\n')
print(json.dumps({mode: run['exitCode'] for mode, run in runs.items()}))
