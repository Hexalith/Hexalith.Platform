"""Existing preallocated shared result slots: completion requires no new SQLite pages or journal.

Provisioning, independent enrollment and persistent allocation are external. Every
source reader/writer holds the same enrolled database directory fence. New results
are authoritative here; legacy SQLite result_json bytes retain their exact identity.
"""
import hashlib
import json
import os
import stat
import struct
from joint_live_phase_runner import canonical, digest, Blocked
import bounded_file_io as selected_io
from linux_persistent_storage import require_private_allocated

HEADER=4096
MAXIMUM=1024*1024
STRIDE=((2*HEADER+2*(MAXIMUM+40)+HEADER-1)//HEADER)*HEADER


class EnrolledPhaseCompletionPool:
    def __init__(self, configuration): self.configuration=configuration

    def _open(self, database):
        config=self.configuration
        if (not isinstance(config,dict) or config.get('protocol')!='PreallocatedSharedPhaseCompletionSlotsV1'
                or type(config.get('slots')) is not int or not 1<=config['slots']<=1024
                or config.get('directory')!=os.path.dirname(database.storage.path)
                or not isinstance(config.get('identity'),str) or not config['identity']
                or not isinstance(config.get('enrollmentSha256'),str) or len(config['enrollmentSha256'])!=64):
            raise Blocked('External.EnrolledPreallocatedSharedPhaseCompletionSlotsV1')
        database.verify()
        path=os.path.join(config['directory'],'phase-completion-capacity.bin')
        fd=os.open('phase-completion-capacity.bin',os.O_RDWR|os.O_NOFOLLOW|os.O_NONBLOCK|os.O_CLOEXEC,dir_fd=database.parent)
        try:
            info=os.fstat(fd)
            if (not stat.S_ISREG(info.st_mode) or info.st_nlink!=1 or info.st_dev!=os.fstat(database.file).st_dev or info.st_dev!=config.get('device') or info.st_ino!=config.get('inode')
                    or info.st_size!=HEADER+config['slots']*STRIDE or info.st_blocks*512<info.st_size):
                raise Blocked('ActualEnrolledPhysicallyAllocatedPhaseCompletionCapacity')
            require_private_allocated(fd,info.st_size)
            genesis=os.pread(fd,HEADER,0).rstrip(b'\0')
            if hashlib.sha256(genesis).hexdigest()!=config['enrollmentSha256'] or json.loads(genesis)!={'protocol':config['protocol'],'identity':config['identity'],'slots':config['slots']}:
                raise Blocked('ExactExistingPhaseCompletionCapacityGenesis')
            return fd
        except BaseException:os.close(fd);raise

    def preflight(self,database):
        fd=self._open(database)
        try:self._scan(fd,database)
        finally:os.close(fd)

    def _header(self,fd,slot):
        values=[];damaged=False;empty=0
        for copy in range(2):
            raw=os.pread(fd,HEADER,HEADER+slot*STRIDE+copy*HEADER).rstrip(b'\0')
            if not raw:empty+=1;continue
            try:
                value=json.loads(raw); checksum=value.pop('checksum')
                if hashlib.sha256(canonical(value)).hexdigest()!=checksum:damaged=True;continue
                if type(value.get('generation')) is not int or value['generation']<1 or value['generation']%2!=copy or value.get('state') not in ('Free','Reserved','Completed'):
                    damaged=True;continue
                values.append(value)
            except (ValueError,KeyError,TypeError,AttributeError):damaged=True
        # A valid alternate may predate an admitted liability owned by any DB.
        # Neither an old Free header nor absent SQL in this DB proves it reusable.
        if damaged:raise Blocked('PreserveUnknownDamagedCompletionMetadata')
        if empty and values:
            # The sole source-provable one-copy interruption is the very first
            # Free write. Later generations imply the alternate was initialized;
            # its disappearance cannot erase a Reserved/Completed liability.
            if len(values)!=1 or values[0]!={'generation':1,'state':'Free'}:
                raise Blocked('PreserveUnknownDamagedCompletionMetadata')
        if len(values)==2 and abs(values[0]['generation']-values[1]['generation'])!=1:
            raise Blocked('PreserveUnknownDamagedCompletionMetadata')
        if not values:
            # A genuinely unused slot has no committed request/result frame.
            # Missing both headers cannot reset an already materialized original.
            offset=HEADER+slot*STRIDE+2*HEADER
            if any(os.pread(fd,40,offset)) or any(os.pread(fd,40,offset+MAXIMUM+40)):
                raise Blocked('PreserveUnknownDamagedCompletionMetadata')
        return max(values,key=lambda value:value['generation']) if values else None

    def _write_header(self,fd,slot,value):
        value=dict(value);value['checksum']=hashlib.sha256(canonical(value)).hexdigest();raw=canonical(value)
        if len(raw)>HEADER:raise Blocked('BoundedPhaseCompletionMetadata')
        offset=HEADER+slot*STRIDE+(value['generation']%2)*HEADER
        self._write(fd,raw.ljust(HEADER,b'\0'),offset);os.fsync(fd)

    @staticmethod
    def _write(fd,data,offset):
        while data:
            count=os.pwrite(fd,data,offset)
            if count<=0:raise OSError('PhaseCompletionWrite')
            data=data[count:];offset+=count

    def _scan(self,fd,database):
        rows=[]
        for slot in range(self.configuration['slots']):
            header=self._header(fd,slot)
            if header is not None and header.get('databaseIdentity')==database.storage.enrollment['identity']:
                original=database.execute('SELECT request_json FROM phase_originals WHERE request_id=?',(header['requestId'],)).fetchone()
                if original is None:
                    # No dispatch can precede this exact fenced SQL admission. Other database identities stay charged.
                    self._write_header(fd,slot,{'generation':header['generation']+1,'state':'Free'})
                    header={'generation':header['generation']+1,'state':'Free'}
                elif digest(json.loads(original[0]))!=header['requestSha256']:
                    raise Blocked('ExactPhaseCompletionOwnerOriginal')
            rows.append((slot,header))
        return rows

    def reserve(self,database,request):
        fd=self._open(database)
        try:
            rows=self._scan(fd,database);identity=database.storage.enrollment['identity'];key=request['requestId']
            prior=[row for row in rows if row[1] and row[1].get('databaseIdentity')==identity and row[1].get('requestId')==key]
            if prior:
                if len(prior)!=1 or prior[0][1]['requestSha256']!=digest(request):raise Blocked('ExactPhaseCompletionReservation')
                require_private_allocated(fd,STRIDE,start=HEADER+prior[0][0]*STRIDE,allow_unwritten=False)
                return
            free=next((row for row in rows if row[1] is None or row[1].get('state')=='Free'),None)
            if free is None:raise Blocked('SharedPhaseCompletionCapacityExhausted')
            slot,old=free;raw=canonical(request)
            if len(raw)>MAXIMUM:raise Blocked('BoundedCompletePhaseRequest')
            # Materialize only a proved free slot before SQL/native admission. Preserve a
            # valid Free header in the alternate copy through every interrupted write.
            generation=(old or {}).get('generation',0)
            for _ in range(2):
                generation+=1;self._write_header(fd,slot,{'generation':generation,'state':'Free'})
            offset=HEADER+slot*STRIDE+2*HEADER;remaining=STRIDE-2*HEADER;zeros=b'\0'*65536
            while remaining:
                selected_io.check();size=min(remaining,len(zeros));self._write(fd,zeros[:size],offset);offset+=size;remaining-=size
            os.fsync(fd)
            require_private_allocated(fd,STRIDE,start=HEADER+slot*STRIDE,allow_unwritten=False)
            old={'generation':generation,'state':'Free'}
            self._frame(fd,slot,False,raw)
            # Clearing the old result frame precedes admission; completed/admitted originals are never recycled.
            self._write(fd,b'\0'*40,HEADER+slot*STRIDE+2*HEADER+MAXIMUM+40);os.fsync(fd)
            self._write_header(fd,slot,{'generation':(old or {}).get('generation',0)+1,'state':'Reserved','databaseIdentity':identity,'requestId':key,'requestSha256':digest(request)})
        finally:os.close(fd)

    def _frame(self,fd,slot,result,raw):
        if len(raw)>MAXIMUM:raise Blocked('BoundedCompleteSignedPhaseResult')
        offset=HEADER+slot*STRIDE+2*HEADER+(MAXIMUM+40 if result else 0)
        self._write(fd,raw,offset+40);os.fsync(fd)
        self._write(fd,struct.pack('>Q',len(raw))+hashlib.sha256(raw).digest(),offset);os.fsync(fd)

    def result(self,database,request,result=None):
        fd=self._open(database)
        try:
            found=[row for row in self._scan(fd,database) if row[1] and row[1].get('databaseIdentity')==database.storage.enrollment['identity'] and row[1].get('requestId')==request['requestId']]
            if len(found)!=1 or found[0][1]['requestSha256']!=digest(request):raise Blocked('ExactRetainedPhaseCompletionCapacity')
            slot,header=found[0]
            require_private_allocated(fd,STRIDE,start=HEADER+slot*STRIDE,allow_unwritten=False)
            offset=HEADER+slot*STRIDE+2*HEADER+MAXIMUM+40
            frame=os.pread(fd,40,offset);length=struct.unpack('>Q',frame[:8])[0]
            retained=None
            if 0<length<=MAXIMUM:
                raw=os.pread(fd,length,offset+40)
                if hashlib.sha256(raw).digest()==frame[8:]:retained=json.loads(raw)
            if header['state']=='Completed' and retained is None:raise Blocked('PreserveUnknownDamagedCompletionOriginal')
            if retained is not None:
                if result is not None and canonical(result)!=canonical(retained):raise Blocked('ImmutableExactPhaseCompletionResult')
                return retained
            if result is None:return None
            self._frame(fd,slot,True,canonical(result))
            self._write_header(fd,slot,dict(header,generation=header['generation']+1,state='Completed'))
            database.verify();return result
        finally:os.close(fd)
