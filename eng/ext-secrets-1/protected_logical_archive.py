"""Read exact C# ProtectedArchiveGeometryV1 originals under accepted public enrollment.

No discovery supplies trust, no resources are created, and no uncertain frame is repaired.
The caller's selected-file worker owns every descriptor through cancellation and cleanup.
"""
import hashlib
import json
import os
import stat
import struct
from contextlib import ExitStack

import bounded_file_io as selected_io

HEADER=8192
MAXIMUM=64*1024*1024


def _decode(raw):
    def unique(pairs):
        result=dict(pairs)
        if len(result)!=len(pairs):raise ValueError('Duplicate protected archive property')
        return result
    return json.loads(raw,object_pairs_hook=unique)


def _identity(info):
    return f'{os.major(info.st_dev)}:{os.minor(info.st_dev)}:{info.st_ino}'


def _exact(fd,length,offset):
    result=bytearray()
    while len(result)<length:
        selected_io.check();part=os.pread(fd,min(65536,length-len(result)),offset+len(result))
        if not part:raise ValueError('Truncated protected original')
        result.extend(part)
    selected_io.check();return bytes(result)


def read(path,maximum,enrollments=()):
    """Legacy literal files remain readable. Fixed slots require an explicitly accepted configuration."""
    path=os.fspath(path)
    if not os.path.isabs(path) or os.path.normpath(path)!=path or len(path)>4096 or not 0<maximum<=MAXIMUM:
        raise ValueError('Exact bounded logical archive locator')
    def acquire():
        try:return selected_io.read(path,maximum)
        except FileNotFoundError:pass
        if not isinstance(enrollments,(list,tuple)) or len(enrollments)>64:raise ValueError('Accepted protected archive enrollments')
        matches=[value for value in enrollments if isinstance(value,dict) and any(path.startswith(root.rstrip('/')+'/') for root in value.get('ArchiveRoots',{}))]
        if len(matches)!=1:raise FileNotFoundError('Exact independently accepted protected archive enrollment required')
        return _read_protected(path,maximum,matches[0])
    return selected_io.run(acquire)


