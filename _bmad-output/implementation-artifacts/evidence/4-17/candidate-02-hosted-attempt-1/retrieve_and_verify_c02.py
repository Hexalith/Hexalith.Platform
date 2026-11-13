"""Retrieve the candidate-02 attempt-1 artifact, inspect before extraction, run the three verifier modes.

Usage: python3 -I retrieve_and_verify_c02.py RUN_ID EVIDENCE_DIR FRESH_DIR PINNED_EVENTSTORE_ARCHIVE ORIGINAL_CONFIG
PINNED_EVENTSTORE_ARCHIVE is a `git archive` of EventStore 495dd24943031ec75e2822c6168ada3c8b9fdb82; ORIGINAL_CONFIG
is the source commit's docs/implementation/story-4-17-broker-candidate-02.json. The archive is untrusted data and
is never executed; the verifier receives the packet and the configuration only as path arguments.
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

run_id, evidence, fresh, eventstore, original = sys.argv[1], Path(sys.argv[2]), Path(sys.argv[3]), Path(sys.argv[4]), Path(sys.argv[5])
REPO = 'Hexalith/Hexalith.Platform'
NAME = f'platform-broker-candidate-02-{run_id}-1'
CONFIG_SHA = 'b8601e3a6d4e8460d039573ca2f7e1728e52aa67e61fc0710d74cab5d072580b'
fresh.mkdir(parents=True, exist_ok=False)


def sha(data):
    return hashlib.sha256(data).hexdigest()


def write(name, value):
    (evidence / name).write_text(json.dumps(value, indent=2) + '\n')


listing = json.loads(subprocess.run(['gh', 'api', '--method', 'GET', f'repos/{REPO}/actions/runs/{run_id}/artifacts'],
                                    capture_output=True, text=True, check=True).stdout)
artifacts = [{k: a.get(k) for k in ('id', 'name', 'size_in_bytes', 'digest', 'expired', 'created_at', 'expires_at')} for a in listing['artifacts']]
record = {'observedUtc': datetime.datetime.now(datetime.timezone.utc).isoformat(), 'artifacts': artifacts, 'expectedName': NAME}
match = [a for a in artifacts if a['name'] == NAME]
if len(match) != 1:
    record['downloadStatus'] = 'unavailable'; write('hosted-artifacts.json', record); raise SystemExit('artifact unavailable')
artifact = match[0]
archive = fresh / 'download' / 'artifact.zip'; archive.parent.mkdir()
argv = ['gh', 'api', '--method', 'GET', f"repos/{REPO}/actions/artifacts/{artifact['id']}/zip"]
with archive.open('xb') as stream:
    proc = subprocess.run(argv, stdout=stream, stderr=subprocess.PIPE)
data = archive.read_bytes()
record.update(downloadArgv=argv, downloadExitCode=proc.returncode, downloadedBytes=len(data), downloadedSha256=sha(data),
              digestMatchesArtifactApi=artifact.get('digest') == 'sha256:' + sha(data))
if proc.returncode or not record['digestMatchesArtifactApi']:
    write('hosted-artifacts.json', record); raise SystemExit('download failed or digest mismatch')
problems, entries = [], []
with zipfile.ZipFile(archive) as bundle:
    for info in bundle.infolist():
        name, mode, path = info.filename, (info.external_attr >> 16) & 0o170000, PurePosixPath(info.filename)
        if name.startswith('/') or '\\' in name or '..' in path.parts or '\x00' in name or (path.parts and path.parts[0].endswith(':')):
            problems.append('unsafe-path:' + name)
        if mode == stat.S_IFLNK:
            problems.append('symlink:' + name)
        elif mode not in (0, stat.S_IFREG, stat.S_IFDIR):
            problems.append('special-file:' + name)
        if info.flag_bits & 0x1:
            problems.append('encrypted:' + name)
        entries.append({'name': name, 'bytes': info.file_size})
    names = [e['name'] for e in entries]
    if len(names) != len(set(names)) or len({n.casefold() for n in names}) != len(names):
        problems.append('duplicate-or-case-colliding-names')
    private = [n for n in names if 'private-transcripts' in n]
    record['inspection'] = {'entries': len(entries), 'uncompressedBytes': sum(e['bytes'] for e in entries), 'problems': problems,
                            'privateTranscriptEntries': private, 'crcFirstBadEntry': bundle.testzip()}
    if problems or private or record['inspection']['crcFirstBadEntry'] is not None:
        write('hosted-artifacts.json', record); raise SystemExit('archive inspection failed')
    extract = fresh / 'extract'; extract.mkdir(); bundle.extractall(extract)
record['extraction'] = 'performed-after-inspection'
write('hosted-artifacts.json', record)
write('artifact-extracted-digests.json', {'note': f'SHA-256 of every file extracted from {NAME} (zip sha256 {sha(data)}); the bytes are not committed',
                                          'files': {str(p.relative_to(extract)): {'bytes': p.stat().st_size, 'sha256': sha(p.read_bytes())}
                                                    for p in sorted(extract.rglob('*')) if p.is_file()}})
retained = {}
for name in ('outcomes.json', 'bootstrap.json', 'worker.json', 'transcripts.json'):
    source = extract / name
    if source.is_file() and not source.is_symlink():
        shutil.copyfile(source, evidence / ('artifact-' + name)); retained[name] = sha(source.read_bytes())
packet = extract / 'packet'
assert sha(original.read_bytes()) == CONFIG_SHA, 'original configuration digest changed'
modes = {'integrity': ['--allow-failures'], 'technical': [], 'authority': ['--require-production-authority']}
verifiers = {}
for mode, flags in modes.items():
    proc = subprocess.run(['python3', '-B', 'tools/validate-dapr-broker-qualification.py', str(packet), '--platform-config', str(original), *flags],
                          cwd=eventstore, capture_output=True, text=True, env={'PATH': '/usr/bin:/bin', 'PYTHONDONTWRITEBYTECODE': '1', 'PYTHONOPTIMIZE': '0'})
    (evidence / f'local-verifier-{mode}.stdout').write_text(proc.stdout)
    (evidence / f'local-verifier-{mode}.stderr').write_text(proc.stderr)
    verifiers[mode] = {'argv': ['python3', '-B', 'tools/validate-dapr-broker-qualification.py', 'EXTRACT/packet', '--platform-config',
                                'docs/implementation/story-4-17-broker-candidate-02.json', *flags],
                       'cwd': 'isolated git archive of EventStore 495dd24943031ec75e2822c6168ada3c8b9fdb82',
                       'exitCode': proc.returncode, 'stdoutSha256': sha(proc.stdout.encode()), 'stderrBytes': len(proc.stderr)}
write('local-verification.json', {'observedUtc': datetime.datetime.now(datetime.timezone.utc).isoformat(), 'runId': int(run_id),
                                  'packetPresent': packet.is_dir(), 'originalConfigurationSha256': CONFIG_SHA,
                                  'retainedArtifactSummaries': retained, 'verifiers': verifiers})
print(json.dumps({'artifact': NAME, 'bytes': len(data), 'entries': len(entries), 'verifiers': {m: v['exitCode'] for m, v in verifiers.items()}}))
