#!/usr/bin/env python3
"""Offline byte/custody verifier for a separately anchored native supplement.

No network, credentials, signatures, approval or operational mode. This verifier
hashes only explicit nonsecret inventories; it never imports packet source.
"""

import argparse
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import stat


ROOT = Path('/home/administrator/hexalith-recovery-evidence/4-0/20261016t063900z-native-measurement-supplement')
V4 = ROOT.parent / '20261015t162756z'
V4_SHA = '1ed5eec5579adeedf7e49519fcba125656caacbaaeaffb6509c49b6575714b14'
PRESERVATION_SHA = 'd53bcd47bc8d87c505eb48077bbff84d67bd43bc42bbddee9dd3c0b77e8bc0f1'
CEILING = '2026-10-16T16:27:56Z'
PLATFORM = Path('/home/administrator/projects/hexalith/platform')
SOURCE_ROOT = PLATFORM / 'eng/recovery-preparation'
HISTORY = (
    ROOT.parent / '20261014t112353z', ROOT.parent / 'final-run-tools',
    Path('/home/administrator/hexalith-retirement-investigation-evidence/20261014t131927z-s427-private-oidc-correction'),
    Path('/home/administrator/hexalith-retirement-investigation-evidence/20261014t144239z-s427-accepted-oidc-continuation'),
    Path('/home/administrator/hexalith-retirement-investigation-evidence/20261015t140709z-s427-expired-recovery-preparation'),
)
RECEIPT_ROOT = PLATFORM / '_bmad-output/implementation-artifacts/evidence/epic-4/4-0/20261015t195103z-local-static-transport-receipt'
STATUS_FILES = {
    PLATFORM / '_bmad-output/implementation-artifacts/sprint-status.yaml',
    Path('/home/administrator/projects/hexalith/timesheets/_bmad-output/implementation-artifacts/sprint-status.yaml'),
}
MAX_FILE = 64 * 1024 * 1024
SOURCE_NAMES = {
    'native_measurement_adapter.py', 'native_https_transport.py',
    'test_native_measurement_adapter.py', 'test_native_https_transport.py',
    'verify_native_supplement.py', 'test_verify_native_supplement.py',
}
STDLIB = Path('/usr/lib/python3.14')
PYTHON = Path('/usr/bin/python3.14')
REQUIRED_LIBRARIES = {
    '/usr/lib/x86_64-linux-gnu/' + name for name in (
        'ld-linux-x86-64.so.2', 'libc.so.6', 'libm.so.6', 'libssl.so.3',
        'libcrypto.so.3', 'libexpat.so.1.11.2', 'libz.so.1.3.1', 'libzstd.so.1.5.7',
    )
}


class Refusal(ValueError):
    """Only fixed reason codes reach output."""


def require(ok, reason):
    if not ok:
        raise Refusal(reason)


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def strict_json(raw):
    def pairs(items):
        value = {}
        for key, item in items:
            require(key not in value, 'duplicate-field')
            value[key] = item
        return value
    value = json.loads(raw, object_pairs_hook=pairs,
                       parse_float=lambda _: require(False, 'floating-field-refused'),
                       parse_constant=lambda _: require(False, 'nonfinite-field'))
    require(type(value) is dict, 'object-required')
    return value


def safe_relative(name):
    require(type(name) is str and 0 < len(name) < 512 and '\\' not in name, 'unsafe-relative-path')
    path = PurePosixPath(name)
    require(not path.is_absolute() and str(path) == name
            and all(part not in ('.', '..') for part in path.parts), 'unsafe-relative-path')
    return Path(name)


def open_bound(path, ancestry):
    """Walk retained directory descriptors; never follow an ancestor symlink."""
    directory = os.open('/', os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW)
    current = Path('/')
    try:
        for part in path.parts[1:-1]:
            following = os.open(part, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW, dir_fd=directory)
            os.close(directory)
            directory = following
            current = current / part
            info = os.fstat(directory)
            require((info.st_dev, info.st_ino, info.st_mode, info.st_uid) == ancestry[current], 'input-path-replaced')
        return os.open(path.name, os.O_RDONLY | os.O_NOFOLLOW | os.O_NONBLOCK, dir_fd=directory)
    finally:
        os.close(directory)


