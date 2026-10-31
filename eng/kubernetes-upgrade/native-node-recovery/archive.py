"""Descriptor-confined node archive primitives, without a live dispatcher.

Only qualified fixed-path metadata may select members. No source directory is
written. Extraction is for an explicitly fenced fresh target, never the source.
Unsupported ACL/xattr or hardlink state refuses capture rather than discarding it.
"""
import hashlib
import io
import os
from pathlib import Path
import stat
import tarfile
import time
from decimal import Decimal

MAX_BYTES = 512 * 1024 * 1024
MAX_FILE = 128 * 1024 * 1024
MAX_MEMBERS = 8192


def require(ok, why):
    if not ok:
        raise ValueError(why)


def parts(name):
    require(isinstance(name, str) and name and len(name.encode()) <= 4096
            and not name.startswith('/') and '\x00' not in name
            and all(v not in ('', '.', '..') for v in name.split('/')), 'archive-member-path-refused')
    return name.split('/')


def parent_fd(root, name):
    values = parts(name)
    directory = os.dup(root)
    try:
        for component in values[:-1]:
            child = os.open(component, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW, dir_fd=directory)
            os.close(directory)
            directory = child
        return directory, values[-1]
    except BaseException:
        os.close(directory)
        raise


def snapshot(info):
    return tuple(getattr(info, k) for k in ('st_dev', 'st_ino', 'st_size', 'st_mtime_ns',
                 'st_ctime_ns', 'st_mode', 'st_uid', 'st_gid', 'st_nlink'))


class HashingReader:
    def __init__(self, fd, deadline):
        self.fd, self.deadline, self.sha, self.bytes = fd, deadline, hashlib.sha256(), 0

    def read(self, size):
        require(time.monotonic() < self.deadline, 'archive-read-deadline')
        value = os.read(self.fd, min(size, 1048576))
        self.bytes += len(value)
        require(self.bytes <= MAX_FILE, 'archive-member-size-bound')
        self.sha.update(value)
        return value


