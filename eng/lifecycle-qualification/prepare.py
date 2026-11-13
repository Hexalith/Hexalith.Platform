#!/usr/bin/env python3
"""Verify immutable lifecycle inputs and reuse downloads, never test results or run state."""
from __future__ import annotations

import argparse
from contextlib import contextmanager
import ctypes
import errno
import fcntl
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import stat
import subprocess
import sys
import tempfile
import time
from xml.etree import ElementTree

SCHEMA = 'hexalith.lifecycle-preparation.v1'
POLICY = 'hexalith.platform.candidate-and-attachment-policy.v1'
SHA = re.compile(r'[0-9a-f]{64}')
COMMIT = re.compile(r'[0-9a-f]{40}(?:[0-9a-f]{24})?')
IDENTITY = re.compile(r'[a-z][a-z0-9-]{0,63}')
VERSION = re.compile(r'[0-9]+(?:[.][0-9A-Za-z-]+){1,4}')
MAX_INPUTS = 65_536
MAX_DOWNLOAD = 536_870_912


class Refusal(ValueError):
    """Fixed metadata-only refusal; private paths and native exception text never escape."""

    def __init__(self, code):
        self.code = code
        super().__init__(code)


def require(condition, code='HXLQ301'):
    if not condition:
        raise Refusal(code)


def unlinked(path):
    path = Path(path).absolute()
    require(not any(part.is_symlink() for part in (path, *path.parents)), 'HXLQ302')
    return path


def private(path, *, fresh=False):
    path = unlinked(path)
    if fresh:
        path.mkdir(mode=0o700)
    else:
        path.mkdir(parents=True, exist_ok=True, mode=0o700)
    info = path.stat()
    require(stat.S_ISDIR(info.st_mode) and info.st_uid == os.geteuid()
            and info.st_mode & 0o077 == 0, 'HXLQ302')
    return path


@contextmanager
def regular(path, maximum):
    path = unlinked(path)
    descriptor = os.open(path, os.O_RDONLY | os.O_NOFOLLOW | os.O_NONBLOCK)
    with os.fdopen(descriptor, 'rb') as stream:
        info = os.fstat(stream.fileno())
        require(stat.S_ISREG(info.st_mode) and info.st_nlink == 1 and 0 < info.st_size <= maximum,
                'HXLQ302')
        yield stream


def body(path, maximum=MAX_INPUTS):
    with regular(path, maximum) as stream:
        value = stream.read(maximum + 1)
    require(len(value) <= maximum, 'HXLQ302')
    return value


def canonical(value):
    return (json.dumps(value, sort_keys=True, separators=(',', ':'), allow_nan=False) + '\n').encode()


def validate(value):
    require(isinstance(value, dict) and set(value) == {'schema', 'policyId', 'sources', 'tool',
            'catalogSha256', 'toolchain', 'downloads', 'minimumPlatformRevision'})
    require(value['schema'] == SCHEMA and value['policyId'] == POLICY)
    sources = value['sources']
    require(isinstance(sources, dict) and set(sources) == {'caller', 'builds', 'platform'}
            and all(isinstance(item, str) and COMMIT.fullmatch(item) for item in sources.values()))
    require(isinstance(value['minimumPlatformRevision'], str) and COMMIT.fullmatch(value['minimumPlatformRevision']))
    tool = value['tool']
    require(isinstance(tool, dict) and set(tool) == {'version', 'sourceRevision', 'framework', 'downloadId'}
            and isinstance(tool['version'], str) and len(tool['version']) <= 128 and VERSION.fullmatch(tool['version'])
            and isinstance(tool['sourceRevision'], str) and COMMIT.fullmatch(tool['sourceRevision'])
            and tool['framework'] in ('net10.0', 'net11.0'))
    require(isinstance(value['catalogSha256'], str) and SHA.fullmatch(value['catalogSha256']))
    chain = value['toolchain']
    require(isinstance(chain, dict) and set(chain) == {'dotnetSdk', 'aspire', 'daprCli', 'daprRuntime'}
            and all(isinstance(item, str) and len(item) <= 128 and VERSION.fullmatch(item) for item in chain.values()))
    downloads = value['downloads']
    require(isinstance(downloads, list) and 0 < len(downloads) <= 32)
    identities, filenames = set(), set()
    for item in downloads:
        require(isinstance(item, dict) and set(item) == {'id', 'fileName', 'sha256'})
        identity, filename, digest = item['id'], item['fileName'], item['sha256']
        require(isinstance(identity, str) and IDENTITY.fullmatch(identity) and identity not in identities)
        require(isinstance(filename, str) and re.fullmatch(r'[a-zA-Z0-9][a-zA-Z0-9._-]{0,199}', filename)
                and filename.endswith(('.nupkg', '.tar.gz', '.zip')) and filename not in filenames)
        require(isinstance(digest, str) and SHA.fullmatch(digest))
        identities.add(identity)
        filenames.add(filename)
    require(isinstance(tool['downloadId'], str) and tool['downloadId'] in identities)
    return value


