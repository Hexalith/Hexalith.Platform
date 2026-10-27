"""Read-only Linux persistence/private-allocation checks for the existing SQLite pool protocol.

These local facts do not establish replication, restore scope, future snapshot
exclusion, hardware durability or independently administered storage ownership.
"""
import ctypes
import fcntl
import os
import struct
import sys
import bounded_file_io as selected_io

_libc=ctypes.CDLL(None,use_errno=True)
_libc.fstatfs.argtypes=[ctypes.c_int,ctypes.c_void_p]
_libc.fstatfs.restype=ctypes.c_int


def require_persistent(fd):
    if sys.platform!='linux' or ctypes.sizeof(ctypes.c_void_p)!=8:
        raise ValueError('External.PersistentAtomicLocalSqliteStorageProtocol')
    buffer=ctypes.create_string_buffer(256)
    if _libc.fstatfs(fd,buffer)!=0:raise OSError(ctypes.get_errno(),'Existing storage filesystem identity unavailable')
    family=struct.unpack_from('=q',buffer.raw)[0]
    if family not in (0xef53,0x58465342,0x9123683e):raise ValueError('External.PersistentAtomicLocalSqliteStorageProtocol')
    return family


def require_private_allocated(fd,size,*,start=0,allow_unwritten=True):
    # No SHARED flag is not an in-place-write guarantee on a default COW filesystem.
    # This selected fixed-slot protocol supports ext4/XFS only; startup never sets NOCOW.
    if require_persistent(fd)==0x9123683e:
        raise ValueError('External.BtrfsFixedSlotInPlaceWriteProtocolUnavailable')
    covered=start;end=start+size;count=0
    while covered<end:
        selected_io.check();frame=bytearray(32+128*56)
        struct.pack_into('=QQIIII',frame,0,covered,end-covered,0,0,128,0)
        fcntl.ioctl(fd,0xc020660b,frame,True)  # FS_IOC_FIEMAP: no allocation or fresh free-space probe.
        mapped=struct.unpack_from('=I',frame,20)[0]
        if not 1<=mapped<=128:raise ValueError('External.EnrolledPrivateAllocatedPhaseCompletionExtents')
        for index in range(mapped):
            logical,physical,length=struct.unpack_from('=QQQ',frame,32+index*56)
            flags=struct.unpack_from('=I',frame,32+index*56+40)[0];count+=1
            # Private allocated or preallocated unwritten extents only; no unknown,
            # delayed, shared/COW, encoded, inline/tail or unaligned allocation.
            if (count>65536 or logical>covered or not physical or not length or logical+length<=covered
                    or flags & ~(0x1|0x1000|(0x800 if allow_unwritten else 0))):
                raise ValueError('External.EnrolledPrivateAllocatedPhaseCompletionExtents')
            covered=logical+length
        if flags&1 and covered<end:raise ValueError('External.EnrolledPrivateAllocatedPhaseCompletionExtents')
