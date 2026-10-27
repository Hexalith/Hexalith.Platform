"""Actual source-only transaction and retained-owner damage boundaries."""
import copy
import os
from pathlib import Path
import sqlite3
import shutil
import stat
import tempfile
import unittest
import uuid
from contextlib import closing
from unittest.mock import patch
from joint_authority_service import existing_database, RetainedPhaseOwner
from joint_live_phase_runner import Blocked, digest, partitions, PHASES
from sqlite_storage_fixture import enrolled_storage
import test_joint_live_phase_runner as runner_fixture
import phase_completion_pool as framing


class RollbackDeleteBarrierTests(unittest.TestCase):
    def test_actual_vfs_full_commit_syncs_held_parent_after_journal_unlink_and_reopens(self):
        with tempfile.TemporaryDirectory() as root:
            path=Path(root)/'db.sqlite'
            with closing(sqlite3.connect(path)) as db,db:db.execute('CREATE TABLE facts(value)')
            storage=enrolled_storage(path);events=[];sync=os.fsync;unlink=os.unlink
            def deleted(name,*args,**kwargs):
                result=unlink(name,*args,**kwargs);events.append(('unlink',str(name)));return result
            def synced(fd):
                result=sync(fd);events.append(('parent' if stat.S_ISDIR(os.fstat(fd).st_mode) else 'file',fd));return result
            with closing(existing_database(path,storage=storage)) as db:
                self.assertEqual((2,),db.execute('PRAGMA synchronous').fetchone())
                with patch.object(os,'unlink',side_effect=deleted),patch.object(os,'fsync',side_effect=synced):
                    db.execute('BEGIN IMMEDIATE');db.execute('INSERT INTO facts VALUES(1)');db.commit()
            removed=next(i for i,event in enumerate(events) if event[0]=='unlink')
            self.assertTrue(any(event[0]=='file' for event in events[:removed]));self.assertEqual('parent',events[removed+1][0])
            with closing(existing_database(path,storage=storage)) as db:self.assertEqual([(1,)],db.execute('SELECT value FROM facts').fetchall())

    def test_failed_post_delete_sync_cannot_acknowledge_admission_or_allow_native_dispatch(self):
        with tempfile.TemporaryDirectory() as root:
            path=Path(root)/'db.sqlite'
            with closing(sqlite3.connect(path)) as db,db:db.execute('CREATE TABLE facts(value)')
            storage=enrolled_storage(path);deleted=False;native=[];sync=os.fsync;unlink=os.unlink
            def remove(name,*args,**kwargs):
                nonlocal deleted
                result=unlink(name,*args,**kwargs);deleted=True;return result
            def fail(fd):
                if deleted and stat.S_ISDIR(os.fstat(fd).st_mode):raise OSError('controlled barrier failure')
                return sync(fd)
            with closing(existing_database(path,storage=storage)) as db:
                with patch.object(os,'unlink',side_effect=remove),patch.object(os,'fsync',side_effect=fail):
                    with self.assertRaises((sqlite3.Error,ValueError)):
                        db.execute('INSERT INTO facts VALUES(1)');db.commit();native.append('forbidden')
            self.assertTrue(deleted);self.assertEqual([],native)
            # The failed acknowledgement is uncertain. Reopen reads the actual durable original;
            # it neither deletes nor rewrites that fact to claim a rollback.
            with closing(existing_database(path,storage=storage)) as db:self.assertEqual([(1,)],db.execute('SELECT value FROM facts').fetchall())