def read_inputs(path):
    def unique(pairs):
        value = {}
        for key, item in pairs:
            require(key not in value)
            value[key] = item
        return value

    def invalid(_):
        raise Refusal('HXLQ301')

    return validate(json.loads(body(path).decode('utf-8'), object_pairs_hook=unique, parse_constant=invalid))


def key(inputs):
    return hashlib.sha256(canonical(validate(inputs))).hexdigest()


def copy_verified(source, destination, expected):
    """Hash the held source bytes as copied, then independently verify the fresh destination."""
    digest = hashlib.sha256()
    count = 0
    with regular(source, MAX_DOWNLOAD) as stream, open(destination, 'xb') as output:
        os.chmod(destination, 0o600)
        for block in iter(lambda: stream.read(131_072), b''):
            count += len(block)
            require(count <= MAX_DOWNLOAD, 'HXLQ303')
            digest.update(block)
            output.write(block)
        output.flush()
        os.fsync(output.fileno())
    require(digest.hexdigest() == expected, 'HXLQ303')
    verify_download(destination, expected)


def verify_download(path, expected):
    with regular(path, MAX_DOWNLOAD) as stream:
        digest = hashlib.file_digest(stream, 'sha256').hexdigest()
    require(digest == expected, 'HXLQ303')


def verify_entry(entry, inputs):
    entry = unlinked(entry)
    info = entry.stat()
    require(stat.S_ISDIR(info.st_mode) and info.st_uid == os.geteuid()
            and info.st_mode & 0o077 == 0, 'HXLQ302')
    require({path.name for path in entry.iterdir()} == {'inputs.json', *[item['fileName'] for item in inputs['downloads']]},
            'HXLQ303')
    require(body(entry / 'inputs.json') == canonical(inputs), 'HXLQ303')
    for item in inputs['downloads']:
        verify_download(entry / item['fileName'], item['sha256'])
    return entry


def publish_directory(source, destination):
    """Linux no-replace rename: a destination created after preflight must survive unchanged."""
    require(sys.platform == 'linux', 'HXLQ304')
    rename = ctypes.CDLL(None, use_errno=True).renameat2
    rename.argtypes = (ctypes.c_int, ctypes.c_char_p, ctypes.c_int, ctypes.c_char_p, ctypes.c_uint)
    rename.restype = ctypes.c_int
    if rename(-100, os.fsencode(source), -100, os.fsencode(destination), 1) != 0:
        code = ctypes.get_errno()
        if code == errno.EEXIST:
            raise Refusal('HXLQ304')
        raise OSError(code, 'Directory publication failed')


