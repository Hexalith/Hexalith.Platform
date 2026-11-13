"""Retrieve the attempt-2 artifact, inspect it before extraction, and run the three independent verifier modes.

Usage (from the Platform repository root):
    python3 -I retrieve_and_verify.py RUN_ID EVIDENCE_DIR FRESH_SCRATCH_DIR PINNED_EVENTSTORE_SOURCE

PINNED_EVENTSTORE_SOURCE is a `git archive` of EventStore 02a4d3b2c9fedd18ea2da4d754ab4b68f7e29213.
The downloaded archive is untrusted data: it is never executed. The verifier is the pinned EventStore
code, run from its own source root, and receives the packet and the Platform checkout's original
configuration only as path arguments.
"""
import datetime
import hashlib
import json
import shutil
import stat
import subprocess
import sys
import zipfile
from pathlib import Path, PurePosixPath

run_id, evidence, scratch, eventstore = sys.argv[1], Path(sys.argv[2]), Path(sys.argv[3]), Path(sys.argv[4])
REPO = 'Hexalith/Hexalith.Platform'
EXPECTED_NAME = f'platform-broker-candidate-01-{run_id}-1'
ORIGINAL = Path('docs/implementation/story-4-17-broker-candidate-01.json').resolve()
ORIGINAL_SHA = '472bae6b4fc77448c96aa066c41469e689d50d0b98dcbea103333d06524208dc'
scratch.mkdir(parents=True, exist_ok=False)


def now():
    return datetime.datetime.now(datetime.timezone.utc).isoformat()


def sha(data):
    return hashlib.sha256(data).hexdigest()


def write(name, value):
    (evidence / name).write_text(json.dumps(value, indent=2) + '\n')


listing = json.loads(subprocess.run(['gh', 'api', '--method', 'GET', f'repos/{REPO}/actions/runs/{run_id}/artifacts'],
                                    capture_output=True, text=True, check=True).stdout)
artifacts = [{k: a.get(k) for k in ['id', 'name', 'size_in_bytes', 'digest', 'expired', 'created_at', 'expires_at']}
             for a in listing.get('artifacts', [])]
record = {'observedUtc': now(), 'totalCount': listing.get('total_count'), 'artifacts': artifacts,
          'expectedArtifactName': EXPECTED_NAME}
match = [a for a in artifacts if a['name'] == EXPECTED_NAME]
if len(match) != 1:
    record['downloadStatus'] = 'unavailable'
    write('hosted-artifacts.json', record)
    raise SystemExit('expected artifact unavailable')
artifact = match[0]

download = scratch / 'download'
download.mkdir()
archive = download / 'artifact.zip'
argv = ['gh', 'api', '--method', 'GET', f"repos/{REPO}/actions/artifacts/{artifact['id']}/zip"]
with archive.open('xb') as stream:
    proc = subprocess.run(argv, stdout=stream, stderr=subprocess.PIPE)
data = archive.read_bytes()
record.update({'downloadArgv': argv, 'downloadExitCode': proc.returncode, 'downloadedBytes': len(data),
               'downloadedSha256': sha(data),
               'digestMatchesArtifactApi': artifact.get('digest') in (None, 'sha256:' + sha(data)),
               'artifactApiDigest': artifact.get('digest')})
if proc.returncode != 0:
    write('hosted-artifacts.json', record)
    raise SystemExit('artifact download failed')

entries, problems = [], []
with zipfile.ZipFile(archive) as bundle:
    for info in bundle.infolist():
        name = info.filename
        mode = (info.external_attr >> 16) & 0o170000
        path = PurePosixPath(name)
        if name.startswith('/') or '\\' in name or '..' in path.parts or (path.parts and path.parts[0].endswith(':')):
            problems.append(f'unsafe-path:{name}')
        if mode == stat.S_IFLNK:
            problems.append(f'symlink:{name}')
        entries.append({'name': name, 'bytes': info.file_size, 'directory': info.is_dir()})
    names = [e['name'] for e in entries]
    if len(names) != len(set(names)):
        problems.append('duplicate-names')
    record['archiveInspection'] = {'entries': len(entries), 'problems': problems,
                                   'privateTranscriptEntries': [n for n in names if 'private-transcripts' in n]}
    if problems:
        write('hosted-artifacts.json', record)
        raise SystemExit('archive inspection failed')
    extract = scratch / 'extract'
    extract.mkdir()
    bundle.extractall(extract)
record['extraction'] = 'performed-after-inspection'
record['archiveEntries'] = entries
write('hosted-artifacts.json', record)

# Retain the workflow's own sanitized summaries; the producer packet stays outside the repository.
retained = {}
for name in ['outcomes.json', 'bootstrap.json', 'worker.json', 'transcripts.json']:
    source = extract / name
    if source.is_file() and not source.is_symlink():
        shutil.copyfile(source, evidence / ('artifact-' + name))
        retained[name] = sha(source.read_bytes())
packet = extract / 'packet'

assert sha(ORIGINAL.read_bytes()) == ORIGINAL_SHA, 'original configuration digest changed'
modes = {'integrity': ['--allow-failures'], 'technical': [], 'authority': ['--require-production-authority']}
verifiers = {}
for mode, flags in modes.items():
    command = ['python3', '-B', 'tools/validate-dapr-broker-qualification.py', str(packet), '--platform-config', str(ORIGINAL), *flags]
    proc = subprocess.run(command, cwd=eventstore, capture_output=True, text=True,
                          env={'PATH': '/usr/bin:/bin', 'PYTHONDONTWRITEBYTECODE': '1', 'PYTHONOPTIMIZE': '0'})
    (evidence / f'local-verifier-{mode}.stdout').write_text(proc.stdout)
    (evidence / f'local-verifier-{mode}.stderr').write_text(proc.stderr)
    verifiers[mode] = {'argv': ['python3', '-B', 'tools/validate-dapr-broker-qualification.py', 'EXTRACT/packet',
                                '--platform-config', 'docs/implementation/story-4-17-broker-candidate-01.json', *flags],
                       'cwd': 'isolated git archive of EventStore 02a4d3b2c9fedd18ea2da4d754ab4b68f7e29213',
                       'exitCode': proc.returncode, 'stdoutSha256': sha(proc.stdout.encode()),
                       'firstLine': proc.stdout.splitlines()[0] if proc.stdout else None}
write('local-verification.json', {'observedUtc': now(), 'runId': int(run_id), 'packetPresent': packet.is_dir(),
                                  'originalConfigurationSha256': ORIGINAL_SHA, 'retainedArtifactSummaries': retained,
                                  'verifiers': verifiers})
print(json.dumps({'artifact': artifact['name'], 'bytes': len(data), 'entries': len(entries),
                  'retained': sorted(retained), 'verifiers': {m: v['exitCode'] for m, v in verifiers.items()}}))