def create(root, metadata, output):
    """Write a streaming gzip/PAX archive into an already-owned encryption pipe."""
    root = Path(root)
    entries = metadata['entries']
    require(isinstance(entries, dict) and 0 < len(entries) <= MAX_MEMBERS, 'archive-member-count-refused')
    for name in entries:
        parts(name)
    rootfd = os.open(root, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW)
    deadline, total, recorded, observed = time.monotonic() + 150, 0, {}, {}
    try:
        with tarfile.open(fileobj=output, mode='w|gz', format=tarfile.PAX_FORMAT) as archive:
            for name, entry in sorted(entries.items()):
                require(time.monotonic() < deadline, 'archive-create-deadline')
                directory, leaf = parent_fd(rootfd, name)
                try:
                    before = os.stat(leaf, dir_fd=directory, follow_symlinks=False)
                    require((before.st_uid, before.st_gid, stat.S_IMODE(before.st_mode)) ==
                            (entry['uid'], entry['gid'], entry['mode']), 'archive-source-owner-mode-drift')
                    # /proc/self/fd anchors the directory descriptor. The leaf is
                    # not followed; source inode/metadata are checked again below.
                    attrs = os.listxattr('/proc/self/fd/' + str(directory) + '/' + leaf, follow_symlinks=False)
                    require(attrs == [], 'archive-unsupported-acl-or-xattr')
                    member = tarfile.TarInfo(name)
                    member.uid, member.gid, member.mode = entry['uid'], entry['gid'], entry['mode']
                    member.uname = member.gname = ''
                    member.mtime = before.st_mtime_ns // 1000000000
                    member.pax_headers = {'mtime': str(before.st_mtime_ns // 1000000000) + '.'
                                          + str(before.st_mtime_ns % 1000000000).zfill(9)}
                    item = dict(entry, mtimeNs=before.st_mtime_ns, sourceAclAndXattrsAbsent=True)
                    if stat.S_ISREG(before.st_mode):
                        require(entry['kind'] == 'file' and before.st_nlink == 1 and before.st_size == entry['bytes']
                                and 0 <= before.st_size <= MAX_FILE, 'archive-source-file-profile-refused')
                        fd = os.open(leaf, os.O_RDONLY | os.O_NOFOLLOW | os.O_NONBLOCK, dir_fd=directory)
                        try:
                            require(snapshot(os.fstat(fd)) == snapshot(before), 'archive-source-file-replaced')
                            member.size = before.st_size
                            source = HashingReader(fd, deadline)
                            archive.addfile(member, source)
                            require(source.bytes == before.st_size and source.sha.hexdigest() == entry['sha256']
                                    and snapshot(os.fstat(fd)) == snapshot(before), 'archive-source-content-drift')
                            total += source.bytes
                            require(total <= MAX_BYTES, 'archive-source-total-bound')
                        finally:
                            os.close(fd)
                    elif stat.S_ISDIR(before.st_mode):
                        require(entry['kind'] == 'directory', 'archive-source-kind-drift')
                        childfd = os.open(leaf, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW, dir_fd=directory)
                        try:
                            children = {v[len(name)+1:].split('/')[0] for v in entries if v.startswith(name + '/')}
                            require(set(os.listdir(childfd)) == children, 'archive-source-directory-census-drift')
                        finally:os.close(childfd)
                        member.type = tarfile.DIRTYPE
                        archive.addfile(member)
                    elif stat.S_ISLNK(before.st_mode):
                        require(entry['kind'] == 'symlink' and os.readlink(leaf, dir_fd=directory) == entry['target'],
                                'archive-source-link-drift')
                        member.type, member.linkname = tarfile.SYMTYPE, entry['target']
                        archive.addfile(member)
                    else:
                        raise ValueError('archive-source-special-file-refused')
                    require(snapshot(os.stat(leaf, dir_fd=directory, follow_symlinks=False)) == snapshot(before),
                            'archive-source-path-drift')
                    recorded[name] = item
                    observed[name] = snapshot(before)
                finally:
                    os.close(directory)
        for name, before in observed.items():
            directory, leaf = parent_fd(rootfd, name)
            try:
                require(snapshot(os.stat(leaf, dir_fd=directory, follow_symlinks=False)) == before,
                        'archive-source-drift-after-member-captured')
            finally:os.close(directory)
    finally:
        os.close(rootfd)
    return {'entries': recorded, 'regularFileBytes': total, 'sourceWritten': False,
            'sourceAclAndXattrsAbsent': True, 'format': 'gzip/PAX/numeric-owner'}


def extract(root, manifest, stream):
    """Extract into an existing empty, private, noexec target tree only.

    The caller must independently establish Docker/network/mount/source fencing
    and supply only authenticated, independently read-back decrypted bytes.
    """
    root = Path(root)
    info = root.lstat()
    require(stat.S_ISDIR(info.st_mode) and stat.S_IMODE(info.st_mode) == 0o700 and not list(root.iterdir()),
            'archive-target-not-new-private-empty-directory')
    rootfd = os.open(root, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW)
    expected, seen, directories, total = manifest['entries'], set(), [], 0
    require(0 < len(expected) <= MAX_MEMBERS, 'archive-target-member-count-refused')
    for name in expected:
        parts(name)
    try:
        # Explicit, individually selected ancestor directories are created first.
        # No tar-controlled implicit ancestor or symlink is ever followed.
        names = set(expected)
        ancestors = { '/'.join(parts(n)[:i]) for n in names for i in range(1, len(parts(n))) }
        for name in sorted(ancestors, key=lambda v: (len(parts(v)), v)):
            if name in expected:
                require(expected[name]['kind'] == 'directory', 'archive-symlink-or-file-ancestor-refused')
            directory, leaf = parent_fd(rootfd, name)
            try:
                os.mkdir(leaf, 0o700, dir_fd=directory)
            finally:
                os.close(directory)
        with tarfile.open(fileobj=stream, mode='r|gz') as archive:
            for member in archive:
                name = member.name.rstrip('/') if member.isdir() else member.name
                parts(name)
                require(name in expected and name not in seen, 'archive-unapproved-or-duplicate-member')
                seen.add(name)
                entry = expected[name]
                require((member.uid, member.gid, member.mode) == (entry['uid'], entry['gid'], entry['mode'])
                        and member.uname == member.gname == '', 'archive-header-owner-mode-mismatch')
                require(set(member.pax_headers) <= {'mtime'} and int(Decimal(member.pax_headers.get(
                    'mtime', str(member.mtime))) * 1000000000) == entry['mtimeNs'], 'archive-header-time-or-pax-refused')
                directory, leaf = parent_fd(rootfd, name)
                try:
                    if member.isfile():
                        require(entry['kind'] == 'file' and member.size == entry['bytes'] and member.size <= MAX_FILE,
                                'archive-file-size-or-kind-refused')
                        fd = os.open(leaf, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600, dir_fd=directory)
                        try:
                            source, sha, count = archive.extractfile(member), hashlib.sha256(), 0
                            while True:
                                data = source.read(1048576)
                                if not data:break
                                sha.update(data);count += len(data);total += len(data)
                                require(total <= MAX_BYTES and count <= entry['bytes'], 'archive-extraction-byte-bound')
                                view = memoryview(data)
                                while view:
                                    n = os.write(fd, view);require(n > 0, 'archive-target-write-refused');view = view[n:]
                            require(count == entry['bytes'] and sha.hexdigest() == entry['sha256'], 'archive-restored-content-mismatch')
                            os.fchown(fd, entry['uid'], entry['gid']);os.fchmod(fd, entry['mode'])
                            os.utime(fd, ns=(entry['mtimeNs'], entry['mtimeNs']))
                        finally:
                            os.close(fd)
                    elif member.isdir():
                        require(entry['kind'] == 'directory', 'archive-directory-kind-mismatch')
                        if name not in ancestors:os.mkdir(leaf, 0o700, dir_fd=directory)
                        directories.append((name, entry))
                    elif member.issym():
                        require(entry['kind'] == 'symlink' and member.linkname == entry['target'], 'archive-link-target-mismatch')
                        os.symlink(entry['target'], leaf, dir_fd=directory)
                        os.chown(leaf, entry['uid'], entry['gid'], dir_fd=directory, follow_symlinks=False)
                        os.utime(leaf, ns=(entry['mtimeNs'], entry['mtimeNs']), dir_fd=directory, follow_symlinks=False)
                    else:
                        raise ValueError('archive-hardlink-or-special-member-refused')
                finally:
                    os.close(directory)
        require(seen == set(expected) and total == manifest['regularFileBytes'], 'archive-complete-inventory-not-restored')
        for name, entry in sorted(directories, key=lambda item: len(parts(item[0])), reverse=True):
            directory, leaf = parent_fd(rootfd, name)
            try:
                fd = os.open(leaf, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW, dir_fd=directory)
                try:
                    os.fchown(fd, entry['uid'], entry['gid']);os.fchmod(fd, entry['mode'])
                    os.utime(fd, ns=(entry['mtimeNs'], entry['mtimeNs']))
                finally:os.close(fd)
            finally:os.close(directory)
    finally:
        os.close(rootfd)
    return {'state':'verified-node-file-restoration-only','members':len(seen),'regularFileBytes':total,
            'numericOwnersAndModesApplied':True,'sourceWritten':False,'nativeBootabilityProven':False}