@contextmanager
def cache_lock(cache, identity):
    locks = private(cache / 'locks')
    descriptor = os.open(unlinked(locks / identity), os.O_RDWR | os.O_CREAT | os.O_NOFOLLOW | os.O_NONBLOCK, 0o600)
    try:
        info = os.fstat(descriptor)
        require(stat.S_ISREG(info.st_mode) and info.st_nlink == 1 and info.st_uid == os.geteuid()
                and info.st_mode & 0o077 == 0, 'HXLQ302')
        fcntl.flock(descriptor, fcntl.LOCK_EX | fcntl.LOCK_NB)
        yield
    finally:
        os.close(descriptor)


def populate(cache, inputs, downloads):
    """Publish only verified immutable downloads. Existing poisoned entries fail; they are never replaced."""
    identity = key(inputs)
    cache = private(cache)
    entries = private(cache / 'entries')
    downloads = unlinked(downloads)
    entry = entries / identity
    with cache_lock(cache, identity):
        if os.path.lexists(entry):
            return verify_entry(entry, inputs), True
        temporary = Path(tempfile.mkdtemp(prefix=identity + '.', dir=entries))
        try:
            for item in inputs['downloads']:
                copy_verified(downloads / item['fileName'], temporary / item['fileName'], item['sha256'])
            with open(temporary / 'inputs.json', 'xb') as output:
                os.chmod(temporary / 'inputs.json', 0o600)
                output.write(canonical(inputs))
            verify_entry(temporary, inputs)
            publish_directory(temporary, entry)
        finally:
            if temporary.exists():
                shutil.rmtree(temporary)
    return entry, False


def restore(cache, inputs, destination):
    """Revalidate every consumed cache file; publish fresh private state without any old run data."""
    identity = key(inputs)
    cache = private(cache)
    destination = unlinked(destination)
    require(not os.path.lexists(destination), 'HXLQ304')
    parent = private(destination.parent)
    # A restore cannot add run state inside the immutable cache or consume itself as its source.
    require(cache != parent and cache not in parent.parents and destination not in cache.parents, 'HXLQ304')
    with cache_lock(cache, identity):
        entry = verify_entry(cache / 'entries' / identity, inputs)
        temporary = Path(tempfile.mkdtemp(prefix='.lifecycle-downloads-', dir=parent))
        try:
            for item in inputs['downloads']:
                copy_verified(entry / item['fileName'], temporary / item['fileName'], item['sha256'])
            with open(temporary / 'inputs.json', 'xb') as output:
                os.chmod(temporary / 'inputs.json', 0o600)
                output.write(canonical(inputs))
            verify_entry(temporary, inputs)
            publish_directory(temporary, destination)
        finally:
            if temporary.exists():
                shutil.rmtree(temporary)
    return destination