def read_file(path, private=False):
    """No symlinks, unsafe ancestry, hardlinks, unbounded or raced reads."""
    path = Path(path)
    require(path.is_absolute() and '..' not in path.parts, 'unsafe-input-path')
    ancestry = {}
    for item in (*reversed(path.parents), path):
        info = item.lstat()
        require(not stat.S_ISLNK(info.st_mode) and info.st_uid in (0, os.getuid()), 'unsafe-input-custody')
        sticky = stat.S_ISDIR(info.st_mode) and info.st_uid == 0 and info.st_mode & stat.S_ISVTX
        require(not stat.S_IMODE(info.st_mode) & 0o022 or sticky, 'writable-input-custody')
        if private and (item == path or ROOT == item or ROOT in item.parents):
            mode = 0o700 if stat.S_ISDIR(info.st_mode) else 0o600
            require(info.st_uid == os.getuid() and stat.S_IMODE(info.st_mode) == mode, 'private-mode-required')
        ancestry[item] = (info.st_dev, info.st_ino, info.st_mode, info.st_uid)
    require(stat.S_ISREG(info.st_mode) and info.st_nlink == 1 and info.st_size <= MAX_FILE, 'unsafe-input-file')
    fd = open_bound(path, ancestry)
    with os.fdopen(fd, 'rb') as stream:
        opened = os.fstat(stream.fileno())
        require((opened.st_dev, opened.st_ino) == (info.st_dev, info.st_ino), 'input-replaced')
        raw = stream.read(MAX_FILE + 1)
        after = os.fstat(stream.fileno())
    require((info.st_size, info.st_mtime_ns, info.st_ctime_ns)
            == (after.st_size, after.st_mtime_ns, after.st_ctime_ns)
            and len(raw) <= MAX_FILE, 'input-changed')
    for item, identity in ancestry.items():
        current = item.lstat()
        require((current.st_dev, current.st_ino, current.st_mode, current.st_uid) == identity, 'input-path-replaced')
    current = path.lstat()
    require((current.st_dev, current.st_ino, current.st_nlink, current.st_size, current.st_mtime_ns, current.st_ctime_ns)
            == (after.st_dev, after.st_ino, 1, after.st_size, after.st_mtime_ns, after.st_ctime_ns), 'input-path-replaced')
    return raw


def check_record(path, record, private=False):
    require(type(record) is dict and set(record) >= {'sha256', 'bytes'}, 'invalid-byte-record')
    require(type(record['bytes']) is int and 0 <= record['bytes'] <= MAX_FILE
            and type(record['sha256']) is str and re.fullmatch('[0-9a-f]{64}', record['sha256']), 'invalid-byte-record')
    raw = read_file(path, private)
    require(len(raw) == record['bytes'] and sha(raw) == record['sha256'], 'byte-binding-mismatch')
    info = path.lstat()
    for name, actual in (('uid', info.st_uid), ('mode', stat.S_IMODE(info.st_mode))):
        if name in record:
            require(record[name] == actual, 'custody-binding-mismatch')
    return raw


def inventory_paths(root):
    paths = set()
    for path in root.rglob('*'):
        info = path.lstat()
        require(not stat.S_ISLNK(info.st_mode), 'packet-symlink')
        if stat.S_ISDIR(info.st_mode):
            require(info.st_uid == os.getuid() and stat.S_IMODE(info.st_mode) == 0o700, 'packet-directory-mode')
        else:
            require(stat.S_ISREG(info.st_mode), 'packet-special-file')
            paths.add(str(path.relative_to(root)))
    return paths


def check_directories(root, file_names, explicit=None):
    expected = set() if explicit is None else set(explicit)
    if explicit is None:
        for name in file_names:
            expected.update(str(parent) for parent in safe_relative(name).parents if str(parent) != '.')
    actual = {str(path.relative_to(root)) for path in root.rglob('*') if path.is_dir() and not path.is_symlink()}
    require(actual == expected, 'unexpected-directory-layout')


def permitted_preservation(path):
    return path in STATUS_FILES or any(path.is_relative_to(root) for root in (V4, RECEIPT_ROOT, *HISTORY))


