"""Disposable test-only independent storage/pool provisioning; never installation evidence."""
import hashlib
import os
from pathlib import Path
import sqlite3
import uuid
from contextlib import closing
from joint_live_phase_runner import canonical
from enrolled_sqlite import EnrolledSqliteStorage
from phase_completion_pool import EnrolledPhaseCompletionPool, HEADER, STRIDE


def enrolled_storage(path):
    path=str(path);identity='fixture-storage-'+str(uuid.uuid4())
    with closing(sqlite3.connect(path)) as database,database:
        schema=(Path(__file__).with_name('joint-authority-schema.sql')).read_text()
        database.execute(next(line for line in schema.splitlines() if line.startswith('CREATE TABLE IF NOT EXISTS database_storage_enrollment')))
        existing=database.execute('SELECT identity FROM database_storage_enrollment WHERE slot=1').fetchone()
        if existing:identity=existing[0]
        else:database.execute('INSERT INTO database_storage_enrollment VALUES(1,?)',(identity,))
    current='/';components=[]
    for part in ['/']+path.split('/')[1:]:
        current='/' if part=='/' else os.path.join(current,part)
        info=os.stat(current,follow_symlinks=False);components.append({'path':current,'device':info.st_dev,'inode':info.st_ino})
    return EnrolledSqliteStorage(path,{'protocol':'LinuxEnrolledSqliteDeleteJournalV1','writerProtocol':'DirectoryFlockAndImmutableNamespaceV1','placementEvidenceSha256':'f'*64,'identity':identity,'path':path,'components':components})


def completion_pool(path,slots=4):
    root=os.path.dirname(str(path));file=os.path.join(root,'phase-completion-capacity.bin');identity='fixture-pool-'+str(uuid.uuid4())
    genesis=canonical({'protocol':'PreallocatedSharedPhaseCompletionSlotsV1','identity':identity,'slots':slots})
    fd=os.open(file,os.O_CREAT|os.O_EXCL|os.O_RDWR,0o600)
    try:
        os.posix_fallocate(fd,0,HEADER+slots*STRIDE);os.pwrite(fd,genesis,0);os.fsync(fd);info=os.fstat(fd)
    finally:os.close(fd)
    return EnrolledPhaseCompletionPool({'protocol':'PreallocatedSharedPhaseCompletionSlotsV1','directory':root,'identity':identity,'slots':slots,'device':info.st_dev,'inode':info.st_ino,'enrollmentSha256':hashlib.sha256(genesis).hexdigest()})
