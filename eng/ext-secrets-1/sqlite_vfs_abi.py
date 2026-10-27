"""Documented public SQLite VFS and file ABI declarations, with private nested layouts."""
import ctypes


class SqliteVfsAbi:
    class _Vfs(ctypes.Structure):
        pass

    _Vfs._fields_ = [('iVersion', ctypes.c_int), ('szOsFile', ctypes.c_int),
        ('mxPathname', ctypes.c_int), ('pNext', ctypes.c_void_p), ('zName', ctypes.c_char_p),
        ('pAppData', ctypes.c_void_p)] + [(name, ctypes.c_void_p) for name in (
        'xOpen', 'xDelete', 'xAccess', 'xFullPathname', 'xDlOpen', 'xDlError',
        'xDlSym', 'xDlClose', 'xRandomness', 'xSleep', 'xCurrentTime',
        'xGetLastError', 'xCurrentTimeInt64', 'xSetSystemCall', 'xGetSystemCall',
        'xNextSystemCall')]
    # Public sqlite3_file/sqlite3_io_methods ABI. No default unixFile is inspected.
    class _File(ctypes.Structure):
        _fields_ = [('pMethods',ctypes.c_void_p)]
    class _Io(ctypes.Structure):
        _fields_ = [('iVersion',ctypes.c_int)]+[(name,ctypes.c_void_p) for name in
            ('xClose','xRead','xWrite','xTruncate','xSync','xFileSize','xLock','xUnlock','xCheckReservedLock','xFileControl','xSectorSize','xDeviceCharacteristics')]