def permitted_runtime(path):
    return path == Path('/usr/bin/python3.14') or path.is_relative_to('/usr/lib/python3.14') or (
        path.is_relative_to('/usr/lib/x86_64-linux-gnu') and re.fullmatch(r'[A-Za-z0-9_.+-]+\.so(?:\.[A-Za-z0-9_.+-]+)*', path.name)
    )


def stdlib_files():
    # -S excludes site configuration and site/dist-packages; -I excludes cwd.
    files = set()
    for path in STDLIB.rglob('*'):
        if {'site-packages', 'dist-packages'}.intersection(path.relative_to(STDLIB).parts):
            continue
        if path.is_file() and not path.is_symlink():
            files.add(str(path))
    return files


def validate_runtime(runtime):
    require(runtime['interpreterFlags'] == ['-I', '-S', '-B'] and runtime['executionQualified'] is False, 'runtime-contract-mismatch')
    files = runtime['files']
    libraries = runtime['dynamicDependencies']
    require(type(files) is dict and type(libraries) is list and libraries
            and len(libraries) == len(set(libraries))
            and set(libraries) >= REQUIRED_LIBRARIES, 'runtime-inventory-incomplete')
    required = {str(PYTHON), *stdlib_files(), *libraries}
    require(set(files) == required, 'runtime-inventory-incomplete')
    for name, record in files.items():
        path = Path(name)
        require(permitted_runtime(path), 'unapproved-runtime-path')
        check_record(path, record)
    links = runtime['stdlibSymlinks']
    actual_links = {str(path): os.readlink(path) for path in STDLIB.rglob('*') if path.is_symlink()
                    and not {'site-packages', 'dist-packages'}.intersection(path.relative_to(STDLIB).parts)}
    require(links == actual_links, 'stdlib-alias-changed')
    for name in links:
        path = Path(name)
        target = path.resolve(strict=True)
        # sitecustomize is disabled by -S; remaining aliases must bind targets.
        if path.name != 'sitecustomize.py':
            require(str(target) in files, 'stdlib-alias-target-unbound')


def validate_sources(sources):
    require(type(sources) is dict and set(sources) == {str(SOURCE_ROOT / name) for name in SOURCE_NAMES}, 'source-inventory-incomplete')
    for name, record in sources.items():
        check_record(Path(name), record)


def recheck_external(before, runtime, sources):
    for name, record in before.items():
        path = Path(name)
        require(permitted_preservation(path), 'unapproved-preservation-path')
        check_record(path, record)
    expected_v4 = {str(Path(name).relative_to(V4)) for name in before if Path(name).is_relative_to(V4)}
    require(inventory_paths(V4) == expected_v4, 'v4-layout-changed')
    check_directories(V4, expected_v4)
    v4_raw = read_file(V4 / 'packet-v4.json', True)
    require(sha(v4_raw) == V4_SHA, 'v4-anchor-mismatch')
    v4_files = strict_json(v4_raw)['files']
    historical = strict_json(check_record(V4 / 'preservation-inventory.json', v4_files['preservation-inventory.json'], True))['roots']
    for root in (*HISTORY, RECEIPT_ROOT):
        expected = {str(Path(name).relative_to(root)) for name in before if Path(name).is_relative_to(root)}
        require(inventory_paths(root) == expected, 'historical-layout-changed')
        explicit = {name for name, record in historical[str(root)].items() if record['kind'] == 'directory'} if root in HISTORY else None
        check_directories(root, expected, explicit)
    validate_runtime(runtime)
    validate_sources(sources)


def expected_preservation(v4_manifest):
    names = {str(V4 / safe_relative(name)) for name in v4_manifest['files']}
    # The user-supplied v4 anchor binds every payload, not merely its filenames.
    for name, record in v4_manifest['files'].items():
        check_record(V4 / safe_relative(name), record, True)
    names.add(str(V4 / 'packet-v4.json'))
    history = strict_json(check_record(V4 / 'preservation-inventory.json', v4_manifest['files']['preservation-inventory.json'], True))
    require(type(history['roots']) is dict and set(history['roots']) == {str(root) for root in HISTORY}, 'historical-roots-mismatch')
    for original, entries in history['roots'].items():
        require(type(entries) is dict, 'historical-shape-mismatch')
        for name, record in entries.items():
            if record['kind'] == 'file':
                path = Path(original) / safe_relative(name)
                check_record(path, record)
                names.add(str(path))
    names.update(str(path) for path in STATUS_FILES)
    names.update(str(RECEIPT_ROOT / name) for name in (
        'README.md', 'validation.json', 'user-supplied-receipt.json', 'SHA256SUMS', 'packet-v4-offline-recheck.json',
    ))
    return names


