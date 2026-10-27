from sqlite_storage_fixture import enrolled_storage
"""Disposable selected-input and receiving-order fixtures. No installation or live cohort."""
import hashlib
import json
import os
from pathlib import Path
import sqlite3
import tempfile
import threading
import time
import unittest
from unittest.mock import patch
from concurrent.futures import TimeoutError
from contextlib import closing
from cryptography.hazmat.primitives.asymmetric import ec
import bounded_file_io as io
import joint_authority_service as service
from joint_live_phase_runner import canonical, Blocked
from test_execution_profile import fixture_profile
from execution_profile import validate_execution_inventory


class Pass10Boundaries(unittest.TestCase):
    def test_actual_inventory_rejects_selected_fifo_symbolic_parent_growth_and_oversize(self):
        with tempfile.TemporaryDirectory() as root:
            profile=fixture_profile(root)
            path=Path(profile['dependencyClosure']['installationFile'])
            original=path.read_bytes()
            path.unlink();os.mkfifo(path)
            start=time.monotonic();self.assertTrue(validate_execution_inventory(profile));self.assertLess(time.monotonic()-start,1)
            path.unlink();path.write_bytes(original)
            parent=path.parent;park=parent.with_name('parked');parent.rename(park);parent.symlink_to(park,target_is_directory=True)
            self.assertTrue(validate_execution_inventory(profile));parent.unlink();park.rename(parent)
            with path.open('wb') as output:output.truncate(1024*1024+1)
            self.assertTrue(validate_execution_inventory(profile))
            path.write_bytes(original)
            real_read=os.read;grew=False
            def growing(fd,count):
                nonlocal grew
                block=real_read(fd,count)
                if block and not grew:
                    grew=True
                    with path.open('ab') as out:out.write(b'changed')
                return block
            with patch.object(io.os,'read',side_effect=growing):
                with self.assertRaises(ValueError):io.read(path,1024*1024)

    def test_timeout_retains_all_four_worker_slots_until_kernel_cleanup(self):
        release=threading.Event();entered=threading.Barrier(5);finished=[]
        def stalled():
            entered.wait();release.wait();finished.append(True)
        def caller():
            with self.assertRaises(TimeoutError):io.run(stalled,.15)
        callers=[threading.Thread(target=caller) for _ in range(4)]
        for task in callers:task.start()
        entered.wait(timeout=2)
        for task in callers:task.join(1)
        try:
            with self.assertRaises(TimeoutError):io.run(lambda:False,.05)
            self.assertEqual([],finished)
        finally:release.set()
        deadline=time.monotonic()+2
        while len(finished)<4 and time.monotonic()<deadline:time.sleep(.01)
        self.assertEqual(4,len(finished));self.assertTrue(io.run(lambda:True,.2))

    def test_all_literal_sqlite_paths_are_existing_only_and_cannot_supply_uri_options(self):
        with tempfile.TemporaryDirectory() as root:
            for suffix in ('?mode=rwc#','#fragment','%3fmode=rwc','normal'):
                path=Path(root)/('store'+suffix)
                with self.assertRaises((sqlite3.OperationalError,ValueError)):service.existing_database(path)
                self.assertEqual([],list(Path(root).iterdir()))
                with sqlite3.connect(path) as db:db.execute('CREATE TABLE enrolled(value)')
                with closing(service.existing_database(path,storage=enrolled_storage(path))) as db:db.execute('SELECT * FROM enrolled')
                path.unlink()

    def test_actual_signer_startup_refuses_fifo_and_symbolic_parent_before_serving(self):
        from cryptography.hazmat.primitives import serialization
        with tempfile.TemporaryDirectory() as root:
            root=Path(root);dbpath=root/'enrolled.sqlite';key=ec.generate_private_key(ec.SECP256R1())
            public=root/'public.pem';public.write_bytes(key.public_key().public_bytes(serialization.Encoding.PEM,serialization.PublicFormat.SubjectPublicKeyInfo))
            signing=root/'private.pem';os.mkfifo(signing)
            config={'publicAnchorFile':str(public),'databasePath':str(dbpath),'role':'authority','signingKeyFile':str(signing)}
            installation=root/'service.json';installation.write_bytes(canonical(config))
            with sqlite3.connect(dbpath) as db:
                db.execute('CREATE TABLE service_enrollment(role,public_anchor_sha256,profile_sha256)')
                db.execute('INSERT INTO service_enrollment VALUES(?,?,?)',('authority',hashlib.sha256(public.read_bytes()).hexdigest(),hashlib.sha256(installation.read_bytes()).hexdigest()))
            config['storageEnrollment']=enrolled_storage(dbpath).enrollment;installation.write_bytes(canonical(config))
            with closing(sqlite3.connect(dbpath)) as db,db:db.execute('UPDATE service_enrollment SET profile_sha256=?',(hashlib.sha256(installation.read_bytes()).hexdigest(),))
            with patch('sys.argv',['joint_authority_service','--installation',str(installation)]),patch.object(service.CohortHttpApplication,'serve') as serve:
                start=time.monotonic()
                with self.assertRaises(ValueError):service.main()
                self.assertLess(time.monotonic()-start,1);serve.assert_not_called()
            signing.unlink();signing.write_bytes(key.private_bytes(serialization.Encoding.PEM,serialization.PrivateFormat.PKCS8,serialization.NoEncryption()))
            moved=root.with_name(root.name+'-held');root.rename(moved);root.symlink_to(moved,target_is_directory=True)
            try:
                with patch('sys.argv',['joint_authority_service','--installation',str(installation)]),self.assertRaises(OSError):service.main()
            finally:root.unlink();moved.rename(root)

