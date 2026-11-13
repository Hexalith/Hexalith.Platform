"""Re-download the candidate-01 attempt-2 artifact, check its digest, inspect every entry, then extract.

Usage:
    python3 -I redownload_and_inspect.py ARTIFACT_ID EXPECTED_SHA256 FRESH_DIR RECORD_JSON

FRESH_DIR must not exist. The archive is untrusted data: nothing from it is executed or imported.
Extraction happens only after the digest matches and the inspection finds no unsafe path, link,
special file, duplicate name or private transcript.
"""
import datetime
import hashlib
import json
import stat
import subprocess
import sys
import zipfile
from pathlib import Path, PurePosixPath

artifact_id, expected, fresh, record_path = sys.argv[1], sys.argv[2], Path(sys.argv[3]), Path(sys.argv[4])
REPO = 'Hexalith/Hexalith.Platform'
fresh.mkdir(parents=True, exist_ok=False)
download, extract = fresh / 'download', fresh / 'extract'
download.mkdir()


def now():
    return datetime.datetime.now(datetime.timezone.utc).isoformat()


record = {'startedUtc': now(), 'artifactId': int(artifact_id), 'expectedSha256': expected}
meta_argv = ['gh', 'api', '--method', 'GET', f'repos/{REPO}/actions/artifacts/{artifact_id}']
meta = json.loads(subprocess.run(meta_argv, capture_output=True, text=True, check=True).stdout)
record['metadataArgv'] = meta_argv
record['metadata'] = {k: meta.get(k) for k in ['id', 'name', 'size_in_bytes', 'digest', 'expired', 'created_at', 'expires_at']}
record['metadata']['workflowRun'] = {k: meta.get('workflow_run', {}).get(k) for k in ['id', 'head_branch', 'head_sha']}

archive = download / 'artifact.zip'
argv = ['gh', 'api', '--method', 'GET', f'repos/{REPO}/actions/artifacts/{artifact_id}/zip']
with archive.open('xb') as stream:
    proc = subprocess.run(argv, stdout=stream, stderr=subprocess.PIPE)
data = archive.read_bytes()
actual = hashlib.sha256(data).hexdigest()
record.update({'downloadArgv': argv, 'downloadExitCode': proc.returncode, 'downloadedBytes': len(data),
               'downloadedSha256': actual, 'sha256MatchesExpected': actual == expected,
               'sha256MatchesArtifactApi': meta.get('digest') == 'sha256:' + actual})


def save():
    record_path.write_text(json.dumps(record, indent=2) + '\n')


if proc.returncode != 0 or actual != expected:
    record['result'] = 'download-or-digest-failure'
    save()
    raise SystemExit('download failed or digest differs')

entries, problems = [], []
with zipfile.ZipFile(archive) as bundle:
    for info in bundle.infolist():
        name = info.filename
        mode = (info.external_attr >> 16) & 0o170000
        path = PurePosixPath(name)
        if (name.startswith('/') or '\\' in name or '..' in path.parts or '\x00' in name
                or (path.parts and path.parts[0].endswith(':'))):
            problems.append('unsafe-path:' + name)
        if mode == stat.S_IFLNK:
            problems.append('symlink:' + name)
        elif mode not in (0, stat.S_IFREG, stat.S_IFDIR):
            problems.append('special-file:' + name)
        if info.flag_bits & 0x1:
            problems.append('encrypted:' + name)
        entries.append({'name': name, 'bytes': info.file_size, 'compressed': info.compress_size,
                        'crc': format(info.CRC, '08x'), 'directory': info.is_dir()})
    names = [e['name'] for e in entries]
    if len(names) != len(set(names)):
        problems.append('duplicate-names')
    casefold = [n.casefold() for n in names]
    if len(casefold) != len(set(casefold)):
        problems.append('case-colliding-names')
    total = sum(e['bytes'] for e in entries)
    if total > 512 * 1024 * 1024:
        problems.append('uncompressed-size-bound')
    record['inspection'] = {'entries': len(entries), 'uncompressedBytes': total, 'problems': problems,
                            'privateTranscriptEntries': [n for n in names if 'private-transcripts' in n],
                            'topLevel': sorted({PurePosixPath(n).parts[0] for n in names})}
    if problems or record['inspection']['privateTranscriptEntries']:
        record['result'] = 'inspection-failure'
        save()
        raise SystemExit('archive inspection failed')
    bad = bundle.testzip()
    record['inspection']['crcFirstBadEntry'] = bad
    if bad is not None:
        record['result'] = 'crc-failure'
        save()
        raise SystemExit('archive CRC failure')
    extract.mkdir()
    bundle.extractall(extract)

files = sorted(p for p in extract.rglob('*') if p.is_file())
record['extracted'] = {'files': len(files),
                       'anySymlink': any(p.is_symlink() for p in extract.rglob('*')),
                       'sha256': {str(p.relative_to(extract)): hashlib.sha256(p.read_bytes()).hexdigest() for p in files}}
record['result'] = 'extracted-after-inspection'
record['finishedUtc'] = now()
save()
print(json.dumps({'sha256': actual, 'matches': actual == expected, 'entries': len(entries),
                  'files': len(files), 'problems': problems}))
