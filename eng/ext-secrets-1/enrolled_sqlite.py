"""Existing-only SQLite acquisition under an independently enrolled storage identity.

The installed immutable namespace and participating-writer contract are mandatory;
local checks do not prove replication, independent administration or restore scope.
"""
import fcntl
import os
import re
import sqlite3
import stat
import time
from pathlib import Path
import bounded_file_io as selected_io
from linux_persistent_storage import require_persistent


from enrolled_sqlite_vfs import EnrolledConnectionVfs


class EnrolledSqliteStorage:
    def __init__(self, path, enrollment):
        self.path = str(path)
        self.enrollment = enrollment
        if (not isinstance(enrollment, dict) or enrollment.get('protocol') != 'LinuxEnrolledSqliteDeleteJournalV1'
                or enrollment.get('writerProtocol') != 'DirectoryFlockAndImmutableNamespaceV1'
                or not re.fullmatch('[0-9a-f]{64}', str(enrollment.get('placementEvidenceSha256', '')))
                or not isinstance(enrollment.get('identity'), str) or not enrollment['identity'].strip()
                or enrollment.get('path') != self.path or not isinstance(enrollment.get('components'), list)):
            raise ValueError('External.ExactIndependentlyEnrolledSqliteStorageIdentity')

    def connect(self, **kwargs):
        return HeldSqliteConnection(self, **kwargs)


class HeldSqliteConnection:
    def __init__(self, storage, **kwargs):
        self.storage, self.connection, self.descriptors = storage, None, []
        self.vfs = None
        self._closed = False
        try:
            raw = storage.path
            if not os.path.isabs(raw) or os.path.normpath(raw) != raw or len(raw) > 4096:
                raise ValueError('ExactExistingDatabaseLocator')
            current = '/'
            components = ['/']
            for part in raw.split('/')[1:]:
                current = os.path.join(current, part); components.append(current)
            enrolled = storage.enrollment['components']
            if len(enrolled) != len(components): raise ValueError('ExactEnrolledSqlitePathComponents')
            for index, path in enumerate(components):
                fd = os.open("/",os.O_RDONLY|os.O_DIRECTORY|os.O_CLOEXEC) if path=="/" else selected_io.acquire(path, directory=index < len(components)-1)
                self.descriptors.append(fd)
                info = os.fstat(fd)
                expected = enrolled[index]
                if (expected != {'path': path, 'device': info.st_dev, 'inode': info.st_ino}
                        or index == len(components)-1 and (not stat.S_ISREG(info.st_mode) or info.st_nlink != 1)):
                    raise ValueError('ExactEnrolledSqlitePathComponents')
            self.parent, self.file = self.descriptors[-2:]
            require_persistent(self.parent);require_persistent(self.file)
            if os.fstat(self.parent).st_dev!=os.fstat(self.file).st_dev:
                raise ValueError('EnrolledSqliteJournalPlacement')
            end = time.monotonic() + min(10, kwargs.get('timeout', 10))
            while True:
                try: fcntl.flock(self.parent, fcntl.LOCK_EX | fcntl.LOCK_NB); break
                except BlockingIOError:
                    if time.monotonic() >= end: raise TimeoutError('EnrolledSqliteWriterFence')
                    time.sleep(.01)
            self.verify()
            # Held parent descriptor prevents ancestor traversal from selecting a different namespace.
            locator = Path('/proc/self/fd') / str(self.parent) / Path(raw).name
            # A connection-owned public VFS opens through the held main descriptor.
            # Its identity cannot be supplied by any concurrent unrelated/decoy open.
            self.vfs = EnrolledConnectionVfs(str(locator),self.file,self.parent)
            try: self.connection=sqlite3.connect(locator.as_uri()+'?mode=rw&vfs='+self.vfs.name.decode('ascii'),uri=True,**kwargs)
            except sqlite3.Error:
                if self.vfs.failure: raise ValueError('ExactAcquiredSqliteMainDescriptorIdentity') from None
                raise
            self.vfs.verify()
            self.verify()
            mode = self.connection.execute('PRAGMA journal_mode').fetchone()[0]
            if mode.lower() != 'delete': raise ValueError('External.EnrolledSqliteDeleteJournalPlacement')
            self.connection.execute('PRAGMA synchronous=FULL')
            rows = self.connection.execute('SELECT slot,identity,typeof(slot),typeof(identity) FROM database_storage_enrollment').fetchall()
            if rows != [(1,storage.enrollment['identity'],'integer','text')]: raise ValueError('ExactExistingSqliteStorageEnrollment')
            self.verify()
        except BaseException:
            self.close(); raise

    def verify(self):
        selected_io.check()
        if self.connection is not None and self.vfs is not None: self.vfs.verify()
        for expected in self.storage.enrollment['components']:
            fd = os.open('/',os.O_RDONLY|os.O_DIRECTORY|os.O_CLOEXEC) if expected['path']=='/' else selected_io.acquire(expected['path'], directory=expected['path'] != self.storage.path)
            try:
                actual = os.fstat(fd)
                if (actual.st_dev, actual.st_ino) != (expected['device'], expected['inode']):
                    raise ValueError('EnrolledSqliteLocatorReplaced')
            finally: os.close(fd)
        for suffix in ('-journal', '-wal', '-shm'):
            try: info = os.stat(Path(self.storage.path).name + suffix, dir_fd=self.parent, follow_symlinks=False)
            except FileNotFoundError: continue
            if (suffix != '-journal' or not stat.S_ISREG(info.st_mode) or info.st_nlink != 1
                    or info.st_dev != os.fstat(self.file).st_dev):
                raise ValueError('EnrolledSqliteJournalPlacement')

    def execute(self, *args, **kwargs):
        self.verify(); result = self.connection.execute(*args, **kwargs); self.verify(); return result

    def commit(self):
        self.verify(); self.connection.commit(); self.verify()

    def rollback(self):
        if self.connection is not None: self.connection.rollback()

    def __del__(self):
        if hasattr(self,"_closed"):
            try: self.close()
            except BaseException: pass

    def close(self):
        if self._closed: return
        self._closed = True
        try:
            if self.connection is not None: self.connection.close()
        finally:
            try:
                if self.vfs is not None: self.vfs.close()
            finally:
                for fd in reversed(self.descriptors): os.close(fd)
                self.descriptors = []