def _read_protected(path,maximum,config):
    chunk,count,metadata=config['ChunkBytes'],config['ChunkCount'],config['MaximumMetadataBytes']
    roots=config['ArchiveRoots'];pool=config['Directory']
    if (config['StorageProtocol']!='Ext4PrivateWrittenNoSnapshotsV1' or type(chunk) is not int or not 4096<=chunk<=65536 or chunk%4096
            or type(count) is not int or not 1<=count<=65536 or type(metadata) is not int or not 65536<=metadata<=32*1024*1024
            or not isinstance(roots,dict) or not 1<=len(roots)<=64):raise ValueError('Exact protected allocation geometry')
    with ExitStack() as stack:
        held=[]
        def hold(name,directory=False):
            selected_io.check();fd=selected_io.acquire(name,directory=directory);stack.callback(os.close,fd)
            before=os.fstat(fd);held.append((name,fd,before,directory));return fd,before
        directory,directory_info=hold(pool,True)
        raw=selected_io.read(pool+'/allocation-enrollment.json',65536)
        if hashlib.sha256(raw).hexdigest()!=config['EnrollmentSha256'].lower():raise ValueError('Changed accepted allocation enrollment')
        enrolled=_decode(raw);enrolled['EnrollmentSha256']=config['EnrollmentSha256']
        if enrolled!=config:raise ValueError('Substituted accepted allocation enrollment')
        for root,identity in roots.items():
            fd,info=hold(root,True)
            if _identity(info)!=identity or info.st_dev!=directory_info.st_dev:raise ValueError('Protected archive root identity')
            marker=_decode(selected_io.read(root+'/.archive-allocation.json',65536))
            if marker!=config:raise ValueError('Protected archive marker differs from accepted enrollment')
        identities=[config['DataIdentity'],config['Metadata0Identity'],config['Metadata1Identity']]
        if len(set(identities))!=3:raise ValueError('Disjoint protected archive resources')
        resources=[];stride=2*HEADER+chunk
        for name,identity,size in zip(('allocation-data.bin','allocation-metadata-0.bin','allocation-metadata-1.bin'),identities,(HEADER+stride*count,metadata+HEADER,metadata+HEADER)):
            fd,info=hold(pool+'/'+name)
            if not stat.S_ISREG(info.st_mode) or info.st_nlink!=1 or _identity(info)!=identity or info.st_dev!=directory_info.st_dev or info.st_size!=size:
                raise ValueError('Exact held protected archive resource')
            resources.append(fd)
        metadata_generations=[]
        for fd in resources[1:]:
            block=_exact(fd,HEADER,0);generation,size,phase=struct.unpack_from('<qii',block)
            if not 1<=generation<=0x7fffffffffffffff or not 1<=size<=metadata or phase!=2 or hashlib.sha256(_exact(fd,size,HEADER)).digest()!=block[16:48]:
                raise ValueError('Unknown damaged protected allocation metadata')
            metadata_generations.append(generation)
        if abs(metadata_generations[0]-metadata_generations[1])!=1:raise ValueError('Unknown nonconsecutive protected allocation metadata')
        data=resources[0]
        geometry=_exact(data,HEADER,0);body=geometry.rstrip(b'\0')
        if _decode(body)!={'Protocol':'ProtectedArchiveGeometryV1','DataIdentity':config['DataIdentity'],'ChunkBytes':chunk,'ChunkCount':count}:
            raise ValueError('Changed enrolled protected archive geometry')
        frames=[]
        for index in range(count):
            banks=[]
            for copy in range(2):
                block=_exact(data,HEADER,HEADER+index*stride+copy*HEADER);length=struct.unpack_from('<i',block)[0]
                if not 1<=length<=HEADER-40 or hashlib.sha256(block[40:40+length]).digest()!=block[8:40]:raise ValueError('Unknown damaged initialized protected archive frame')
                frame=_decode(block[40:40+length]);generation=frame['Generation']
                if type(generation) is not int or not 1<=generation<=0x7fffffffffffffff or generation%2!=copy or frame['State'] not in ('Free','Reserved','Preparing','Published'):
                    raise ValueError('Malformed protected archive frame')
                banks.append(frame)
            if abs(banks[0]['Generation']-banks[1]['Generation'])!=1:raise ValueError('Unknown nonconsecutive protected archive frames')
            frames.append(max(banks,key=lambda value:value['Generation']))
        digest=hashlib.sha256(path.encode()).hexdigest()
        matches=[(index,frame) for index,frame in enumerate(frames) if frame['Path']==digest and frame['State']=='Published']
        if not matches:raise FileNotFoundError('No exact committed logical original')
        if len(matches)!=1:raise ValueError('Conflicting protected archive locators')
        index,head=matches[0];length=head['Length'];parts=head['Count']
        if type(length) is not int or not 1<=length<=maximum or type(parts) is not int or not 1<=parts<=count:raise ValueError('Bounded logical original')
        raw=bytearray();visited=set()
        while index>=0:
            selected_io.check()
            if type(index) is not int or index>=count or index in visited or len(visited)>=parts:raise ValueError('Damaged logical original chain')
            visited.add(index);frame=frames[index]
            if frame['Ticket']!=head['Ticket'] or frame['Publication']!=head['Publication'] or frame['State'] not in ('Preparing','Published'):
                raise ValueError('Substituted protected archive chain ownership')
            size=min(chunk,length-len(raw))
            if size<=0:raise ValueError('Protected archive chain exceeds declared body')
            raw.extend(_exact(data,size,HEADER+index*stride+2*HEADER));index=frame['Next']
            if type(index) is not int or index < -1:raise ValueError('Malformed protected chain locator')
        if len(raw)!=length or len(visited)!=parts or hashlib.sha256(raw).hexdigest()!=head['Sha256']:raise ValueError('Damaged protected original digest')
        for name,fd,before,is_directory in held:
            selected_io.check();after=os.fstat(fd)
            if (before.st_dev,before.st_ino,before.st_size,before.st_mtime_ns,before.st_ctime_ns)!=(after.st_dev,after.st_ino,after.st_size,after.st_mtime_ns,after.st_ctime_ns):
                raise ValueError('Protected backend changed during read')
            check=selected_io.acquire(name,directory=is_directory)
            try:
                if _identity(os.fstat(check))!=_identity(before):raise ValueError('Protected backend locator changed')
            finally:os.close(check)
        selected_io.check();return bytes(raw)
