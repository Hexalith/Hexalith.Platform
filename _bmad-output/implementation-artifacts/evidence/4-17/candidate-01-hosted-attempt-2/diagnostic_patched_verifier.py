"""NON-AUTHORITATIVE diagnostic: would the attempt-2 packet pass integrity if one verifier false positive were removed?

Usage (from the Platform repository root):
    python3 -I diagnostic_patched_verifier.py EVIDENCE_DIR PINNED_EVENTSTORE_SOURCE PACKET_DIR FRESH_COPY_DIR

It copies the pinned EventStore 02a4d3b2 archive to FRESH_COPY_DIR and applies three edits there, never in a checkout:
  1. dapr_platform_broker_configuration.py: skip `docker exec` receipts in sidecar identification. The pinned check
     treats any receipt containing the argument `--config` as a daprd launch, and kafka-topics.sh topic creation
     passes `--config retention.ms=60000`.
  2-3. dapr_broker_qualification.py: point the trusted owning-checkout root at the unpatched archive, so the
     source-binding checks compare the packet with the pinned bytes rather than with edit 1.
The result proves nothing about qualification; only the pinned verifier's verdict counts.
"""
import datetime
import hashlib
import json
import shutil
import subprocess
import sys
from pathlib import Path

evidence, pinned, packet, copy = (Path(a).resolve() for a in sys.argv[1:5])
ORIGINAL = Path('docs/implementation/story-4-17-broker-candidate-01.json').resolve()
shutil.copytree(pinned, copy, symlinks=True)
EDITS = [
    ('tools/dapr_platform_broker_configuration.py',
     '        argv = receipt["argv"]\n        fields = ("--app-id", "--resources-path", "--config")\n',
     '        argv = receipt["argv"]\n        if argv[:2] == ["docker", "exec"]:\n            continue\n'
     '        fields = ("--app-id", "--resources-path", "--config")\n'),
    ('tools/dapr_broker_qualification.py',
     '(Path(__file__).resolve().parents[1] / selected_profile)', '(Path(PINNED_ROOT) / selected_profile)'),
    ('tools/dapr_broker_qualification.py',
     'owning_root = Path(__file__).resolve().parents[1]', 'owning_root = Path(PINNED_ROOT)'),
]
for name, old, new in EDITS:
    target = copy / name
    text = target.read_text()
    assert text.count(old) == 1, (name, old)
    target.write_text(text.replace(old, new.replace('PINNED_ROOT', repr(str(pinned)))))


def first_trigger():
    identities = json.loads((packet / 'execution-identities.json').read_text())
    executable = identities.get('daprdExecutable', {}).get('path')
    fields = ('--app-id', '--resources-path', '--config')
    for index, receipt in enumerate(json.loads((packet / 'commands.json').read_text())):
        argv = receipt['argv']
        identified = any(a == f or a.startswith(f + '=') for a in argv[1:] for f in fields)
        if identified and argv[0] != executable:
            return {'index': index, 'argv': argv, 'exitCode': receipt.get('exitCode')}
    return None


modes = {'integrity': ['--allow-failures'], 'technical': [], 'authority': ['--require-production-authority']}
runs = {}
for mode, flags in modes.items():
    proc = subprocess.run(['python3', '-B', 'tools/validate-dapr-broker-qualification.py', str(packet),
                           '--platform-config', str(ORIGINAL), *flags], cwd=copy, capture_output=True, text=True,
                          env={'PATH': '/usr/bin:/bin', 'PYTHONDONTWRITEBYTECODE': '1', 'PYTHONOPTIMIZE': '0'})
    runs[mode] = {'flags': flags, 'exitCode': proc.returncode, 'stdout': proc.stdout, 'stderr': proc.stderr}
(evidence / 'local-diagnostic-verification.json').write_text(json.dumps({
    'observedUtc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
    'authoritative': False,
    'purpose': 'locate the pinned verifier rejection; not qualification evidence',
    'pinnedSource': 'git archive of EventStore 02a4d3b2c9fedd18ea2da4d754ab4b68f7e29213',
    'edits': [{'file': n, 'old': o, 'new': w.replace('PINNED_ROOT', 'PINNED_ARCHIVE_ROOT')} for n, o, w in EDITS],
    'patchedFileSha256': {n: hashlib.sha256((copy / n).read_bytes()).hexdigest() for n in sorted({e[0] for e in EDITS})},
    'firstPinnedFalsePositiveReceipt': first_trigger(),
    'runs': runs,
}, indent=2) + '\n')
print(json.dumps({mode: run['exitCode'] for mode, run in runs.items()}))