class ActualObserverResultAdmission(unittest.TestCase):
    def test_persisted_observer_readiness_orders_read_then_current_grant_and_retains_original(self):
        from test_persisted_phase_observer import PersistedObserverTests
        observed=PersistedObserverTests();observed.setUp()
        self.addCleanup(observed.temp.cleanup)
        database=observed.root/'result-grants.sqlite'
        bearer='fixture-workload';method='preflight-phase';stamp=hashlib.sha256(bearer.encode()).hexdigest()
        with sqlite3.connect(database) as db:
            db.execute('CREATE TABLE workload_grants(bearer_sha256 PRIMARY KEY,methods_json,valid_until,revoked)')
            db.execute('INSERT INTO workload_grants VALUES(?,?,?,0)',(stamp,json.dumps([method]),int(time.time())+300))
        signer=ec.generate_private_key(ec.SECP256R1())
        app=service.CohortHttpApplication(database,signer,observed.observer.dispatch,'file:/fixture-channel',storage=enrolled_storage(database))
        body=canonical({'protocol':'OQ-5.4-1.joint.v1','requestId':'fixture-read','method':method,'payload':{'binding':observed.binding,'phase':0,'cases':observed.request['cases']}})
        headers={'dapr-api-token':'channel','Authorization':'Bearer '+bearer}
        original=(observed.evidence_root/'owner-original.json').read_bytes()
        read=io.read
        def revoke_during_read(*args,**kwargs):
            result=read(*args,**kwargs)
            with closing(sqlite3.connect(database)) as db,db:db.execute('UPDATE workload_grants SET revoked=1')
            return result
        with patch('qualify.read_mounted_credential',return_value='channel'),patch.object(io,'read',side_effect=revoke_during_read):
            with self.assertRaises(Blocked):app.handle(method,headers,body)
        self.assertEqual(original,(observed.evidence_root/'owner-original.json').read_bytes())
        with closing(sqlite3.connect(database)) as db,db:db.execute('UPDATE workload_grants SET revoked=0')
        attempted=threading.Event();committed=threading.Event();threads=[]
        class OrderedSigner:
            def sign(self,*args):
                def revoke():
                    with closing(sqlite3.connect(database,timeout=2)) as db,db:
                        attempted.set();db.execute('UPDATE workload_grants SET revoked=1')
                    committed.set()
                task=threading.Thread(target=revoke);threads.append(task);task.start();attempted.wait(1)
                # The final result transaction owns the current grant while signing, after all reads.
                if committed.is_set():raise AssertionError('Revocation passed the result ordering point')
                return signer.sign(*args)
        app.signer=OrderedSigner()
        with patch('qualify.read_mounted_credential',return_value='channel'):
            response=app.handle(method,headers,body)
        for task in threads:task.join(3)
        self.assertTrue(committed.is_set());self.assertEqual(method,response['payload']['method'])
        with closing(sqlite3.connect(database)) as db,db:db.execute('UPDATE workload_grants SET revoked=0,valid_until=?',(int(time.time())-1,))
        with patch('qualify.read_mounted_credential',return_value='channel'),self.assertRaises(Blocked):app.handle(method,headers,body)
        self.assertEqual(original,(observed.evidence_root/'owner-original.json').read_bytes())
