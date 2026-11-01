"""Bounded binary ciphertext retrieval/readback; no target selection or authority."""
import hashlib
import os
from pathlib import Path
import selectors
import signal
import stat
import re
import subprocess
import time
MAX_CIPHERTEXT=513*1024*1024
MAX_PLAINTEXT=512*1024*1024

def require(ok,why):
    if not ok:raise ValueError(why)

def reap(child):
    if child.poll() is None:
        try:os.killpg(child.pid,signal.SIGKILL)
        except ProcessLookupError:pass
    child.wait(timeout=5)
    for stream in (child.stdin,child.stdout,child.stderr):
        if stream is not None and not stream.closed:stream.close()

def read_stream(argv,expected_size,expected_digest,*,output=None,timeout=180,maximum=MAX_CIPHERTEXT):
    require(type(expected_size) is int and 0<expected_size<=maximum<=MAX_CIPHERTEXT
            and isinstance(expected_digest,str) and re.fullmatch('[a-f0-9]{64}',expected_digest) and 0<timeout<=300,
            'binary-stream-binding-refused')
    fd=None;child=None;count=0;digest=hashlib.sha256();errors=bytearray();deadline=time.monotonic()+timeout
    if output is not None:
        output=Path(output);parent=output.parent.lstat()
        require(stat.S_ISDIR(parent.st_mode) and parent.st_uid==os.getuid()
                and stat.S_IMODE(parent.st_mode)==0o700,'binary-output-custody-refused')
        fd=os.open(output,os.O_WRONLY|os.O_CREAT|os.O_EXCL|os.O_NOFOLLOW,0o600)
    primary=None
    try:
        child=subprocess.Popen(argv,stdin=subprocess.DEVNULL,stdout=subprocess.PIPE,stderr=subprocess.PIPE,
                               start_new_session=True,env={'LANG':'C.UTF-8'})
        with selectors.DefaultSelector() as selected:
            for stream,label in ((child.stdout,'data'),(child.stderr,'err')):
                os.set_blocking(stream.fileno(),False);selected.register(stream,selectors.EVENT_READ,label)
            while selected.get_map():
                require(time.monotonic()<deadline,'binary-stream-deadline')
                for key,_ in selected.select(.1):
                    raw=os.read(key.fd,1048576)
                    if not raw:selected.unregister(key.fileobj);continue
                    if key.data=='err':errors.extend(raw);require(len(errors)<=65536,'binary-stream-diagnostic-bound');continue
                    count+=len(raw);require(count<=expected_size,'binary-stream-size-bound');digest.update(raw)
                    if fd is not None:
                        view=memoryview(raw)
                        while view:
                            written=os.write(fd,view);require(written>0,'binary-stream-write-refused');view=view[written:]
        child.wait(timeout=max(.001,deadline-time.monotonic()))
        require(child.returncode==0 and count==expected_size and digest.hexdigest()==expected_digest,
                'binary-stream-exit-length-or-digest-refused')
        if fd is not None:os.fsync(fd)
        return {'bytes':count,'sha256':digest.hexdigest(),'stderrSha256':hashlib.sha256(errors).hexdigest(),
                'streamComplete':True,'outputFileWritten':fd is not None}
    except BaseException as exc:
        primary=exc
        raise
    finally:
        failures=[]
        if child is not None:
            try:reap(child)
            except BaseException as exc:failures.append(exc)
        if fd is not None:
            try:os.close(fd)
            except BaseException as exc:failures.append(exc)
        if failures:raise BaseExceptionGroup('binary-stream-primary-and-cleanup',
                                             ([primary] if primary else [])+failures)

def retrieval_program(name,size):
    require(name in ('snapshot.db.age','node-recovery.tar.gz.age','result.json')
            and type(size) is int and 0<size<=MAX_CIPHERTEXT,'binary-retrieval-path-or-bound-refused')
    return ("import os,stat,sys\nfd=os.open("+repr('/work/'+name)+",os.O_RDONLY|os.O_NOFOLLOW)\n"
            "try:\n before=os.fstat(fd)\n"
            " if not stat.S_ISREG(before.st_mode) or before.st_uid!=0 or before.st_nlink!=1 or before.st_size!="+str(size)+":raise ValueError('retrieval-source-profile')\n"
            " count=0\n while True:\n  data=os.read(fd,1048576)\n  if not data:break\n  count+=len(data)\n"
            "  if count>"+str(size)+":raise ValueError('retrieval-source-size')\n"
            "  sys.stdout.buffer.write(data)\n"
            " after=os.fstat(fd)\n"
            " if count!="+str(size)+" or any(getattr(before,k)!=getattr(after,k) for k in ['st_dev','st_ino','st_mode','st_uid','st_gid','st_size','st_mtime_ns','st_ctime_ns']):raise ValueError('retrieval-source-drift')\n"
            "finally:os.close(fd)\n")