class DamagedPhaseHeaderLiabilityTests(unittest.TestCase):
    def test_damaged_reserved_or_completed_header_never_reuses_older_free_or_other_owner_copy(self):
        for state in ('Reserved','Completed','Unknown'):
            for damage in ('nonempty','zero','both-zero'):
                for alternate in ('Free','Reserved'):
                    with self.subTest(state=state,alternate=alternate,damage=damage):
                        f=runner_fixture.JointRunnerTests();f.setUp()
                        try:
                            attempt=f.authority.reserve(f.binding,str(uuid.uuid4()))['attemptId'];key=digest(f.binding)
                            request={'protocol':'OQ-5.4-1.joint.v1','attemptId':attempt,'bindingDigest':key,'phase':0,'owner':PHASES[0],'previousSha256':key,'cases':partitions()[0],'requestId':digest([attempt,key,0,key])};f.authority.retain(request)
                            class Authority:
                                def call(_,method,value):return f.store.dispatch(method,value)
                            class Executor:
                                calls=0;lookups=0
                                def execute(self,r):self.calls+=1;return None
                                def lookup(self,r):self.lookups+=1;return None
                            executor=Executor();pool=f.completion_capacity
                            def owner():return RetainedPhaseOwner(f.path,0,f.binding,executor,lambda p:{'payload':p,'signature':'fixture'},Authority(),storage=f.storage,completion_capacity=pool)
                            owner().dispatch('execute',request);self.assertEqual(1,executor.calls)
                            with owner()._connect() as database:
                                fd=pool._open(database)
                                try:
                                    slot,header=next(row for row in pool._scan(fd,database) if row[1] and row[1].get('requestId')==request['requestId'])
                                    generation=header['generation']
                                    older={'generation':generation-1,'state':alternate}
                                    if alternate=='Reserved':older.update(databaseIdentity='another-database-identity',requestId='another-original',requestSha256='f'*64)
                                    pool._write_header(fd,slot,older)
                                    newest=dict(header,state=state);pool._write_header(fd,slot,newest)
                                    offset=framing.HEADER+slot*framing.STRIDE+(generation%2)*framing.HEADER
                                    if damage=='nonempty':
                                        value=os.pread(fd,1,offset);os.pwrite(fd,bytes([value[0]^1]),offset)
                                    else:
                                        os.pwrite(fd,bytes(framing.HEADER),offset)
                                        if damage=='both-zero':os.pwrite(fd,bytes(framing.HEADER),framing.HEADER+slot*framing.STRIDE+((generation+1)%2)*framing.HEADER)
                                    os.fsync(fd)
                                finally:os.close(fd)
                            path=Path(pool.configuration['directory'])/'phase-completion-capacity.bin';damaged=path.read_bytes()
                            for method,selected in (('lookup',request),('execute',request)):
                                with self.assertRaises(Blocked) as refusal:owner().dispatch(method,selected)
                                self.assertIn('PreserveUnknownDamagedCompletionMetadata',refusal.exception.missing)
                                self.assertEqual(damaged,path.read_bytes())
                            second=Path(f.path).with_name('another-owner.sqlite');shutil.copyfile(f.path,second)
                            with closing(sqlite3.connect(second)) as db,db:
                                db.execute('DELETE FROM phase_originals');db.execute("UPDATE database_storage_enrollment SET identity='fixture-second-owner'")
                            other_storage=enrolled_storage(second)
                            other=RetainedPhaseOwner(second,0,f.binding,executor,lambda p:{'payload':p,'signature':'fixture'},Authority(),storage=other_storage,completion_capacity=pool)
                            with self.assertRaises(Blocked) as refusal:other.dispatch('execute',request)
                            self.assertIn('PreserveUnknownDamagedCompletionMetadata',refusal.exception.missing);self.assertEqual(damaged,path.read_bytes())
                            with closing(sqlite3.connect(second)) as db:self.assertEqual(0,db.execute('SELECT count(*) FROM phase_originals').fetchone()[0])
                            self.assertEqual(1,executor.calls);self.assertEqual(0,executor.lookups)
                            with closing(sqlite3.connect(f.path)) as db:self.assertEqual([(request['requestId'],)],db.execute('SELECT request_id FROM phase_originals').fetchall())
                        finally:f.tearDown()

    def test_genuine_first_free_header_interruption_remains_safe_before_sql_admission(self):
        f=runner_fixture.JointRunnerTests();f.setUp()
        try:
            attempt=f.authority.reserve(f.binding,str(uuid.uuid4()))['attemptId'];key=digest(f.binding)
            request={'protocol':'OQ-5.4-1.joint.v1','attemptId':attempt,'bindingDigest':key,'phase':0,'owner':PHASES[0],'previousSha256':key,'cases':partitions()[0],'requestId':digest([attempt,key,0,key])};f.authority.retain(request)
            class Authority:
                def call(_,method,value):return f.store.dispatch(method,value)
            class Executor:
                calls=0;lookups=0
                def execute(self,r):self.calls+=1;return None
                def lookup(self,r):self.lookups+=1;return None
            executor=Executor();pool=f.completion_capacity
            def owner():return RetainedPhaseOwner(f.path,0,f.binding,executor,lambda p:{'payload':p,'signature':'fixture'},Authority(),storage=f.storage,completion_capacity=pool)
            write=pool._write_header
            def interrupted(fd,slot,value):
                write(fd,slot,value)
                if value=={'generation':1,'state':'Free'}:raise OSError('controlled pre-SQL first-header interruption')
            with patch.object(pool,'_write_header',side_effect=interrupted):
                with self.assertRaises(OSError):owner().dispatch('execute',request)
            self.assertEqual(0,executor.calls)
            with closing(sqlite3.connect(f.path)) as db:self.assertEqual(0,db.execute('SELECT count(*) FROM phase_originals').fetchone()[0])
            owner().dispatch('execute',request);self.assertEqual(1,executor.calls)
            path=Path(pool.configuration['directory'])/'phase-completion-capacity.bin';before=path.read_bytes();owner().dispatch('lookup',request)
            self.assertEqual(1,executor.calls);self.assertEqual(1,executor.lookups);self.assertEqual(before,path.read_bytes())
        finally:f.tearDown()
