"""Disposable fixed archives and actual retained-owner admission; no installed cohort."""
from contextlib import closing
import hashlib
import json
import os
from pathlib import Path
import shutil
import sqlite3
import struct
import tempfile
import unittest
import uuid

from joint_authority_service import RetainedPhaseOwner
from joint_live_phase_runner import Blocked, PHASES, canonical, digest, partitions
import phase_completion_pool as framing
import protected_logical_archive as archive
from sqlite_storage_fixture import enrolled_storage
import test_joint_live_phase_runner as runner_fixture


class ImpossiblePhaseHeadersTests(unittest.TestCase):
    def test_checksum_valid_impossible_banks_cannot_recycle_an_admitted_slot_or_dispatch_again(self):
        for fault in ('wrong-bank','nonconsecutive'):
            for state in ('Free','Reserved','Completed'):
                with self.subTest(fault=fault,state=state):
                    f=runner_fixture.JointRunnerTests();f.setUp()
                    try:
                        attempt=f.authority.reserve(f.binding,str(uuid.uuid4()))['attemptId'];key=digest(f.binding)
                        request={'protocol':'OQ-5.4-1.joint.v1','attemptId':attempt,'bindingDigest':key,'phase':0,'owner':PHASES[0],'previousSha256':key,'cases':partitions()[0],'requestId':digest([attempt,key,0,key])};f.authority.retain(request)
                        class Authority:
                            def call(self,method,value):return f.store.dispatch(method,value)
                        class Executor:
                            effects=0;lookups=0
                            def execute(self,r):self.effects+=1;return None
                            def lookup(self,r):self.lookups+=1;return None
                        executor=Executor();pool=f.completion_capacity
                        def owner(path=f.path,storage=f.storage):
                            return RetainedPhaseOwner(path,0,f.binding,executor,lambda p:{'payload':p,'signature':'fixture'},Authority(),storage=storage,completion_capacity=pool)
                        owner().dispatch('execute',request);self.assertEqual(1,executor.effects)
                        with owner()._connect() as database:
                            fd=pool._open(database)
                            try:
                                slot,header=next(row for row in pool._scan(fd,database) if row[1] and row[1].get('requestId')==request['requestId'])
                                # Keep the real admitted header intact. Introduce an impossible
                                # higher checksum-valid alternate, which used to win max(generation).
                                generation=header['generation']+(2 if fault=='wrong-bank' else 3)
                                forged=dict(header,generation=generation,state=state)
                                if state=='Free':forged={'generation':generation,'state':'Free'}
                                forged['checksum']=hashlib.sha256(canonical(forged)).hexdigest()
                                offset=framing.HEADER+slot*framing.STRIDE+((header['generation']+1)%2)*framing.HEADER
                                os.pwrite(fd,canonical(forged).ljust(framing.HEADER,b'\0'),offset);os.fsync(fd)
                            finally:os.close(fd)
                        path=Path(pool.configuration['directory'])/'phase-completion-capacity.bin';before=path.read_bytes()
                        with closing(sqlite3.connect(f.path)) as db:originals=db.execute('SELECT * FROM phase_originals').fetchall()
                        for method in ('lookup','execute'):
                            with self.assertRaises(Blocked) as refusal:owner().dispatch(method,request)
                            self.assertIn('PreserveUnknownDamagedCompletionMetadata',refusal.exception.missing)
                            self.assertEqual(before,path.read_bytes())
                        other=Path(f.path).with_name('other-owner.sqlite');shutil.copyfile(f.path,other)
                        with closing(sqlite3.connect(other)) as db,db:
                            db.execute('DELETE FROM phase_originals');db.execute("UPDATE database_storage_enrollment SET identity='other-database'")
                        other_storage=enrolled_storage(other)
                        with self.assertRaises(Blocked):owner(other,other_storage).dispatch('execute',request)
                        with closing(sqlite3.connect(other)) as db:self.assertEqual(0,db.execute('SELECT count(*) FROM phase_originals').fetchone()[0])
                        with closing(sqlite3.connect(f.path)) as db:self.assertEqual(originals,db.execute('SELECT * FROM phase_originals').fetchall())
                        self.assertEqual(before,path.read_bytes());self.assertEqual(1,executor.effects);self.assertEqual(0,executor.lookups)
                    finally:f.tearDown()


class ProtectedGeometryTests(unittest.TestCase):
    def test_actual_logical_reader_agrees_with_csharp_dividing_chunk_boundaries(self):
        for chunk in (4096,8192,16384,32768,65536,12288,20480,24576,49152,69632,4097):
            with self.subTest(chunk=chunk),tempfile.TemporaryDirectory() as directory:
                root=Path(directory);path=root/'logical-original';body=b'fixture exact original'
                config=self.enroll(root,path,body,chunk)
                if chunk in (4096,8192,16384,32768,65536):
                    self.assertEqual(body,archive.read(path,1024,[config]))
                else:
                    with self.assertRaisesRegex(ValueError,'Exact protected allocation geometry'):archive.read(path,1024,[config])

    @staticmethod
    def enroll(root,path,body,chunk):
        count=1;metadata=65536;header=archive.HEADER;stride=2*header+chunk
        data=root/'allocation-data.bin';data.write_bytes(bytes(header+stride))
        meta=[]
        for copy in range(2):
            file=root/f'allocation-metadata-{copy}.bin';raw=bytearray(metadata+header)
            struct.pack_into('<qii',raw,0,copy+1,2,2);raw[16:48]=hashlib.sha256(b'{}').digest();raw[header:header+2]=b'{}';file.write_bytes(raw);meta.append(file)
        config={'StorageProtocol':'Ext4PrivateWrittenNoSnapshotsV1','Directory':str(root),'ChunkBytes':chunk,'ChunkCount':count,'MaximumMetadataBytes':metadata,
                'ArchiveRoots':{str(root):archive._identity(root.stat())},'DataIdentity':archive._identity(data.stat()),
                'Metadata0Identity':archive._identity(meta[0].stat()),'Metadata1Identity':archive._identity(meta[1].stat()),'EnrollmentSha256':''}
        raw=canonical(config);(root/'allocation-enrollment.json').write_bytes(raw);config['EnrollmentSha256']=hashlib.sha256(raw).hexdigest()
        (root/'.archive-allocation.json').write_bytes(canonical(config))
        with data.open('r+b') as output:
            geometry=canonical({'Protocol':'ProtectedArchiveGeometryV1','DataIdentity':config['DataIdentity'],'ChunkBytes':chunk,'ChunkCount':count})
            output.write(geometry.ljust(header,b'\0'))
            for copy in range(2):
                frame={'Generation':4 if copy==0 else 3,'State':'Published' if copy==0 else 'Preparing','Path':hashlib.sha256(str(path).encode()).hexdigest(),
                       'Ticket':'fixture-ticket','Publication':'fixture-publication','Length':len(body),'Count':1,'Next':-1,'Sha256':hashlib.sha256(body).hexdigest()}
                encoded=canonical(frame);block=bytearray(header);struct.pack_into('<i',block,0,len(encoded));block[8:40]=hashlib.sha256(encoded).digest();block[40:40+len(encoded)]=encoded
                output.seek(header+copy*header);output.write(block)
            output.seek(3*header);output.write(body);output.flush();os.fsync(output.fileno())
        return config


if __name__=='__main__':unittest.main()