def verify(expected):
    require(type(expected) is str and re.fullmatch('[0-9a-f]{64}', expected), 'invalid-anchor')
    manifest_raw = read_file(ROOT / 'packet-v1.json', True)
    require(sha(manifest_raw) == expected, 'supplement-anchor-mismatch')
    manifest = strict_json(manifest_raw)
    require(manifest['kind'] == 'hexalith.native-measurement-supplement'
            and manifest['operationalGate'] == 'closed'
            and manifest['v4Sha256'] == V4_SHA
            and manifest['policyCeilingEnds'] == CEILING, 'supplement-contract-mismatch')
    files = manifest['files']
    require(type(files) is dict and 1 <= len(files) <= 256, 'invalid-payload-inventory')
    require(inventory_paths(ROOT) == {*files, 'packet-v1.json'}, 'supplement-layout-mismatch')
    check_directories(ROOT, files)
    payload = {}
    for name, record in files.items():
        payload[name] = check_record(ROOT / safe_relative(name), record, True)
    v4_raw = read_file(V4 / 'packet-v4.json', True)
    require(sha(v4_raw) == V4_SHA, 'v4-anchor-mismatch')
    require(sha(payload['preservation-before.json']) == PRESERVATION_SHA, 'preservation-anchor-mismatch')
    before = strict_json(payload['preservation-before.json'])['files']
    require(type(before) is dict and len(before) == 495
            and set(before) == expected_preservation(strict_json(v4_raw)), 'preservation-baseline-mismatch')
    for name, record in before.items():
        path = Path(name)
        require(permitted_preservation(path), 'unapproved-preservation-path')
        check_record(path, record)
    expected_v4 = {str(Path(name).relative_to(V4)) for name in before if Path(name).is_relative_to(V4)}
    require(inventory_paths(V4) == expected_v4 and len(expected_v4) == 140, 'v4-layout-changed')
    runtime = strict_json(payload['runtime-inventory.json'])
    validate_runtime(runtime)
    sources = strict_json(payload['source-inventory.json'])['files']
    validate_sources(sources)
    # Recheck all payloads at the end; parsed bytes above remain sealed in memory.
    for name, record in files.items():
        check_record(ROOT / safe_relative(name), record, True)
    require(inventory_paths(ROOT) == {*files, 'packet-v1.json'}, 'supplement-layout-changed')
    check_directories(ROOT, files)
    require(sha(read_file(ROOT / 'packet-v1.json', True)) == expected, 'supplement-changed')
    recheck_external(before, runtime, sources)
    now = datetime.now(timezone.utc)
    expired = now >= datetime.fromisoformat(CEILING.replace('Z', '+00:00'))
    return {'kind': 'hexalith.native-supplement-offline-verification',
            'observedAt': now.isoformat(), 'supplementSha256': expected,
            'payloadFiles': len(files), 'v4FilesUnchanged': len(expected_v4),
            'preservationFilesUnchanged': len(before), 'runtimeFiles': len(runtime['files']),
            'sourceFiles': len(sources), 'policyCeilingEnds': CEILING, 'candidateExpired': expired,
            'policyOrCaptureTimesRefreshed': False, 'operationalGate': 'closed',
            'authorityAuthenticated': False, 'executionApprovalReady': False}


class Parser(argparse.ArgumentParser):
    def error(self, message):
        raise Refusal('invalid-invocation')


def main():
    try:
        parser = Parser(description=__doc__, allow_abbrev=False)
        parser.add_argument('--sha256', required=True)
        args = parser.parse_args()
        print(json.dumps(verify(args.sha256), sort_keys=True))
        return 0
    except (Refusal, OSError, ValueError, KeyError, TypeError, RecursionError):
        print(json.dumps({'status': 'refused', 'operationalGate': 'closed', 'reason': 'offline-integrity-or-custody-refusal'}))
        return 1


if __name__ == '__main__':
    raise SystemExit(main())
