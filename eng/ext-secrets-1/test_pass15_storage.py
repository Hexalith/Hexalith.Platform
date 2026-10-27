import copy
import os
from pathlib import Path
import sqlite3
import tempfile
import unittest
import uuid
from unittest.mock import patch
from contextlib import closing
from enrolled_sqlite import EnrolledSqliteStorage
from joint_authority_service import existing_database, RetainedPhaseOwner
from joint_live_phase_runner import Blocked, canonical, digest, partitions, PHASES
from sqlite_storage_fixture import enrolled_storage, completion_pool
from test_joint_live_phase_runner import Owner
import test_joint_live_phase_runner as runner_fixture


class EnrolledStorageTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory();self.addCleanup(self.temp.cleanup);self.root=Path(self.temp.name);self.path=self.root/'db.sqlite'
        with closing(sqlite3.connect(self.path)) as db,db:db.execute('CREATE TABLE facts(value)');db.execute('INSERT INTO facts VALUES(1)')
        self.storage=enrolled_storage(self.path)

    def test_exact_independent_identity_is_required_and_normal_restart_preserves_it(self):
        with self.assertRaises(ValueError):existing_database(self.path)
        for _ in range(3):
            with closing(existing_database(self.path,storage=self.storage)) as db:self.assertEqual((1,),db.execute('SELECT value FROM facts').fetchone())
        wrong=copy.deepcopy(self.storage.enrollment);wrong['identity']='another-storage'
        with self.assertRaises(ValueError):existing_database(self.path,storage=EnrolledSqliteStorage(self.path,wrong))

    def test_volatile_tmpfs_identity_cannot_substitute_for_actual_persistent_database_placement(self):
        with tempfile.TemporaryDirectory(prefix='story54-volatile-fixture-',dir='/dev/shm') as root:
            path=Path(root)/'db.sqlite';storage=enrolled_storage(path)
            with self.assertRaisesRegex(ValueError,'PersistentAtomicLocalSqliteStorageProtocol'):existing_database(path,storage=storage)

    def test_the_opened_sqlite_descriptor_must_match_even_when_a_substitution_restores_the_locator(self):
        import shutil
        other=self.root/'copied.sqlite';shutil.copyfile(self.path,other)
        with closing(sqlite3.connect(other)) as db,db:db.execute('UPDATE facts SET value=2')
        connect=sqlite3.connect
        def substitute_then_restore(*args,**kwargs):
            self.path.rename(self.root/'held-original.sqlite');other.rename(self.path)
            try:return connect(*args,**kwargs)
            finally:self.path.rename(other);(self.root/'held-original.sqlite').rename(self.path)
        with patch('enrolled_sqlite.sqlite3.connect',side_effect=substitute_then_restore):
            with self.assertRaisesRegex(ValueError,'ExactAcquiredSqliteMainDescriptorIdentity'):existing_database(self.path,storage=self.storage)

    def test_final_parent_and_journal_links_and_wal_are_refused(self):
        original=self.path.with_suffix('.held');self.path.rename(original);self.path.symlink_to(original)
        with self.assertRaises(OSError):existing_database(self.path,storage=self.storage)
        self.path.unlink();original.rename(self.path)
        for suffix in ('-journal','-wal','-shm'):
            side=Path(str(self.path)+suffix);side.symlink_to(self.path)
            with self.assertRaises(ValueError):existing_database(self.path,storage=self.storage)
            side.unlink()
        root=self.root.with_name(self.root.name+'-held');self.root.rename(root);self.root.symlink_to(root,target_is_directory=True)
        try:
            with self.assertRaises(OSError):existing_database(self.path,storage=self.storage)
        finally:self.root.unlink();root.rename(self.root)

    def test_replacement_during_open_and_transaction_never_selects_another_history(self):
        other=self.root/'other.sqlite'
        with closing(sqlite3.connect(other)) as db,db:db.execute('CREATE TABLE facts(value)');db.execute('INSERT INTO facts VALUES(2)')
        connect=sqlite3.connect
        def replace(*args,**kwargs):
            result=connect(*args,**kwargs);self.path.rename(self.root/'surviving.sqlite');other.rename(self.path);return result
        with patch('enrolled_sqlite.sqlite3.connect',side_effect=replace):
            with self.assertRaises(ValueError):existing_database(self.path,storage=self.storage)
        self.path.rename(other);(self.root/'surviving.sqlite').rename(self.path)
        with closing(existing_database(self.path,storage=self.storage)) as db:
            db.execute('BEGIN IMMEDIATE');db.execute('UPDATE facts SET value=3');self.path.rename(self.root/'surviving.sqlite');other.rename(self.path)
            with self.assertRaises(ValueError):db.commit()
            db.rollback()
        with closing(sqlite3.connect(self.root/'surviving.sqlite')) as db,db:self.assertEqual((1,),db.execute('SELECT value FROM facts').fetchone())


