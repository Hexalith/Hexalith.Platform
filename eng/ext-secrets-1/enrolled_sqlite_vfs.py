"""Per-connection Linux descriptor acquisition using SQLite's public VFS ABI.

Only documented sqlite3_vfs fields are used. The already loaded _sqlite3 runtime
opens the main database through a held descriptor, so another thread's IO cannot
be mistaken for this connection. Journal names remain under the held parent.
No CPython object memory or SQLite private file layout is inspected.
"""
import ctypes
import errno
import fcntl
import stat
import itertools
import os
import sqlite3
import _sqlite3
import threading


from sqlite_vfs_abi import SqliteVfsAbi

_Vfs, _File, _Io = SqliteVfsAbi._Vfs, SqliteVfsAbi._File, SqliteVfsAbi._Io

_Open = ctypes.CFUNCTYPE(ctypes.c_int, ctypes.POINTER(_Vfs), ctypes.c_char_p,
    ctypes.c_void_p, ctypes.c_int, ctypes.POINTER(ctypes.c_int))
_FullPath = ctypes.CFUNCTYPE(ctypes.c_int, ctypes.POINTER(_Vfs), ctypes.c_char_p,
    ctypes.c_int, ctypes.c_void_p)
_Callback = ctypes.CFUNCTYPE
_Close = _Callback(ctypes.c_int,ctypes.c_void_p)
_Read = _Callback(ctypes.c_int,ctypes.c_void_p,ctypes.c_void_p,ctypes.c_int,ctypes.c_int64)
_Write = _Read
_Truncate = _Callback(ctypes.c_int,ctypes.c_void_p,ctypes.c_int64)
_Int = _Callback(ctypes.c_int,ctypes.c_void_p,ctypes.c_int)
_Size = _Callback(ctypes.c_int,ctypes.c_void_p,ctypes.POINTER(ctypes.c_int64))
_Check = _Callback(ctypes.c_int,ctypes.c_void_p,ctypes.POINTER(ctypes.c_int))
_Control = _Callback(ctypes.c_int,ctypes.c_void_p,ctypes.c_int,ctypes.c_void_p)
_Delete = _Callback(ctypes.c_int,ctypes.POINTER(_Vfs),ctypes.c_char_p,ctypes.c_int)
_Access = _Callback(ctypes.c_int,ctypes.POINTER(_Vfs),ctypes.c_char_p,ctypes.c_int,ctypes.POINTER(ctypes.c_int))
_PENDING, _RESERVED, _SHARED, _SHARED_SIZE = 0x40000000,0x40000001,0x40000002,510
_names = itertools.count()
_registration = threading.Lock()
# SQLite may defer a connection close until a cursor is finalized. Its public
# callback storage stays pinned until that actual final xClose, never GC timing.
_leases = {}


def _runtime():
    try:
        lib = ctypes.CDLL(_sqlite3.__file__)
        lib.sqlite3_libversion_number.restype = ctypes.c_int
        lib.sqlite3_vfs_find.argtypes = [ctypes.c_char_p]
        lib.sqlite3_vfs_find.restype = ctypes.POINTER(_Vfs)
        lib.sqlite3_vfs_register.argtypes = [ctypes.POINTER(_Vfs), ctypes.c_int]
        lib.sqlite3_vfs_unregister.argtypes = [ctypes.POINTER(_Vfs)]
        if sqlite3.threadsafety < 1: raise ValueError('External.LoadedSqlitePublicVfsDescriptorAcquisitionV1')
        expected = sqlite3.sqlite_version_info
        if lib.sqlite3_libversion_number() != expected[0]*1000000+expected[1]*1000+expected[2]:
            raise ValueError('ExactLoadedSqliteVfsRuntime')
        return lib
    except (AttributeError, OSError) as error:
        raise ValueError('External.LoadedSqlitePublicVfsDescriptorAcquisitionV1') from error


