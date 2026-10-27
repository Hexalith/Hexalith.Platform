"""Disposable actual private IO and SQLite connection acquisition fixtures."""
import gc
import os
from pathlib import Path
import shutil
import sqlite3
import tempfile
import threading
import time
import unittest
from concurrent.futures import ThreadPoolExecutor
from contextlib import closing
from unittest.mock import patch
from cryptography.hazmat.primitives import serialization
from cryptography.hazmat.primitives.asymmetric import ec
import bounded_file_io as selected_io
from joint_authority_service import existing_database
from sqlite_storage_fixture import enrolled_storage


class PrivateFileOwnershipTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory();self.addCleanup(self.temp.cleanup);self.path=Path(self.temp.name)/'fixture-key.pem'
        self.key=ec.generate_private_key(ec.SECP256R1());self.path.write_bytes(self.key.private_bytes(serialization.Encoding.PEM,serialization.PrivateFormat.PKCS8,serialization.NoEncryption()))

    def test_actual_signer_parses_the_only_writable_buffer_without_immutable_reads_or_private_hashing(self):
        owned=[];readv=os.readv
        def read(fd,regions):
            owned.extend(region.obj for region in regions);return readv(fd,regions)
        def consume(material):
            self.assertIsInstance(material,bytearray);self.assertTrue(any(material));return serialization.load_pem_private_key(material,password=None)
        with patch.object(os,'read',side_effect=AssertionError('private path must use direct writable IO')),patch.object(os,'readv',side_effect=read),patch.object(selected_io.hashlib,'sha256',side_effect=AssertionError('private bytes are never hashed')):
            signer=selected_io.use_private(self.path,8192,consume)
        self.assertEqual(self.key.public_key().public_numbers(),signer.public_key().public_numbers());self.assertTrue(owned)
        for value in owned:self.assertFalse(any(value))

    def test_fault_growth_and_late_cancelled_reads_clear_every_owned_region_and_retain_worker_capacity(self):
        for fault in ('read-error','growth','late'):
            with self.subTest(fault=fault):
                owned=[];entered=threading.Event();release=threading.Event();readv=os.readv;slots=threading.BoundedSemaphore(1);consumed=[]
                def read(fd,regions):
                    owned.extend(region.obj for region in regions)
                    if fault=='late':entered.set();release.wait(2)
                    actual=readv(fd,regions)
                    if fault=='read-error':raise OSError('fixture-private-marker-never-logged')
                    if fault=='growth' and actual:
                        # Fill the owned growth sentinel; it must clear even though admission fails.
                        regions[0][-1]=99;return len(regions[0])
                    return actual
                def consume(material):consumed.append(material);return serialization.load_pem_private_key(material,password=None)
                with patch.object(selected_io,'_slots',slots),patch.object(os,'readv',side_effect=read):
                    with self.assertRaises((OSError,TimeoutError,ValueError)):
                        selected_io.run(lambda:selected_io.use_private(self.path,8192,consume),timeout=.05)
                    if fault=='late':
                        self.assertTrue(entered.is_set());self.assertFalse(slots.acquire(blocking=False));release.set()
                        until=time.monotonic()+2
                        while not slots.acquire(blocking=False):
                            self.assertLess(time.monotonic(),until);time.sleep(.01)
                        slots.release()
                self.assertFalse(consumed);self.assertTrue(owned)
                for value in owned:self.assertFalse(any(value))


class ActualSqliteConnectionOwnershipTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory();self.addCleanup(self.temp.cleanup);self.root=Path(self.temp.name)
        self.path=self.root/'enrolled.sqlite'
        with closing(sqlite3.connect(self.path)) as db,db:db.execute('CREATE TABLE facts(value)');db.execute('INSERT INTO facts VALUES(1)')
        self.other=self.root/'other.sqlite';shutil.copyfile(self.path,self.other)
        with closing(sqlite3.connect(self.other)) as db,db:db.execute('UPDATE facts SET value=2')
        self.readable=self.root/'readable';self.readable.write_text('fixture');self.writable=self.root/'writable';self.writable.write_text('fixture')
        self.storage=enrolled_storage(self.path)

    def test_actual_connection_ignores_unrelated_legitimate_io_and_enrolled_inode_decoy_opens(self):
        connect=sqlite3.connect;held=[]
        def unrelated(*args,**kwargs):
            held.extend([os.open(self.readable,os.O_RDONLY),os.open(self.writable,os.O_RDWR),os.open(self.path,os.O_RDWR)])
            return connect(*args,**kwargs)
        try:
            with patch('enrolled_sqlite.sqlite3.connect',side_effect=unrelated):
                with closing(existing_database(self.path,storage=self.storage)) as db:
                    self.assertEqual((1,),db.execute('SELECT value FROM facts').fetchone());db.execute('UPDATE facts SET value=3');db.commit()
        finally:
            for fd in held:os.close(fd)
        with closing(existing_database(self.path,storage=self.storage)) as db:self.assertEqual((3,),db.execute('SELECT value FROM facts').fetchone())

    def test_decoy_open_cannot_prove_a_connection_that_bypasses_its_exact_source_owned_vfs(self):
        connect=sqlite3.connect;held=[]
        def foreign(*args,**kwargs):
            held.append(os.open(self.path,os.O_RDWR));return connect(self.other)
        try:
            with patch('enrolled_sqlite.sqlite3.connect',side_effect=foreign):
                with self.assertRaisesRegex(ValueError,'ExactAcquiredSqliteMainDescriptorIdentity'):existing_database(self.path,storage=self.storage)
        finally:
            for fd in held:os.close(fd)
        with closing(existing_database(self.path,storage=self.storage)) as db:self.assertEqual((1,),db.execute('SELECT value FROM facts').fetchone())

    def test_native_main_open_cannot_substitute_a_foreign_descriptor_with_an_enrolled_inode_decoy(self):
        # Preserve even the independently enrolled storage row in a foreign inode.
        shutil.copyfile(self.path,self.other)
        with closing(sqlite3.connect(self.other)) as db,db:db.execute('UPDATE facts SET value=2')
        real_open=os.open;decoys=[]
        def substitute(path,*args,**kwargs):
            if str(path).startswith('/proc/self/fd/'):
                decoys.append(real_open(self.path,os.O_RDWR));return real_open(self.other,os.O_RDWR)
            return real_open(path,*args,**kwargs)
        try:
            with patch.object(os,'open',side_effect=substitute):
                with self.assertRaisesRegex(ValueError,'ExactAcquiredSqliteMainDescriptorIdentity'):existing_database(self.path,storage=self.storage)
        finally:
            for fd in decoys:os.close(fd)
        with closing(existing_database(self.path,storage=self.storage)) as db:self.assertEqual((1,),db.execute('SELECT value FROM facts').fetchone())

    def test_actual_deferred_cursor_cleanup_and_missing_public_runtime_exports_leave_no_connection_owner(self):
        import enrolled_sqlite_vfs as native
        before=set(native._leases)
        for _ in range(3):
            db=existing_database(self.path,storage=self.storage);cursor=db.execute('SELECT value FROM facts UNION ALL SELECT 3');self.assertEqual((1,),cursor.fetchone())
            db.close()
            with self.assertRaises(sqlite3.ProgrammingError):cursor.close()
            del cursor;gc.collect()
        self.assertEqual(before,set(native._leases))
        with patch.object(native,'_runtime',side_effect=ValueError('External.LoadedSqlitePublicVfsDescriptorAcquisitionV1')):
            with self.assertRaisesRegex(ValueError,'External.LoadedSqlitePublicVfsDescriptorAcquisitionV1'):existing_database(self.path,storage=self.storage)
        with closing(existing_database(self.path,storage=self.storage)) as db:self.assertEqual((1,),db.execute('SELECT value FROM facts').fetchone())

    def test_concurrent_independent_enrolled_opens_and_same_writer_fence_remain_coherent(self):
        storage2=enrolled_storage(self.other)
        def read(pair):
            path,storage=pair
            with closing(existing_database(path,storage=storage)) as db:return db.execute('SELECT value FROM facts').fetchone()
        with ThreadPoolExecutor(max_workers=4) as workers:
            self.assertEqual([(1,),(2,)]*8,list(workers.map(read,[(self.path,self.storage),(self.other,storage2)]*8)))

    def test_failed_or_timed_out_connection_acquisition_cleans_up_and_restart_preserves_sql(self):
        real_open=os.open
        for fault in ('failure','timeout'):
            with self.subTest(fault=fault):
                def acquire(path,*args,**kwargs):
                    if str(path).startswith('/proc/self/fd/'):
                        if fault=='failure':raise OSError('fixture connection acquisition failure')
                        time.sleep(.08)
                    return real_open(path,*args,**kwargs)
                with patch.object(os,'open',side_effect=acquire):
                    with self.assertRaises((ValueError,TimeoutError,OSError)):
                        selected_io.run(lambda:existing_database(self.path,storage=self.storage),timeout=.04)
                time.sleep(.12);gc.collect()
                with closing(existing_database(self.path,storage=self.storage)) as db:self.assertEqual((1,),db.execute('SELECT value FROM facts').fetchone())