class PhaseCapacityAndAttemptTests(unittest.TestCase):
    def setUp(self):self.fixture=runner_fixture.JointRunnerTests();self.fixture.setUp();self.addCleanup(self.fixture.tearDown)
    def request(self):
        f=self.fixture;attempt=f.authority.reserve(f.binding,str(uuid.uuid4()))['attemptId'];key=digest(f.binding)
        r={'protocol':'OQ-5.4-1.joint.v1','attemptId':attempt,'bindingDigest':key,'phase':0,'owner':PHASES[0],'previousSha256':key,'cases':partitions()[0],'requestId':digest([attempt,key,0,key])}
        f.authority.retain(r);return r
    def test_new_attempt_identifiers_bound_transport_amplification_and_old_rows_stay_readable(self):
        f=self.fixture;readiness=[Owner().preflight(f.binding,p,partitions()[p])['readiness'] for p in range(4)]
        for value in (None,123,{},[],True,'x'*990000,str(uuid.uuid4()).upper(),'{' + str(uuid.uuid4()) + '}'):
            with self.subTest(value=str(value)[:40]):
                with self.assertRaises((Blocked,ValueError)):f.store.dispatch('reserve',{'binding':f.binding,'attemptId':value,'ownerReadiness':readiness})
                with closing(sqlite3.connect(f.path)) as db,db:self.assertEqual(0,db.execute('SELECT count(*) FROM attempts').fetchone()[0])
        with closing(sqlite3.connect(f.path)) as db,db:db.execute('INSERT INTO attempts VALUES(?,?)',(digest(f.binding),'preserved-legacy-attempt'))
        self.assertEqual('preserved-legacy-attempt',f.store.dispatch('lookup-attempt',{'binding':f.binding})['attemptId'])
        self.assertEqual('preserved-legacy-attempt',f.store.dispatch('reserve',{'binding':f.binding,'attemptId':'invalid-new','ownerReadiness':readiness})['attemptId'])

    def test_preallocated_slot_completion_survives_loss_and_needs_no_fresh_sql_result_pages(self):
        f=self.fixture;request=self.request();pool=f.completion_capacity
        class Authority:
            def call(_,m,v):return f.store.dispatch(m,v)
        class Executor:
            calls=0;lookups=0
            def execute(self,r):self.calls+=1;return ('Passed',r['cases'],{'exact':'native'})
            def lookup(self,r):self.lookups+=1;return None
        executor=Executor()
        def receiver():return RetainedPhaseOwner(f.path,0,f.binding,executor,lambda p:{'payload':p,'signature':'fixture-original-signature'},Authority(),storage=f.storage,completion_capacity=pool)
        write=pool._write_header
        def lose(fd,slot,value):
            if value.get('state')=='Completed':raise OSError('fixture lost completion metadata acknowledgment')
            return write(fd,slot,value)
        with patch.object(pool,'_write_header',side_effect=lose),patch('os.posix_fallocate',side_effect=OSError('ordinary capacity exhausted')):
            with self.assertRaises(OSError):receiver().dispatch('execute',request)
        result=receiver().dispatch('lookup',request)
        self.assertEqual('Passed',result['payload']['state']);self.assertEqual(result,receiver().dispatch('lookup',request));self.assertEqual(1,executor.calls);self.assertEqual(0,executor.lookups)
        with closing(sqlite3.connect(f.path)) as db,db:self.assertIsNone(db.execute('SELECT result_json FROM phase_originals').fetchone()[0])

    def test_damaged_completed_frame_blocks_without_lookup_or_replacement(self):
        import phase_completion_pool as framing
        f=self.fixture;request=self.request();pool=f.completion_capacity
        class Authority:
            def call(_,method,value):return f.store.dispatch(method,value)
        class Executor:
            calls=0;lookups=0
            def execute(self,r):self.calls+=1;return ('Passed',r['cases'],{'exact':'native'})
            def lookup(self,r):self.lookups+=1;return ('Passed',r['cases'],{'substituted':'later-state'})
        executor=Executor()
        def owner():return RetainedPhaseOwner(f.path,0,f.binding,executor,lambda p:{'payload':p,'signature':'fixture'},Authority(),storage=f.storage,completion_capacity=pool)
        self.assertEqual('Passed',owner().dispatch('execute',request)['payload']['state'])
        path=Path(pool.configuration['directory'])/'phase-completion-capacity.bin'
        with owner()._connect() as database:
            fd=pool._open(database)
            try:
                slot,header=next(row for row in pool._scan(fd,database) if row[1] and row[1].get('requestId')==request['requestId'])
                self.assertEqual('Completed',header['state'])
                offset=framing.HEADER+slot*framing.STRIDE+2*framing.HEADER+framing.MAXIMUM+40+40
                body=os.pread(fd,1,offset);os.pwrite(fd,bytes([body[0]^1]),offset);os.fsync(fd)
            finally:os.close(fd)
        damaged=path.read_bytes()
        for method in ('lookup','execute','lookup'):
            with self.assertRaises(Blocked) as refusal:owner().dispatch(method,request)
            self.assertIn('PreserveUnknownDamagedCompletionOriginal',refusal.exception.missing)
            self.assertEqual(damaged,path.read_bytes())
        self.assertEqual(1,executor.calls);self.assertEqual(0,executor.lookups)

    def test_shared_unknown_or_sparse_completion_extents_refuse_before_any_native_dispatch(self):
        import linux_persistent_storage as backing
        f=self.fixture;request=self.request()
        class Authority:
            def call(_,method,value):return f.store.dispatch(method,value)
        class Executor:
            calls=0
            def execute(self,r):self.calls+=1;return None
            def lookup(self,r):return None
        executor=Executor();owner=RetainedPhaseOwner(f.path,0,f.binding,executor,lambda p:{'payload':p},Authority(),storage=f.storage,completion_capacity=f.completion_capacity)
        original=backing.fcntl.ioctl
        def shared(fd,operation,frame,mutate):
            if operation==0xc020660b:
                import struct
                struct.pack_into('=I',frame,20,1);struct.pack_into('=QQQ',frame,32,0,4096,os.fstat(fd).st_size);struct.pack_into('=I',frame,72,0x2001);return 0
            return original(fd,operation,frame,mutate)
        with patch.object(backing.fcntl,'ioctl',side_effect=shared):
            with self.assertRaisesRegex(ValueError,'PrivateAllocatedPhaseCompletionExtents'):owner.dispatch('execute',request)
        self.assertEqual(0,executor.calls)
        with closing(sqlite3.connect(f.path)) as db,db:self.assertEqual(0,db.execute('SELECT count(*) FROM phase_originals').fetchone()[0])
        # An unshared current map does not grant the selected fixed-slot protocol on default datacow Btrfs.
        with patch.object(backing,'require_persistent',return_value=0x9123683e):
            with self.assertRaisesRegex(ValueError,'BtrfsFixedSlotInPlaceWriteProtocolUnavailable'):owner.dispatch('execute',request)
        self.assertEqual(0,executor.calls)


    def test_free_slot_materialization_failure_precedes_admission_and_recovers_without_losing_originals(self):
        f=self.fixture;request=self.request();pool=f.completion_capacity
        class Authority:
            def call(_,method,value):return f.store.dispatch(method,value)
        class Executor:
            calls=0
            def execute(self,r):self.calls+=1;return ('Passed',r['cases'],{'retained':'exact'})
            def lookup(self,r):return None
        executor=Executor();owner=RetainedPhaseOwner(f.path,0,f.binding,executor,lambda p:{'payload':p,'signature':'fixture'},Authority(),storage=f.storage,completion_capacity=pool)
        write=pool._write
        def fail_materialization(fd,data,offset):
            if len(data)==65536:raise OSError('fixture materialization allocation/commit failure before native admission')
            return write(fd,data,offset)
        with patch.object(pool,'_write',side_effect=fail_materialization):
            with self.assertRaises(OSError):owner.dispatch('execute',request)
        self.assertEqual(0,executor.calls)
        with closing(sqlite3.connect(f.path)) as db,db:self.assertEqual(0,db.execute('SELECT count(*) FROM phase_originals').fetchone()[0])
        result=owner.dispatch('execute',request);self.assertEqual('Passed',result['payload']['state']);self.assertEqual(result,owner.dispatch('lookup',request));self.assertEqual(1,executor.calls)

    def test_workload_expiry_winning_during_materialization_denies_at_the_final_sql_admission_boundary(self):
        from test_joint_admission_ordering import JointAdmissionTests
        f=self.fixture;request=self.request();pool=f.completion_capacity;clock=[100]
        class Authority:
            def call(_,method,value):return f.store.dispatch(method,value)
        class Executor:
            calls=0
            def execute(self,r):self.calls+=1;return None
            def lookup(self,r):return None
        executor=Executor();owner=RetainedPhaseOwner(f.path,0,f.binding,executor,lambda p:{'payload':p},Authority(),storage=f.storage,completion_capacity=pool);write=pool._write
        def expire(fd,data,offset):
            if len(data)==65536:clock[0]=101
            return write(fd,data,offset)
        helper=JointAdmissionTests();helper.fixture=f
        def dispatch(method,value):
            with closing(sqlite3.connect(f.path)) as db,db:db.execute('UPDATE workload_grants SET valid_until=101')
            return owner.dispatch(method,value)
        with patch('time.time',side_effect=lambda:clock[0]),patch.object(pool,'_write',side_effect=expire):
            with self.assertRaises(Blocked):helper._app('execute',request,dispatch)
        self.assertEqual(0,executor.calls)
        with closing(sqlite3.connect(f.path)) as db,db:self.assertEqual(0,db.execute('SELECT count(*) FROM phase_originals').fetchone()[0])

    def test_missing_capacity_blocks_before_dispatch_and_unknown_keeps_the_slot(self):
        f=self.fixture;request=self.request()
        class Authority:
            def call(_,m,v):return f.store.dispatch(m,v)
        class Executor:
            calls=0
            def execute(self,r):self.calls+=1;return None
            def lookup(self,r):return None
        executor=Executor();owner=RetainedPhaseOwner(f.path,0,f.binding,executor,lambda p:{'payload':p},Authority(),storage=f.storage)
        with self.assertRaises(Blocked):owner.dispatch('execute',request)
        self.assertEqual(0,executor.calls)
        owner.completion_capacity=f.completion_capacity;self.assertIsNone(owner.dispatch('execute',request));self.assertIsNone(owner.dispatch('lookup',request));self.assertEqual(1,executor.calls)
