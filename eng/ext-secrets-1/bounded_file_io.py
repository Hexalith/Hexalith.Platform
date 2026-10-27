"""Held no-follow regular-file IO with one caller deadline and bounded kernel ownership.

A timed-out worker keeps its slot until the actual syscall and cleanup return.
"""
import contextvars
import hashlib
import os
import stat
import threading
import time
from concurrent.futures import Future, TimeoutError
from pathlib import Path
from worker_startup_ownership import provably_unstarted

_slots = threading.BoundedSemaphore(4)
_deadline = contextvars.ContextVar('selected_file_io_deadline', default=None)


def check():
    if _deadline.get() is not None and time.monotonic() >= _deadline.get():
        raise TimeoutError('SelectedInputDeadline')


def run(action, timeout=30):
    if _deadline.get() is not None:
        check()
        value=action();check();return value
    end = time.monotonic() + timeout
    slots = _slots
    if not slots.acquire(timeout=max(0, end-time.monotonic())):
        raise TimeoutError('SelectedInputCapacity')
    future = Future()
    def worker():
        token = _deadline.set(end)
        try:
            check()
            result=action();check();future.set_result(result)
        except BaseException as error:
            future.set_exception(error)
        finally:
            _deadline.reset(token)
            slots.release()
    context=contextvars.copy_context()
    thread = None
    try:
        thread = threading.Thread(target=lambda:context.run(worker), daemon=True)
        thread.start()
    except BaseException:
        # Bootstrap acknowledgement can fail before Thread.ident is assigned.
        if provably_unstarted(thread):
            slots.release()
        raise
    return future.result(timeout=max(0, end-time.monotonic()))


def acquire(path, directory=False):
    raw = os.fspath(path)
    if not os.path.isabs(raw) or os.path.normpath(raw) != raw or len(raw) > 4096:
        raise ValueError('ExactAbsoluteSelectedFile')
    parent = os.open('/', os.O_RDONLY | os.O_DIRECTORY | os.O_CLOEXEC)
    try:
        parts = raw.split('/')[1:]
        for index, name in enumerate(parts):
            check()
            flags = os.O_RDONLY | os.O_NONBLOCK | os.O_NOFOLLOW | os.O_CLOEXEC
            if index < len(parts)-1 or directory:
                flags |= os.O_DIRECTORY
            child = os.open(name, flags, dir_fd=parent)
            os.close(parent)
            parent = child
        result, parent = parent, None
        return result
    finally:
        if parent is not None:
            os.close(parent)


def _scan(path, maximum, hash_only=False, prefix=0, private=False, consume=None, forbidden_inodes=()):
    fd = acquire(path)
    data = bytearray()
    try:
        before = os.fstat(fd)
        if (before.st_dev,before.st_ino) in forbidden_inodes:
            raise ValueError('configured credential cannot be public evidence')
        if not stat.S_ISREG(before.st_mode) or not 0 < before.st_size <= maximum:
            raise ValueError('BoundedRegularSelectedFile')
        hashed = None if private else hashlib.sha256()
        count = 0
        if private:
            # readv writes directly into the only owned writable region. Include a
            # growth sentinel; every byte, including unused/read-failure space, clears.
            data = bytearray(before.st_size+1)
            while count < len(data):
                check()
                region = memoryview(data)[count:min(len(data),count+65536)]
                try: length = os.readv(fd,[region])
                finally: region.release()
                if not length: break
                count += length
                if count > before.st_size: raise ValueError('SelectedFileGrew')
            if count == before.st_size:
                # The sentinel is zero and has never received private content.
                del data[count:]
        else:
            while True:
                check()
                chunk = os.read(fd, min(65536, maximum+1-count))
                if not chunk: break
                count += len(chunk)
                if count > before.st_size: raise ValueError('SelectedFileGrew')
                if hash_only: hashed.update(chunk)
                else: data.extend(chunk[:max(0,prefix-len(data))] if prefix else chunk)
        after = os.fstat(fd)
        if count != before.st_size or (before.st_size, before.st_mtime_ns, before.st_ctime_ns) != (after.st_size, after.st_mtime_ns, after.st_ctime_ns):
            raise ValueError('SelectedFileChanged')
        check()
        if consume is not None:
            return consume(data)
        return hashed.hexdigest() if hash_only else bytes(data)
    finally:
        if private:
            for index in range(len(data)): data[index] = 0
        os.close(fd)


def read(path, maximum, *, private=False, forbidden_inodes=()):
    return run(lambda: _scan(path, maximum, private=private, forbidden_inodes=forbidden_inodes))


def use_private(path, maximum, consume):
    # The decoder receives the only owned bytearray; it is cleared on every outcome,
    # including a late syscall after its caller timed out.
    return run(lambda: _scan(path,maximum,private=True,consume=consume))


def file_hash(path, maximum=1024*1024*1024):
    return run(lambda: _scan(path, maximum, hash_only=True))


def magic(path, maximum):
    return run(lambda: _scan(path, maximum, prefix=4))


def census(path, maximum=10000, maximum_depth=64):
    def collect():
        found = set()
        entries_seen = 0
        directories_seen = 0
        def walk(fd, raw, depth):
            nonlocal entries_seen, directories_seen
            check()
            if depth > maximum_depth:
                raise ValueError('BoundedExecutionCensusDepth')
            with os.scandir(fd) as entries:
                for entry in entries:
                    check()
                    entries_seen += 1
                    if entries_seen > 2*maximum:
                        raise ValueError('BoundedExecutionCensusEntries')
                    info = entry.stat(follow_symlinks=False)
                    if stat.S_ISLNK(info.st_mode) or not (stat.S_ISREG(info.st_mode) or stat.S_ISDIR(info.st_mode)):
                        raise ValueError('RegularClosedExecutionCensus')
                    item = str(Path(raw) / entry.name)
                    if stat.S_ISDIR(info.st_mode):
                        directories_seen += 1
                        if directories_seen > maximum:
                            raise ValueError('BoundedExecutionCensusEntries')
                        child = os.open(entry.name, os.O_RDONLY | os.O_NONBLOCK | os.O_NOFOLLOW | os.O_DIRECTORY | os.O_CLOEXEC, dir_fd=fd)
                        try:
                            walk(child, item, depth+1)
                        finally:
                            os.close(child)
                    else:
                        found.add(item)
                        if len(found) > maximum:
                            raise ValueError('BoundedExecutionCensus')
        descriptor = acquire(path, directory=True)
        try:
            walk(descriptor, str(path), 0)
        finally:
            os.close(descriptor)
        return found
    return run(collect)