class EnrolledConnectionVfs:
    """Owns one registered VFS until the exact connection has closed."""
    def __init__(self, locator, descriptor, parent_descriptor):
        self.lib = _runtime()
        self.base = self.lib.sqlite3_vfs_find(None)
        if not self.base or self.base.contents.iVersion not in (1, 2, 3):
            raise ValueError('External.LoadedSqlitePublicVfsDescriptorAcquisitionV1')
        self.locator = os.fsencode(locator)
        self.main = ctypes.create_string_buffer(os.fsencode('/proc/self/fd/'+str(descriptor))+b'\0\0')
        self.opened = 0
        self.failure = False
        self.closed = False
        self.name = ('platform-enrolled-'+str(next(_names))).encode('ascii')
        self.vfs = _Vfs()
        # Copy only the publicly declared version's fields from the loaded VFS.
        size = _Vfs.xCurrentTimeInt64.offset if self.base.contents.iVersion == 1 else _Vfs.xSetSystemCall.offset if self.base.contents.iVersion == 2 else ctypes.sizeof(_Vfs)
        ctypes.memmove(ctypes.byref(self.vfs), self.base, size)
        self.vfs.zName = self.name
        self.vfs.pNext = None
        self.files = {}
        self.parent = os.dup(parent_descriptor)
        expected = os.fstat(descriptor)
        self.expected = (expected.st_dev,expected.st_ino)
        self.initial_parent_change = os.fstat(self.parent).st_ctime_ns
        self.vfs.szOsFile = ctypes.sizeof(_File)
        self.io = _Io(); self.io.iVersion = 1
        self.io_callbacks = []
        def install(name, signature, action):
            def call(*args):
                try: return action(*args)
                except BlockingIOError: return 5  # SQLITE_BUSY
                except BaseException: return 10  # SQLITE_IOERR, no exception text crosses native ABI
            callback=signature(call);self.io_callbacks.append(callback)
            setattr(self.io,name,ctypes.cast(callback,ctypes.c_void_p).value)
        def descriptor_for(file): return self.files[file][0]
        def close_file(file):
            fd,level=self.files.pop(file);os.close(fd)
            if self.closed and not self.files:
                os.close(self.parent)
                with _registration: _leases.pop(self.name,None)
            return 0
        def read_file(file,destination,length,offset):
            raw=os.pread(descriptor_for(file),length,offset)
            ctypes.memmove(destination,raw,len(raw))
            if len(raw)<length:
                ctypes.memset(destination+len(raw),0,length-len(raw));return 522  # SQLITE_IOERR_SHORT_READ
            return 0
        def write_file(file,source,length,offset):
            # These are public SQLite pages/journal bytes, never private credentials.
            raw=ctypes.string_at(source,length);written=0
            while written<length:
                actual=os.pwrite(descriptor_for(file),raw[written:],offset+written)
                if actual<=0: return 778
                written+=actual
            return 0
        def truncate(file,size): os.ftruncate(descriptor_for(file),size);return 0
        def sync(file,flags):
            os.fsync(descriptor_for(file));os.fsync(self.parent);return 0
        def file_size(file,destination): destination[0]=os.fstat(descriptor_for(file)).st_size;return 0
        def lock(file,level):
            fd,current=self.files[file]
            if level<=current: return 0
            if current==0:
                fcntl.lockf(fd,fcntl.LOCK_SH|fcntl.LOCK_NB,1,_PENDING)
                try: fcntl.lockf(fd,fcntl.LOCK_SH|fcntl.LOCK_NB,_SHARED_SIZE,_SHARED)
                finally: fcntl.lockf(fd,fcntl.LOCK_UN,1,_PENDING)
                current=1;self.files[file]=(fd,current)
            if level==2: fcntl.lockf(fd,fcntl.LOCK_EX|fcntl.LOCK_NB,1,_RESERVED)
            if level>=3:
                fcntl.lockf(fd,fcntl.LOCK_EX|fcntl.LOCK_NB,1,_PENDING)
                self.files[file]=(fd,3)
            if level==4: fcntl.lockf(fd,fcntl.LOCK_EX|fcntl.LOCK_NB,_SHARED_SIZE,_SHARED)
            self.files[file]=(fd,level);return 0
        def unlock(file,level):
            fd,current=self.files[file]
            if current>=3 and level==1: fcntl.lockf(fd,fcntl.LOCK_SH|fcntl.LOCK_NB,_SHARED_SIZE,_SHARED)
            if level==0: fcntl.lockf(fd,fcntl.LOCK_UN,_SHARED_SIZE,_SHARED)
            if current>=2:
                fcntl.lockf(fd,fcntl.LOCK_UN,2,_PENDING)
            self.files[file]=(fd,level);return 0
        def reserved(file,destination):
            fd,current=self.files[file]
            if current>=2: destination[0]=1;return 0
            try: fcntl.lockf(fd,fcntl.LOCK_EX|fcntl.LOCK_NB,1,_RESERVED)
            except BlockingIOError: destination[0]=1;return 0
            fcntl.lockf(fd,fcntl.LOCK_UN,1,_RESERVED);destination[0]=0;return 0
        def control(file,operation,argument):
            if operation==1:  # SQLITE_FCNTL_LOCKSTATE
                ctypes.cast(argument,ctypes.POINTER(ctypes.c_int))[0]=self.files[file][1];return 0
            return 12  # SQLITE_NOTFOUND: optional controls remain unsupported
        install('xClose',_Close,close_file);install('xRead',_Read,read_file);install('xWrite',_Write,write_file)
        install('xTruncate',_Truncate,truncate);install('xSync',_Int,sync);install('xFileSize',_Size,file_size)
        install('xLock',_Int,lock);install('xUnlock',_Int,unlock);install('xCheckReservedLock',_Check,reserved)
        install('xFileControl',_Control,control);install('xSectorSize',_Close,lambda file:4096)
        install('xDeviceCharacteristics',_Close,lambda file:0)

        @_Open
        def open_file(unused, path, file, flags, actual_flags):
            fd=None
            try:
                if flags & 0x100:  # SQLITE_OPEN_MAIN_DB
                    if path != self.locator or self.opened or flags & 4:
                        self.failure=True;return 14
                    current=os.stat(os.path.basename(self.locator),dir_fd=self.parent,follow_symlinks=False)
                    if (current.st_dev,current.st_ino)!=self.expected or os.fstat(self.parent).st_ctime_ns!=self.initial_parent_change:
                        self.failure=True;return 14
                    fd=os.open('/proc/self/fd/'+str(descriptor),os.O_RDWR|os.O_CLOEXEC)
                    info=os.fstat(fd)
                    if (info.st_dev,info.st_ino)!=self.expected or os.fstat(self.parent).st_ctime_ns!=self.initial_parent_change: self.failure=True;return 14
                    self.opened+=1
                elif path==self.locator+b'-journal':
                    # Existing DELETE journal placement; no WAL/shared memory/temp database.
                    mode=os.O_RDWR|os.O_CLOEXEC|os.O_NOFOLLOW|os.O_NONBLOCK
                    if flags&4: mode|=os.O_CREAT
                    if flags&16: mode|=os.O_EXCL
                    fd=os.open(os.path.basename(path),mode,0o600,dir_fd=self.parent)
                    info=os.fstat(fd)
                    if not stat.S_ISREG(info.st_mode) or info.st_nlink!=1 or info.st_dev!=self.expected[0]: self.failure=True;return 14
                else: self.failure=True;return 14
                self.files[file]=(fd,0);fd=None
                ctypes.cast(file,ctypes.POINTER(_File)).contents.pMethods=ctypes.addressof(self.io)
                if actual_flags: actual_flags[0]=flags
                return 0
            except BaseException:
                self.failure=True;return 14
            finally:
                if fd is not None: os.close(fd)

        @_Delete
        def delete_file(unused,path,sync_directory):
            try:
                if path!=self.locator+b'-journal': self.failure=True;return 10
                os.unlink(os.path.basename(path),dir_fd=self.parent)
                # FULL need not request sync_directory. Admission is durable only
                # after DELETE's commit record (journal absence) reaches this parent.
                os.fsync(self.parent)
                return 0
            except FileNotFoundError:
                try: os.fsync(self.parent);return 0
                except BaseException: self.failure=True;return 10
            except BaseException: self.failure=True;return 10

        @_Access
        def access_file(unused,path,flags,result):
            try:
                if path not in (self.locator,self.locator+b'-journal',self.locator+b'-wal',self.locator+b'-shm'):
                    self.failure=True;return 10
                info=os.stat(os.path.basename(path),dir_fd=self.parent,follow_symlinks=False)
                result[0]=int(stat.S_ISREG(info.st_mode) and info.st_size>0);return 0
            except FileNotFoundError: result[0]=0;return 0
            except BaseException: return 10

        @_FullPath
        def full_path(unused, path, capacity, destination):
            try:
                if path != self.locator or len(self.locator)+1 > capacity:
                    self.failure = True
                    return 14
                ctypes.memmove(destination, self.locator+b'\0', len(self.locator)+1)
                return 0
            except BaseException:
                self.failure = True
                return 14

        self.callbacks = (open_file, full_path,delete_file,access_file)
        self.vfs.xOpen = ctypes.cast(open_file, ctypes.c_void_p).value
        self.vfs.xDelete = ctypes.cast(delete_file,ctypes.c_void_p).value
        self.vfs.xAccess = ctypes.cast(access_file,ctypes.c_void_p).value
        self.vfs.xFullPathname = ctypes.cast(full_path, ctypes.c_void_p).value
        with _registration:
            if self.lib.sqlite3_vfs_register(ctypes.byref(self.vfs), 0) != 0:
                os.close(self.parent)
                raise ValueError('LoadedSqliteVfsRegistration')
            _leases[self.name] = self

    def verify(self):
        if self.failure or self.opened != 1:
            raise ValueError('ExactAcquiredSqliteMainDescriptorIdentity')

    def close(self):
        if not self.closed:
            with _registration:
                if self.lib.sqlite3_vfs_unregister(ctypes.byref(self.vfs)) != 0:
                    raise ValueError('LoadedSqliteVfsUnregistration')
            self.closed = True
            if not self.files:
                os.close(self.parent)
                with _registration: _leases.pop(self.name,None)