def source_audit(checkout, platform_repository, inputs):
    """Report exact current mismatches without changing pins or treating publication as acceptance."""
    validate(inputs)
    checkout, platform_repository = unlinked(checkout), unlinked(platform_repository)
    reasons = []

    def git(root, *arguments):
        result = subprocess.run(['git', '-c', 'core.commitGraph=false', *arguments], cwd=root,
                                capture_output=True, text=True, timeout=30)
        require(result.returncode == 0, 'HXLQ305')
        return result.stdout.strip()

    def check(condition, reason):
        if not condition:
            reasons.append(reason)

    sources = inputs['sources']
    check(git(checkout, 'rev-parse', 'HEAD') == sources['caller'], 'caller-source-mismatch')
    check(not git(checkout, 'status', '--porcelain', '--untracked-files=all', '--ignore-submodules=none'), 'caller-source-dirty')
    for name in ('builds', 'platform'):
        path = 'references/Hexalith.' + name.title()
        check(git(checkout, 'rev-parse', 'HEAD:' + path) == sources[name], name + '-gitlink-mismatch')
    tool = json.loads(body(checkout / '.config/dotnet-tools.json'))['tools']['hexalith.builds.module.cli']
    check(tool.get('version') == inputs['tool']['version'] and tool.get('rollForward') is False, 'caller-tool-mismatch')
    catalog = git(checkout / 'references/Hexalith.Builds', 'rev-parse', 'HEAD')
    check(catalog == sources['builds'], 'catalog-checkout-mismatch')
    # Git emits the exact committed bytes; working-tree CRLF conversion is checked separately.
    raw = subprocess.run(['git', '-c', 'core.commitGraph=false', 'show', sources['builds'] + ':Props/Directory.Packages.props'],
                         cwd=checkout / 'references/Hexalith.Builds', capture_output=True, timeout=30)
    require(raw.returncode == 0, 'HXLQ305')
    check(hashlib.sha256(raw.stdout).hexdigest() == inputs['catalogSha256'], 'catalog-content-mismatch')
    check(not git(checkout / 'references/Hexalith.Builds', 'status', '--porcelain', '--untracked-files=all'), 'catalog-source-dirty')
    properties = ElementTree.fromstring(raw.stdout)
    for name, property_name in (('aspire', 'HexalithAspireAppHostSdkVersion'), ('daprCli', 'HexalithDaprCliVersion'),
                                ('daprRuntime', 'HexalithDaprRuntimeVersion')):
        node = properties.find('.//' + property_name)
        check(node is not None and node.text == inputs['toolchain'][name], 'toolchain-' + name + '-mismatch')
    sdk = json.loads(body(checkout / 'global.json'))['sdk']['version']
    check(sdk == inputs['toolchain']['dotnetSdk'], 'toolchain-dotnetSdk-mismatch')
    ancestry = subprocess.run(['git', '-c', 'core.commitGraph=false', 'merge-base', '--is-ancestor',
                              inputs['minimumPlatformRevision'], sources['platform']], cwd=platform_repository,
                             capture_output=True, timeout=30)
    require(ancestry.returncode in (0, 1), 'HXLQ305')
    check(ancestry.returncode == 0, 'platform-live-port-binding-unavailable')
    return {'schema': SCHEMA, 'inputSetSha256': key(inputs), 'sources': sources, 'reasons': reasons,
            'sourceBindingsMatch': not reasons, 'liveQualificationExecuted': False, 'toolAccepted': False,
            'candidateAcceptance': False, 'publicationVerified': False}


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('operation', choices=('key', 'check-sources', 'populate', 'restore'))
    parser.add_argument('--inputs', type=Path, required=True)
    parser.add_argument('--cache', type=Path)
    parser.add_argument('--downloads', type=Path)
    parser.add_argument('--destination', type=Path)
    parser.add_argument('--checkout', type=Path)
    parser.add_argument('--platform-repository', type=Path)
    args = parser.parse_args(argv)
    started = time.monotonic()
    try:
        inputs = read_inputs(args.inputs)
        result = {'schema': SCHEMA, 'inputSetSha256': key(inputs), 'operation': args.operation,
                  'candidateAcceptance': False, 'toolAccepted': False, 'liveQualificationExecuted': False}
        if args.operation == 'check-sources':
            require(args.checkout is not None and args.platform_repository is not None)
            result.update(source_audit(args.checkout, args.platform_repository, inputs))
        elif args.operation == 'populate':
            require(args.cache is not None and args.downloads is not None)
            _, result['cacheHit'] = populate(args.cache, inputs, args.downloads)
            result['verifiedDownloads'] = len(inputs['downloads'])
        elif args.operation == 'restore':
            require(args.cache is not None and args.destination is not None)
            restore(args.cache, inputs, args.destination)
            result['verifiedDownloads'] = len(inputs['downloads'])
        result['elapsedSeconds'] = round(time.monotonic() - started, 6)
        print(json.dumps(result, sort_keys=True))
        return 1 if result.get('reasons') else 0
    except Exception as error:
        print(json.dumps({'failureCode': error.code if isinstance(error, Refusal) else 'HXLQ305',
                          'redacted': True, 'candidateAcceptance': False, 'toolAccepted': False}), file=sys.stderr)
        return 1


if __name__ == '__main__':
    raise SystemExit(main())
